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
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Warheads
{
	[Desc("Primitive Wars damage (Pw.exe 0x457cc4): the attack value, after firepower modifiers, minus the target's ",
		"PwArmor, at least 1. Versus percentages still apply afterwards. Spread > 0 is the splash radius.")]
	public class SubtractArmorDamageWarhead : TargetDamageWarhead
	{
		protected override void InflictDamage(Actor victim, Actor firedBy, HitShape shape, WarheadArgs args)
		{
			// Levels and upgrades add flat attack and armour (IPwStatBonus).
			var bonus = firedBy != null && !firedBy.Disposed ? firedBy.TraitsImplementing<IPwStatBonus>().Sum(b => b.AttackBonus) : 0;
			var attack = Util.ApplyPercentageModifiers(Damage + bonus, args.DamageModifiers);
			var armor = victim.TraitsImplementing<PwArmor>().Sum(a => a.Value) + victim.TraitsImplementing<IPwStatBonus>().Sum(b => b.ArmorBonus);
			var damage = Util.ApplyPercentageModifiers(Math.Max(1, attack - armor), new[] { DamageVersus(victim, shape, args) });
			victim.InflictDamage(firedBy, new Damage(damage, DamageTypes));
		}
	}
}
