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
	[Desc("An item on the ground (Item.itm; rules/<race>/items.yaml is generated). Made by a building, it lies beside it ",
		"until one of its owner's units of the Equips type picks it up (PwItemUser) and becomes the Becomes type for good.")]
	public class PwItemInfo : TraitInfo<PwItem>
	{
		[ActorReference]
		[FieldLoader.Require]
		public readonly string Equips = null;

		[ActorReference]
		[FieldLoader.Require]
		public readonly string Becomes = null;
	}

	public class PwItem
	{
		// Set by the first unit to reach it, so two can't take the same item.
		public bool Taken;

		// The unit walking to it (PickUpItem), so hunters don't all go for the same one.
		public Actor Claimant;

		// Free for a unit to go for on its own: nobody else is on the way to it.
		public bool IsClaimedByOther(Actor self) =>
			Claimant != null && Claimant != self && !Claimant.IsDead && Claimant.IsInWorld;
	}

	[Desc("Picks up items (PwItem) of its own type: the order walks to the item, then the unit becomes the item's type, ",
		"keeping its level, experience and health.")]
	public class PwItemUserInfo : TraitInfo
	{
		[VoiceReference]
		public readonly string Voice = "Action";

		public override object Create(ActorInitializer init) { return new PwItemUser(this); }
	}

	public class PwItemUser : IIssueOrder, IResolveOrder, IOrderVoice
	{
		public const string OrderID = "PwPickUp";

		readonly PwItemUserInfo info;

		public PwItemUser(PwItemUserInfo info)
		{
			this.info = info;
		}

		public static bool CanPickUp(Actor self, Actor item)
		{
			var info = item.Info.TraitInfoOrDefault<PwItemInfo>();
			return info != null && info.Equips == self.Info.Name && item.Owner == self.Owner && !item.IsDead && item.IsInWorld
				&& !item.Trait<PwItem>().Taken;
		}

		IEnumerable<IOrderTargeter> IIssueOrder.Orders
		{
			get { yield return new PickUpTargeter(); }
		}

		Order IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			return order.OrderID == OrderID ? new Order(order.OrderID, self, target, queued) : null;
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return order.OrderString == OrderID ? info.Voice : null;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == OrderID && order.Target.Type == TargetType.Actor && CanPickUp(self, order.Target.Actor))
				self.QueueActivity(order.Queued, new PickUpItem(self, order.Target.Actor));
		}

		sealed class PickUpTargeter : UnitOrderTargeter
		{
			public PickUpTargeter()
				: base(PwItemUser.OrderID, 7, "capture", false, true) { }

			public override bool CanTargetActor(Actor self, Actor target, TargetModifiers modifiers, ref string cursor)
			{
				return CanPickUp(self, target);
			}

			public override bool CanTargetFrozenActor(Actor self, FrozenActor target, TargetModifiers modifiers, ref string cursor)
			{
				return false;
			}
		}
	}

	public static class PwItemPlacement
	{
		// Where a building puts a finished item: a free exit cell, the front ones first, or (all taken) the first exit
		// cell anyway: items don't take up a cell, so they always appear.
		public static CPos Cell(Actor building)
		{
			var exits = building.Exits().OrderByDescending(e => e.Info.Priority).Select(e => building.Location + e.Info.ExitCell).ToList();
			if (exits.Count == 0)
				return building.Location;

			bool HasItem(CPos cell) => building.World.ActorsHavingTrait<PwItem>().Any(a => a.Location == cell);

			return exits.FirstOrDefault(c => building.World.Map.Contains(c) && !HasItem(c), exits[0]);
		}
	}
}
