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
using System.Reflection;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets.Logic
{
	// Puts the in-game interface on the race's bar (InterfaceBarWidget) every tick, so it follows any window size: the
	// minimap in the hole, the selection panel on the left, the command panel's slots (two rows, scaled with the bar)
	// on the right, and the bar's Menu, Allies and Msg buttons on the raised blocks with the original button art.
	// Menu opens the game menu (OPTIONS_BUTTON, MenuButtonsChromeLogic); Allies the game info, Msg the chat. Also the
	// berries (CASH) and population (POPULATION) counters at the top right.
	public class InterfaceBarLogic : ChromeLogic
	{
		const int SlotSize = 40;
		const int SlotRows = 2;

		readonly World world;
		readonly InterfaceBarWidget bar;
		readonly RadarWidget radar;
		readonly (Widget Icon, Widget Label)[] counters;
		readonly Widget selection, commands;
		Rectangle radarArea;
		readonly ButtonWidget[] slots;
		readonly (ButtonWidget Button, string Area, string Image)[] buttons;
		readonly Widget worldRoot, menuRoot;
		Widget openPanel;

		[ObjectCreator.UseCtor]
		public InterfaceBarLogic(Widget widget, World world)
		{
			this.world = world;
			bar = widget.Get<InterfaceBarWidget>("INTERFACE_BAR");
			var race = world.LocalPlayer?.Faction.InternalName;
			var collection = race != null && ChromeProvider.Collections.ContainsKey($"bar-{race}") ? $"bar-{race}" : "bar-primitive";
			bar.GetCollection = () => collection;

			radar = widget.Get<RadarWidget>("RADAR");
			selection = widget.Get("SELECTION_PANEL");
			commands = widget.Get("COMMAND_PANEL");
			slots = commands.Children.OfType<ButtonWidget>().Where(b => b.Id.StartsWith("SLOT_", System.StringComparison.Ordinal)).ToArray();
			foreach (var slot in slots)
			{
				slot.Get<SpriteWidget>("ICON").GetScale = () => bar.Scale;
				slot.Get<SpriteWidget>("ICON").GetSprite ??= () => null;
			}

			buttons = new[]
			{
				(widget.Get<ButtonWidget>("OPTIONS_BUTTON"), "menu", "menu"),
				(widget.Get<ButtonWidget>("ALLIES_BUTTON"), "allies", "allies"),
				(widget.Get<ButtonWidget>("MSG_BUTTON"), "msg", "msg"),
			};

			foreach (var (button, _, image) in buttons)
			{
				var b = button;
				var art = b.Get<ImageWidget>("ART");
				art.GetImageCollection = () => collection + "-buttons";
				art.GetImageName = () => b.Depressed || b.IsHighlighted() ? image + "-pressed" : image;
			}

			var cash = widget.Get<LabelWithTooltipWidget>("CASH");
			cash.GetText = () =>
			{
				var resources = world.LocalPlayer?.PlayerActor.TraitOrDefault<PlayerResources>();
				return resources == null ? "" : FluentProvider.GetMessage("label-berries", "amount", resources.Cash + resources.Resources);
			};
			cash.GetTooltipText = () => FluentProvider.GetMessage("tooltip-berries");
			var cashIcon = widget.Get<StretchedImageWidget>("CASH_ICON");
			cashIcon.GetTooltipText = cash.GetTooltipText;

			var population = widget.Get<LabelWithTooltipWidget>("POPULATION");
			population.GetText = () =>
			{
				var player = world.LocalPlayer;
				return player == null ? "" : FluentProvider.GetMessage("label-population",
					"used", PwPopulation.Used(player), "limit", PwPopulation.Capacity(player));
			};
			population.GetTooltipText = () => FluentProvider.GetMessage("tooltip-population");
			var populationIcon = widget.Get<StretchedImageWidget>("POPULATION_ICON");
			populationIcon.GetTooltipText = population.GetTooltipText;
			counters = new (Widget, Widget)[] { (cashIcon, cash), (populationIcon, population) };

			worldRoot = Ui.Root.Get("WORLD_ROOT");
			menuRoot = Ui.Root.Get("MENU_ROOT");
			buttons[1].Button.OnClick = () => OpenMenuPanel(IngameInfoPanel.Objectives);
			buttons[2].Button.OnClick = () => OpenMenuPanel(IngameInfoPanel.Chat);

			Layout();
		}

		public override void Tick() => Layout();

		static WidgetBounds Bounds(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);

		void Layout()
		{
			bar.Layout();
			var hole = bar.Area("hole");
			if (hole != radarArea)
			{
				radarArea = hole;
				radar.Bounds = Bounds(hole);
				RefreshRadar();
			}

			// The counters at the top right: icon (the original 14 px, scaled with the bar) then the number.
			var iconSize = (int)(16 * bar.Scale);
			var right = bar.Bounds.Width - 8;
			for (var i = 0; i < counters.Length; i++)
			{
				var (icon, label) = counters[i];
				var y = 8 + i * (iconSize + 6);
				icon.Bounds = new WidgetBounds(right - 110 - iconSize - 6, y, iconSize, iconSize);
				label.Bounds = new WidgetBounds(right - 110, y + (iconSize - 20) / 2, 110, 20);
			}

			selection.Bounds = Bounds(bar.Area("left-panel"));
			commands.Bounds = Bounds(bar.Area("right-panel"));

			foreach (var (button, area, _) in buttons)
			{
				button.Bounds = Bounds(bar.Area(area));
				button.Get<ImageWidget>("ART").Bounds = new WidgetBounds(0, 0, button.Bounds.Width, button.Bounds.Height);
			}

			// The slots: two rows, as many columns as fit, each the original 38 px icon scaled with the bar.
			var size = (int)(SlotSize * bar.Scale);
			var gap = (commands.Bounds.Height - SlotRows * size) / (SlotRows + 1);
			var columns = System.Math.Max(1, (commands.Bounds.Width - gap) / (size + gap));
			for (var i = 0; i < slots.Length; i++)
			{
				var slot = slots[i];
				slot.Bounds = new WidgetBounds(gap + i % columns * (size + gap), gap + i / columns * (size + gap), size, size);
				slot.Get<SpriteWidget>("ICON").Bounds = new WidgetBounds(1, 1, size - 2, size - 2);
				slot.Get<LabelWidget>("PROGRESS").Bounds = new WidgetBounds(0, size - 16, size, 14);
				if (i >= columns * SlotRows)
					slot.Visible = false;
			}
		}

		// The engine's radar works out where it draws (and takes clicks) when it starts, from where it was then; after
		// it moves or resizes with the bar it must do so again. Its refresh is private: called through reflection
		// (release-20250330), then its area is moved to the screen as its opening animation does.
		static readonly MethodInfo RadarBoundsChanged = typeof(RadarWidget).GetMethod("MapBoundsChanged", BindingFlags.NonPublic | BindingFlags.Instance);
		static readonly FieldInfo RadarMapRect = typeof(RadarWidget).GetField("mapRect", BindingFlags.NonPublic | BindingFlags.Instance);

		void RefreshRadar()
		{
			if (RadarBoundsChanged == null || RadarMapRect == null)
				throw new InvalidOperationException("RadarWidget changed: InterfaceBarLogic.RefreshRadar needs updating for this engine.");

			RadarBoundsChanged.Invoke(radar, null);
			var r = (Rectangle)RadarMapRect.GetValue(radar);
			var origin = radar.RenderOrigin;
			RadarMapRect.SetValue(radar, new Rectangle(r.X + origin.X, r.Y + origin.Y, r.Width, r.Height));
		}

		// As MenuButtonsChromeLogic does for its buttons: the game info at a panel, pausing a single player game.
		void OpenMenuPanel(IngameInfoPanel initialPanel)
		{
			if (openPanel != null)
				return;

			world.CancelInputMode();
			worldRoot.IsVisible = () => false;
			var cachedPause = world.PredictedPaused;
			if (world.LobbyInfo.NonBotClients.Count() == 1)
				world.SetPauseState(true);

			openPanel = Game.LoadWidget(world, "INGAME_MENU", menuRoot, new WidgetArgs
			{
				{ "initialPanel", initialPanel },
				{
					"onExit", () =>
					{
						worldRoot.IsVisible = () => true;
						if (world.LobbyInfo.NonBotClients.Count() == 1)
							world.SetPauseState(cachedPause);

						menuRoot.RemoveChild(openPanel);
						openPanel = null;
					}
				},
			});

			Game.RunAfterTick(Ui.ResetTooltips);
		}
	}
}
