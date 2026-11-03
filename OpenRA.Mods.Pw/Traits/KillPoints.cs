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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("A living creature (.chr 0x1f4 bit 0x10: people, beasts, demons; not machines or buildings). Killing it ",
		"feeds the killer's kill points (PwKillPoints), whoever owned it.")]
	public class PwLivingInfo : TraitInfo<PwLiving>, Requires<IHealthInfo> { }

	public class PwLiving : INotifyKilled
	{
		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			var killer = e.Attacker;
			if (killer == null || killer.Disposed || killer.IsDead || killer == self)
				return;

			killer.TraitOrDefault<PwKillPoints>()?.Gain(killer, self);
		}
	}

	[Desc("Demon kill points (KP; the help file's Special Abilities, C: Pw.exe FUN_004578a9): a Demon's health is its KP. ",
		"Damage and casting (PwSpell's cost) take KP; killing a living creature (PwLiving), friend or foe, gives back a ",
		"share of the victim's full health, up to the Demon's own. Demons don't heal over time.")]
	public class PwKillPointsInfo : TraitInfo, Requires<IHealthInfo>
	{
		[Desc("Percent of the victim's full health regained (the original's quarter).")]
		public readonly int Share = 25;

		public override object Create(ActorInitializer init) { return new PwKillPoints(this); }
	}

	public class PwKillPoints
	{
		readonly PwKillPointsInfo info;

		public PwKillPoints(PwKillPointsInfo info)
		{
			this.info = info;
		}

		public void Gain(Actor self, Actor victim)
		{
			var health = self.Trait<IHealth>();
			var gain = victim.Trait<IHealth>().MaxHP * info.Share / 100;
			gain = Math.Min(gain, health.MaxHP - health.HP);
			if (gain > 0)
				health.InflictDamage(self, self, new Damage(-gain), true);
		}
	}
}
