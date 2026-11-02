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
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets
{
	// The race's bottom interface bar (JW2_02 Intfbg.spr, chrome/bar-<race>.png), fitted to any window: drawn at a
	// scale that makes it HeightShare of the window height (never smaller than the original, never wider than the
	// window), its middle (raised blocks and the minimap hole) centred and kept whole, its two lower panels widened by
	// repeating one beam section. The chrome collection bar-<race> holds the pieces and, as regions that are not
	// drawn, the areas the other widgets go in (hole, left-panel, right-panel, menu, allies, msg); Area() gives them
	// on screen. Clicks on the bar don't reach the world.
	public class InterfaceBarWidget : Widget
	{
		public string Collection = "bar-primitive";
		public Func<string> GetCollection;

		[Desc("Share of the window height the bar takes (the original's 161 of 600 is 0.27).")]
		public float HeightShare = 0.21f;

		// Bar coordinates of the pieces' edges, from the collection (all relative to the original 800-wide bar).
		Sprite leftFixed, leftTile, leftJoin, center, rightJoin, rightTile, rightFixed;
		string loaded;

		public float Scale { get; private set; } = 1f;
		public int Top { get; private set; }

		// Screen x of the centre piece's left edge (the bar's x = center region's X there).
		int centerLeft;
		int leftTiles, rightTiles;
		float leftTileWidth, rightTileWidth;

		[ObjectCreator.UseCtor]
		public InterfaceBarWidget() { }

		protected InterfaceBarWidget(InterfaceBarWidget other)
			: base(other)
		{
			Collection = other.Collection;
			GetCollection = other.GetCollection;
			HeightShare = other.HeightShare;
		}

		public override Widget Clone() { return new InterfaceBarWidget(this); }

		string CurrentCollection => GetCollection?.Invoke() ?? Collection;

		bool Load()
		{
			var collection = CurrentCollection;
			if (loaded == collection)
				return center != null;

			loaded = collection;
			leftFixed = ChromeProvider.TryGetImage(collection, "left-fixed");
			leftTile = ChromeProvider.TryGetImage(collection, "left-tile");
			leftJoin = ChromeProvider.TryGetImage(collection, "left-join");
			center = ChromeProvider.TryGetImage(collection, "center");
			rightJoin = ChromeProvider.TryGetImage(collection, "right-join");
			rightTile = ChromeProvider.TryGetImage(collection, "right-tile");
			rightFixed = ChromeProvider.TryGetImage(collection, "right-fixed");
			return center != null;
		}

		// The bar's full width in its own pixels (the original 800).
		int BarWidth => rightFixed.Bounds.Right;
		int BarHeight => center.Bounds.Height;

		public void Layout()
		{
			if (!Load())
				return;

			var b = RenderBounds;
			Scale = Math.Min(Math.Max(1f, b.Height * HeightShare / BarHeight), (float)b.Width / BarWidth);
			Top = b.Bottom - (int)(BarHeight * Scale);

			centerLeft = b.X + (int)((b.Width - center.Bounds.Width * Scale) / 2);
			var leftSpace = centerLeft - b.X - (leftFixed.Bounds.Width + leftJoin.Bounds.Width) * Scale;
			leftTiles = Math.Max(1, (int)Math.Ceiling(leftSpace / (leftTile.Bounds.Width * Scale)));
			leftTileWidth = Math.Max(0, leftSpace) / leftTiles;

			var rightStart = centerLeft + center.Bounds.Width * Scale;
			var rightSpace = b.Right - rightStart - (rightJoin.Bounds.Width + rightFixed.Bounds.Width) * Scale;
			rightTiles = Math.Max(1, (int)Math.Ceiling(rightSpace / (rightTile.Bounds.Width * Scale)));
			rightTileWidth = Math.Max(0, rightSpace) / rightTiles;
		}

		// A named area of the bar (a region of the collection, in the original bar's pixels) on screen. Areas that
		// reach into a lower panel stretch with it: left of the middle they end where the middle starts, right of it
		// they start where it ends.
		public Rectangle Area(string name)
		{
			if (!Load())
				return Rectangle.Empty;

			var region = ChromeProvider.TryGetImage(CurrentCollection, name)?.Bounds ?? Rectangle.Empty;
			var b = RenderBounds;
			int X(int barX)
			{
				var c = center.Bounds;
				if (barX < c.X)
					return b.X + (int)(barX * Scale * (centerLeft - b.X) / (c.X * Scale));

				if (barX > c.Right)
					return b.Right - (int)((BarWidth - barX) * Scale * (b.Right - (centerLeft + c.Width * Scale)) / ((BarWidth - c.Right) * Scale));

				return centerLeft + (int)((barX - c.X) * Scale);
			}

			var left = X(region.Left);
			var right = X(region.Right);
			return new Rectangle(left, Top + (int)(region.Y * Scale), right - left, (int)(region.Height * Scale));
		}

		public override void Draw()
		{
			Layout();
			if (center == null)
				return;

			var b = RenderBounds;
			var renderer = Game.Renderer.RgbaSpriteRenderer;
			var scale = new float3(Scale, Scale, 1);
			var x = (float)b.X;

			renderer.DrawSprite(leftFixed, new float3(x, Top, 0), scale);
			x += leftFixed.Bounds.Width * Scale;
			for (var i = 0; i < leftTiles; i++, x += leftTileWidth)
				renderer.DrawSprite(leftTile, new float3(x, Top, 0), new float3(leftTileWidth / leftTile.Bounds.Width, Scale, 1));

			renderer.DrawSprite(leftJoin, new float3(x, Top, 0), scale);
			renderer.DrawSprite(center, new float3(centerLeft, Top, 0), scale);

			x = centerLeft + center.Bounds.Width * Scale;
			renderer.DrawSprite(rightJoin, new float3(x, Top, 0), scale);
			x += rightJoin.Bounds.Width * Scale;
			for (var i = 0; i < rightTiles; i++, x += rightTileWidth)
				renderer.DrawSprite(rightTile, new float3(x, Top, 0), new float3(rightTileWidth / rightTile.Bounds.Width, Scale, 1));

			renderer.DrawSprite(rightFixed, new float3(b.Right - rightFixed.Bounds.Width * Scale, Top, 0), scale);
		}

		// The bar's solid part: the lower panels from their top edge (56 in the original), the middle from its top.
		bool OnBar(int2 pos)
		{
			if (center == null || pos.Y < Top)
				return false;

			var panelTop = Top + (int)(56 * Scale);
			var middleTop = Top + (int)(12 * Scale);
			return pos.Y >= panelTop || (pos.X >= centerLeft && pos.X < centerLeft + center.Bounds.Width * Scale && pos.Y >= middleTop);
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			return OnBar(mi.Location);
		}

		// The widget covers the window (it lays the bar out from it), but only the bar is under the mouse: otherwise it
		// becomes the widget under the mouse everywhere, and its plain cursor hides the world's (attack, gather, ...).
		public override bool EventBoundsContains(int2 location)
		{
			return OnBar(location);
		}
	}
}
