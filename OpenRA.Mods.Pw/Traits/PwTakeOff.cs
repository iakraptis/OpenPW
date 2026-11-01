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

using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("A flying unit that climbs to its cruise altitude as soon as it appears. Aircraft not placed by the map (trained, ",
		"scripted) otherwise wait on the ground for their first order, where ground weapons can hit them.")]
	public class PwTakeOffInfo : TraitInfo<PwTakeOff>, Requires<AircraftInfo> { }

	public class PwTakeOff : INotifyCreated
	{
		void INotifyCreated.Created(Actor self)
		{
			self.QueueActivity(new TakeOff(self));
		}
	}
}
