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

using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("A wild creature that can't fight back runs off when hit (Pw.exe FUN_00458278): if it was idle, it moves to a ",
		"random spot up to Distance away, roaring (its Select voice, .chr sound group 0) one time in RoarOneIn. Armed ",
		"creatures fight back instead.")]
	public class PwFleeWhenHitInfo : TraitInfo, Requires<IMoveInfo>
	{
		[Desc("How far it runs, in cells (64 px in the original).")]
		public readonly int Distance = 2;

		public readonly int RoarOneIn = 4;

		public override object Create(ActorInitializer init) { return new PwFleeWhenHit(this); }
	}

	public class PwFleeWhenHit : INotifyDamage
	{
		readonly PwFleeWhenHitInfo info;

		public PwFleeWhenHit(PwFleeWhenHitInfo info)
		{
			this.info = info;
		}

		void INotifyDamage.Damaged(Actor self, AttackInfo e)
		{
			if (e.Damage.Value <= 0 || e.Attacker == null || self.IsDead || !self.IsIdle || self.Info.HasTraitInfo<AttackBaseInfo>())
				return;

			var random = self.World.SharedRandom;
			var cell = self.Location + new CVec(random.Next(-info.Distance, info.Distance + 1), random.Next(-info.Distance, info.Distance + 1));
			if (self.World.Map.Contains(cell))
				self.QueueActivity(self.Trait<IMove>().MoveTo(cell, 1));

			if (info.RoarOneIn > 0 && random.Next(info.RoarOneIn) == 0)
				PwVoiced.PlayLocal(self, "Select");
		}
	}
}
