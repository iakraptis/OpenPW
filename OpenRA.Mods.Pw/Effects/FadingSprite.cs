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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Effects;
using OpenRA.Graphics;

namespace OpenRA.Mods.Pw.Effects
{
	// A sprite left where an actor was, fading out over a number of ticks (a destroyed Elf building, PwConjured).
	public class FadingSprite : IEffect, ISpatiallyPartitionable
	{
		readonly World world;
		readonly WPos pos;
		readonly string palette;
		readonly Animation anim;
		readonly int ticks;
		int age;

		public FadingSprite(World world, WPos pos, string image, string sequence, string palette, int ticks)
		{
			this.world = world;
			this.pos = pos;
			this.palette = palette;
			this.ticks = ticks;
			anim = new Animation(world, image);
			anim.PlayRepeating(sequence);
			world.ScreenMap.Add(this, pos, anim.Image);
		}

		public void Tick(World world)
		{
			anim.Tick();
			if (++age >= ticks)
				world.AddFrameEndTask(w => { w.Remove(this); w.ScreenMap.Remove(this); });
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (world.FogObscures(pos))
				return SpriteRenderable.None;

			var alpha = 1f - (float)age / ticks;
			return anim.Render(pos, wr.Palette(palette)).Select(r => r is IModifyableRenderable m ? m.WithAlpha(alpha) : r);
		}
	}
}
