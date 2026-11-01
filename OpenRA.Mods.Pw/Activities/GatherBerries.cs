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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Activities
{
	// The gathering loop: to the bush, gather (the worker's gather animation), to the main building, deliver, back.
	// Workers gather and deliver from any cell around the bush or building (its sides and corners), the nearest free one,
	// so several workers spread around it instead of queueing for one spot.
	public class GatherBerries : Activity
	{
		const int MaxMoveAttempts = 3;

		readonly BerryGatherer gatherer;
		readonly IMove move;
		readonly Mobile mobile;
		Actor bush;
		WPos lastBushPosition;
		int gatherTicks;
		int moveAttempts;
		int gatheringToken = Actor.InvalidConditionToken;

		public GatherBerries(Actor self, Actor bush)
		{
			gatherer = self.Trait<BerryGatherer>();
			move = self.Trait<IMove>();
			mobile = self.TraitOrDefault<Mobile>();
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

				if (!IsBeside(self, dropOff))
					return MoveBeside(self, dropOff);

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
			if (gatheringToken == Actor.InvalidConditionToken)
			{
				if (!IsBeside(self, bush))
					return MoveBeside(self, bush);

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

		// To the nearest free cell around the actor; when all are taken, as near to the nearest one as possible.
		bool MoveBeside(Actor self, Actor actor)
		{
			if (moveAttempts++ >= MaxMoveAttempts)
				return true;

			var cells = Around(actor)
				.Where(self.World.Map.Contains)
				.OrderBy(c => (self.World.Map.CenterOfCell(c) - self.CenterPosition).LengthSquared)
				.ToList();

			if (cells.Count == 0)
				return true;

			var free = cells.Where(c => mobile == null || mobile.CanEnterCell(c, self)).Select(c => (CPos?)c).FirstOrDefault();
			QueueChild(free.HasValue ? move.MoveTo(free.Value) : move.MoveTo(cells[0], 0, null, true));
			return false;
		}

		static bool IsBeside(Actor self, Actor actor)
		{
			return Around(actor).Contains(self.Location);
		}

		// The cells next to the actor's footprint, corners included.
		static IEnumerable<CPos> Around(Actor actor)
		{
			var building = actor.Info.TraitInfoOrDefault<BuildingInfo>();
			var footprint = building != null ? building.Tiles(actor.Location).ToHashSet() : new HashSet<CPos> { actor.Location };
			return footprint
				.SelectMany(c => CVec.Directions.Select(d => c + d))
				.Where(c => !footprint.Contains(c))
				.Distinct();
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
