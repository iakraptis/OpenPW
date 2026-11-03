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
using OpenRA.Effects;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Pw.Effects
{
	// Berries flying from a bush to the main building (the Elf Wizard's Berry fly, JW2_11 record 16): they move
	// straight at the building and are its owner's berries when they get there. If it is gone meanwhile, they are lost.
	public class BerryFlight : IEffect, ISpatiallyPartitionable
	{
		readonly World world;
		readonly Actor dropOff;
		readonly Player owner;
		readonly int amount;
		readonly int speed;
		readonly string palette;
		readonly Animation anim;
		WPos pos;

		public BerryFlight(World world, WPos from, Actor dropOff, Player owner, int amount, string image, string sequence, string palette, int speed)
		{
			this.world = world;
			this.dropOff = dropOff;
			this.owner = owner;
			this.amount = amount;
			this.speed = speed;
			this.palette = palette;
			pos = from;
			anim = new Animation(world, image);
			anim.PlayRepeating(sequence);
			world.ScreenMap.Add(this, pos, anim.Image);
		}

		public void Tick(World world)
		{
			anim.Tick();
			if (dropOff.IsDead || !dropOff.IsInWorld)
			{
				Remove();
				return;
			}

			var to = dropOff.CenterPosition - pos;
			if (to.HorizontalLength <= speed)
			{
				if (dropOff.Owner == owner)
					owner.PlayerActor.Trait<PlayerResources>().GiveCash(amount);

				Remove();
				return;
			}

			pos += to * speed / to.Length;
			world.ScreenMap.Update(this, pos, anim.Image);
		}

		void Remove() => world.AddFrameEndTask(w => { w.Remove(this); w.ScreenMap.Remove(this); });

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (world.FogObscures(pos))
				return SpriteRenderable.None;

			return anim.Render(pos, wr.Palette(palette));
		}
	}
}
