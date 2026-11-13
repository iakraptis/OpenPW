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
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.Pw.AssetInstaller
{
	// The bars' Menu buttons, made for the mod so that every race's says "MENU" (the original Elf's and Demon's carry
	// a picture): the word in a font of the race's character (SIL Open Font License fonts, AssetInstaller/Fonts),
	// lit in the style and colours of the race's art, on the race's own blank plaque, cut from its bar where the
	// button sits. Released and pressed (darker, the word a pixel down and right, the plaque's top and left edges in
	// shadow), side by side in chrome/bar-<race>-menu.png.
	public static class MenuButtons
	{
		// Supersampling of the lettering.
		const int Up = 8;

		sealed class Style
		{
			public string Font;

			// The plaque: its top left in the bar art's own pixels, and the button's size.
			public int X, Y, Width, Height;

			// The box the word is fitted into: side margin, top, bottom.
			public int Side, Top, Bottom;

			// Released and pressed: fill gradient (top, bottom), lit edge, shaded edge.
			public float3[] Fill, FillPressed;
			public float3 Light, LightPressed, Dark, Outline;

			// Halo around the released word, none if null.
			public float3? Glow;
		}

		static float3 C(int r, int g, int b) => new(r, g, b);

		static readonly (string Race, Style Style)[] Styles =
		{
			// Glowing ember-orange letters on the dark wood, as the original's lit carving; pressed, the glow dies down.
			("primitive", new Style
			{
				Font = "CaesarDressing-Regular.ttf", X = 460, Y = 25, Width = 82, Height = 32, Side = 9, Top = 9, Bottom = 6,
				Fill = new[] { C(255, 214, 120), C(226, 112, 26) }, FillPressed = new[] { C(196, 120, 52), C(128, 62, 18) },
				Light = C(255, 240, 190), LightPressed = C(210, 150, 90), Dark = C(120, 48, 8), Outline = C(36, 18, 6),
				Glow = C(255, 140, 30)
			}),

			// Pale stone letters with chiselled shadows on the green-grey stone.
			("tyrano", new Style
			{
				Font = "MetalMania-Regular.ttf", X = 468, Y = 43, Width = 81, Height = 28, Side = 8, Top = 9, Bottom = 2,
				Fill = new[] { C(222, 220, 190), C(150, 148, 116) }, FillPressed = new[] { C(150, 148, 120), C(96, 94, 72) },
				Light = C(250, 248, 226), LightPressed = C(250, 248, 226), Dark = C(70, 68, 50), Outline = C(24, 24, 16)
			}),

			// Relief carved in the red wood, lit like the tree ornament.
			("elf", new Style
			{
				Font = "UncialAntiqua-Regular.ttf", X = 460, Y = 31, Width = 85, Height = 31, Side = 11, Top = 7, Bottom = 8,
				Fill = new[] { C(246, 184, 138), C(196, 112, 72) }, FillPressed = new[] { C(180, 116, 82), C(126, 66, 42) },
				Light = C(255, 218, 182), LightPressed = C(255, 218, 182), Dark = C(90, 36, 20), Outline = C(34, 12, 4)
			}),

			// Inlaid in the iron plate with the swirl's copper-pink, a blood-red shadow around it.
			("demon", new Style
			{
				Font = "PirataOne-Regular.ttf", X = 457, Y = 16, Width = 87, Height = 27, Side = 12, Top = 6, Bottom = 3,
				Fill = new[] { C(246, 206, 186), C(176, 92, 82) }, FillPressed = new[] { C(176, 136, 118), C(116, 56, 50) },
				Light = C(255, 236, 222), LightPressed = C(255, 236, 222), Dark = C(70, 24, 24), Outline = C(16, 10, 10),
				Glow = C(120, 20, 16)
			}),
		};

		// Writes chrome/bar-<race>-menu.png from the race's bar art (RGBA, sheetWidth pixels a row).
		public static void Export(InstallContext c, IPlatform platform, string race, byte[] bar, int sheetWidth)
		{
			var style = Styles.First(s => s.Race == race).Style;
			var (w, h) = (style.Width, style.Height);
			var word = Word(platform, style);

			const int SheetWidth = 256;
			const int SheetHeight = 32;
			var sheet = new byte[4 * SheetWidth * SheetHeight];
			for (var pressed = 0; pressed < 2; pressed++)
			{
				var rgb = Button(style, bar, sheetWidth, word, pressed == 1);
				for (var y = 0; y < h; y++)
				{
					for (var x = 0; x < w; x++)
					{
						var o = 4 * (y * SheetWidth + x + pressed * (w + 1));
						var v = rgb[y, x];
						sheet[o] = (byte)Math.Clamp(v.X + 0.5f, 0, 255);
						sheet[o + 1] = (byte)Math.Clamp(v.Y + 0.5f, 0, 255);
						sheet[o + 2] = (byte)Math.Clamp(v.Z + 0.5f, 0, 255);
						sheet[o + 3] = bar[4 * ((style.Y + y) * sheetWidth + style.X + x) + 3];
					}
				}
			}

			c.WriteFile($"chrome/bar-{race}-menu.png", new Png(sheet, SpriteFrameType.Rgba32, SheetWidth, SheetHeight).Save());
		}

		static float3[,] Button(Style s, byte[] bar, int sheetWidth, float[,] word, bool pressed)
		{
			var (w, h) = (s.Width, s.Height);
			var rgb = new float3[h, w];
			for (var y = 0; y < h; y++)
			{
				for (var x = 0; x < w; x++)
				{
					var o = 4 * ((s.Y + y) * sheetWidth + s.X + x);
					rgb[y, x] = new float3(bar[o], bar[o + 1], bar[o + 2]);
				}
			}

			var m = word;
			if (pressed)
			{
				// Sunk in: darker, its top and left edges in shadow.
				var edge = new float[h, w];
				for (var y = 0; y < h; y++)
					for (var x = 0; x < w; x++)
						edge[y, x] = y < 2 || x < 2 ? 1 : 0;

				edge = Blur(edge, 1);
				for (var y = 0; y < h; y++)
					for (var x = 0; x < w; x++)
						rgb[y, x] = Mix(0.72f * rgb[y, x], float3.Zero, edge[y, x] * 0.6f);

				m = Shift(word, 1, 1);
			}

			var fill = pressed ? s.FillPressed : s.Fill;
			var light = pressed ? s.LightPressed : s.Light;
			var glow = pressed ? null : s.Glow;
			var halo = glow != null ? Blur(m, 2.2f) : null;
			var rim = Blur(m, 0.7f);
			var below = Shift(m, 1, 1);
			var above = Shift(m, -1, -1);
			for (var y = 0; y < h; y++)
			{
				var body = Mix(fill[0], fill[1], h > 1 ? (float)y / (h - 1) : 0);
				for (var x = 0; x < w; x++)
				{
					var a = m[y, x];
					var v = rgb[y, x];
					if (halo != null)
						v = Mix(v, glow.Value, Math.Clamp(halo[y, x] * 0.9f - a, 0, 1));

					v = Mix(v, s.Outline, Math.Clamp(below[y, x] * 0.85f + rim[y, x] * 0.6f, 0, 1) * (1 - a));
					v = Mix(v, body, a);
					v = Mix(v, light, Math.Clamp(a - below[y, x], 0, 1) * 0.8f);
					v = Mix(v, s.Dark, Math.Clamp(a - above[y, x], 0, 1) * 0.7f);
					rgb[y, x] = v;
				}
			}

			return rgb;
		}

		// The word's coverage (0 to 1) at the button's size: the largest font size whose ink fits the letter box,
		// centred in it.
		static float[,] Word(IPlatform platform, Style s)
		{
			byte[] data;
			using (var stream = typeof(MenuButtons).Assembly.GetManifestResourceStream("Fonts/" + s.Font))
				data = stream.ReadAllBytes();

			using var font = platform.CreateFont(data);
			var boxW = (s.Width - 2 * s.Side) * Up;
			var boxH = (s.Height - s.Top - s.Bottom) * Up;

			(FontGlyph Glyph, int X)[] Layout(int size)
			{
				var pen = 0f;
				return "MENU".Select(ch =>
				{
					var g = font.CreateGlyph(ch, size, 1);
					var at = (int)pen;
					pen += g.Advance;
					return (g, at);
				}).ToArray();
			}

			static Rectangle Ink((FontGlyph Glyph, int X)[] glyphs)
			{
				var left = glyphs.Min(g => g.X + g.Glyph.Offset.X);
				var top = glyphs.Min(g => g.Glyph.Offset.Y);
				var right = glyphs.Max(g => g.X + g.Glyph.Offset.X + g.Glyph.Size.Width);
				var bottom = glyphs.Max(g => g.Glyph.Offset.Y + g.Glyph.Size.Height);
				return Rectangle.FromLTRB(left, top, right, bottom);
			}

			int lo = 4, hi = 400;
			while (lo < hi)
			{
				var mid = (lo + hi + 1) / 2;
				var ink = Ink(Layout(mid));
				if (ink.Width <= boxW && ink.Height <= boxH)
					lo = mid;
				else
					hi = mid - 1;
			}

			var glyphs = Layout(lo);
			var box = Ink(glyphs);
			var originX = s.Side * Up + (boxW - box.Width) / 2 - box.Left;
			var originY = s.Top * Up + (boxH - box.Height) / 2 - box.Top;

			var bigW = s.Width * Up;
			var bigH = s.Height * Up;
			var big = new float[bigH, bigW];
			foreach (var (g, gx) in glyphs)
			{
				for (var y = 0; y < g.Size.Height; y++)
				{
					for (var x = 0; x < g.Size.Width; x++)
					{
						var px = originX + gx + g.Offset.X + x;
						var py = originY + g.Offset.Y + y;
						if (px >= 0 && py >= 0 && px < bigW && py < bigH)
							big[py, px] = Math.Max(big[py, px], g.Data[y * g.Size.Width + x] / 255f);
					}
				}
			}

			var m = new float[s.Height, s.Width];
			for (var y = 0; y < s.Height; y++)
			{
				for (var x = 0; x < s.Width; x++)
				{
					var sum = 0f;
					for (var j = 0; j < Up; j++)
						for (var i = 0; i < Up; i++)
							sum += big[y * Up + j, x * Up + i];

					m[y, x] = sum / (Up * Up);
				}
			}

			return m;
		}

		static float3 Mix(float3 a, float3 b, float t) => (1 - t) * a + t * b;

		// out(x, y) = a(x - dx, y - dy), zero outside.
		static float[,] Shift(float[,] a, int dx, int dy)
		{
			var (h, w) = (a.GetLength(0), a.GetLength(1));
			var o = new float[h, w];
			for (var y = 0; y < h; y++)
				for (var x = 0; x < w; x++)
					if (x - dx >= 0 && x - dx < w && y - dy >= 0 && y - dy < h)
						o[y, x] = a[y - dy, x - dx];

			return o;
		}

		// Gaussian blur (sigma in pixels), edges clamped.
		static float[,] Blur(float[,] a, float sigma)
		{
			var r = (int)Math.Ceiling(3 * sigma);
			var k = Enumerable.Range(-r, 2 * r + 1).Select(i => (float)Math.Exp(-i * i / (2 * sigma * sigma))).ToArray();
			var total = k.Sum();
			var (h, w) = (a.GetLength(0), a.GetLength(1));
			var t = new float[h, w];
			var o = new float[h, w];
			for (var y = 0; y < h; y++)
				for (var x = 0; x < w; x++)
					t[y, x] = Enumerable.Range(-r, 2 * r + 1).Sum(i => k[i + r] * a[y, Math.Clamp(x + i, 0, w - 1)]) / total;

			for (var y = 0; y < h; y++)
				for (var x = 0; x < w; x++)
					o[y, x] = Enumerable.Range(-r, 2 * r + 1).Sum(i => k[i + r] * t[Math.Clamp(y + i, 0, h - 1), x]) / total;

			return o;
		}
	}
}
