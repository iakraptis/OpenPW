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
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Projectiles;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Projectiles
{
	[Desc("A Primitive Wars bullet with the original target-tracking, path-piercing, and fixed-endpoint modes.")]
	public class PwBulletInfo : BulletInfo, IRulesetLoaded<WeaponInfo>
	{
		[Desc("Original projectile behavior: 0 for fixed endpoint, 2 for target tracking, or 4 for path piercing.")]
		public readonly int Mode = 2;

		public override IProjectile Create(ProjectileArgs args) { return new PwBullet(this, args); }

		void IRulesetLoaded<WeaponInfo>.RulesetLoaded(Ruleset rules, WeaponInfo weapon)
		{
			if (Mode != 0 && Mode != 2 && Mode != 4)
				throw new YamlException("PwBullet Mode must be 0, 2, or 4.");
		}
	}

	public class PwBullet : Bullet
	{
		const int MaxPiercedActors = 16;
		const int PathStep = 64;
		const int ActorSearchPadding = 1024;

		readonly PwBulletInfo info;
		readonly HashSet<Actor> hitActors = [];
		readonly WVec targetOffset;

		// The original hands a tracking shot without splash straight to its target (Pw.exe 0x4770f7 passes the target
		// to the damage routine 0x457d4b): no one beside it is hurt. With splash, the damage spreads around it.
		readonly bool splash;

		// Fired at a unit (mode 2): if it is gone when the shot arrives, a shot without splash hurts no one (Pw.exe
		// 0x477107 skips the damage for a dead target).
		readonly bool tracking;

		bool exploded;

		public PwBullet(PwBulletInfo info, ProjectileArgs args)
			: base(info, ExtendPathPiercingTarget(info, args))
		{
			this.info = info;

			if (info.Mode == 2 && args.GuidedTarget.Type == TargetType.Actor)
				targetOffset = target - args.GuidedTarget.Actor.CenterPosition;

			splash = args.Weapon.Warheads.Any(w => w is SpreadDamageWarhead s && s.Range != null);
			tracking = info.Mode == 2 && args.GuidedTarget.Type == TargetType.Actor;
		}

		static ProjectileArgs ExtendPathPiercingTarget(PwBulletInfo info, ProjectileArgs args)
		{
			if (info.Mode != 4)
				return args;

			var direction = args.PassiveTarget - args.Source;
			var horizontalLength = direction.HorizontalLength;
			var range = Util.ApplyPercentageModifiers(args.Weapon.Range.Length, args.RangeModifiers ?? Array.Empty<int>());
			if (horizontalLength == 0 || horizontalLength >= range)
				return args;

			args.PassiveTarget = args.Source + new WVec(
				(int)((long)direction.X * range / horizontalLength),
				(int)((long)direction.Y * range / horizontalLength),
				(int)((long)direction.Z * range / horizontalLength));
			return args;
		}

		public override void Tick(World world)
		{
			if (info.Mode == 2 && Args.GuidedTarget.Type == TargetType.Actor)
				target = Args.GuidedTarget.Actor.CenterPosition + targetOffset;

			if (info.Mode != 4)
			{
				base.Tick(world);
				return;
			}

			var from = pos;
			exploded = false;
			base.Tick(world);

			if (!exploded)
				CheckPath(world, from, pos);
		}

		protected override void Explode(World world)
		{
			exploded = true;
			if (info.Mode == 2 && Args.GuidedTarget.Type == TargetType.Actor && !Args.GuidedTarget.Actor.IsDead)
			{
				if (pos != target)
					return;

				world.AddFrameEndTask(w => w.Remove(this));
				var victim = Args.GuidedTarget.Actor;
				var impactPosition = victim.CenterPosition;
				var trackingWarheadArgs = new WarheadArgs(Args)
				{
					ImpactOrientation = new WRot(WAngle.Zero, Util.GetVerticalAngle(lastPos, pos), Args.Facing),
					ImpactPosition = impactPosition,
					WeaponTarget = Target.FromActor(victim),
				};

				Args.Weapon.Impact(splash ? Target.FromPos(impactPosition) : Target.FromActor(victim), trackingWarheadArgs);
				return;
			}

			if (tracking && !splash)
			{
				world.AddFrameEndTask(w => w.Remove(this));
				ImpactEffects(pos);
				return;
			}

			if (info.Mode != 4)
			{
				base.Explode(world);
				return;
			}

			CheckPath(world, lastPos, pos);
			world.AddFrameEndTask(w => w.Remove(this));

			ImpactEffects(pos);
		}

		// The weapon's impact without its damage (the effect art, sounds).
		void ImpactEffects(WPos at)
		{
			var warheadArgs = new WarheadArgs(Args)
			{
				ImpactOrientation = new WRot(WAngle.Zero, Util.GetVerticalAngle(lastPos, pos), Args.Facing),
				ImpactPosition = at,
			};

			foreach (var warhead in Args.Weapon.Warheads)
				if (warhead is not DamageWarhead)
					warhead.DoImpact(Target.FromPos(at), warheadArgs);
		}

		void CheckPath(World world, WPos from, WPos to)
		{
			if (hitActors.Count >= MaxPiercedActors || from == to)
				return;

			var distance = (to - from).Length;
			var steps = Math.Max((distance + PathStep - 1) / PathStep, 1);
			var aimed = Args.GuidedTarget.Type == TargetType.Actor ? Args.GuidedTarget.Actor : null;
			var candidates = world.ActorMap.ActorsInBox(
				new WPos(Math.Min(from.X, to.X) - ActorSearchPadding, Math.Min(from.Y, to.Y) - ActorSearchPadding, Math.Min(from.Z, to.Z)),
				new WPos(Math.Max(from.X, to.X) + ActorSearchPadding, Math.Max(from.Y, to.Y) + ActorSearchPadding, Math.Max(from.Z, to.Z)));

			for (var step = 1; step <= steps && hitActors.Count < MaxPiercedActors; step++)
			{
				var position = WPos.Lerp(from, to, step, steps);
				foreach (var victim in candidates)
				{
					if (victim == Args.SourceActor || victim.IsDead || hitActors.Contains(victim))
						continue;

					// Allies are passed through, unless aimed at (Pw.exe FUN_0047afc0 tests the shooter's alliances).
					if (Args.SourceActor != null && victim != aimed
						&& Args.SourceActor.Owner.RelationshipWith(victim.Owner) == PlayerRelationship.Ally)
						continue;

					var target = Target.FromActor(victim);
					if (!target.IsValidFor(Args.SourceActor) ||
						!Args.Weapon.IsValidAgainst(target, world, Args.SourceActor) ||
						!ContainsPosition(victim, position))
						continue;

					hitActors.Add(victim);
					Args.Weapon.Impact(target, new WarheadArgs(Args)
					{
						ImpactPosition = position,
						WeaponTarget = target
					});

					if (hitActors.Count >= MaxPiercedActors)
						break;
				}
			}
		}

		static bool ContainsPosition(Actor actor, WPos position)
		{
			foreach (var targetable in actor.EnabledTargetablePositions)
				if (targetable is HitShape shape && shape.DistanceFromEdge(actor, position).Length <= 0)
					return true;

			return false;
		}
	}
}
