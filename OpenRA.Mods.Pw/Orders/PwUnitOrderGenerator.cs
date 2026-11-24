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
using OpenRA.Mods.Pw.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Orders
{
	// The default order generator (mod.yaml), with items first: under the mouse, the engine targets the actor with the
	// highest selection priority, and an item isn't selectable, so a building or unit overlapping it always won and the
	// item couldn't be picked up. Here an item that a selected unit can equip (PwItemUser.CanPickUp) is the target
	// whatever else is under the mouse; the other selected units get the order they would for its cell.
	public class PwUnitOrderGenerator : UnitOrderGenerator
	{
		public PwUnitOrderGenerator(World world)
			: base(world) { }

		static Actor ItemAtMouse(World world, int2 worldPixel)
		{
			var users = world.Selection.Actors.Where(a => a.Owner == world.LocalPlayer && !a.Disposed).ToList();
			if (users.Count == 0)
				return null;

			// Two items overlapping: the one whose middle is nearest the mouse.
			return world.ScreenMap.ActorsAtMouse(worldPixel)
				.Where(a => a.Actor.Info.HasTraitInfo<PwItemInfo>() && !world.FogObscures(a.Actor) && users.Any(u => PwItemUser.CanPickUp(u, a.Actor)))
				.MinByOrDefault(a =>
				{
					var r = a.Bounds.BoundingRect;
					return (new int2(r.Left + r.Width / 2, r.Top + r.Height / 2) - worldPixel).LengthSquared;
				}).Actor;
		}

		protected override IEnumerable<Order> OrderInner(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			var item = ItemAtMouse(world, worldPixel);
			if (item == null)
				return base.OrderInner(world, cell, worldPixel, mi);

			return ItemOrders(world, cell, mi, Target.FromActor(item));
		}

		IEnumerable<Order> ItemOrders(World world, CPos cell, MouseInput mi, Target target)
		{
			var orders = world.Selection.Actors
				.Select(a => OrderForUnit(a, target, cell, mi))
				.Where(o => o != null)
				.ToList();

			var actorsInvolved = orders.Select(o => o.Actor).Distinct().ToArray();
			if (actorsInvolved.Length == 0)
				yield break;

			// As the base class: the player's actions-per-minute count reads this.
			yield return new Order("CreateGroup", actorsInvolved[0].Owner.PlayerActor, false, actorsInvolved);

			foreach (var o in orders)
			{
				var order = o.Trait.IssueOrder(o.Actor, o.Order, o.Target, mi.Modifiers.HasModifier(Modifiers.Shift));
				if (order != null)
					yield return order;
			}
		}

		public override string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			var item = ItemAtMouse(world, worldPixel);
			if (item == null)
				return base.GetCursor(world, cell, worldPixel, mi);

			var target = Target.FromActor(item);
			return world.Selection.Actors
				.Select(a => OrderForUnit(a, target, cell, mi))
				.Where(o => o != null && o.Cursor != null)
				.MaxByOrDefault(o => o.Order.OrderPriority)?.Cursor ?? base.GetCursor(world, cell, worldPixel, mi);
		}

		// Classic mouse: a left click over such an item orders the pick-up instead of selecting what lies under it.
		public override bool InputOverridesSelection(World world, int2 xy, MouseInput mi)
		{
			return ItemAtMouse(world, xy) != null || base.InputOverridesSelection(world, xy, mi);
		}
	}
}
