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

using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("The Attack command (attack-move) goes on to the enemies at the place it was sent to. The engine's walk there ends ",
		"when the unit is blocked by others within 8 cells of it, which leaves a unit at the back of a group standing beyond ",
		"automatic targeting's reach while the rest fight; this unit then attacks the nearest enemy within Radius of the ",
		"place, as the Attack command does (AttackSource.AttackMove: a melee group spreads over the enemy group), until none ",
		"is left there. An attack that ends at once (others in the way) is tried again every RetryInterval while it is idle.")]
	public class PwAttackMoveFinishInfo : TraitInfo, Requires<AttackMoveInfo>
	{
		[Desc("How far from the place, in cells, its enemies are attacked.")]
		public readonly int Radius = 8;

		[Desc("Ticks between tries while it stands idle with enemies left there.")]
		public readonly int RetryInterval = PwTime.Ticks(15);

		public override object Create(ActorInitializer init) { return new PwAttackMoveFinish(this); }
	}

	public class PwAttackMoveFinish : INotifyCreated, IResolveOrder, INotifyIdle
	{
		readonly PwAttackMoveFinishInfo info;

		// Null for an unarmed unit (no AutoTarget).
		AutoTarget autoTarget;

		// Where the last Attack command sent it, until it is given another order or nothing is left to attack there.
		WPos? place;

		int ticksToRetry;

		public PwAttackMoveFinish(PwAttackMoveFinishInfo info)
		{
			this.info = info;
		}

		// Not in the constructor: the actor's other traits may not be created yet.
		void INotifyCreated.Created(Actor self)
		{
			autoTarget = self.TraitOrDefault<AutoTarget>();
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "AttackMove" || order.OrderString == "AssaultMove")
			{
				place = order.Target.Type != TargetType.Invalid ? order.Target.CenterPosition : null;
				ticksToRetry = 0;
			}
			else if (!order.Queued)
				place = null;
		}

		void INotifyIdle.TickIdle(Actor self)
		{
			if (place == null || autoTarget == null || --ticksToRetry > 0)
				return;

			ticksToRetry = info.RetryInterval;

			// Holding its ground or its fire, it is not sent after them.
			if (!autoTarget.AllowMove || autoTarget.Stance < UnitStance.AttackAnything)
			{
				place = null;
				return;
			}

			var position = self.CenterPosition;
			var enemy = self.World.FindActorsInCircle(place.Value, WDist.FromCells(info.Radius))
				.Where(a => !a.IsDead && a.IsInWorld && !a.Owner.NonCombatant
					&& self.Owner.RelationshipWith(a.Owner) == PlayerRelationship.Enemy && a.CanBeViewedByPlayer(self.Owner))
				.OrderBy(a => (a.CenterPosition - position).LengthSquared).ThenBy(a => a.ActorID)
				.FirstOrDefault(a => autoTarget.ActiveAttackBases.Any(ab => ab.ChooseArmamentsForTarget(Target.FromActor(a), false).Any()));

			if (enemy == null)
			{
				place = null;
				return;
			}

			var target = Target.FromActor(enemy);
			foreach (var ab in autoTarget.ActiveAttackBases)
				self.QueueActivity(ab.GetAttackActivity(self, AttackSource.AttackMove, target, true, false));
		}
	}
}
