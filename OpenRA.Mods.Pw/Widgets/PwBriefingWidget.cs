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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets
{
	// A campaign mission's briefing over the whole window (the original's Brief.mnt): the page's 800x600 picture (a file of
	// the map) as large as fits, black around it; the header in the screen's first area, the page's text in the second,
	// the CONTINUE button in the third (chrome collection "briefing": its images and the three areas, from Brief.mnt).
	// CONTINUE, Enter or Space turns the page, and after the last closes it; Escape closes it at once.
	public class PwBriefingWidget : Widget
	{
		const int ScreenWidth = 800;
		const int ScreenHeight = 600;

		readonly World world;
		readonly string header;
		readonly List<(Sprite Picture, string Text)> pages = [];
		readonly Action onClose;
		readonly Sprite button, buttonHover;
		readonly Rectangle headerArea, textArea, buttonArea;
		readonly List<Sheet> sheets = [];
		int page;
		bool hover;

		public PwBriefingWidget(World world, PwBriefingInfo info, Action onClose)
		{
			this.world = world;
			this.onClose = onClose;
			header = PwBriefingInfo.Unescape(info.Header);
			foreach (var (file, text) in info.PageList)
				pages.Add((LoadPicture(file), text));

			button = ChromeProvider.GetImage("briefing", "continue");
			buttonHover = ChromeProvider.GetImage("briefing", "continue-hover");
			headerArea = Area("area-0", new Rectangle(99, 25, 602, 76));
			textArea = Area("area-1", new Rectangle(99, 120, 602, 391));
			buttonArea = Area("area-2", new Rectangle(336, 532, 130, 32));
			FitWindow();
		}

		static Rectangle Area(string name, Rectangle fallback)
		{
			return ChromeProvider.TryGetImage("briefing", name)?.Bounds ?? fallback;
		}

		Sprite LoadPicture(string file)
		{
			if (string.IsNullOrEmpty(file) || !world.Map.Package.Contains(file))
				return null;

			using var stream = world.Map.Package.GetStream(file);
			var sheet = new Sheet(SheetType.BGRA, stream);
			sheets.Add(sheet);
			return new Sprite(sheet, new Rectangle(0, 0, ScreenWidth, ScreenHeight), TextureChannel.RGBA);
		}

		void FitWindow()
		{
			var size = Game.Renderer.Resolution;
			Bounds = new WidgetBounds(0, 0, size.Width, size.Height);
		}

		// The 800x600 screen, as large as fits the window, centred: its scale and top left corner.
		static (float Scale, float X, float Y) Screen()
		{
			var size = Game.Renderer.Resolution;
			var scale = Math.Min(size.Width / (float)ScreenWidth, size.Height / (float)ScreenHeight);
			return (scale, (size.Width - ScreenWidth * scale) / 2, (size.Height - ScreenHeight * scale) / 2);
		}

		static Rectangle OnWindow(Rectangle area)
		{
			var (scale, x, y) = Screen();
			return new Rectangle((int)(x + area.X * scale), (int)(y + area.Y * scale), (int)(area.Width * scale), (int)(area.Height * scale));
		}

		public override void Tick()
		{
			if (!Game.IsCurrentWorld(world))
			{
				Dispose();
				return;
			}

			FitWindow();
			if (!HasKeyboardFocus)
				TakeKeyboardFocus();

			// Every click is the briefing's while it shows: the minimap under CONTINUE took them too and moved the camera.
			if (!HasMouseFocus)
				TakeMouseFocus(default);
		}

		public override void Draw()
		{
			var size = Game.Renderer.Resolution;
			WidgetUtils.FillRectWithColor(new Rectangle(0, 0, size.Width, size.Height), Color.Black);
			var (scale, x, y) = Screen();
			var (picture, text) = pages[page];
			if (picture != null)
				Game.Renderer.RgbaSpriteRenderer.DrawSprite(picture, new Vector3(x, y, 0), scale);

			DrawLines(Game.Renderer.Fonts["Bold"], header, OnWindow(headerArea), Color.FromArgb(255, 230, 160), true);
			DrawLines(Game.Renderer.Fonts["Regular"], text, OnWindow(textArea), Color.FromArgb(245, 235, 215), false);

			var area = OnWindow(buttonArea);
			var image = hover ? buttonHover : button;
			Game.Renderer.RgbaSpriteRenderer.DrawSprite(image, new Vector3(area.X, area.Y, 0),
				new Vector3(area.Width, area.Height, 0) / new Vector3(image.Size.X, image.Size.Y, 1));
		}

		// The text, wrapped to the area's width, from its top (centred for the header), with a dark outline over the picture.
		static void DrawLines(SpriteFont font, string text, Rectangle area, Color color, bool centred)
		{
			var top = area.Y;
			foreach (var paragraph in text.Split('\n'))
			{
				if (paragraph.Length == 0)
				{
					top += font.Measure("A").Y / 2;
					continue;
				}

				foreach (var line in WidgetUtils.WrapText(paragraph, area.Width, font).Split('\n'))
				{
					var measure = font.Measure(line);
					var left = centred ? area.X + (area.Width - measure.X) / 2 : area.X;
					font.DrawTextWithContrast(line, new Vector2(left, top), color, Color.Black, 2);
					top += measure.Y + 2;
				}
			}
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			var over = OnWindow(buttonArea).Contains(mi.Location);
			hover = over;
			if (mi.Event == MouseInputEvent.Up && mi.Button == MouseButton.Left && over)
				Next();

			// Everything under it is out of reach while it shows.
			return true;
		}

		public override bool HandleKeyPress(KeyInput e)
		{
			if (e.Event != KeyInputEvent.Down)
				return false;

			if (e.Key == Keycode.RETURN || e.Key == Keycode.SPACE)
				Next();
			else if (e.Key == Keycode.ESCAPE)
				Close();
			else
				return false;

			return true;
		}

		void Next()
		{
			Game.Sound.PlayNotification(world.Map.Rules, null, "Sounds", "ClickSound", null);
			if (page + 1 < pages.Count)
				page++;
			else
				Close();
		}

		void Close()
		{
			YieldKeyboardFocus();
			YieldMouseFocus(default);
			Dispose();
			onClose();
		}

		// After this tick: the UI may be iterating its widgets.
		void Dispose()
		{
			Game.RunAfterTick(() =>
			{
				Parent?.RemoveChild(this);
				foreach (var sheet in sheets)
					sheet.Dispose();

				sheets.Clear();
			});
		}
	}
}
