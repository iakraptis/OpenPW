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
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	// Meat (docs/game/rules.md, Meat): a wild creature's kill leaves meat worth about its cost (Pw.exe FUN_00462ea6). It
	// rots, losing 1 every 32 ticks (FUN_00462a30, item types 1-4: period 1). A living unit picks it up, carries it and
	// eats it while hurt: 1 meat heals 1 HP each 8 ticks (FUN_0046089a).
	public class PwMeatAmountInit : ValueActorInit<int>, ISingleInstanceInit
	{
		public PwMeatAmountInit(int value)
			: base(value) { }
	}

	[Desc("Meat on the ground. Rots: loses 1 every RotInterval ticks, gone at 0.")]
	public class PwMeatInfo : TraitInfo
	{
		public readonly int RotInterval = 32;

		public override object Create(ActorInitializer init) { return new PwMeat(init, this); }
	}

	public class PwMeat : ITick
	{
		readonly PwMeatInfo info;
		int ticks;

		public int Amount { get; private set; }
		public bool Taken { get; private set; }

		public PwMeat(ActorInitializer init, PwMeatInfo info)
		{
			this.info = info;
			Amount = init.GetValue<PwMeatAmountInit, int>(1);
		}

		public int Take(Actor self)
		{
			if (Taken)
				return 0;

			Taken = true;
			self.World.AddFrameEndTask(w => self.Dispose());
			return Amount;
		}

		void ITick.Tick(Actor self)
		{
			if (Taken || ++ticks < info.RotInterval)
				return;

			ticks = 0;
			if (--Amount <= 0)
				Take(self);
		}
	}

	[Desc("Leaves meat when killed: Amount (its cost, .chr 0x190), three quarters plus a random quarter.")]
	public class PwDropsMeatInfo : TraitInfo
	{
		public readonly int Amount = 0;

		[Desc("Meat actor by amount: below 500, 1000, 2500 and above (the four Food sprites).")]
		public readonly string[] Actors = { "meat-small", "meat-middle", "meat-large", "meat-big" };

		public override object Create(ActorInitializer init) { return new PwDropsMeat(this); }
	}

	public class PwDropsMeat : INotifyKilled
	{
		readonly PwDropsMeatInfo info;

		public PwDropsMeat(PwDropsMeatInfo info)
		{
			this.info = info;
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (info.Amount <= 0)
				return;

			var amount = info.Amount * 3 / 4 + self.World.SharedRandom.Next(info.Amount / 4 + 1);
			var actor = info.Actors[amount < 500 ? 0 : amount < 1000 ? 1 : amount < 2500 ? 2 : 3];
			var cell = self.Location;
			var owner = self.World.WorldActor.Owner;
			self.World.AddFrameEndTask(w => w.CreateActor(actor, new TypeDictionary
			{
				new LocationInit(cell),
				new OwnerInit(owner),
				new PwMeatAmountInit(amount),
			}));
		}
	}

	[Desc("A living unit that picks up meat and eats it while hurt (1 meat heals 1 HP every 8 ticks). Idle, it fetches meat ",
		"lying within SearchRange on its own.")]
	public class PwMeatEaterInfo : TraitInfo, Requires<IHealthInfo>
	{
		public readonly WDist SearchRange = WDist.FromCells(6);

		[Desc("Ticks between looks for meat while idle.")]
		public readonly int SearchInterval = 32;

		[VoiceReference]
		public readonly string Voice = "Action";

		public override object Create(ActorInitializer init) { return new PwMeatEater(init.Self, this); }
	}

	public class PwMeatEater : ITick, IIssueOrder, IResolveOrder, IOrderVoice, INotifyIdle
	{
		public const string OrderID = "PwPickUpMeat";

		// Share meat (the command panel's button): sent to one member, the others in ExtraActors.
		public const string ShareOrderID = "PwShareMeat";

		// Ticks the members have to meet before the meat is shared among those who made it.
		const int ShareTimeout = 400;

		// Ticks per meat eaten (1 HP healed each).
		const int HealingInterval = 8;

		readonly PwMeatEaterInfo info;
		readonly IHealth health;
		int searchTicks;
		int healingTicks;

		public int Meat { get; private set; }

		public PwMeatEater(Actor self, PwMeatEaterInfo info)
		{
			this.info = info;
			health = self.Trait<IHealth>();
		}

		public void Store(int amount) => Meat += Math.Max(0, amount);

		public void SetMeat(int amount) => Meat = Math.Max(0, amount);

		// The order that makes the units meet and split their meat evenly.
		public static Order Share(Actor[] units) =>
			new(ShareOrderID, units[0], false) { ExtraActors = units.Skip(1).ToArray() };

		public static bool IsMeat(Actor a) => a != null && !a.IsDead && a.IsInWorld && a.TraitOrDefault<PwMeat>() is PwMeat m && !m.Taken;

		void ITick.Tick(Actor self)
		{
			if (Meat <= 0 || health.HP >= health.MaxHP)
			{
				healingTicks = 0;
				return;
			}

			if (++healingTicks < HealingInterval)
				return;

			healingTicks = 0;
			Meat--;
			self.InflictDamage(self, new Damage(-1));
		}

		void INotifyIdle.TickIdle(Actor self)
		{
			if (++searchTicks < info.SearchInterval)
				return;

			searchTicks = 0;
			var meat = self.World.FindActorsInCircle(self.CenterPosition, info.SearchRange)
				.Where(IsMeat)
				.OrderBy(a => (a.CenterPosition - self.CenterPosition).LengthSquared)
				.FirstOrDefault();

			if (meat != null)
				self.QueueActivity(new PickUpMeat(self, meat));
		}

		IEnumerable<IOrderTargeter> IIssueOrder.Orders
		{
			get { yield return new MeatTargeter(); }
		}

		Order IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			return order.OrderID == OrderID ? new Order(order.OrderID, self, target, queued) : null;
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return order.OrderString == OrderID || order.OrderString == ShareOrderID ? info.Voice : null;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == ShareOrderID)
			{
				// The living meat eaters of the owner, meeting at the one nearest their middle.
				var members = new[] { self }.Concat(order.ExtraActors ?? Array.Empty<Actor>())
					.Where(a => !a.IsDead && a.IsInWorld && a.Owner == self.Owner && a.TraitOrDefault<PwMeatEater>() != null && a.TraitOrDefault<IMove>() != null)
					.Distinct().ToList();
				if (members.Count < 2)
					return;

				var middle = new WPos(members.Sum(m => m.CenterPosition.X) / members.Count, members.Sum(m => m.CenterPosition.Y) / members.Count, 0);
				var meeting = members.MinBy(m => (m.CenterPosition - middle).HorizontalLengthSquared).Location;
				var group = new MeatShareGroup(members, meeting, self.World.WorldTick + ShareTimeout);
				foreach (var m in members)
					m.QueueActivity(false, new ShareMeat(m, group));

				return;
			}

			if (order.OrderString == OrderID && order.Target.Type == TargetType.Actor && IsMeat(order.Target.Actor))
				self.QueueActivity(order.Queued, new PickUpMeat(self, order.Target.Actor));
		}

		sealed class MeatTargeter : UnitOrderTargeter
		{
			public MeatTargeter()
				: base(PwMeatEater.OrderID, 7, "capture", true, true) { }

			public override bool CanTargetActor(Actor self, Actor target, TargetModifiers modifiers, ref string cursor)
			{
				return IsMeat(target);
			}

			public override bool CanTargetFrozenActor(Actor self, FrozenActor target, TargetModifiers modifiers, ref string cursor)
			{
				return false;
			}
		}
	}

	[Desc("Heals Amount HP every Interval ticks on its own (wild creatures: 1 every 32, Pw.exe FUN_0046089a).")]
	public class PwRegeneratesInfo : TraitInfo, Requires<IHealthInfo>
	{
		public readonly int Amount = 1;
		public readonly int Interval = 32;

		public override object Create(ActorInitializer init) { return new PwRegenerates(init.Self, this); }
	}

	public class PwRegenerates : ITick
	{
		readonly PwRegeneratesInfo info;
		readonly IHealth health;
		int ticks;

		public PwRegenerates(Actor self, PwRegeneratesInfo info)
		{
			this.info = info;
			health = self.Trait<IHealth>();
		}

		void ITick.Tick(Actor self)
		{
			if (++ticks < info.Interval)
				return;

			ticks = 0;
			if (health.HP < health.MaxHP)
				self.InflictDamage(self, new Damage(-info.Amount));
		}
	}
}
