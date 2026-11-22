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
using OpenRA.Effects;
using OpenRA.Graphics;
using OpenRA.Mods.Pw.Traits;

namespace OpenRA.Mods.Pw.Effects
{
	// A dead unit (PwBody): its die animation, facing the way it fell, then the animation's last frame lying on the
	// ground for a while. It goes when its time is up, or at once when Resurrect raises it (its grave is taken).
	public class PwBodyEffect : IEffect, ISpatiallyPartitionable
	{
		// Below the units standing at the same place.
		const int ZOffset = -512;

		readonly World world;
		readonly WPos pos;
		readonly string palette;
		readonly Animation anim;
		readonly int frameTicks;
		readonly int frames;
		readonly int lifetime;
		readonly PwGraveyard.Grave grave;
		int age;

		public PwBodyEffect(World world, WPos pos, WAngle facing, string image, string sequence, string palette,
			int lifetime, PwGraveyard.Grave grave)
		{
			this.world = world;
			this.pos = pos;
			this.palette = palette;
			this.lifetime = lifetime;
			this.grave = grave;

			var info = world.Map.Sequences.GetSequence(image, sequence);
			frames = Math.Max(1, info.Length);
			frameTicks = Math.Max(1, info.Tick / 40);
			anim = new Animation(world, image, () => facing);
			anim.PlayFetchIndex(sequence, () => Math.Min(age / frameTicks, frames - 1));
			world.ScreenMap.Add(this, pos, anim.Image);
		}

		public void Tick(World world)
		{
			if (++age >= lifetime || (grave != null && grave.Taken))
			{
				world.AddFrameEndTask(w => { w.Remove(this); w.ScreenMap.Remove(this); });
				return;
			}

			// The frame comes from the age (PlayFetchIndex), read again on each tick.
			anim.Tick();
			world.ScreenMap.Update(this, pos, anim.Image);
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (world.FogObscures(pos))
				return SpriteRenderable.None;

			return anim.Render(pos, WVec.Zero, ZOffset, wr.Palette(palette));
		}
	}
}
