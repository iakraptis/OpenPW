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
using System.Reflection;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets.Logic
{
	// Puts the in-game interface on the race's bar (InterfaceBarWidget) every tick, so it follows any window size: the
	// minimap in the hole, the selection and unit-training panels on the left, the command panel's slots (two rows,
	// scaled with the bar) on the right, and the bar's Menu, Allies and Msg buttons on the raised blocks with original art.
	// Menu opens the game menu (OPTIONS_BUTTON, MenuButtonsChromeLogic); Allies the game info, Msg the chat. Also the
	// berries (CASH) and population (POPULATION) counters at the top right.
	public class InterfaceBarLogic : ChromeLogic
	{
		const int SlotSize = 40;

		// The camera zooms out this much past the default view (the engine stops there otherwise, as it does for
		// players; spectators get 0.5).
		const float ZoomOutScale = 0.75f;
		const int SlotRows = 2;
		const int TrainingWidth = 48;
		const int PanelGap = 6;

		readonly World world;
		readonly InterfaceBarWidget bar;
		readonly RadarWidget radar;
		readonly (Widget Icon, Widget Label)[] counters;
		readonly PwExposure exposure;
		readonly Widget noHQPanel, noHQBack;
		readonly LabelWidget noHQTitle, noHQRowTemplate;
		readonly List<LabelWidget> noHQRows = new();
		readonly Widget selection, training, commands;
		readonly SpriteWidget trainingIcon;
		readonly ColorBlockWidget trainingProgressBack, trainingProgressFill;
		float trainingProgress;
		Rectangle radarArea;
		readonly ButtonWidget[] slots;
		readonly (ButtonWidget Button, string Area, string Image)[] buttons;
		readonly Widget worldRoot, menuRoot;
		Widget openPanel;

		[ObjectCreator.UseCtor]
		public InterfaceBarLogic(Widget widget, World world, WorldRenderer worldRenderer)
		{
			this.world = world;
			worldRenderer.Viewport.UnlockMinimumZoom(ZoomOutScale);
			bar = widget.Get<InterfaceBarWidget>("INTERFACE_BAR");
			var race = world.LocalPlayer?.Faction.InternalName;
			var collection = race != null && ChromeProvider.Collections.ContainsKey($"bar-{race}") ? $"bar-{race}" : "bar-primitive";
			bar.GetCollection = () => collection;

			radar = widget.Get<RadarWidget>("RADAR");
			selection = widget.Get("SELECTION_PANEL");
			training = widget.Get("TRAINING_PANEL");
			trainingIcon = training.Get<SpriteWidget>("ICON");
			trainingProgressBack = training.Get<ColorBlockWidget>("PROGRESS_BACK");
			trainingProgressFill = training.Get<ColorBlockWidget>("PROGRESS_FILL");
			training.Visible = true;
			trainingIcon.Visible = false;
			trainingProgressBack.Visible = false;
			trainingProgressFill.Visible = false;
			trainingIcon.GetScale = () => bar.Scale;
			trainingIcon.GetSprite = () => null;
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
				var art = b.Get<StretchedImageWidget>("ART");

				// The Menu button is the mod's own lettered one (AssetInstaller/MenuButtons.cs), the others the original art.
				art.ImageCollection = collection + (image == "menu" ? "-menu" : "-buttons");
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

			// The players without a main building and their time before being exposed (PwExposure), at the top left:
			// one row each (name, race, time), the player's own red, allies' orange, enemies' green.
			exposure = world.WorldActor.TraitOrDefault<PwExposure>();
			noHQPanel = widget.Get("NO_HQ_PANEL");
			noHQBack = noHQPanel.Get("BACK");
			noHQTitle = noHQPanel.Get<LabelWidget>("TITLE");
			noHQRowTemplate = noHQPanel.Get<LabelWidget>("ROW");
			noHQPanel.RemoveChild(noHQRowTemplate);
			var title = FluentProvider.GetMessage("label-no-hq-panel-title");
			noHQTitle.GetText = () => title;
			noHQPanel.IsVisible = () => noHQRows.Any(r => r.IsVisible());

			worldRoot = Ui.Root.Get("WORLD_ROOT");
			menuRoot = Ui.Root.Get("MENU_ROOT");
			buttons[1].Button.OnClick = () => OpenMenuPanel(IngameInfoPanel.Objectives);
			buttons[2].Button.OnClick = () => OpenMenuPanel(IngameInfoPanel.Chat);

			Layout();
		}

		public override void Tick()
		{
			UpdateTraining();
			Layout();
		}

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

			LayoutNoHQPanel();

			// The selection panel takes the left panel, less the training column at its far right.
			var left = bar.Area("left-panel");
			var panelGap = (int)(PanelGap * bar.Scale);
			var trainingWidth = Math.Min((int)(TrainingWidth * bar.Scale), Math.Max(0, left.Width));
			var selectionWidth = Math.Max(0, left.Width - trainingWidth - panelGap);
			selection.Bounds = new WidgetBounds(left.X, left.Y, selectionWidth, left.Height);
			training.Bounds = new WidgetBounds(left.Right - trainingWidth, left.Y, trainingWidth, left.Height);
			var trainingIconSize = Math.Min((int)(38 * bar.Scale), Math.Max(0, trainingWidth - (int)(4 * bar.Scale)));
			trainingIcon.Bounds = new WidgetBounds((trainingWidth - trainingIconSize) / 2,
				(left.Height - trainingIconSize) / 2 - (int)(3 * bar.Scale), trainingIconSize, trainingIconSize);
			var progressWidth = Math.Min((int)(42 * bar.Scale), trainingWidth);
			var progressHeight = Math.Max(3, (int)(4 * bar.Scale));
			var progressX = (trainingWidth - progressWidth) / 2;
			var progressY = trainingIcon.Bounds.Y + trainingIcon.Bounds.Height + (int)(2 * bar.Scale);
			trainingProgressBack.Bounds = new WidgetBounds(progressX, progressY, progressWidth, progressHeight);
			trainingProgressFill.Bounds = new WidgetBounds(progressX, progressY,
				(int)(progressWidth * trainingProgress), progressHeight);
			commands.Bounds = Bounds(bar.Area("right-panel"));

			// The buttons sit at the bar's button areas, each the size of its own art (it differs a little by race).
			foreach (var (button, area, image) in buttons)
			{
				var at = bar.Area(area);
				var art = button.Get<StretchedImageWidget>("ART");
				var artSize = ChromeProvider.TryGetImage(art.ImageCollection, image)?.Bounds.Size ?? new Size(at.Width, at.Height);
				button.Bounds = new WidgetBounds(at.X, at.Y, (int)(artSize.Width * bar.Scale), (int)(artSize.Height * bar.Scale));
				art.Bounds = new WidgetBounds(0, 0, button.Bounds.Width, button.Bounds.Height);
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
				slot.Get("DIM").Bounds = new WidgetBounds(1, 1, size - 2, size - 2);
				slot.Get("OUTLINE").Bounds = new WidgetBounds(0, 0, size, size);
				slot.Get<LabelWidget>("PROGRESS").Bounds = new WidgetBounds(0, size - 16, size, 14);
				if (i >= columns * SlotRows)
					slot.Visible = false;
			}
		}

		void UpdateTraining()
		{
			var player = world.LocalPlayer;
			if (player == null)
			{
				HideTraining();
				trainingProgress = 0f;
				return;
			}

			var selectedBuildings = world.Selection.Actors
				.Where(a => a.IsInWorld && !a.IsDead && a.Owner == player && a.Info.HasTraitInfo<BuildingInfo>());
			var item = selectedBuildings
				.SelectMany(a => a.TraitsImplementing<ProductionQueue>())
				.Where(q => q.Enabled)
				.Select(q => q.CurrentItem())
				.FirstOrDefault(i =>
				{
					if (i == null)
						return false;

					var info = world.Map.Rules.Actors[i.Item];
					return info.HasTraitInfo<PwUpgradeInfo>()
						|| (info.HasTraitInfo<IHealthInfo>()
							&& !info.HasTraitInfo<BuildingInfo>()
							&& !info.HasTraitInfo<PwItemInfo>());
				});
			if (item == null)
			{
				HideTraining();
				trainingProgress = 0f;
				return;
			}

			var actorInfo = world.Map.Rules.Actors[item.Item];
			var image = actorInfo.TraitInfoOrDefault<RenderSpritesInfo>()?.Image ?? actorInfo.Name;
			var iconSequence = actorInfo.TraitInfo<BuildableInfo>().Icon;
			var sequences = world.Map.Sequences;
			var sprite = sequences.Images.Contains(image) && sequences.HasSequence(image, iconSequence)
				? sequences.GetSequence(image, iconSequence).GetSprite(0)
				: null;
			if (sprite == null)
			{
				HideTraining();
				trainingProgress = 0f;
				return;
			}

			trainingIcon.Visible = true;
			trainingProgressBack.Visible = true;
			trainingProgressFill.Visible = true;
			trainingIcon.GetSprite = () => sprite;
			trainingProgress = item.Done ? 1f : item.Started && item.TotalTime > 0
				? (float)(item.TotalTime - item.RemainingTime) / item.TotalTime
				: 0f;
			trainingProgress = trainingProgress.Clamp(0f, 1f);
		}

		void HideTraining()
		{
			trainingIcon.Visible = false;
			trainingProgressBack.Visible = false;
			trainingProgressFill.Visible = false;
		}

		// The engine's radar works out where it draws (and takes clicks) when it starts, from where it was then; after
		// it moves or resizes with the bar it must do so again. Its refresh is private: called through reflection
		// (release-20250330), then its area is moved to the screen as its opening animation does.
		static readonly MethodInfo RadarBoundsChanged = typeof(RadarWidget).GetMethod("MapBoundsChanged", BindingFlags.NonPublic | BindingFlags.Instance);
		static readonly FieldInfo RadarMapRect = typeof(RadarWidget).GetField("mapRect", BindingFlags.NonPublic | BindingFlags.Instance);

		void LayoutNoHQPanel()
		{
			var local = world.LocalPlayer;
			var rows = exposure == null || !exposure.Enabled ? new List<(string Text, Color Color)>() : exposure.PlayersWithoutHQ
				.Where(p => !p.Spectating)
				.Select(p => (Player: p, Left: exposure.TicksLeft(p)))
				.Where(e => e.Left >= 0)
				.OrderBy(e => e.Left)
				.Select(e => (FluentProvider.GetMessage(e.Left == 0 ? "label-no-hq-entry-exposed" : "label-no-hq-entry",
					"player", e.Player.ResolvedPlayerName, "race", FluentProvider.GetMessage(e.Player.Faction.Name),
					"time", PwExposure.FormatTime(e.Left, world.Timestep)), RowColor(local, e.Player)))
				.ToList();

			while (noHQRows.Count < rows.Count)
			{
				var row = (LabelWidget)noHQRowTemplate.Clone();
				noHQPanel.AddChild(row);
				noHQRows.Add(row);
			}

			var font = Game.Renderer.Fonts[noHQRowTemplate.Font];
			var width = Game.Renderer.Fonts[noHQTitle.Font].Measure(noHQTitle.GetText()).X;
			var lineHeight = font.Measure("Ag").Y + 2;
			for (var i = 0; i < noHQRows.Count; i++)
			{
				var shown = i < rows.Count;
				var (text, color) = shown ? rows[i] : ("", Color.White);
				noHQRows[i].IsVisible = () => shown;
				noHQRows[i].GetText = () => text;
				noHQRows[i].GetColor = () => color;
				if (shown)
					width = Math.Max(width, font.Measure(text).X);
			}

			if (rows.Count == 0)
				return;

			width += 16;
			noHQTitle.Bounds = new WidgetBounds(8, 4, width - 16, 18);
			for (var i = 0; i < rows.Count; i++)
				noHQRows[i].Bounds = new WidgetBounds(8, 24 + i * lineHeight, width - 16, lineHeight);

			noHQPanel.Bounds = new WidgetBounds(8, 8, width, 24 + rows.Count * lineHeight + 6);
			noHQBack.Bounds = new WidgetBounds(1, 1, width - 2, noHQPanel.Bounds.Height - 2);
		}

		// A row's colour: red for the viewer's own, orange for an ally's, green for an enemy's (white, spectating).
		static Color RowColor(Player viewer, Player p)
		{
			if (viewer == null)
				return Color.White;

			if (p == viewer)
				return Color.FromArgb(255, 64, 64);

			return viewer.RelationshipWith(p) == PlayerRelationship.Ally ? Color.FromArgb(255, 160, 40) : Color.FromArgb(80, 220, 80);
		}

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
