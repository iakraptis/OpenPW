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
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	// Flat attack and armour added to an actor (levels, upgrades), on top of its weapon damage and PwArmor.
	public interface IPwStatBonus
	{
		int AttackBonus { get; }
		int ArmorBonus { get; }
	}

	[Desc("Levels 0-9 (docs/game/rules.md, Pw.exe FUN_00458adc): a kill gives experience, every XpPerLevel experience is a ",
		"level. Attack and armour grow evenly to their caps by level 9; Level.lvt adds a shorter attack delay and range.")]
	public class PwLevelsInfo : TraitInfo, Requires<ArmamentInfo>
	{
		[Desc("Experience per level (.chr 0x1e8).")]
		public readonly int XpPerLevel = 10;

		public readonly int MaxLevel = 9;

		[Desc("Attack gained by MaxLevel: cap minus base (.chr 0x16c - 0x158).")]
		public readonly int AttackGain = 0;

		[Desc("Armour gained by MaxLevel: cap minus base (.chr 0x170 - 0x15c).")]
		public readonly int ArmorGain = 0;

		[Desc("The attack delay part of the weapons' ReloadDelay (.chr 0x13cc), which DelayReduction shortens.")]
		public readonly int AttackDelay = 0;

		[Desc("Per level 0-9: % taken off AttackDelay (Level.lvt row 0).")]
		public readonly int[] DelayReduction = Array.Empty<int>();

		[Desc("Per level 0-9: attack range bonus in pixels (Level.lvt row 1).")]
		public readonly int[] RangeBonus = Array.Empty<int>();

		public override object Create(ActorInitializer init) { return new PwLevels(init, this); }
	}

	public class PwLevels : IPwStatBonus, IReloadModifier, IRangeModifier, ITransformActorInitModifier
	{
		readonly PwLevelsInfo info;
		readonly Actor self;
		Armament[] armaments;

		public int Level { get; private set; }
		public int Experience { get; private set; }

		// Experience per level, less the player's "Reduce Level Up Exp." upgrade.
		public int ExperiencePerLevel => Math.Max(1, info.XpPerLevel - PwUpgrades.Bonus(self, u => u.LevelCost));

		public PwLevels(ActorInitializer init, PwLevelsInfo info)
		{
			self = init.Self;
			this.info = info;

			// Carried over when the unit becomes another type (an item picked up).
			Level = init.GetValue<PwLevelInit, int>(0);
			Experience = init.GetValue<PwExperienceInit, int>(0);
		}

		void ITransformActorInitModifier.ModifyTransformActorInit(Actor self, TypeDictionary init)
		{
			init.Add(new PwLevelInit(Level));
			init.Add(new PwExperienceInit(Experience));
		}

		// Cumulative even shares: gain(L) = ⌊d·(L+1)/9⌋ − ⌊d·L/9⌋ per level, so ⌊d·L/9⌋ in all.
		int Share(int total) => total * Level / info.MaxLevel;

		public int AttackBonus => Share(info.AttackGain);
		public int ArmorBonus => Share(info.ArmorGain);

		static int At(int[] table, int level) => level < table.Length ? table[level] : 0;

		public void GiveExperience(int amount)
		{
			if (amount <= 0 || Level >= info.MaxLevel)
				return;

			Experience += amount;
			while (Level < info.MaxLevel && Experience >= ExperiencePerLevel)
			{
				Experience -= ExperiencePerLevel;
				Level++;
			}
		}

		Armament[] Armaments => armaments ??= self.TraitsImplementing<Armament>().ToArray();

		int IReloadModifier.GetReloadModifier()
		{
			var reduction = At(info.DelayReduction, Level);
			var reload = Armaments.FirstOrDefault()?.Weapon.ReloadDelay ?? 0;
			if (reduction == 0 || reload <= 0)
				return 100;

			var delay = Math.Min(info.AttackDelay, reload);
			var shortened = reload - delay * reduction / 100;
			return Math.Max(1, shortened * 100 / reload);
		}

		int IRangeModifier.GetRangeModifier()
		{
			var bonus = At(info.RangeBonus, Level);
			var range = Armaments.FirstOrDefault()?.Weapon.Range.Length ?? 0;
			if (bonus == 0 || range <= 0)
				return 100;

			return (range + bonus * 32) * 100 / range;
		}
	}

	public class PwLevelInit : ValueActorInit<int>, ISingleInstanceInit
	{
		public PwLevelInit(int value)
			: base(value) { }
	}

	public class PwExperienceInit : ValueActorInit<int>, ISingleInstanceInit
	{
		public PwExperienceInit(int value)
			: base(value) { }
	}

	[Desc("A kill of this actor gives its killer experience (Pw.exe FUN_00458adc): its population (.chr 0x184), unless ",
		"the killer is an ally.")]
	public class PwGivesExperienceInfo : TraitInfo
	{
		[Desc("Experience given. -1 uses the UsesPopulation amount.")]
		public readonly int Experience = -1;

		public override object Create(ActorInitializer init) { return new PwGivesExperience(this); }
	}

	public class PwGivesExperience : INotifyKilled
	{
		readonly PwGivesExperienceInfo info;

		public PwGivesExperience(PwGivesExperienceInfo info)
		{
			this.info = info;
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			var killer = e.Attacker;
			if (killer == null || killer.Disposed || killer.Owner.IsAlliedWith(self.Owner))
				return;

			var amount = info.Experience >= 0 ? info.Experience : self.Info.TraitInfoOrDefault<UsesPopulationInfo>()?.Amount ?? 0;
			killer.TraitOrDefault<PwLevels>()?.GiveExperience(amount);
		}
	}
}
