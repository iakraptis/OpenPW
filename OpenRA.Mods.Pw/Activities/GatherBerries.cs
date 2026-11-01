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
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Activities
{
	// The gathering loop: to the bush, gather (the worker's gather animation), to the main building, deliver, back.
	public class GatherBerries : Activity
	{
		const int MaxMoveAttempts = 3;

		readonly BerryGatherer gatherer;
		readonly IMove move;
		Actor bush;
		WPos lastBushPosition;
		int gatherTicks;
		int moveAttempts;
		int gatheringToken = Actor.InvalidConditionToken;

		public GatherBerries(Actor self, Actor bush)
		{
			gatherer = self.Trait<BerryGatherer>();
			move = self.Trait<IMove>();
			this.bush = bush;
			lastBushPosition = bush.CenterPosition;
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling)
				return true;

			// Loaded: take the berries home.
			if (gatherer.Carried > 0)
			{
				var dropOff = gatherer.NearestDropOff(self);
				if (dropOff == null)
					return true;

				var target = Target.FromActor(dropOff);
				var range = Reach(dropOff);
				if (!target.IsInRange(self.CenterPosition, range))
					return MoveTo(target, range);

				var berries = gatherer.Unload(self);
				self.Owner.PlayerActor.Trait<PlayerResources>().GiveCash(berries);
				moveAttempts = 0;
				return false;
			}

			// The bush is gone: the nearest other one, or stop.
			if (bush == null || bush.IsDead || bush.Disposed || !bush.IsInWorld)
			{
				StopGathering(self);
				bush = gatherer.NearestBush(self, lastBushPosition);
				if (bush == null)
					return true;
			}

			// The bush is empty: the nearest one with berries, or wait here for this one to grow back.
			if (gatheringToken == Actor.InvalidConditionToken && bush.Trait<BerryBush>().IsEmpty)
			{
				var other = gatherer.NearestBush(self, lastBushPosition);
				if (other == null)
					return false;

				bush = other;
				moveAttempts = 0;
			}

			lastBushPosition = bush.CenterPosition;
			var bushTarget = Target.FromActor(bush);
			var bushRange = Reach(bush);
			if (gatheringToken == Actor.InvalidConditionToken)
			{
				if (!bushTarget.IsInRange(self.CenterPosition, bushRange))
					return MoveTo(bushTarget, bushRange);

				moveAttempts = 0;
				gatherTicks = gatherer.Info.GatherTicks;
				if (!string.IsNullOrEmpty(gatherer.Info.GatheringCondition))
					gatheringToken = self.GrantCondition(gatherer.Info.GatheringCondition);

				var facing = self.TraitOrDefault<IFacing>();
				if (facing != null)
					facing.Facing = (bush.CenterPosition - self.CenterPosition).Yaw;
			}

			if (--gatherTicks > 0)
				return false;

			StopGathering(self);
			gatherer.Load(self, bush.Trait<BerryBush>().Take(gatherer.Info.Amount));
			return false;
		}

		bool MoveTo(in Target target, WDist range)
		{
			if (moveAttempts++ >= MaxMoveAttempts)
				return true;

			QueueChild(move.MoveWithinRange(target, range));
			return false;
		}

		// The actor's edge plus a cell: half its footprint (buildings, two-cell bushes) and one cell.
		static WDist Reach(Actor actor)
		{
			var dimensions = actor.Info.TraitInfoOrDefault<BuildingInfo>()?.Dimensions ?? new CVec(1, 1);
			return new WDist(512 * Math.Max(dimensions.X, dimensions.Y) + 1024);
		}

		void StopGathering(Actor self)
		{
			if (gatheringToken != Actor.InvalidConditionToken)
				gatheringToken = self.RevokeCondition(gatheringToken);
		}

		protected override void OnLastRun(Actor self)
		{
			StopGathering(self);
		}
	}
}
