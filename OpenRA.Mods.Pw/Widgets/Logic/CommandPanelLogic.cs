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
	// Also the berries counter (CASH) and the population (POPULATION). Icons are the original portraits (each image's "icon" sequence).
	// Hosted on an always-visible container, so it keeps ticking while the buttons are hidden.
	public class CommandPanelLogic : ChromeLogic
	{
		readonly World world;
		readonly ButtonWidget[] slots;
		readonly LabelWidget title;

		[ObjectCreator.UseCtor]
		public CommandPanelLogic(Widget widget, World world)
		{
			this.world = world;
			slots = widget.Children.OfType<ButtonWidget>().Where(b => b.Id.StartsWith("SLOT_", System.StringComparison.Ordinal)).ToArray();
			title = widget.Get<LabelWidget>("TITLE");
			foreach (var slot in slots)
				slot.Visible = false;

			var cash = widget.Get<LabelWidget>("CASH");
			cash.GetText = () =>
			{
				var resources = world.LocalPlayer?.PlayerActor.TraitOrDefault<PlayerResources>();
				return resources == null ? "" : FluentProvider.GetMessage("label-berries", "amount", resources.Cash + resources.Resources);
			};

			var population = widget.Get<LabelWidget>("POPULATION");
			population.GetText = () =>
			{
				var player = world.LocalPlayer;
				return player == null ? "" : FluentProvider.GetMessage("label-population",
					"used", PwPopulation.Used(player), "limit", PwPopulation.Capacity(player));
			};
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

				// One unit that levels: its name, level and experience.
				if (producer != null && producer.TraitOrDefault<PwLevels>() != null)
					ShowTitle(producer);
				else
					title.Visible = false;

				// Its ability (the Chief's Fall out): the button shows the mana, usable once researched and paid for.
				var fallOut = producer?.TraitOrDefault<PwFallOut>();
				if (fallOut != null)
					ShowAbility(producer, fallOut);
			}
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

			ShowTitle(builderActor);
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
				slot.IsDisabled = () => !available || resources.Cash + resources.Resources < cost;
				slot.OnClick = () => world.OrderGenerator = new BuildAtCellOrderGenerator(world, builderActor, actorInfo.Name);
			}
		}

		void ShowTraining(Actor producer, ProductionQueue queue)
		{
			var items = queue.BuildableItems().OrderBy(a => a.TraitInfoOrDefault<BuildableInfo>()?.BuildPaletteOrder ?? 9999).ToArray();
			ShowTitle(producer);
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

				slot.IsDisabled = () => false;

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

		void ShowTitle(Actor actor)
		{
			var tooltip = actor.Info.TraitInfos<TooltipInfo>().FirstOrDefault();
			title.Visible = true;
			var levels = actor.TraitOrDefault<PwLevels>();
			var meat = actor.TraitOrDefault<PwMeatEater>();
			title.GetText = () =>
			{
				var name = tooltip != null ? FluentProvider.GetMessage(tooltip.Name) : actor.Info.Name;
				var text = levels == null ? name : FluentProvider.GetMessage("label-unit-level",
					"name", name, "level", levels.Level, "experience", levels.Experience, "next", levels.ExperiencePerLevel);
				return meat != null && meat.Meat > 0 ? text + "   " + FluentProvider.GetMessage("label-unit-meat", "meat", meat.Meat) : text;
			};
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
			slot.GetTooltipText = () => FluentProvider.GetMessage(fallOut.IsUnlocked(caster) ? "label-ability-fall-out" : "label-ability-fall-out-locked",
				"cost", fallOut.Info.Cost);
			slot.Get<SpriteWidget>("ICON").GetSprite = () => icon;
			slot.Get<LabelWidget>("PROGRESS").GetText = () => $"{mana.Mana}/{mana.Max}";
			slot.IsDisabled = () => !fallOut.IsReady(caster);
			slot.OnClick = () => world.OrderGenerator = new AbilityOrderGenerator(caster, PwFallOut.OrderID, PwFallOut.CanTarget);
		}

		void Show(ButtonWidget slot, ActorInfo actorInfo, int cost, System.Func<string> progress)
		{
			var tooltip = actorInfo.TraitInfos<TooltipInfo>().FirstOrDefault();
			var name = tooltip != null ? FluentProvider.GetMessage(tooltip.Name) : actorInfo.Name;
			var image = actorInfo.TraitInfoOrDefault<RenderSpritesInfo>()?.Image ?? actorInfo.Name;
			Sprite portrait = null;
			if (world.Map.Sequences.HasSequence(image, "icon"))
				portrait = world.Map.Sequences.GetSequence(image, "icon").GetSprite(0);

			slot.Visible = true;
			slot.GetText = () => "";
			slot.GetTooltipText = () => $"{name} ({cost})";
			slot.Get<SpriteWidget>("ICON").GetSprite = () => portrait;
			slot.Get<LabelWidget>("PROGRESS").GetText = progress;
		}
	}
}
