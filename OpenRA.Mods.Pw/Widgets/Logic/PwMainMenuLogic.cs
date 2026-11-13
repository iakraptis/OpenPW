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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets.Logic
{
	// Lays the main menu out on the original screen (Main.mnt, chrome/main-menu.yaml) every tick, so it follows any
	// window size: the 800x600 background scaled to fit the window and centred; the picture buttons, the version and
	// the panel in the middle (the submenus and the Settings and Extras buttons) on their original spots, scaled with
	// it. Each picture button draws its original art for its state. Runs beside MainMenuLogic, which does what they do;
	// the Credits picture opens the credits as the Extras menu's button does, and Intro waits for the intro.
	public class PwMainMenuLogic : ChromeLogic
	{
		const string Collection = "main-menu";
		const int ScreenWidth = 800;
		const int ScreenHeight = 600;

		readonly Widget surround, background, mainMenu, menus, panelButtons, version;
		readonly (ButtonWidget Button, string Area)[] pictures;

		[ObjectCreator.UseCtor]
		public PwMainMenuLogic(Widget widget)
		{
			surround = widget.Get("SURROUND");
			background = widget.Get("BACKGROUND");
			mainMenu = widget.Get("MAIN_MENU");
			menus = widget.Get("MENUS");
			panelButtons = widget.Get("PANEL_BUTTONS");
			version = widget.Get("VERSION_LABEL");

			// Development builds have no version, only the SDK's placeholder.
			var modVersion = Game.ModData.Manifest.Metadata.Version;
			version.IsVisible = () => !modVersion.Contains("DEV_VERSION", StringComparison.Ordinal);

			pictures = new[]
			{
				(mainMenu.Get<ButtonWidget>("SINGLEPLAYER_BUTTON"), "single"),
				(mainMenu.Get<ButtonWidget>("MULTIPLAYER_BUTTON"), "multi"),
				(mainMenu.Get<ButtonWidget>("INTRO_BUTTON"), "replay"),
				(mainMenu.Get<ButtonWidget>("MAIN_CREDITS_BUTTON"), "credit"),
				(mainMenu.Get<ButtonWidget>("QUIT_BUTTON"), "quit"),
			};

			foreach (var (button, image) in pictures)
			{
				var b = button;
				b.Get<StretchedImageWidget>("ART").GetImageName = () =>
				{
					if (b.IsDisabled())
						return image + "-disabled";

					if (b.Depressed)
						return image + "-pressed";

					var hover = Ui.MouseOverWidget == b || b.Children.Contains(Ui.MouseOverWidget);
					return hover ? image + "-hover" : image;
				};
			}

			// The original played its intro movie (Bink) here; there's no player for it yet.
			var intro = pictures[2].Button;
			intro.IsDisabled = () => true;
			intro.GetTooltipText = () => FluentProvider.GetMessage("tooltip-main-menu-intro-unavailable");

			// As the Extras menu's Credits button: the stock MainMenuLogic set that one up already.
			var credits = widget.Get("EXTRAS_MENU").Get<ButtonWidget>("CREDITS_BUTTON");
			pictures[3].Button.OnClick = () => credits.OnClick();

			// Mod change: Exit stays on the submenus too (Single Player, Extras, Map Editor), not only on the main
			// screen. MainMenuLogic finds it under MAIN_MENU (hidden on the submenus), so it moves out afterwards.
			var quit = pictures[4].Button;
			var submenus = new[] { menus.Get("SINGLEPLAYER_MENU"), menus.Get("EXTRAS_MENU"), menus.Get("MAP_EDITOR_MENU") };
			mainMenu.RemoveChild(quit);
			widget.AddChild(quit);
			quit.IsVisible = () => mainMenu.IsVisible() || Array.Exists(submenus, m => m.IsVisible());

			Layout();
		}

		public override void Tick() => Layout();

		void Layout()
		{
			var window = Game.Renderer.Resolution;
			var scale = Math.Min((float)window.Width / ScreenWidth, (float)window.Height / ScreenHeight);
			var left = (int)((window.Width - ScreenWidth * scale) / 2);
			var top = (int)((window.Height - ScreenHeight * scale) / 2);

			// A region of the original screen on the window.
			Rectangle Area(string name)
			{
				var r = ChromeProvider.TryGetImage(Collection, "area-" + name)?.Bounds ?? Rectangle.Empty;
				var x = left + (int)(r.X * scale);
				var y = top + (int)(r.Y * scale);
				return new Rectangle(x, y, left + (int)(r.Right * scale) - x, top + (int)(r.Bottom * scale) - y);
			}

			surround.Bounds = new WidgetBounds(0, 0, window.Width, window.Height);
			mainMenu.Bounds = new WidgetBounds(0, 0, window.Width, window.Height);
			background.Bounds = Bounds(Area("bg"));

			foreach (var (button, area) in pictures)
			{
				button.Bounds = Bounds(Area(area));
				button.Get("ART").Bounds = new WidgetBounds(0, 0, button.Bounds.Width, button.Bounds.Height);
			}

			// The panel: the submenus centred in it, the Settings and Extras buttons at its bottom right.
			var panel = Area("bink");
			menus.Bounds = new WidgetBounds(panel.X + (panel.Width - menus.Bounds.Width) / 2,
				panel.Y + (panel.Height - menus.Bounds.Height) / 2, menus.Bounds.Width, menus.Bounds.Height);
			panelButtons.Bounds = new WidgetBounds(panel.Right - panelButtons.Bounds.Width, panel.Bottom - panelButtons.Bounds.Height,
				panelButtons.Bounds.Width, panelButtons.Bounds.Height);

			var versionArea = Area("version");
			version.Bounds = new WidgetBounds(versionArea.X - 200, versionArea.Y, versionArea.Width + 200, versionArea.Height);
		}

		static WidgetBounds Bounds(Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
	}
}
