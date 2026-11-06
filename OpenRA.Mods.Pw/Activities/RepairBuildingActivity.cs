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
	public class RepairBuildingActivity : Activity
	{
		const int MaxMoveAttempts = 3;

		readonly Actor building;
		readonly Builder builder;
		int moveAttempts;
		int remainingTicks;

		public RepairBuildingActivity(Actor building, Builder builder)
		{
			this.building = building;
			this.builder = builder;
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling || !builder.CanRepair(self, building))
				return true;

			var target = Target.FromActor(building);
			if (!target.IsInRange(self.CenterPosition, builder.Info.RepairRange))
			{
				if (moveAttempts++ >= MaxMoveAttempts)
					return true;

				QueueChild(self.Trait<IMove>().MoveWithinRange(target, builder.Info.RepairRange));
				return false;
			}

			if (remainingTicks > 0)
			{
				remainingTicks--;
				return false;
			}

			var health = building.Trait<IHealth>();
			var hpToRepair = System.Math.Min(builder.Info.RepairStep, health.MaxHP - health.HP);
			if (hpToRepair <= 0 || health.MaxHP <= 0)
				return true;

			var cost = System.Math.Max(1, (int)((long)hpToRepair * builder.Info.RepairPercent * building.GetSellValue()
				/ (health.MaxHP * 100L)));
			if (!self.Owner.PlayerActor.Trait<PlayerResources>().TakeCash(cost, true))
				return true;

			health.InflictDamage(building, self, new Damage(-hpToRepair), true);
			remainingTicks = builder.Info.RepairInterval;
			return false;
		}
	}
}
