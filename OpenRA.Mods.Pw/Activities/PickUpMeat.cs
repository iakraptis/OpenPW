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

namespace OpenRA.Mods.Pw.Activities
{
	// Walk onto the meat (it takes up no space) and pick it all up (Pw.exe FUN_004630b3).
	public class PickUpMeat : Activity
	{
		const int MaxMoveAttempts = 3;
		static readonly WDist Reach = new(1536);

		readonly Actor meat;
		readonly IMove move;
		int moveAttempts;

		public PickUpMeat(Actor self, Actor meat)
		{
			this.meat = meat;
			move = self.Trait<IMove>();
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling || !PwMeatEater.IsMeat(meat))
				return true;

			if ((meat.CenterPosition - self.CenterPosition).HorizontalLengthSquared > Reach.LengthSquared)
			{
				if (moveAttempts++ >= MaxMoveAttempts)
					return true;

				QueueChild(move.MoveTo(meat.Location, 1));
				return false;
			}

			self.Trait<PwMeatEater>().Store(meat.Trait<PwMeat>().Take(meat));
			return true;
		}
	}
}
