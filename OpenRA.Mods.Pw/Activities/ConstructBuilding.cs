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
using OpenRA.Mods.Pw.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Activities
{
	// The worker walks up to an unfinished building and goes inside it (leaves the world); UnderConstruction builds
	// it and lets the worker out when it is finished. Used for a just-placed site and to resume an abandoned one.
	// Ported from OpenSYW.
	public class ConstructBuilding : Activity
	{
		const int MaxMoveAttempts = 3;

		readonly Actor site;
		int moveAttempts;

		public ConstructBuilding(Actor site)
		{
			this.site = site;
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling || site.IsDead || site.Disposed || !site.IsInWorld)
				return true;

			var underConstruction = site.TraitOrDefault<UnderConstruction>();
			if (underConstruction == null || underConstruction.IsComplete || underConstruction.HasBuilder)
				return true;

			var target = Target.FromActor(site);
			var range = underConstruction.Info.EnterRange;
			if (!target.IsInRange(self.CenterPosition, range))
			{
				if (moveAttempts++ >= MaxMoveAttempts)
					return true;

				QueueChild(self.Trait<IMove>().MoveWithinRange(target, range));
				return false;
			}

			if (underConstruction.TryAssignBuilder(self))
				self.World.AddFrameEndTask(w => w.Remove(self));

			return true;
		}
	}
}
