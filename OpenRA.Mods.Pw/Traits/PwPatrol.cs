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
using OpenRA.Mods.Pw.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("Patrolling: the command panel's Patrol button, then a click on the map (order PwPatrol). The unit goes back and",
		"forth between where it stands and that place, attacking every enemy it meets on the way (attack-move), until it",
		"is given another order. Queued (Shift), the patrol starts where the orders before it end.")]
	public class PwPatrolInfo : TraitInfo, Requires<IMoveInfo>
	{
		[VoiceReference]
		public readonly string Voice = "Action";

		[Desc("Color of the target lines to both ends.")]
		public readonly Color TargetLineColor = Color.OrangeRed;

		public override object Create(ActorInitializer init) { return new PwPatrol(this); }
	}

	public class PwPatrol : IResolveOrder, IOrderVoice
	{
		public const string OrderID = "PwPatrol";

		readonly PwPatrolInfo info;

		public PwPatrol(PwPatrolInfo info) { this.info = info; }

		public static Order Patrol(Actor unit, CPos cell, bool queued) =>
			new(OrderID, unit, Target.FromCell(unit.World, cell), queued);

		// Patrolling now: its legs (attack-moves) run inside the Patrol activity, so that stays the current one.
		public static bool IsPatrolling(Actor unit) => unit.CurrentActivity is Patrol;

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderID || order.Target.Type != TargetType.Terrain)
				return;

			var move = self.Trait<IMove>();
			var to = move.NearestMoveableCell(self.World.Map.Clamp(self.World.Map.CellContaining(order.Target.CenterPosition)));

			// Queued, it starts where the orders before it leave the unit (their last target line's end).
			var from = self.Location;
			if (order.Queued && self.CurrentActivity != null)
				for (var a = self.CurrentActivity; a != null; a = a.NextActivity)
					foreach (var node in a.TargetLineNodes(self))
						if (node.Target.Type != TargetType.Invalid)
							from = self.World.Map.CellContaining(node.Target.CenterPosition);

			self.QueueActivity(order.Queued, new Patrol(self, from, to, info.TargetLineColor));
			self.ShowTargetLines();
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order) =>
			order.OrderString == OrderID ? info.Voice : null;
	}
}
