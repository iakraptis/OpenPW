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
using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Activities
{
	// An order to attack, for PwMeleeAttack. A melee weapon reaches only the cells around its target (^Melee: 1.5 cells, the 8
	// neighbours of a one-cell target) and a unit does
	// not walk into a cell another holds; the engine's Attack gives up when none is free, which leaves the unit standing
	// (and picking the same target again). This runs the engine's Attack as a child: while every cell in reach of the
	// target is taken it fights the nearest enemy it can reach, or else waits, and then returns to its target.
	public class MeleeAttack : Activity
	{
		readonly AttackFrontal attack;
		readonly Mobile mobile;
		readonly Target ordered;
		readonly bool forceAttack;
		readonly Color? targetLineColor;
		readonly int searchRadius;
		readonly int checkInterval;

		bool attackedOrdered;
		int ticksToCheck;

		public MeleeAttack(Actor self, AttackFrontal attack, in Target target, bool forceAttack, Color? targetLineColor, int searchRadius, int checkInterval)
		{
			this.attack = attack;
			ordered = target;
			this.forceAttack = forceAttack;
			this.targetLineColor = targetLineColor;
			this.searchRadius = searchRadius;
			this.checkInterval = checkInterval;
			mobile = self.Trait<Mobile>();
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling || !ordered.IsValidFor(self))
				return true;

			if (--ticksToCheck > 0)
				return false;

			if (ordered.Type != TargetType.Actor || CanFight(self, ordered))
			{
				// The engine's Attack ended with the target still there and a cell free for it (no way there, say): as in
				// the engine, the order is over.
				if (attackedOrdered)
					return true;

				attackedOrdered = true;
				QueueChild(new Attack(self, ordered, true, forceAttack, targetLineColor));
				return false;
			}

			attackedOrdered = false;
			var other = NearestReachableEnemy(self);
			if (other.Type == TargetType.Actor)
				QueueChild(new Attack(self, other, true, false, targetLineColor));
			else
				ticksToCheck = checkInterval;

			return false;
		}

		Target NearestReachableEnemy(Actor self)
		{
			var position = self.CenterPosition;
			return self.World.FindActorsInCircle(position, WDist.FromCells(searchRadius))
				.Where(a => a != ordered.Actor && !a.IsDead && a.IsInWorld && !a.Owner.NonCombatant
					&& self.Owner.RelationshipWith(a.Owner) == PlayerRelationship.Enemy && a.CanBeViewedByPlayer(self.Owner))
				.OrderBy(a => (a.CenterPosition - position).LengthSquared).ThenBy(a => a.ActorID)
				.Select(Target.FromActor)
				.FirstOrDefault(t => attack.ChooseArmamentsForTarget(t, false).Any() && CanFight(self, t));
		}

		// Already in reach of it, or a free cell in reach of it exists.
		bool CanFight(Actor self, in Target t)
		{
			var range = attack.GetMaximumRangeVersusTarget(t);
			if (t.IsInRange(self.CenterPosition, range))
				return true;

			var map = self.World.Map;
			foreach (var cell in map.FindTilesInCircle(map.CellContaining(t.CenterPosition), (range.Length + 1023) / 1024))
				if (mobile.CanStayInCell(cell) && mobile.CanEnterCell(cell) && t.IsInRange(map.CenterOfCell(cell), range))
					return true;

			return false;
		}

		public override IEnumerable<Target> GetTargets(Actor self)
		{
			return ChildActivity != null ? ChildActivity.GetTargets(self) : Target.None;
		}

		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
		{
			return ChildActivity != null ? ChildActivity.TargetLineNodes(self) : Enumerable.Empty<TargetLineNode>();
		}
	}
}
