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

using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Effects;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Activities
{
	// The worker walks up to an unfinished building and goes inside it (leaves the world), unless configured to work
	// from range. Used for a just-placed site and to resume an abandoned one. Ported from OpenSYW.
	public class ConstructBuilding : Activity
	{
		const int MaxMoveAttempts = 3;

		readonly Actor site;
		readonly Builder builder;
		int moveAttempts;
		int workToken = Actor.InvalidConditionToken;
		ElfLightningBeam lightning;

		public bool IsLightningActive => lightning?.IsActive ?? false;

		public ConstructBuilding(Actor site, Builder builder)
		{
			this.site = site;
			this.builder = builder;
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling || site.IsDead || site.Disposed || !site.IsInWorld)
			{
				StopWorking(self);
				return true;
			}

			var underConstruction = site.TraitOrDefault<UnderConstruction>();
			if (underConstruction == null || underConstruction.IsComplete)
			{
				StopWorking(self);
				return true;
			}

			if (underConstruction.HasBuilder)
			{
				if (underConstruction.Builder != self || !builder.Info.WorksFromRange)
					return true;

				return false;
			}

			var target = Target.FromActor(site);
			var range = underConstruction.Info.EnterRange;
			if (!target.IsInRange(self.CenterPosition, range))
			{
				if (moveAttempts++ >= MaxMoveAttempts)
					return true;

				QueueChild(self.Trait<IMove>().MoveWithinRange(target, range));
				return false;
			}

			if (!underConstruction.TryAssignBuilder(self, !builder.Info.WorksFromRange))
				return true;

			if (!builder.Info.WorksFromRange)
			{
				self.World.AddFrameEndTask(w => w.Remove(self));
				return true;
			}

			if (!string.IsNullOrEmpty(builder.Info.WorkCondition))
				workToken = self.GrantCondition(builder.Info.WorkCondition);

			var facing = self.TraitOrDefault<IFacing>();
			if (facing != null)
				facing.Facing = (site.CenterPosition - self.CenterPosition).Yaw;

			lightning = new ElfLightningBeam(self.World, self, site);
			self.World.AddFrameEndTask(w => w.Add(lightning));

			return false;
		}

		void StopWorking(Actor self)
		{
			if (workToken != Actor.InvalidConditionToken)
				workToken = self.RevokeCondition(workToken);

			lightning?.Stop();
			lightning = null;
		}

		protected override void OnLastRun(Actor self)
		{
			StopWorking(self);
			if (!site.Disposed)
				site.TraitOrDefault<UnderConstruction>()?.UnassignBuilder(self);
		}
	}
}
