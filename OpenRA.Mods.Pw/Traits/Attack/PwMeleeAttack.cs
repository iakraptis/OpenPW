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
using OpenRA.Mods.Pw.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("AttackFrontal for melee units: a unit that cannot get next to its target, because others hold every cell in",
		"reach of it, fights the nearest enemy it can reach instead of standing behind them, then returns to its target.")]
	public class PwMeleeAttackInfo : AttackFrontalInfo
	{
		[Desc("How far, in cells, a blocked unit looks for another enemy.")]
		public readonly int SearchRadius = 6;

		[Desc("Ticks between checks of whether the target can be reached.")]
		public readonly int CheckInterval = PwTime.Ticks(15);

		public override object Create(ActorInitializer init) { return new PwMeleeAttack(init.Self, this); }
	}

	public class PwMeleeAttack : AttackFrontal
	{
		readonly PwMeleeAttackInfo info;

		public PwMeleeAttack(Actor self, PwMeleeAttackInfo info)
			: base(self, info)
		{
			this.info = info;
		}

		public override Activity GetAttackActivity(
			Actor self, AttackSource source, in Target newTarget, bool allowMove, bool forceAttack, Color? targetLineColor = null)
		{
			if (!allowMove || !self.Info.HasTraitInfo<MobileInfo>())
				return base.GetAttackActivity(self, source, newTarget, allowMove, forceAttack, targetLineColor);

			// The group behaviour (spreading over the target's group, going on to it) is for the Attack command and the units'
			// own targeting; a player's direct order on a unit (right click, source Default) is for that unit alone.
			var groupWise = source != AttackSource.Default;
			return new MeleeAttack(self, this, newTarget, forceAttack, targetLineColor, info.SearchRadius, info.CheckInterval, groupWise);
		}
	}
}
