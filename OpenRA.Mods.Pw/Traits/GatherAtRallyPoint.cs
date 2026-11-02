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

using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("A worker trained while its producer's rally point is on a berry bush starts gathering there once it has",
		"walked to the rally point, instead of standing idle by the bush. Any order given before then cancels it.",
		"After OpenSYW's HarvestAtRallyPoint.")]
	public class GatherAtRallyPointInfo : TraitInfo, Requires<BerryGathererInfo>
	{
		[Desc("The rally point counts as on a bush when a bush with berries is within this many cells of it.")]
		public readonly int SearchRadius = 1;

		public override object Create(ActorInitializer init) => new GatherAtRallyPoint(init, this);
	}

	public class GatherAtRallyPoint : INotifyCreated, INotifyIdle, IResolveOrder
	{
		readonly GatherAtRallyPointInfo info;
		CPos? rally;
		Actor bush;

		public GatherAtRallyPoint(ActorInitializer init, GatherAtRallyPointInfo info)
		{
			this.info = info;

			// The producer's rally path (Production passes it to new units); the unit walks it on creation.
			var path = init.GetOrDefault<RallyPointInit>()?.Value;
			if (path != null && path.Length > 0)
				rally = path[^1];
		}

		void INotifyCreated.Created(Actor self)
		{
			if (rally == null)
				return;

			var center = self.World.Map.CenterOfCell(rally.Value);
			bush = self.World.FindActorsInCircle(center, WDist.FromCells(info.SearchRadius + 1))
				.Where(a => a.TraitOrDefault<BerryBush>() is BerryBush b && !b.IsEmpty)
				.MinByOrDefault(a => (a.CenterPosition - center).LengthSquared);
		}

		// First idle after the walk to the rally point (the creation activity keeps the unit busy until then).
		void INotifyIdle.TickIdle(Actor self)
		{
			if (bush == null)
				return;

			var target = bush;
			bush = null;
			if (!target.IsDead && target.IsInWorld)
				self.QueueActivity(new GatherBerries(self, target));
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			bush = null;
		}
	}
}
