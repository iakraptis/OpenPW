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
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Mods.Pw.Warheads;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets.Logic
{
	// The left panel of the interface bar: what is selected. One actor: its large portrait (the image's "portrait"
	// sequence), name, health bar, and its numbers (HP, level and experience, attack, armour, the meat it carries, mana,
	// a building's construction). Several: their small portraits with health bars; a click selects that one alone.
	// Every part has a tooltip. InterfaceBarLogic places the panel; this lays it out inside, scaled with the bar.
	public class SelectionPanelLogic : ChromeLogic
	{
		const int MaxIcons = 24;
		static readonly Color HealthGood = Color.FromArgb(40, 200, 40);
		static readonly Color HealthHurt = Color.FromArgb(230, 200, 30);
		static readonly Color HealthLow = Color.FromArgb(220, 40, 30);

		readonly World world;
		readonly Widget panel;
		InterfaceBarWidget bar;
		readonly SpriteWidget portrait;
		readonly ColorBlockWidget healthBack, healthFill;
		readonly LabelWithTooltipWidget name;
		readonly LabelWithTooltipWidget[] lines;
		readonly ButtonWidget[] icons;
		Actor[] shown = Array.Empty<Actor>();

		[ObjectCreator.UseCtor]
		public SelectionPanelLogic(Widget widget, World world)
		{
			this.world = world;
			panel = widget;
			portrait = widget.Get<SpriteWidget>("PORTRAIT");
			portrait.GetScale = () => bar?.Scale ?? 1f;
			portrait.GetSprite = () => null;
			healthBack = widget.Get<ColorBlockWidget>("HEALTH_BACK");
			healthFill = widget.Get<ColorBlockWidget>("HEALTH_FILL");
			name = widget.Get<LabelWithTooltipWidget>("NAME");
			lines = Enumerable.Range(0, 6).Select(i => widget.Get<LabelWithTooltipWidget>($"LINE_{i}")).ToArray();
			icons = Enumerable.Range(0, MaxIcons).Select(i => widget.Get<ButtonWidget>($"UNIT_{i}")).ToArray();
			for (var i = 0; i < icons.Length; i++)
			{
				var index = i;
				var icon = icons[i];
				icon.Get<SpriteWidget>("ICON").GetScale = () => (bar?.Scale ?? 1f) * 0.9f;
				icon.Get<SpriteWidget>("ICON").GetSprite = () => null;
				icon.OnClick = () =>
				{
					if (index < shown.Length && shown[index].IsInWorld)
						world.Selection.Combine(world, new[] { shown[index] }, false, true);
				};
			}
		}

		static string Name(ActorInfo info)
		{
			var tooltip = info.TraitInfos<TooltipInfo>().FirstOrDefault();
			return tooltip != null ? FluentProvider.GetMessage(tooltip.Name) : info.Name;
		}

		Sprite Sequence(Actor actor, string sequence)
		{
			var image = actor.Info.TraitInfoOrDefault<RenderSpritesInfo>()?.Image ?? actor.Info.Name;
			return world.Map.Sequences.HasSequence(image, sequence) ? world.Map.Sequences.GetSequence(image, sequence).GetSprite(0) : null;
		}

		static Color HealthColor(IHealth health) =>
			health.HP * 2 > health.MaxHP ? HealthGood : health.HP * 4 > health.MaxHP ? HealthHurt : HealthLow;

		// The attack it deals per hit: its weapon's attack plus levels and upgrades, with morale and other modifiers.
		static int Attack(Actor actor)
		{
			var armament = actor.TraitsImplementing<Armament>().FirstOrDefault();
			var warhead = armament?.Weapon.Warheads.OfType<SubtractArmorDamageWarhead>().FirstOrDefault();
			if (warhead == null)
				return 0;

			var attack = warhead.Damage + actor.TraitsImplementing<IPwStatBonus>().Sum(b => b.AttackBonus);
			return OpenRA.Mods.Common.Util.ApplyPercentageModifiers(attack, actor.TraitsImplementing<IFirepowerModifier>().Select(m => m.GetFirepowerModifier()));
		}

		static int Armor(Actor actor)
		{
			var armor = actor.TraitsImplementing<PwArmor>().Sum(a => a.Value) + actor.TraitsImplementing<IPwStatBonus>().Sum(b => b.ArmorBonus);
			return OpenRA.Mods.Common.Util.ApplyPercentageModifiers(armor, actor.TraitsImplementing<PwArmorMultiplier>().Select(m => m.Modifier));
		}

		public override void Tick()
		{
			bar ??= Ui.Root.GetOrNull<InterfaceBarWidget>("INTERFACE_BAR");
			var selected = world.Selection.Actors.Where(a => a.IsInWorld && !a.IsDead).ToArray();

			// The container stays visible (a hidden widget's logic doesn't tick); its contents come and go.
			if (selected.Length == 0)
			{
				portrait.Visible = name.Visible = healthBack.Visible = healthFill.Visible = false;
				Hide(lines);
				Hide(icons);
				return;
			}

			var r = panel.Bounds;
			var k = bar?.Scale ?? 1f;
			if (selected.Length == 1)
				ShowOne(selected[0], r.Width, r.Height, k);
			else
				ShowMany(selected, r.Width, r.Height, k);
		}

		static void Hide(IEnumerable<Widget> widgets)
		{
			foreach (var w in widgets)
				w.Visible = false;
		}

		void ShowOne(Actor actor, int width, int height, float k)
		{
			Hide(icons);
			shown = Array.Empty<Actor>();

			var size = (int)(50 * k);
			var pad = (int)(4 * k);
			portrait.Visible = true;
			portrait.Bounds = new WidgetBounds(pad, (height - size) / 2, size, size);
			var sprite = Sequence(actor, "portrait") ?? Sequence(actor, "icon");
			portrait.GetSprite = () => sprite;

			var x = pad * 2 + size;
			var textWidth = width - x - pad;
			name.Visible = true;
			name.Bounds = new WidgetBounds(x, pad, textWidth, 18);
			var title = Name(actor.Info);
			var owner = actor.Owner;
			name.GetText = () => title;
			name.GetTooltipText = () => owner.PlayerName == "Creeps" || owner.NonCombatant ? title : $"{title} ({owner.ResolvedPlayerName})";

			var health = actor.TraitOrDefault<IHealth>();
			healthBack.Visible = healthFill.Visible = health != null;
			if (health != null)
			{
				var barWidth = textWidth;
				healthBack.Bounds = new WidgetBounds(x, pad + 20, barWidth, (int)Math.Max(4, 5 * k));
				healthFill.Bounds = new WidgetBounds(x, pad + 20, barWidth * health.HP / Math.Max(1, health.MaxHP), healthBack.Bounds.Height);
				healthFill.GetColor = () => HealthColor(health);
			}

			// The numbers, two columns of three lines under the health bar.
			var entries = new List<(string Text, string Tooltip)>();
			if (health != null)
				entries.Add((FluentProvider.GetMessage("label-stat-hp", "hp", health.HP, "max", health.MaxHP), FluentProvider.GetMessage("tooltip-stat-hp")));

			var levels = actor.TraitOrDefault<PwLevels>();
			if (levels != null)
				entries.Add((FluentProvider.GetMessage("label-stat-level", "level", levels.Level, "xp", levels.Experience, "next", levels.ExperiencePerLevel),
					FluentProvider.GetMessage("tooltip-stat-level")));

			if (actor.TraitsImplementing<Armament>().Any())
				entries.Add((FluentProvider.GetMessage("label-stat-attack", "attack", Attack(actor)), FluentProvider.GetMessage("tooltip-stat-attack")));

			if (actor.TraitsImplementing<PwArmor>().Any())
				entries.Add((FluentProvider.GetMessage("label-stat-armor", "armor", Armor(actor)), FluentProvider.GetMessage("tooltip-stat-armor")));

			var meat = actor.TraitOrDefault<PwMeatEater>();
			if (meat != null)
				entries.Add((FluentProvider.GetMessage("label-stat-meat", "meat", meat.Meat), FluentProvider.GetMessage("tooltip-stat-meat")));

			var mana = actor.TraitOrDefault<PwMana>();
			if (mana != null)
				entries.Add((FluentProvider.GetMessage("label-stat-mana", "mana", mana.Mana, "max", mana.Max), FluentProvider.GetMessage("tooltip-stat-mana")));

			var construction = actor.TraitOrDefault<UnderConstruction>();
			if (construction != null && !construction.IsComplete)
				entries.Add((FluentProvider.GetMessage("label-stat-construction", "percent", (int)(construction.Progress * 100)),
					FluentProvider.GetMessage("tooltip-stat-construction")));

			var top = pad + 20 + healthBack.Bounds.Height + 2;
			var lineHeight = Math.Max(14, (height - top - pad) / 3);
			for (var i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				line.Visible = i < entries.Count;
				if (!line.Visible)
					continue;

				var (text, tooltip) = entries[i];
				line.Bounds = new WidgetBounds(x + i / 3 * (textWidth / 2), top + i % 3 * lineHeight, textWidth / 2, lineHeight);
				line.GetText = () => text;
				line.GetTooltipText = () => tooltip;
			}
		}

		void ShowMany(Actor[] selected, int width, int height, float k)
		{
			portrait.Visible = name.Visible = healthBack.Visible = healthFill.Visible = false;
			Hide(lines);

			shown = selected.Take(MaxIcons).ToArray();
			var pad = (int)(3 * k);
			var size = (int)(38 * k * 0.9f) + 4;
			var columns = Math.Max(1, (width - pad) / (size + pad));
			var rows = Math.Max(1, (height - pad) / (size + pad));
			for (var i = 0; i < icons.Length; i++)
			{
				var icon = icons[i];
				icon.Visible = i < shown.Length && i < columns * rows;
				if (!icon.Visible)
					continue;

				var actor = shown[i];
				icon.Bounds = new WidgetBounds(pad + i % columns * (size + pad), pad + i / columns * (size + pad), size, size);
				var sprite = Sequence(actor, "icon");
				var iconWidget = icon.Get<SpriteWidget>("ICON");
				iconWidget.Bounds = new WidgetBounds(2, 2, size - 4, size - 4);
				iconWidget.GetSprite = () => sprite;

				var health = actor.TraitOrDefault<IHealth>();
				var hp = icon.Get<ColorBlockWidget>("HP");
				hp.Visible = health != null;
				if (health != null)
				{
					hp.Bounds = new WidgetBounds(2, size - 6, (size - 4) * health.HP / Math.Max(1, health.MaxHP), 4);
					hp.GetColor = () => HealthColor(health);
				}

				var title = Name(actor.Info);
				icon.GetTooltipText = () => health != null ? $"{title} ({health.HP}/{health.MaxHP})" : title;
			}
		}
	}
}
