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

namespace OpenRA.Mods.Pw.Widgets
{
	// The victory or defeat screen over the whole window (Mod change; PwEndScreenLogic adds it to the UI root): the
	// collection's picture fitted to the window, its blurred copy filling the margins, fading in. The picture carries
	// the message panel with its title (tools/end_screens.py); the widget writes the elapsed time and "Click to
	// continue" in the panel's lower part. A click or Escape, Enter or Space closes it, and opens the in-game menu
	// (statistics, leave) unless one is already open.
	public class PwEndScreenWidget : Widget
	{
		// The picture's size and its panel in picture pixels: tools/end_screens.py PANEL (centre 1010, 416; 426 x 212).
		const int PictureWidth = 1248;
		const int PictureHeight = 832;
		const int FadeMilliseconds = 600;

		// Clicks this soon after it appears don't close it (a click meant for the battle).
		const int IgnoreInputMilliseconds = 800;

		static readonly Rectangle Panel = new(1010 - 213, 416 - 106, 426, 212);

		readonly World world;
		readonly Sprite picture;
		readonly Sprite back;
		readonly string elapsed;
		readonly string next;
		readonly long shownAt;

		public PwEndScreenWidget(World world, string collection, int elapsedTicks)
		{
			this.world = world;
			picture = ChromeProvider.GetImage(collection, "picture");
			back = ChromeProvider.GetImage(collection, "back");
			var seconds = elapsedTicks * world.Timestep / 1000;
			var time = seconds >= 3600 ? $"{seconds / 3600}:{seconds / 60 % 60:D2}:{seconds % 60:D2}" : $"{seconds / 60}:{seconds % 60:D2}";
			elapsed = FluentProvider.GetMessage("label-end-elapsed", "time", time);
			next = FluentProvider.GetMessage("label-end-continue");
			shownAt = Game.RunTime;
			FitWindow();
		}

		void FitWindow()
		{
			var size = Game.Renderer.Resolution;
			Bounds = new WidgetBounds(0, 0, size.Width, size.Height);
		}

		public override void Tick()
		{
			if (!Game.IsCurrentWorld(world))
			{
				Parent?.RemoveChild(this);
				return;
			}

			FitWindow();
			if (!HasKeyboardFocus)
				TakeKeyboardFocus();
		}

		public override void Draw()
		{
			var size = Game.Renderer.Resolution;
			var alpha = Math.Min(1f, (Game.RunTime - shownAt) / (float)FadeMilliseconds);
			var renderer = Game.Renderer.RgbaSpriteRenderer;

			// The margins: the blurred copy covering the window.
			var backScale = Math.Max(size.Width / back.Size.X, size.Height / back.Size.Y);
			renderer.DrawSprite(back, new float3((size.Width - back.Size.X * backScale) / 2, (size.Height - back.Size.Y * backScale) / 2, 0),
				backScale, float3.Ones, alpha);

			// The picture, as large as fits, centred.
			var scale = Math.Min(size.Width / (float)PictureWidth, size.Height / (float)PictureHeight);
			var x = (size.Width - PictureWidth * scale) / 2;
			var y = (size.Height - PictureHeight * scale) / 2;
			renderer.DrawSprite(picture, new float3(x, y, 0), scale, float3.Ones, alpha);

			if (alpha < 1)
				return;

			// The two lines in the panel's lower part, as the mock-ups: 46 and 20 picture pixels above its bottom.
			var font = Game.Renderer.Fonts["MediumBold"];
			var centre = x + (Panel.X + Panel.Width / 2f) * scale;
			var bottom = y + Panel.Bottom * scale;
			DrawCentred(font, elapsed, centre, bottom - 46 * scale, Color.FromArgb(240, 230, 210));
			DrawCentred(font, next, centre, bottom - 20 * scale, Color.FromArgb(200, 190, 170));
		}

		static void DrawCentred(SpriteFont font, string text, float centreX, float centreY, Color color)
		{
			var measure = font.Measure(text);
			font.DrawTextWithContrast(text, new float2(centreX - measure.X / 2f, centreY - measure.Y / 2f), color, Color.Black, 2);
		}

		bool Ready => Game.RunTime - shownAt >= IgnoreInputMilliseconds;

		public override bool HandleMouseInput(MouseInput mi)
		{
			// Everything under it is out of reach while it shows.
			if (mi.Event == MouseInputEvent.Up && Ready)
				Close();

			return true;
		}

		public override bool HandleKeyPress(KeyInput e)
		{
			if (e.Event == KeyInputEvent.Down && Ready && (e.Key == Keycode.ESCAPE || e.Key == Keycode.RETURN || e.Key == Keycode.SPACE))
			{
				Close();
				return true;
			}

			return false;
		}

		void Close()
		{
			YieldKeyboardFocus();
			Parent?.RemoveChild(this);

			// The in-game menu (statistics, leave), unless one is open already.
			var menuRoot = Ui.Root.GetOrNull("MENU_ROOT");
			if (menuRoot != null && menuRoot.Children.Count == 0)
				Ui.Root.GetOrNull<MenuButtonWidget>("OPTIONS_BUTTON")?.OnClick?.Invoke();
		}
	}
}
