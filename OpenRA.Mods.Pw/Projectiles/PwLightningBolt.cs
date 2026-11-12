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
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Pw.Effects;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Projectiles
{
	[Desc("A bolt of lightning: hits at once (as InstantHit) and shows a jagged line from the shooter to its target for a",
		"few ticks, bent anew every tick so it flickers (the Gargoyle's red bolt; the original's Red eye is an instant shot",
		"whose bolt is drawn in code, so the look is the mod's).")]
	public class PwLightningBoltInfo : IProjectileInfo
	{
		[Desc("Ticks the bolt stays visible.")]
		public readonly int Duration = 6;

		[Desc("Straight pieces the bolt is made of.")]
		public readonly int Segments = 8;

		[Desc("How far a bend can stray from the straight line.")]
		public readonly WDist Jitter = new(160);

		[Desc("Glow around the bolt.")]
		public readonly Color GlowColor = Color.FromArgb(150, 220, 20, 10);

		public readonly int GlowWidth = 7;

		[Desc("The bolt's bright core.")]
		public readonly Color CoreColor = Color.FromArgb(255, 255, 170, 140);

		public readonly int CoreWidth = 2;

		[Desc("Height above the shooter's centre the bolt starts from.")]
		public readonly WDist SourceHeight = new(256);

		public IProjectile Create(ProjectileArgs args) { return new PwLightningBolt(this, args); }
	}

	public class PwLightningBolt : IProjectile
	{
		readonly PwLightningBoltInfo info;
		readonly ProjectileArgs args;
		Target target;
		WPos end;
		WPos[] points = Array.Empty<WPos>();
		int ticks;

		public PwLightningBolt(PwLightningBoltInfo info, ProjectileArgs args)
		{
			this.info = info;
			this.args = args;
			target = args.Weapon.TargetActorCenter ? args.GuidedTarget : Target.FromPos(args.PassiveTarget);
			end = args.PassiveTarget;
		}

		public void Tick(World world)
		{
			if (target.Type == TargetType.Invalid)
				target = Target.FromPos(end);
			else
				end = target.CenterPosition;

			// It strikes the moment it is fired.
			if (ticks == 0)
			{
				args.Weapon.Impact(target, new WarheadArgs(args)
				{
					ImpactOrientation = new WRot(WAngle.Zero, OpenRA.Mods.Common.Util.GetVerticalAngle(args.Source, end), args.Facing),
					ImpactPosition = end,
				});
			}

			if (++ticks > info.Duration)
			{
				world.AddFrameEndTask(w => w.Remove(this));
				return;
			}

			points = Bend(world, args.CurrentSource() + new WVec(0, 0, info.SourceHeight.Length), end);
		}

		// The straight line from start to end, its inner points pushed sideways at random.
		WPos[] Bend(World world, WPos start, WPos to)
		{
			var delta = to - start;
			var length = Math.Max(1, delta.HorizontalLength);
			var result = new WPos[info.Segments + 1];
			for (var i = 0; i <= info.Segments; i++)
			{
				var along = start + delta * i / info.Segments;
				if (i == 0 || i == info.Segments)
				{
					result[i] = along;
					continue;
				}

				var side = world.LocalRandom.Next(-info.Jitter.Length, info.Jitter.Length + 1);
				result[i] = along + new WVec(-delta.Y * side / length, delta.X * side / length, 0);
			}

			return result;
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (points.Length < 2 || points.All(p => wr.World.FogObscures(p)))
				return Enumerable.Empty<IRenderable>();

			return new IRenderable[]
			{
				new LightningRenderable(points, info.GlowColor, info.GlowWidth),
				new LightningRenderable(points, info.CoreColor, info.CoreWidth)
			};
		}
	}
}
