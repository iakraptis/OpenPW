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
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.Pw.Effects
{
	// A dead unit (PwBody): its die animation, facing the way it fell, then the animation's last frame lying on the
	// ground for a while, or an unseen mark where it fell (Marker: flyers and the Wizard, whose art leaves no body), shown
	// as a grey dot only to a player who can raise it (owns a Resurrect caster). It goes when its time is up, or at once
	// when Resurrect raises it (its grave is taken).
	public class PwBodyEffect : IEffect, ISpatiallyPartitionable, IEffectAnnotation
	{
		// Below the units standing at the same place.
		const int ZOffset = -512;
		static readonly WDist MarkerRadius = new(96);
		static readonly Color MarkerColor = Color.FromArgb(200, 160, 160, 160);

		readonly World world;
		readonly WPos pos;
		readonly string palette;
		readonly Animation anim;
		readonly int frameTicks;
		readonly int frames;
		readonly int lifetime;
		readonly PwGraveyard.Grave grave;
		readonly bool marker;
		int age;

		public PwBodyEffect(World world, WPos pos, WAngle facing, string image, string sequence, string palette,
			int lifetime, PwGraveyard.Grave grave, bool marker)
		{
			this.marker = marker;
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

		// Fallen: the die animation has played.
		bool Fallen => age >= frames * frameTicks;

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (world.FogObscures(pos) || (marker && Fallen))
				return SpriteRenderable.None;

			return anim.Render(pos, WVec.Zero, ZOffset, wr.Palette(palette));
		}

		public IEnumerable<IRenderable> RenderAnnotation(WorldRenderer wr)
		{
			if (!marker || !Fallen || world.FogObscures(pos)
				|| !world.WorldActor.Trait<PwGraveyard>().CanRaise(world, world.RenderPlayer))
				return SpriteRenderable.None;

			// On the ground under where it fell (a flyer dies in the air).
			var ground = pos - new WVec(0, 0, world.Map.DistanceAboveTerrain(pos).Length);
			return [new CircleAnnotationRenderable(ground, MarkerRadius, 1, MarkerColor, filled: true)];
		}
	}
}
