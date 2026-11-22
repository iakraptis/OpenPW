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
using OpenRA.Mods.Pw.Effects;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Activities
{
	// The gathering loop: to the bush, gather (the worker's gather animation), to the main building, deliver, back.
	// Workers gather and deliver from any cell around the bush or building (its sides and corners), the nearest free one,
	// so several workers spread around it instead of queueing for one spot. A worker holds the bush while it gathers
	// (BerryBush.MaxGatherers, one at a time as in the original); finding it held, it goes to the nearest bush with
	// room within OtherBushCells, or waits beside it (Pw.exe 0x45b832, 0x45ba99).
	public class GatherBerries : Activity
	{
		const int MaxMoveAttempts = 3;

		// How far a worker looks for another bush when its own is held: the original searches 5 cells around it.
		const int OtherBushCells = 5;

		// Ticks between looks for room while waiting at a held bush.
		const int WaitTicks = 8 * PwTime.TicksPerOriginal;

		readonly BerryGatherer gatherer;
		readonly IMove move;
		readonly Mobile mobile;
		Actor bush;
		WPos lastBushPosition;
		int gatherTicks;
		int moveAttempts;
		int waitTicks;
		Actor reserved;
		int gatheringToken = Actor.InvalidConditionToken;
		int flightToken = Actor.InvalidConditionToken;
		BerryFlight berryFlight;
		ElfLightningBeam lightning;

		public bool IsBerryFlightActive => berryFlight != null && !berryFlight.IsComplete;
		public bool IsLightningActive => lightning?.IsActive ?? false;

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

			if (berryFlight != null)
			{
				if (berryFlight.IsComplete)
				{
					berryFlight = null;
					StopBerryFlight(self);
					StopGathering(self);
					return false;
				}

				var facing = self.TraitOrDefault<IFacing>();
				if (facing != null)
					facing.Facing = (berryFlight.Position - self.CenterPosition).Yaw;

				return false;
			}

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

				// Held by another worker: another bush with room nearby, or wait here.
				if (!bush.Trait<BerryBush>().Reserve(self))
				{
					if (waitTicks-- > 0)
						return false;

					waitTicks = WaitTicks;
					var other = OtherBush(self);
					if (other != null)
						bush = other;

					return false;
				}

				reserved = bush;
				waitTicks = 0;
				gatherTicks = gatherer.Info.GatherTicks;
				if (!string.IsNullOrEmpty(gatherer.Info.GatheringCondition))
					gatheringToken = self.GrantCondition(gatherer.Info.GatheringCondition);

				PwVoiced.PlayLocal(self, "Work");

				var facing = self.TraitOrDefault<IFacing>();
				if (facing != null)
					facing.Facing = (bush.CenterPosition - self.CenterPosition).Yaw;

				if (gatherer.Info.FliesBerries)
				{
					lightning = new ElfLightningBeam(self.World, self, bush);
					self.World.AddFrameEndTask(w => w.Add(lightning));
				}
			}

			if (--gatherTicks > 0)
				return false;

			var taken = bush.Trait<BerryBush>().Take(gatherer.TripAmount(self));
			Release(self);

			// The Wizard keeps casting and follows the berries until they reach the main building.
			if (gatherer.Info.FliesBerries)
			{
				StopLightning();
				var dropOff = gatherer.NearestDropOff(self);
				var info = gatherer.Info;
				if (dropOff != null && taken > 0)
				{
					berryFlight = new BerryFlight(self.World, bush.CenterPosition, dropOff, self.Owner, taken,
						info.FlightImage, info.FlightSequence, info.FlightPalette, info.FlightSpeed);
					if (!string.IsNullOrEmpty(info.FlightCondition))
						flightToken = self.GrantCondition(info.FlightCondition);

					var flight = berryFlight;
					self.World.AddFrameEndTask(w => w.Add(flight));
					return false;
				}

				StopGathering(self);
				return false;
			}

			StopGathering(self);
			gatherer.Load(self, taken);
			return false;
		}

		// The nearest bush with berries and room for this worker within OtherBushCells of it.
		Actor OtherBush(Actor self)
		{
			return self.World.FindActorsInCircle(self.CenterPosition, WDist.FromCells(OtherBushCells))
				.Where(a => a != bush && !a.IsDead && a.IsInWorld && a.TraitOrDefault<BerryBush>() is BerryBush b && !b.IsEmpty
					&& b.HasRoom(self))
				.OrderBy(a => (a.CenterPosition - self.CenterPosition).LengthSquared)
				.FirstOrDefault();
		}

		void Release(Actor self)
		{
			if (reserved != null && !reserved.Disposed)
				reserved.TraitOrDefault<BerryBush>()?.Release(self);

			reserved = null;
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
			var footprint = building != null ? building.Tiles(actor.Location).ToHashSet() : [actor.Location];
			return footprint
				.SelectMany(c => CVec.Directions.Select(d => c + d))
				.Where(c => !footprint.Contains(c))
				.Distinct();
		}

		void StopGathering(Actor self)
		{
			Release(self);
			StopLightning();
			if (gatheringToken != Actor.InvalidConditionToken)
				gatheringToken = self.RevokeCondition(gatheringToken);
		}

		void StopLightning()
		{
			lightning?.Stop();
			lightning = null;
		}

		void StopBerryFlight(Actor self)
		{
			if (flightToken != Actor.InvalidConditionToken)
				flightToken = self.RevokeCondition(flightToken);
		}

		protected override void OnLastRun(Actor self)
		{
			StopBerryFlight(self);
			StopGathering(self);
		}
	}
}
