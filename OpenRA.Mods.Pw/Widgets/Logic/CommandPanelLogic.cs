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

using System;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Pw.Orders;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets.Logic
{
	// The command panel's slot buttons (SLOT_0, SLOT_1, …, each with an ICON sprite and a PROGRESS label):
	// - a worker selected: its build list (Builder); a click enters placement mode;
	// - one producing building selected: what it trains; click queues one, right click (or Shift+click) cancels one.
	// - the Chief: his ability (Fall out) with his mana;
	// - armed units: Attack, then a click on the map: they walk there and attack whatever they meet (attack-move);
	//   Patrol, then a click: they go back and forth between where they are and there, attacking the same way;
	// - units that can hunt: the Hunt toggle (PwHunter), in the next free slot;
	// - two or more meat eaters with meat: Share meat (they meet and split it evenly), in the next;
	// - transports: Unload, then a click on the ground where they unload (PwUnloadAt), in the next;
	// - Tyrano units: a merge per recipe the selection makes up (PwMerge), Split for merged units (PwMerged), and the
	//   Disguise toggle (PwDisguise), in the slots after those.
	// Icons are the original portraits (each image's "icon" sequence); every slot has a tooltip (name and cost; time, what is missing, how to use it). InterfaceBarLogic
	// places the panel and the slots; SelectionPanelLogic shows what is selected.
	public class CommandPanelLogic : ChromeLogic
	{
		readonly World world;
		readonly SlotButtonWidget[] slots;

		// The Attack and Patrol slots' keys: the engine's AttackMove hotkey and PW's Patrol (hotkeys.yaml), A and P by
		// default, set in the hotkey settings.
		readonly HotkeyReference attackMoveKey;
		readonly HotkeyReference patrolKey;
		static readonly HotkeyReference NoKey = new();

		[ObjectCreator.UseCtor]
		public CommandPanelLogic(Widget widget, World world, ModData modData)
		{
			this.world = world;
			attackMoveKey = modData.Hotkeys["AttackMove"];
			patrolKey = modData.Hotkeys["Patrol"];
			slots = widget.Children.OfType<SlotButtonWidget>().Where(b => b.Id.StartsWith("SLOT_", System.StringComparison.Ordinal)).ToArray();
			foreach (var slot in slots)
			{
				var s = slot;
				s.Visible = false;
				s.Get("DIM").IsVisible = () => s.IsDisabled();
				s.Get("OUTLINE").IsVisible = () => s.IsHighlighted();
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

			// Only training and spells cast on their own take the right button, and only Attack and Patrol a key; in every
			// other use a right click passes through and no key is bound.
			foreach (var slot in slots)
			{
				slot.OnRightClick = null;
				slot.Key = NoKey;
			}

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

				// A Demon's spells: each with its kill point cost, usable from its level on.
				if (producer != null)
					foreach (var spell in producer.TraitsImplementing<PwSpell>())
						if (slots.FirstOrDefault(s => !s.Visible) is SlotButtonWidget slot)
							ShowSpell(slot, producer, spell);
			}

			// Attack (attack-move), for the selected armed units: in the first free slot (after the ability), not with a
			// build list or training.
			var attackers = selected.Where(a => a.Info.HasTraitInfo<IMoveInfo>() && a.Info.HasTraitInfo<AttackBaseInfo>()).ToArray();
			var free = slots.FirstOrDefault(s => !s.Visible);
			if (attackers.Length > 0 && queue == null && builder == null && free != null)
				ShowAttack(free, attackers);

			// Patrol, for the same units: in the next free slot.
			var patrollers = attackers.Where(a => a.TraitOrDefault<PwPatrol>() != null).ToArray();
			free = slots.FirstOrDefault(s => !s.Visible);
			if (patrollers.Length > 0 && queue == null && builder == null && free != null)
				ShowPatrol(free, patrollers);

			// Hunting, for the selected units that can hunt: in the first free slot (after the build list or ability).
			var hunters = selected.Where(a => a.TraitOrDefault<PwHunter>() is PwHunter h && !h.IsTraitDisabled).ToArray();
			free = slots.FirstOrDefault(s => !s.Visible);
			if (hunters.Length > 0 && queue == null && free != null)
				ShowHunt(free, hunters);

			// Share meat: two or more meat eaters, one of them carrying some.
			var eaters = selected.Where(a => a.TraitOrDefault<PwMeatEater>() != null && a.TraitOrDefault<IMove>() != null).ToArray();
			free = slots.FirstOrDefault(s => !s.Visible);
			if (eaters.Length > 1 && eaters.Any(e => e.Trait<PwMeatEater>().Meat > 0) && queue == null && free != null)
				ShowShareMeat(free, eaters);

			// Unload at a place: the selected transports.
			var transports = selected.Where(a => a.TraitOrDefault<PwUnloadAt>() != null).ToArray();
			free = slots.FirstOrDefault(s => !s.Visible);
			if (transports.Length > 0 && queue == null && free != null)
				ShowUnload(free, transports);

			if (queue != null || builder != null)
				return;

			// Tyrano: each merge the selected units make up, splitting merged ones, and the Disguise toggle.
			var undisguised = selected.Where(a => a.TraitOrDefault<PwDisguise>()?.Disguised != true).ToArray();
			foreach (var into in undisguised.Select(a => a.Info.TraitInfoOrDefault<PwMergeInfo>()?.Into).Where(i => i != null).Distinct())
			{
				var recipe = world.Map.Rules.Actors[into].TraitInfo<PwMergedInfo>();
				var parts = PwMerge.Pick(undisguised, into, recipe);
				free = slots.FirstOrDefault(s => !s.Visible);
				if (parts != null && free != null)
					ShowMerge(free, into, recipe, parts);
			}

			var merged = selected.Where(a => a.TraitOrDefault<PwMerged>()?.Parts.Count > 0).ToArray();
			free = slots.FirstOrDefault(s => !s.Visible);
			if (merged.Length > 0 && free != null)
				ShowSplit(free, merged);

			var disguisers = selected.Where(a => a.TraitOrDefault<PwDisguise>() != null).ToArray();
			free = slots.FirstOrDefault(s => !s.Visible);
			if (disguisers.Length > 0 && free != null)
				ShowDisguise(free, disguisers);
		}

		// Combine (TwinVelocis, TwinRhamphos) or mutate (Mutant): the parts meet and become one; locked until the merged
		// type's requirements are met.
		void ShowMerge(ButtonWidget slot, string into, PwMergedInfo recipe, Actor[] parts)
		{
			var info = world.Map.Rules.Actors[into];
			var icon = CommandIcon(recipe.Image);
			var name = ActorName(info);
			var partNames = string.Join(", ", parts.Select(p => ActorName(p.Info)));
			var techTree = parts[0].Owner.PlayerActor.Trait<TechTree>();
			var missing = string.Join(", ", recipe.Prerequisites
				.Where(p => !techTree.HasPrerequisites(new[] { p }))
				.Select(p => world.Map.Rules.Actors.TryGetValue(p, out var a) ? ActorName(a) : p));

			slot.Visible = true;
			slot.GetText = () => "";
			slot.IsHighlighted = () => false;
			slot.IsDisabled = () => !PwMerge.CanMerge(parts[0].Owner, recipe);
			slot.GetTooltipText = () => FluentProvider.GetMessage("label-merge", "name", name);
			slot.GetTooltipDesc = () => PwMerge.CanMerge(parts[0].Owner, recipe)
				? FluentProvider.GetMessage("tooltip-merge", "parts", partNames, "name", name)
				: FluentProvider.GetMessage("tooltip-build-needs", "needs", missing);
			slot.Get<SpriteWidget>("ICON").GetSprite = () => icon;
			slot.Get<LabelWidget>("PROGRESS").GetText = () => "";
			slot.OnClick = () => world.IssueOrder(PwMerge.Merge(parts));
		}

		// Merged units become the units they were made from again.
		void ShowSplit(ButtonWidget slot, Actor[] merged)
		{
			var icon = CommandIcon("command-split");
			var partNames = string.Join(", ", merged[0].Info.TraitInfo<PwMergedInfo>().From
				.Select(t => ActorName(world.Map.Rules.Actors[t])));

			slot.Visible = true;
			slot.GetText = () => "";
			slot.IsHighlighted = () => false;
			slot.IsDisabled = () => false;
			slot.GetTooltipText = () => FluentProvider.GetMessage("label-split");
			slot.GetTooltipDesc = () => FluentProvider.GetMessage("tooltip-split", "parts", partNames);
			slot.Get<SpriteWidget>("ICON").GetSprite = () => icon;
			slot.Get<LabelWidget>("PROGRESS").GetText = () => "";
			slot.OnClick = () =>
			{
				foreach (var m in merged.Where(m => !m.IsDead))
					world.IssueOrder(PwMerged.Split(m));
			};
		}

		// A toggle: disguises the selected units that can (turns them back when all of them are disguised).
		void ShowDisguise(ButtonWidget slot, Actor[] units)
		{
			bool AllDisguised() => units.All(u => u.IsDead || u.Trait<PwDisguise>().Disguised);
			bool Available() => units.Any(u => !u.IsDead && u.Trait<PwDisguise>().IsAvailable(u));
			var disguise = CommandIcon("command-disguise");
			var turnBack = CommandIcon("command-undisguise");

			slot.Visible = true;
			slot.GetText = () => "";
			slot.IsHighlighted = () => false;
			slot.IsDisabled = () => !AllDisguised() && !Available();
			slot.GetTooltipText = () => FluentProvider.GetMessage(AllDisguised() ? "label-undisguise" : "label-disguise");
			slot.GetTooltipDesc = () => FluentProvider.GetMessage(AllDisguised() ? "tooltip-undisguise"
				: Available() ? "tooltip-disguise" : "tooltip-disguise-locked");
			slot.Get<SpriteWidget>("ICON").GetSprite = () => AllDisguised() ? turnBack : disguise;
			slot.Get<LabelWidget>("PROGRESS").GetText = () => "";
			slot.OnClick = () =>
			{
				var on = !AllDisguised();
				foreach (var u in units.Where(u => !u.IsDead))
					world.IssueOrder(PwDisguise.Toggle(u, on));
			};
		}

		// A command button's icon: the "icon" sequence of the image, in the local player's race frame style when there
		// is one (<image>-<race>: each race has its own button sheet, the art is the same).
		Sprite CommandIcon(string image)
		{
			// HasSequence throws for an image with no sequences at all.
			var sequences = world.Map.Sequences;
			bool Has(string name) => sequences.Images.Contains(name) && sequences.HasSequence(name, "icon");
			var raced = $"{image}-{world.LocalPlayer?.Faction.InternalName}";
			if (Has(raced))
				return sequences.GetSequence(raced, "icon").GetSprite(0);

			return Has(image) ? sequences.GetSequence(image, "icon").GetSprite(0) : null;
		}

		// The units meet and split the meat they carry evenly; the one with the most meat takes the order.
		void ShowShareMeat(ButtonWidget slot, Actor[] eaters)
		{
			var icon = CommandIcon("command-share-meat");

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

		// Then a click on the ground: the transports go there and let their passengers out. Usable while one carries any.
		void ShowUnload(ButtonWidget slot, Actor[] transports)
		{
			var icon = CommandIcon("command-unload");
			bool Loaded() => transports.Any(t => !t.IsDead && !t.Trait<Cargo>().IsEmpty());

			slot.Visible = true;
			slot.GetText = () => "";
			slot.IsHighlighted = () => false;
			slot.IsDisabled = () => !Loaded();
			slot.GetTooltipText = () => FluentProvider.GetMessage("label-unload");
			slot.GetTooltipDesc = () => FluentProvider.GetMessage(Loaded() ? "tooltip-unload" : "tooltip-unload-empty");
			slot.Get<SpriteWidget>("ICON").GetSprite = () => icon;
			slot.Get<LabelWidget>("PROGRESS").GetText = () => "";
			slot.OnClick = () => world.OrderGenerator = new UnloadAtOrderGenerator(transports);
		}

		// Then a click on the map: the units walk there and attack every enemy they meet on the way, buildings included
		// (OpenRA's attack-move, as OpenSYW's Attack button). Lit while it waits for the click.
		void ShowAttack(ButtonWidget slot, Actor[] attackers)
		{
			var icon = CommandIcon("command-attack");

			slot.Visible = true;
			slot.GetText = () => "";
			slot.IsDisabled = () => false;
			slot.IsHighlighted = () => false;
			slot.GetTooltipText = () => KeyLabel(FluentProvider.GetMessage("label-attack"), attackMoveKey);
			slot.GetTooltipDesc = () => FluentProvider.GetMessage("tooltip-attack");
			slot.Get<SpriteWidget>("ICON").GetSprite = () => icon;
			slot.Get<LabelWidget>("PROGRESS").GetText = () => "";
			slot.OnClick = () => world.OrderGenerator =
				new PwAttackMoveOrderGenerator(world, attackers.Where(a => !a.IsDead));
			slot.Key = attackMoveKey;
		}

		// Then a click on the map: the units go back and forth between where they are and there, attacking every enemy
		// they meet (PwPatrol). Lit while they are all patrolling, as Hunt is while they hunt (a one-off order such as
		// Attack or Unload isn't lit).
		void ShowPatrol(ButtonWidget slot, Actor[] patrollers)
		{
			var icon = CommandIcon("command-patrol");

			slot.Visible = true;
			slot.GetText = () => "";
			slot.IsDisabled = () => false;
			slot.IsHighlighted = () => patrollers.All(p => p.IsDead || PwPatrol.IsPatrolling(p));
			slot.GetTooltipText = () => KeyLabel(FluentProvider.GetMessage("label-patrol"), patrolKey);
			slot.GetTooltipDesc = () => FluentProvider.GetMessage("tooltip-patrol");
			slot.Get<SpriteWidget>("ICON").GetSprite = () => icon;
			slot.Get<LabelWidget>("PROGRESS").GetText = () => "";
			slot.OnClick = () => world.OrderGenerator = new PatrolOrderGenerator(patrollers);
			slot.Key = patrolKey;
		}

		// A toggle: lit while every selected hunter hunts; a click turns hunting on for all of them (off when all hunt).
		void ShowHunt(ButtonWidget slot, Actor[] hunters)
		{
			var icon = CommandIcon("command-hunt");

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

				var purpose = Purpose(buildable);
				slot.GetTooltipDesc = () => purpose + (!available
					? FluentProvider.GetMessage("tooltip-build-needs", "needs", missing)
					: resources.Cash + resources.Resources < cost
					? FluentProvider.GetMessage("tooltip-not-enough-berries", "time", Seconds(actorInfo))
					: FluentProvider.GetMessage("tooltip-build", "time", Seconds(actorInfo)));
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

				var buildable = actorInfo.TraitInfoOrDefault<BuildableInfo>();
				var missing = Missing(buildable, techTree);
				var purpose = Purpose(buildable);
				slot.IsDisabled = () => !queue.CanBuild(actorInfo);
				slot.GetTooltipDesc = () => purpose + (queue.CanBuild(actorInfo)
					? FluentProvider.GetMessage("tooltip-train", "time", Seconds(actorInfo))
					: FluentProvider.GetMessage("tooltip-build-needs", "needs", missing));

				// Click queues one; right click, or Shift+click, cancels one (the last of its kind in the queue: the one
				// in training when it is the only one, and its cost comes back). OnMouseUp is left alone: the button
				// calls OnClick from it.
				slot.OnClick = () =>
				{
					if (Game.GetModifierKeys().HasModifier(Modifiers.Shift))
						world.IssueOrder(Order.CancelProduction(producer, name, 1));
					else
						world.IssueOrder(Order.StartProduction(producer, name, 1));
				};
				slot.OnRightClick = () => world.IssueOrder(Order.CancelProduction(producer, name, 1));
			}
		}

		// What a building or unit is for (Buildable.Description, as OpenSYW's build palette), as the tooltip's first line;
		// empty for what has none (research).
		static string Purpose(BuildableInfo buildable) =>
			string.IsNullOrEmpty(buildable?.Description) ? "" : FluentProvider.GetMessage(buildable.Description) + "\n";

		// The buildings (or research) still missing for it, by name: the prerequisites the player doesn't meet yet.
		string Missing(BuildableInfo buildable, TechTree techTree)
		{
			if (buildable == null)
				return "";

			return string.Join(", ", buildable.Prerequisites
				.Where(p => !p.StartsWith('~') && !techTree.HasPrerequisites(new[] { p }))
				.Select(p => world.Map.Rules.Actors.TryGetValue(p.TrimStart('!'), out var a) ? ActorName(a) : p));
		}

		// A label with its key, "Attack (A)", as the key is bound now.
		static string KeyLabel(string label, HotkeyReference key)
		{
			var hotkey = key.GetValue();
			return hotkey.IsValid() ? $"{label} ({hotkey.DisplayString()})" : label;
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

		// A spell: a click casts it (on a unit or place: then a click on the map). A spell the caster also casts on its
		// own (Cure) glows while that is on, and a right click turns it off and on.
		void ShowSpell(SlotButtonWidget slot, Actor caster, PwSpell spell)
		{
			var info = spell.Info;
			var icon = info.Icon != null ? CommandIcon(info.Icon) : null;
			slot.Visible = true;
			slot.GetText = () => "";
			slot.IsHighlighted = () => info.AutoHeal && spell.AutoCast;
			if (info.AutoHeal)
				slot.OnRightClick = () => world.IssueOrder(PwSpell.SetAutoCast(caster, info.Name, !spell.AutoCast));

			var cost = info.Cost > 0 ? info.Cost : Math.Max(info.ManaCost, info.ManaPerStep);
			slot.GetTooltipText = () => FluentProvider.GetMessage($"spell-{info.Name}", "cost", cost);
			slot.GetTooltipDesc = () => spell.IsUnlocked(caster)
				? FluentProvider.GetMessage($"spell-{info.Name}-desc")
				: (spell.Info.Prerequisites.Length > 0 && (caster.TraitOrDefault<PwLevels>()?.Level ?? 0) >= info.MinLevel)
				? FluentProvider.GetMessage($"spell-{info.Name}-locked")
				: FluentProvider.GetMessage("label-spell-locked", "level", info.MinLevel);
			slot.Get<SpriteWidget>("ICON").GetSprite = () => icon;
			slot.Get<LabelWidget>("PROGRESS").GetText = () => cost > 0 ? $"{cost}" : "";
			slot.IsDisabled = () => !spell.IsReady(caster);
			slot.OnClick = () =>
			{
				if (info.Target == PwSpellTarget.Self)
					world.IssueOrder(PwSpell.Cast(caster, info.Name, Target.FromActor(caster)));
				else if (info.Target == PwSpellTarget.Ground)
					world.OrderGenerator = new AbilityOrderGenerator(caster, PwSpell.OrderID, info.Name);
				else
					world.OrderGenerator = new AbilityOrderGenerator(caster, PwSpell.OrderID, spell.CanTarget, info.Name);
			};
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
