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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("Hunting, toggled per unit (the command panel's Hunt button, order PwHunt). While on, the unit, whenever it is",
		"idle, picks up meat lying within Radius, else attacks the wild creature within Radius it can hit: aggressive",
		"ones (armed) first, then the nearest. After a kill it is idle again, so it takes the meat and goes for the next.",
		"Other orders still work and hunting carries on when the unit is idle again, except a player's attack order on a",
		"target, which turns hunting off. A new design, not the original's.")]
	public class PwHunterInfo : ConditionalTraitInfo, Requires<AttackBaseInfo>
	{
		[Desc("How far around the unit it looks for prey and meat.")]
		public readonly WDist Radius = WDist.FromCells(8);

		[Desc("Ticks between looks while idle.")]
		public readonly int ScanInterval = PwTime.Ticks(15);

		[Desc("Ticks a creature it gave up on (out of reach, flew off) is left alone.")]
		public readonly int GiveUpTicks = PwTime.Ticks(500);

		[GrantedConditionReference]
		[Desc("Condition granted while hunting is on (none by default).")]
		public readonly string HuntingCondition = null;

		[VoiceReference]
		public readonly string Voice = "Action";

		public override object Create(ActorInitializer init) { return new PwHunter(this); }
	}

	public class PwHunter : ConditionalTrait<PwHunterInfo>, IResolveOrder, IOrderVoice, INotifyIdle
	{
		public const string OrderID = "PwHunt";

		readonly Dictionary<Actor, int> givenUp = [];
		int scanTicks, token = Actor.InvalidConditionToken;
		Actor prey;

		public PwHunter(PwHunterInfo info)
			: base(info) { }

		public bool Hunting { get; private set; }

		// The order the Hunt button sends: ExtraData 1 turns hunting on, 0 off.
		public static Order Toggle(Actor self, bool on) => new(OrderID, self, false) { ExtraData = on ? 1u : 0u };

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (IsTraitDisabled)
				return;

			// A player's attack order on a target ends hunting: the unit was given its own fight. Bots keep theirs
			// (their hunting module turns it on and off by where the unit is).
			if (Hunting && !self.Owner.IsBot && (order.OrderString == "Attack" || order.OrderString == "ForceAttack"))
			{
				SetHunting(self, false);
				return;
			}

			if (order.OrderString == OrderID)
				SetHunting(self, order.ExtraData != 0);
		}

		void SetHunting(Actor self, bool on)
		{
			Hunting = on;
			prey = null;
			if (Hunting && token == Actor.InvalidConditionToken && !string.IsNullOrEmpty(Info.HuntingCondition))
				token = self.GrantCondition(Info.HuntingCondition);
			else if (!Hunting && token != Actor.InvalidConditionToken)
				token = self.RevokeCondition(token);
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order) =>
			order.OrderString == OrderID && order.ExtraData != 0 ? Info.Voice : null;

		// Wild creatures that leave meat: owned by a non-combatant enemy (Creeps).
		public static bool IsPrey(Actor self, Actor a) =>
			a != null && !a.IsDead && a.IsInWorld && a.Owner.NonCombatant && a.Info.HasTraitInfo<PwDropsMeatInfo>()
			&& self.Owner.RelationshipWith(a.Owner) == PlayerRelationship.Enemy;

		void INotifyIdle.TickIdle(Actor self)
		{
			if (!Hunting || IsTraitDisabled || ++scanTicks < Info.ScanInterval)
				return;

			scanTicks = 0;
			var now = self.World.WorldTick;

			// Idle again while the last prey lives: it couldn't be reached (or got away). Leave it for a while.
			if (prey != null && IsPrey(self, prey))
				givenUp[prey] = now + Info.GiveUpTicks;

			prey = null;
			foreach (var gone in givenUp.Where(g => g.Value <= now || g.Key.IsDead).Select(g => g.Key).ToList())
				givenUp.Remove(gone);

			var around = self.World.FindActorsInCircle(self.CenterPosition, Info.Radius).ToList();

			// The meat first (a living unit carries it and eats it when hurt).
			if (self.TraitOrDefault<PwMeatEater>() != null)
			{
				var meat = around.Where(PwMeatEater.IsMeat).MinByOrDefault(a => (a.CenterPosition - self.CenterPosition).LengthSquared);
				if (meat != null)
				{
					self.QueueActivity(new PickUpMeat(self, meat));
					return;
				}
			}

			var attacks = self.TraitsImplementing<AttackBase>().Where(a => !a.IsTraitDisabled).ToList();
			var next = around
				.Where(a => IsPrey(self, a) && !givenUp.ContainsKey(a) && a.CanBeViewedByPlayer(self.Owner)
					&& attacks.Any(ab => ab.HasAnyValidWeapons(Target.FromActor(a))))
				.OrderBy(a => a.Info.HasTraitInfo<AttackBaseInfo>() ? 0 : 1)
				.ThenBy(a => (a.CenterPosition - self.CenterPosition).LengthSquared)
				.FirstOrDefault();
			if (next == null)
				return;

			prey = next;
			var target = Target.FromActor(next);
			attacks.First(ab => ab.HasAnyValidWeapons(target)).AttackTarget(target, AttackSource.Default, false, true);
		}
	}
}
