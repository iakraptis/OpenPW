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
using OpenRA.Effects;
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.Pw.Effects
{
	public class ElfLightningBeam : IEffect
	{
		readonly World world;
		readonly Actor source;
		readonly Actor target;
		bool stopped;
		bool removalQueued;
		int age;

		public bool IsActive => !stopped && IsValid(source) && IsValid(target);

		public ElfLightningBeam(World world, Actor source, Actor target)
		{
			this.world = world;
			this.source = source;
			this.target = target;
		}

		public void Stop() => stopped = true;

		public void Tick(World world)
		{
			if (IsActive)
			{
				age++;
				return;
			}

			if (removalQueued)
				return;

			removalQueued = true;
			world.AddFrameEndTask(w => w.Remove(this));
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (!IsActive)
				return Enumerable.Empty<IRenderable>();

			var start = source.CenterPosition + new WVec(0, 0, 512);
			var end = target.CenterPosition + new WVec(0, 0, 256);
			if (world.FogObscures(start) || world.FogObscures(end))
				return Enumerable.Empty<IRenderable>();

			var delta = end - start;
			var horizontalLength = Math.Max(1, delta.HorizontalLength);
			var points = new WPos[6];
			points[0] = start;
			points[^1] = end;
			for (var i = 1; i < points.Length - 1; i++)
			{
				var wave = Math.Sin(age * 0.9 + i * 2.4);
				var displacement = (int)(wave * 240);
				var offset = new WVec(-delta.Y * displacement / horizontalLength,
					delta.X * displacement / horizontalLength, 0);
				points[i] = start + delta * i / (points.Length - 1) + offset;
			}

			return new IRenderable[]
			{
				new LightningBeamRenderable(points, Color.FromArgb(150, 40, 100, 255), 3),
				new LightningBeamRenderable(points, Color.FromArgb(240, 210, 245, 255), 1)
			};
		}

		static bool IsValid(Actor actor) => actor != null && !actor.IsDead && !actor.Disposed && actor.IsInWorld;

		sealed class LightningBeamRenderable : IRenderable, IFinalizedRenderable
		{
			readonly WPos[] points;
			readonly Color color;
			readonly int width;

			public WPos Pos => points[0];
			public int ZOffset => 0;
			public bool IsDecoration => true;

			public LightningBeamRenderable(WPos[] points, Color color, int width)
			{
				this.points = points;
				this.color = color;
				this.width = width;
			}

			public IRenderable WithZOffset(int newOffset) => this;

			public IRenderable OffsetBy(in WVec offset)
			{
				var translated = offset;
				return new LightningBeamRenderable(points.Select(p => p + translated).ToArray(), color, width);
			}

			public IRenderable AsDecoration() => this;
			public IFinalizedRenderable PrepareRender(WorldRenderer wr) => this;

			public void Render(WorldRenderer wr)
			{
				var previous = wr.Viewport.WorldToViewPx(wr.Screen3DPosition(points[0]));
				foreach (var point in points.Skip(1))
				{
					var current = wr.Viewport.WorldToViewPx(wr.Screen3DPosition(point));
					Game.Renderer.RgbaColorRenderer.DrawLine(previous, current, width, color);
					previous = current;
				}
			}

			public void RenderDebugGeometry(WorldRenderer wr) { }
			public Rectangle ScreenBounds(WorldRenderer wr) => Rectangle.Empty;
		}
	}
}
