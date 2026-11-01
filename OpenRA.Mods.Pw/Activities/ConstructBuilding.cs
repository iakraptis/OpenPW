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
	// The worker walks up to an unfinished building and works on it, playing its build animation, until it is
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
				// Close enough to the building's edge: its half size plus the work range.
				var dimensions = site.Info.TraitInfo<BuildingInfo>().Dimensions;
				var range = underConstruction.Info.WorkRange + new WDist(512 * Math.Max(dimensions.X, dimensions.Y));
				var target = Target.FromActor(site);
				if (!target.IsInRange(self.CenterPosition, range))
				{
					if (moveAttempts++ >= MaxMoveAttempts)
						return true;

					QueueChild(self.Trait<IMove>().MoveWithinRange(target, range));
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
					facing.Facing = (site.CenterPosition - self.CenterPosition).Yaw;
			}

			// The work sound, once per cycle of the build animation.
			if (workTicks++ % WorkCycle == 0)
				PwVoiced.PlayLocal(self, "Work");

			return false;
		}

		protected override void OnLastRun(Actor self)
		{
			working?.ReleaseBuilder(self);
			if (conditionToken != Actor.InvalidConditionToken)
				conditionToken = self.RevokeCondition(conditionToken);
		}
	}
}
