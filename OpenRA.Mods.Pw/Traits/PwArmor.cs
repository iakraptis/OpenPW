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

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("Primitive Wars armour: a flat value subtracted from every hit by SubtractArmorDamage warheads ",
		"(docs/game/rules.md). Several enabled instances add up, so upgrades and levels can be conditional instances.")]
	public class PwArmorInfo : ConditionalTraitInfo
	{
		[Desc("Armour points.")]
		public readonly int Value = 0;

		public override object Create(ActorInitializer init) { return new PwArmor(this); }
	}

	public class PwArmor : ConditionalTrait<PwArmorInfo>
	{
		public PwArmor(PwArmorInfo info)
			: base(info) { }

		public int Value => IsTraitDisabled ? 0 : Info.Value;
	}

	[Desc("Multiplies the actor's armour (PwArmor plus level and upgrade bonuses) by Modifier percent while enabled ",
		"(the Chief's morale: 110).")]
	public class PwArmorMultiplierInfo : ConditionalTraitInfo
	{
		public readonly int Modifier = 100;

		public override object Create(ActorInitializer init) { return new PwArmorMultiplier(this); }
	}

	public class PwArmorMultiplier : ConditionalTrait<PwArmorMultiplierInfo>
	{
		public PwArmorMultiplier(PwArmorMultiplierInfo info)
			: base(info) { }

		public int Modifier => IsTraitDisabled ? 100 : Info.Modifier;
	}
}
