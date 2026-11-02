#region Copyright & License Information
/*
 * Copyright (c) The OpenPW Developers and Contributors
 * This file is part of OpenPW, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Pw.Orders;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets.Logic
{
	// The command panel's slot buttons (SLOT_0, SLOT_1, …, each with an ICON sprite and a PROGRESS label):
	// - a worker selected: its build list (Builder); a click enters placement mode;
	// - one producing building selected: what it trains; click queues one, Shift+click cancels one.
	// - the Chief: his ability (Fall out) with his mana;
	// - units that can hunt: the Hunt toggle (PwHunter), in the first free slot;
	// - two or more meat eaters with meat: Share meat (they meet and split it evenly), in the next.
	// Icons are the original portraits (each image's "icon" sequence); every slot has a tooltip (name and cost; time, what is missing, how to use it). InterfaceBarLogic
	// places the panel and the slots; SelectionPanelLogic shows what is selected.
	public class CommandPanelLogic : ChromeLogic
	{
		readonly World world;
		readonly ButtonWidget[] slots;

		[ObjectCreator.UseCtor]
		public CommandPanelLogic(Widget widget, World world)
		{
			this.world = world;
			slots = widget.Children.OfType<ButtonWidget>().Where(b => b.Id.StartsWith("SLOT_", System.StringComparison.Ordinal)).ToArray();
			foreach (var slot in slots)
			{
				var s = slot;
				s.Visible = false;
				s.Get("DIM").IsVisible = () => s.IsDisabled();
			}
		}

		public override void Tick()
		{
			var player = world.LocalPlayer;
			var selected = world.Selection.Actors.Where(a => a.IsInWorld && !a.IsDead && a.Owner == player).ToArray();
			var builder = selected.FirstOrDefault(a => a.TraitOrDefault<Builder>() != null);
			var producer = selected.Length == 1 ? selected[0] : null;
			var finished = producer?.TraitOrDefault<UnderConstruction>()?.IsComplete ?? true;
			var queue = finished ? producer?.TraitsImplementing<ProductionQueue>().FirstOrDefault(q => q.Enabled) : null;

			if (builder != null)
				ShowBuildList(builder);
			else if (queue != null)
				ShowTraining(producer, queue);
			else
			{
				foreach (var slot in slots)
					slot.Visible = false;

				// Its ability (the Chief's Fall out): the button shows the mana, usable once researched and paid for.
				var fallOut = producer?.TraitOrDefault<PwFallOut>();
				if (fallOut != null)
					ShowAbility(producer, fallOut);
			}

			// Hunting, for the selected units that can hunt: in the first free slot (after the build list or ability).
			var hunters = selected.Where(a => a.TraitOrDefault<PwHunter>() is PwHunter h && !h.IsTraitDisabled).ToArray();
			var free = slots.FirstOrDefault(s => !s.Visible);
			if (hunters.Length > 0 && queue == null && free != null)
				ShowHunt(free, hunters);

			// Share meat: two or more meat eaters, one of them carrying some.
			var eaters = selected.Where(a => a.TraitOrDefault<PwMeatEater>() != null && a.TraitOrDefault<IMove>() != null).ToArray();
			free = slots.FirstOrDefault(s => !s.Visible);
			if (eaters.Length > 1 && eaters.Any(e => e.Trait<PwMeatEater>().Meat > 0) && queue == null && free != null)
				ShowShareMeat(free, eaters);
		}

		// The units meet and split the meat they carry evenly; the one with the most meat takes the order.
		void ShowShareMeat(ButtonWidget slot, Actor[] eaters)
		{
			Sprite icon = null;
			if (world.Map.Sequences.HasSequence("command-share-meat", "icon"))
				icon = world.Map.Sequences.GetSequence("command-share-meat", "icon").GetSprite(0);

			var total = eaters.Sum(e => e.Trait<PwMeatEater>().Meat);
			slot.Visible = true;
			slot.GetText = () => "";
			slot.IsDisabled = () => false;
			slot.IsHighlighted = () => false;
			slot.GetTooltipText = () => FluentProvider.GetMessage("label-share-meat");
			slot.GetTooltipDesc = () => FluentProvider.GetMessage("tooltip-share-meat", "total", total, "units", eaters.Length, "each", total / eaters.Length);
			slot.Get<SpriteWidget>("ICON").GetSprite = () => icon;
			slot.Get<LabelWidget>("PROGRESS").GetText = () => "";
			slot.OnClick = () =>
				world.IssueOrder(PwMeatEater.Share(eaters.Where(e => !e.IsDead).OrderByDescending(e => e.Trait<PwMeatEater>().Meat).ToArray()));
		}

		// A toggle: lit while every selected hunter hunts; a click turns hunting on for all of them (off when all hunt).
		void ShowHunt(ButtonWidget slot, Actor[] hunters)
		{
			Sprite icon = null;
			if (world.Map.Sequences.HasSequence("command-hunt", "icon"))
				icon = world.Map.Sequences.GetSequence("command-hunt", "icon").GetSprite(0);

			bool AllHunting() => hunters.All(h => h.IsDead || h.Trait<PwHunter>().Hunting);
			slot.Visible = true;
			slot.GetText = () => "";
			slot.IsDisabled = () => false;
			slot.IsHighlighted = AllHunting;
			slot.GetTooltipText = () => FluentProvider.GetMessage(AllHunting() ? "label-hunt-on" : "label-hunt-off");
			slot.GetTooltipDesc = () => FluentProvider.GetMessage("tooltip-hunt");
			slot.Get<SpriteWidget>("ICON").GetSprite = () => icon;
			slot.Get<LabelWidget>("PROGRESS").GetText = () => "";
			slot.OnClick = () =>
			{
				var on = !AllHunting();
				foreach (var h in hunters.Where(h => !h.IsDead))
					world.IssueOrder(PwHunter.Toggle(h, on));
			};
		}

		void ShowBuildList(Actor builderActor)
		{
			var builder = builderActor.Trait<Builder>();
			var resources = builderActor.Owner.PlayerActor.Trait<PlayerResources>();
			var techTree = builderActor.Owner.PlayerActor.Trait<TechTree>();
			var types = builder.Info.Types
				.Select(t => world.Map.Rules.Actors[t])
				.OrderBy(a => a.TraitInfoOrDefault<BuildableInfo>()?.BuildPaletteOrder ?? 9999)
				.ToArray();

			for (var i = 0; i < slots.Length; i++)
			{
				var slot = slots[i];
				if (i >= types.Length)
				{
					slot.Visible = false;
					continue;
				}

				var actorInfo = types[i];
				var cost = actorInfo.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;
				var buildable = actorInfo.TraitInfoOrDefault<BuildableInfo>();
				var available = buildable == null || buildable.Prerequisites.Length == 0 || techTree.HasPrerequisites(buildable.Prerequisites);

				Show(slot, actorInfo, cost, () => "");
				var missing = Missing(buildable, techTree);
				slot.GetTooltipDesc = () => !available
					? FluentProvider.GetMessage("tooltip-build-needs", "needs", missing)
					: resources.Cash + resources.Resources < cost
					? FluentProvider.GetMessage("tooltip-not-enough-berries", "time", Seconds(actorInfo))
					: FluentProvider.GetMessage("tooltip-build", "time", Seconds(actorInfo));
				slot.IsDisabled = () => !available || resources.Cash + resources.Resources < cost;
				slot.OnClick = () => world.OrderGenerator = new BuildAtCellOrderGenerator(world, builderActor, actorInfo.Name);
			}
		}

		void ShowTraining(Actor producer, ProductionQueue queue)
		{
			// Everything it trains, also what isn't available yet (greyed out, its tooltip says what it needs). Items with
			// hidden prerequisites ("~": researched upgrades, ...) stay out of the list.
			var techTree = producer.Owner.PlayerActor.Trait<TechTree>();
			var items = queue.AllItems().OrderBy(a => a.TraitInfoOrDefault<BuildableInfo>()?.BuildPaletteOrder ?? 9999).ToArray();
			for (var i = 0; i < slots.Length; i++)
			{
				var slot = slots[i];
				if (i >= items.Length)
				{
					slot.Visible = false;
					continue;
				}

				var actorInfo = items[i];
				var name = actorInfo.Name;
				var cost = actorInfo.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;

				Show(slot, actorInfo, cost, () =>
				{
					var queued = queue.AllQueued().Where(q => q.Item == name).ToList();
					if (queued.Count == 0)
						return "";

					var first = queued[0];
					var percent = first.TotalTime > 0 ? 100 * (first.TotalTime - first.RemainingTime) / first.TotalTime : 0;
					return queued.Count > 1 ? $"{percent}% ×{queued.Count}" : $"{percent}%";
				});

				var missing = Missing(actorInfo.TraitInfoOrDefault<BuildableInfo>(), techTree);
				slot.IsDisabled = () => !queue.CanBuild(actorInfo);
				slot.GetTooltipDesc = () => queue.CanBuild(actorInfo)
					? FluentProvider.GetMessage("tooltip-train", "time", Seconds(actorInfo))
					: FluentProvider.GetMessage("tooltip-build-needs", "needs", missing);

				// Click queues one, Shift+click cancels one (buttons only take the left mouse button). OnMouseUp is left
				// alone: the button calls OnClick from it.
				slot.OnClick = () =>
				{
					if (Game.GetModifierKeys().HasModifier(Modifiers.Shift))
						world.IssueOrder(Order.CancelProduction(producer, name, 1));
					else
						world.IssueOrder(Order.StartProduction(producer, name, 1));
				};
			}
		}

		// The buildings (or research) still missing for it, by name: the prerequisites the player doesn't meet yet.
		string Missing(BuildableInfo buildable, TechTree techTree)
		{
			if (buildable == null)
				return "";

			return string.Join(", ", buildable.Prerequisites
				.Where(p => !p.StartsWith('~') && !techTree.HasPrerequisites(new[] { p }))
				.Select(p => world.Map.Rules.Actors.TryGetValue(p.TrimStart('!'), out var a) ? ActorName(a) : p));
		}

		static string ActorName(ActorInfo info)
		{
			var tooltip = info.TraitInfos<TooltipInfo>().FirstOrDefault();
			return tooltip != null ? FluentProvider.GetMessage(tooltip.Name) : info.Name;
		}

		// Build or training time in seconds at the default speed (71 ms per tick): a worker's building takes its
		// UnderConstruction duration, a trained unit or research its BuildDuration.
		static int Seconds(ActorInfo info)
		{
			var ticks = info.TraitInfoOrDefault<UnderConstructionInfo>()?.Duration ?? info.TraitInfoOrDefault<BuildableInfo>()?.BuildDuration ?? 0;
			return ticks * 71 / 1000;
		}

		void ShowAbility(Actor caster, PwFallOut fallOut)
		{
			var slot = slots[0];
			var mana = caster.Trait<PwMana>();
			Sprite icon = null;
			if (world.Map.Sequences.HasSequence(fallOut.Info.Image, "icon"))
				icon = world.Map.Sequences.GetSequence(fallOut.Info.Image, "icon").GetSprite(0);

			slot.Visible = true;
			slot.GetText = () => "";
			slot.IsHighlighted = () => false;
			slot.GetTooltipText = () => FluentProvider.GetMessage("label-ability-fall-out", "cost", fallOut.Info.Cost);
			slot.GetTooltipDesc = () => FluentProvider.GetMessage(fallOut.IsUnlocked(caster) ? "tooltip-ability-fall-out" : "label-ability-fall-out-locked");
			slot.Get<SpriteWidget>("ICON").GetSprite = () => icon;
			slot.Get<LabelWidget>("PROGRESS").GetText = () => $"{mana.Mana}/{mana.Max}";
			slot.IsDisabled = () => !fallOut.IsReady(caster);
			slot.OnClick = () => world.OrderGenerator = new AbilityOrderGenerator(caster, PwFallOut.OrderID, PwFallOut.CanTarget);
		}

		void Show(ButtonWidget slot, ActorInfo actorInfo, int cost, System.Func<string> progress)
		{
			var name = ActorName(actorInfo);
			var image = actorInfo.TraitInfoOrDefault<RenderSpritesInfo>()?.Image ?? actorInfo.Name;
			Sprite portrait = null;
			if (world.Map.Sequences.HasSequence(image, "icon"))
				portrait = world.Map.Sequences.GetSequence(image, "icon").GetSprite(0);

			slot.Visible = true;
			slot.GetText = () => "";
			slot.IsHighlighted = () => false;
			slot.GetTooltipText = () => FluentProvider.GetMessage("tooltip-slot", "name", name, "cost", cost);
			slot.Get<SpriteWidget>("ICON").GetSprite = () => portrait;
			slot.Get<LabelWidget>("PROGRESS").GetText = progress;
		}
	}
}
