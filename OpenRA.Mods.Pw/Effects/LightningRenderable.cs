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
using OpenRA.Primitives;

namespace OpenRA.Mods.Pw.Effects
{
	// A bent line through world positions, drawn over the world (the Elf's building beam, the Gargoyle's bolt). Its
	// width is in world pixels, so it scales with the zoom.
	public sealed class LightningRenderable : IRenderable, IFinalizedRenderable
	{
		readonly WPos[] points;
		readonly Color color;
		readonly int width;

		public WPos Pos => points[0];
		public int ZOffset => 0;
		public bool IsDecoration => true;

		public LightningRenderable(WPos[] points, Color color, int width)
		{
			this.points = points;
			this.color = color;
			this.width = width;
		}

		public IRenderable WithZOffset(int newOffset) => this;

		public IRenderable OffsetBy(in WVec offset)
		{
			var translated = offset;
			return new LightningRenderable(points.Select(p => p + translated).ToArray(), color, width);
		}

		public IRenderable AsDecoration() => this;
		public IFinalizedRenderable PrepareRender(WorldRenderer wr) => this;

		public void Render(WorldRenderer wr)
		{
			var projectedPoints = points.Select(wr.Screen3DPosition).ToArray();
			var worldWidth = Math.Max(1, width * wr.TileScale / wr.TileSize.Width);
			var screenWidth = wr.ScreenVector(new WVec(worldWidth, 0, 0))[0];
			Game.Renderer.WorldRgbaColorRenderer.DrawLine(projectedPoints, screenWidth, color);
		}

		public void RenderDebugGeometry(WorldRenderer wr) { }
		public Rectangle ScreenBounds(WorldRenderer wr) => Rectangle.Empty;
	}
}
