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
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("A worker that gathers berries (docs/game/rules.md): ordered onto a bush (PwGather) it plays its gather ",
		"animation once, takes Amount berries, carries them to the nearest main building (AcceptsBerries) and comes back, ",
		"moving on to the nearest other bush when one is empty.")]
	public class BerryGathererInfo : TraitInfo
	{
		[Desc("Berries per trip (12 in the original; the gathering upgrade adds 4).")]
		public readonly int Amount = 12;

		[Desc("Ticks of gathering per trip: the length of the worker's gather animation (.chr group 4 sequence).")]
		public readonly int GatherTicks = 48;

		[Desc("How far to look for another bush when one is empty.")]
		public readonly WDist SearchRange = WDist.FromCells(16);

		[GrantedConditionReference]
		[Desc("Condition granted while gathering (plays the gather animation).")]
		public readonly string GatheringCondition = "gathering";

		[GrantedConditionReference]
		[Desc("Condition granted while carrying berries (the carrying animations).")]
		public readonly string CarryingCondition = "carrying";

		[Desc("The worker stays at the bush and its berries fly to the main building (the Elf Wizard's Berry fly, JW2_11 ",
			"record 16) instead of being carried there.")]
		public readonly bool FliesBerries = false;

		[Desc("Art of the flying berries (image and sequence).")]
		public readonly string FlightImage = "spell-berry-fly";

		[SequenceReference(nameof(FlightImage))]
		public readonly string FlightSequence = "flight";

		[PaletteReference]
		public readonly string FlightPalette = "effect";

		[Desc("Speed of the flying berries: the record's 4 px per tick, x 710/576 as every missile (weapons/defaults.yaml).")]
		public readonly int FlightSpeed = 158;

		[VoiceReference]
		public readonly string Voice = "Action";

		public override object Create(ActorInitializer init) { return new BerryGatherer(this); }
	}

	public class BerryGatherer : IIssueOrder, IResolveOrder, IOrderVoice
	{
		public const string OrderID = "PwGather";

		public readonly BerryGathererInfo Info;
		int carryingToken = Actor.InvalidConditionToken;

		public int Carried { get; private set; }

		public BerryGatherer(BerryGathererInfo info)
		{
			Info = info;
		}

		// Berries per trip: Amount plus the gathering upgrade.
		public int TripAmount(Actor self) => Info.Amount + PwUpgrades.Bonus(self, u => u.Gathering);

		public void Load(Actor self, int berries)
		{
			Carried += berries;
			if (Carried > 0 && carryingToken == Actor.InvalidConditionToken && !string.IsNullOrEmpty(Info.CarryingCondition))
				carryingToken = self.GrantCondition(Info.CarryingCondition);
		}

		public int Unload(Actor self)
		{
			var berries = Carried;
			Carried = 0;
			if (carryingToken != Actor.InvalidConditionToken)
				carryingToken = self.RevokeCondition(carryingToken);

			return berries;
		}

		public Actor NearestBush(Actor self, WPos from)
		{
			return self.World.FindActorsInCircle(from, Info.SearchRange)
				.Where(a => !a.IsDead && a.IsInWorld && a.TraitOrDefault<BerryBush>() is BerryBush b && !b.IsEmpty)
				.OrderBy(a => (a.CenterPosition - from).LengthSquared)
				.FirstOrDefault();
		}

		public Actor NearestDropOff(Actor self)
		{
			return self.World.ActorsHavingTrait<AcceptsBerries>()
				.Where(a => a.Owner == self.Owner && !a.IsDead && a.IsInWorld &&
					(a.TraitOrDefault<UnderConstruction>()?.IsComplete ?? true))
				.OrderBy(a => (a.CenterPosition - self.CenterPosition).LengthSquared)
				.FirstOrDefault();
		}

		IEnumerable<IOrderTargeter> IIssueOrder.Orders
		{
			get { yield return new GatherTargeter(); }
		}

		Order IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			return order.OrderID == OrderID ? new Order(order.OrderID, self, target, queued) : null;
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return order.OrderString == OrderID ? Info.Voice : null;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == OrderID && order.Target.Type == TargetType.Actor)
				self.QueueActivity(order.Queued, new GatherBerries(self, order.Target.Actor));
		}

		sealed class GatherTargeter : UnitOrderTargeter
		{
			public GatherTargeter()
				: base(BerryGatherer.OrderID, 6, "harvest", false, true) { }

			public override bool CanTargetActor(Actor self, Actor target, TargetModifiers modifiers, ref string cursor)
			{
				return target.Info.HasTraitInfo<BerryBushInfo>();
			}

			public override bool CanTargetFrozenActor(Actor self, FrozenActor target, TargetModifiers modifiers, ref string cursor)
			{
				return target.Info.HasTraitInfo<BerryBushInfo>();
			}
		}
	}
}
