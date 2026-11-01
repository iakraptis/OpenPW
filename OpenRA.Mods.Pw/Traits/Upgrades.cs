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
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("One level of an upgrade (Upgrade.upi, docs/game/rules.md; rules/<race>/upgrades.yaml is generated). Its ",
		"building researches it like a unit; once done it is recorded by the player's PwUpgrades and its bonuses apply to ",
		"Units. The bonuses are this level's own, added to the earlier levels'.")]
	public class PwUpgradeInfo : TraitInfo<PwUpgradeInfo.Unused>
	{
		public class Unused { }

		public readonly int Level = 1;

		[Desc("Unit types it improves. Empty for player-wide upgrades.")]
		public readonly HashSet<string> Units = new();

		[Desc("Attack added (flat, like the original).")]
		public readonly int Attack = 0;

		[Desc("Armour added.")]
		public readonly int Armor = 0;

		[Desc("Walk speed added, pixels per tick.")]
		public readonly int Speed = 0;

		[Desc("Berries added to each trip.")]
		public readonly int Gathering = 0;

		[Desc("Experience taken off each level (Reduce Level Up Exp.).")]
		public readonly int LevelCost = 0;

		[Desc("Not used yet: a unit ability (Chief's fall out).")]
		public readonly int Ability = 0;

		[Desc("Transport room added (PwTransportCapacity).")]
		public readonly int Transport = 0;

		[Desc("Not used yet: detection.")]
		public readonly int Detection = 0;

		public bool Affects(ActorInfo unit) => Units.Count == 0 || Units.Contains(unit.Name);
	}

	[Desc("Attach to the player: the upgrades it has researched, which count as prerequisites (so level 2 follows ",
		"level 1, and a researched level is no longer offered).")]
	public class PwUpgradesInfo : TraitInfo, ITechTreePrerequisiteInfo, IRulesetLoaded
	{
		string[] upgrades = Array.Empty<string>();

		IEnumerable<string> ITechTreePrerequisiteInfo.Prerequisites(ActorInfo info) => upgrades;

		void IRulesetLoaded<ActorInfo>.RulesetLoaded(Ruleset rules, ActorInfo info)
		{
			upgrades = rules.Actors.Where(a => a.Value.HasTraitInfo<PwUpgradeInfo>()).Select(a => a.Key).ToArray();
		}

		public override object Create(ActorInitializer init) { return new PwUpgrades(init.Self); }
	}

	public class PwUpgrades : ITechTreePrerequisite
	{
		readonly Actor self;
		readonly HashSet<string> researched = new();
		readonly List<PwUpgradeInfo> done = new();

		public PwUpgrades(Actor self)
		{
			this.self = self;
		}

		public IEnumerable<string> ProvidesPrerequisites => researched;

		public bool IsResearched(string upgrade) => researched.Contains(upgrade);

		public void Complete(ActorInfo upgrade)
		{
			if (!researched.Add(upgrade.Name))
				return;

			done.Add(upgrade.TraitInfo<PwUpgradeInfo>());
			self.Owner.PlayerActor.Trait<TechTree>().ActorChanged(self);
		}

		// The sum of a bonus over the researched upgrades that affect this unit type.
		public int Bonus(ActorInfo unit, Func<PwUpgradeInfo, int> bonus)
		{
			var sum = 0;
			foreach (var upgrade in done)
				if (upgrade.Affects(unit))
					sum += bonus(upgrade);

			return sum;
		}

		public static int Bonus(Actor actor, Func<PwUpgradeInfo, int> bonus) =>
			actor.Owner.PlayerActor.TraitOrDefault<PwUpgrades>()?.Bonus(actor.Info, bonus) ?? 0;
	}

	[Desc("The researched upgrades' attack, armour and speed for this unit (PwUpgrades on its owner).")]
	public class PwUpgradeBonusInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new PwUpgradeBonus(init.Self); }
	}

	public class PwUpgradeBonus : IPwStatBonus, ISpeedModifier
	{
		readonly Actor self;

		public PwUpgradeBonus(Actor self)
		{
			this.self = self;
		}

		public int AttackBonus => PwUpgrades.Bonus(self, u => u.Attack);
		public int ArmorBonus => PwUpgrades.Bonus(self, u => u.Armor);

		int ISpeedModifier.GetSpeedModifier()
		{
			var bonus = PwUpgrades.Bonus(self, u => u.Speed);
			var speed = self.Info.TraitInfoOrDefault<MobileInfo>()?.Speed ?? 0;
			return bonus == 0 || speed <= 0 ? 100 : (speed + bonus * 32) * 100 / speed;
		}
	}
}
