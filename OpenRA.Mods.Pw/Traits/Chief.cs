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
using OpenRA.Mods.Pw.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("Mana for abilities and spells (.chr stat 4: the Chief's 100, an Elf's 100 or 200), refilling over time; ",
		"researched mana (PwUpgrade.Mana: Study Mana Potion Secret) raises the most it holds.")]
	public class PwManaInfo : TraitInfo
	{
		public readonly int Max = 100;

		[Desc("Mana regained every RegenInterval ticks (not known from the original: full in about a minute).")]
		public readonly int RegenAmount = 1;

		public readonly int RegenInterval = 8;

		public override object Create(ActorInitializer init) { return new PwMana(init.Self, this); }
	}

	public class PwMana : ITick
	{
		readonly Actor self;
		readonly PwManaInfo info;
		int ticks;

		public PwMana(Actor self, PwManaInfo info)
		{
			this.self = self;
			this.info = info;
			Mana = info.Max;
		}

		public int Mana { get; private set; }
		public int Max => info.Max + PwUpgrades.Bonus(self, u => u.Mana);

		public void Give(int amount) => Mana = Math.Min(Max, Mana + amount);

		public bool Spend(int amount)
		{
			if (Mana < amount)
				return false;

			Mana -= amount;
			return true;
		}

		void ITick.Tick(Actor self)
		{
			if (Mana >= Max || ++ticks < info.RegenInterval)
				return;

			ticks = 0;
			Give(info.RegenAmount);
		}
	}

	[Desc("Spreads mana (the ManaSpread: .chr 0x1f8 bit 0x4, and its name; the amounts are the mod's): every Interval ",
		"ticks, allied units with mana (PwMana) within Range gain Amount.")]
	public class PwManaAuraInfo : TraitInfo
	{
		public readonly WDist Range = WDist.FromCells(4);

		public readonly int Amount = 1;

		public readonly int Interval = 8;

		public override object Create(ActorInitializer init) { return new PwManaAura(this); }
	}

	public class PwManaAura : ITick
	{
		readonly PwManaAuraInfo info;
		int ticks;

		public PwManaAura(PwManaAuraInfo info)
		{
			this.info = info;
		}

		void ITick.Tick(Actor self)
		{
			if (++ticks < info.Interval)
				return;

			ticks = 0;
			foreach (var a in self.World.FindActorsInCircle(self.CenterPosition, info.Range))
				if (a != self && !a.IsDead && a.Owner.IsAlliedWith(self.Owner))
					a.TraitOrDefault<PwMana>()?.Give(info.Amount);
		}
	}

	[Desc("The Chief's Fall out (JW2_11 Fall out.maz): aimed at an enemy unit, it wears the target's life down to 1 ",
		"(PwFallOutVictim). Costs Cost mana; usable once the upgrade Upgrade is researched.")]
	public class PwFallOutInfo : TraitInfo, Requires<PwManaInfo>
	{
		public readonly int Cost = 80;

		public readonly WDist Range = WDist.FromCells(6);

		[Desc("Upgrade level that unlocks it (Improve Fall out for Chief).")]
		public readonly string Upgrade = "primitive-upgrade-fall-out-for-chief-1";

		[Desc("Image whose icon sequence is the button.")]
		public readonly string Image = "ability-fall-out";

		public readonly string Sound = "spell-fall-out-0.wav";

		[VoiceReference]
		public readonly string Voice = "Action";

		public override object Create(ActorInitializer init) { return new PwFallOut(this); }
	}

	public class PwFallOut : IResolveOrder, IOrderVoice
	{
		public const string OrderID = "PwFallOut";

		public readonly PwFallOutInfo Info;

		public PwFallOut(PwFallOutInfo info)
		{
			Info = info;
		}

		public bool IsUnlocked(Actor self) =>
			self.Owner.PlayerActor.TraitOrDefault<PwUpgrades>()?.IsResearched(Info.Upgrade) ?? false;

		public bool IsReady(Actor self) => IsUnlocked(self) && self.Trait<PwMana>().Mana >= Info.Cost;

		public static bool CanTarget(Actor self, Actor target) =>
			target != null && !target.IsDead && target.IsInWorld && target.Info.HasTraitInfo<PwFallOutVictimInfo>()
			&& self.Owner.RelationshipWith(target.Owner) != PlayerRelationship.Ally;

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return order.OrderString == OrderID ? Info.Voice : null;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == OrderID && order.Target.Type == TargetType.Actor && CanTarget(self, order.Target.Actor) && IsReady(self))
				self.QueueActivity(order.Queued, new CastFallOut(self, order.Target.Actor));
		}
	}

	[Desc("Can be struck by Fall out: loses Damage HP every Interval ticks until it has 1 left, showing Condition meanwhile.")]
	public class PwFallOutVictimInfo : TraitInfo, Requires<IHealthInfo>
	{
		public readonly int Damage = 1;
		public readonly int Interval = 1;

		[GrantedConditionReference]
		public readonly string Condition = "fallout";

		public override object Create(ActorInitializer init) { return new PwFallOutVictim(init.Self, this); }
	}

	public class PwFallOutVictim : ITick
	{
		readonly PwFallOutVictimInfo info;
		readonly IHealth health;
		int token = Actor.InvalidConditionToken;
		int ticks;

		public PwFallOutVictim(Actor self, PwFallOutVictimInfo info)
		{
			this.info = info;
			health = self.Trait<IHealth>();
		}

		public bool Active => token != Actor.InvalidConditionToken;

		public void Strike(Actor self)
		{
			if (!Active && health.HP > 1)
				token = self.GrantCondition(info.Condition);
		}

		void ITick.Tick(Actor self)
		{
			if (!Active || ++ticks < info.Interval)
				return;

			ticks = 0;
			if (health.HP > 1)
				self.InflictDamage(self, new Damage(Math.Min(info.Damage, health.HP - 1)));

			if (health.HP <= 1)
				token = self.RevokeCondition(token);
		}
	}
}
