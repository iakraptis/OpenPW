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
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Activities
{
	// The worker walks to the cell below an unfinished building and works on it, playing its build animation, until it is
	// finished or the worker gets another order (the site then waits for a worker, PwResumeBuild).
	public class ConstructBuilding : Activity
	{
		const int MaxMoveAttempts = 3;

		// Ticks between work sounds: about one cycle of a worker's build animation.
		const int WorkCycle = 32;

		readonly Actor site;
		int moveAttempts;
		int workTicks;
		int conditionToken = Actor.InvalidConditionToken;
		UnderConstruction working;

		public ConstructBuilding(Actor site)
		{
			this.site = site;
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling || site.IsDead || site.Disposed || !site.IsInWorld)
				return true;

			var underConstruction = site.TraitOrDefault<UnderConstruction>();
			if (underConstruction == null || underConstruction.IsComplete)
				return true;

			if (working == null)
			{
				// The worker builds from the cell right below the building, facing it (its build animation shows it
				// working upwards): the middle of the bottom row first, else the nearest free cell of that row.
				var cells = BuildCells(site);
				if (!cells.Contains(self.Location))
				{
					var mobile = self.TraitOrDefault<Mobile>();
					var free = cells.Where(c => mobile == null || mobile.CanEnterCell(c, self)).Select(c => (CPos?)c).FirstOrDefault();
					if (moveAttempts++ >= MaxMoveAttempts || (free == null && cells.Count == 0))
						return true;

					QueueChild(self.Trait<IMove>().MoveTo(free ?? cells[0], 0, null, free == null));
					return false;
				}

				if (!underConstruction.TryAssignBuilder(self))
					return true;

				working = underConstruction;
				var builder = self.Trait<Builder>();
				if (!string.IsNullOrEmpty(builder.Info.BuildingCondition))
					conditionToken = self.GrantCondition(builder.Info.BuildingCondition);

				var facing = self.TraitOrDefault<IFacing>();
				if (facing != null)
					facing.Facing = WAngle.Zero;
			}

			// The work sound, once per cycle of the build animation.
			if (workTicks++ % WorkCycle == 0)
				PwVoiced.PlayLocal(self, "Work");

			return false;
		}

		// The cells along the building's bottom edge, from the middle outwards.
		static List<CPos> BuildCells(Actor site)
		{
			var dimensions = site.Info.TraitInfo<BuildingInfo>().Dimensions;
			var row = site.Location.Y + dimensions.Y;
			var middle = site.Location.X + (dimensions.X - 1) / 2;
			return Enumerable.Range(site.Location.X, dimensions.X)
				.OrderBy(x => Math.Abs(x - middle))
				.Select(x => new CPos(x, row))
				.Where(site.World.Map.Contains)
				.ToList();
		}

		protected override void OnLastRun(Actor self)
		{
			working?.ReleaseBuilder(self);
			if (conditionToken != Actor.InvalidConditionToken)
				conditionToken = self.RevokeCondition(conditionToken);
		}
	}
}
