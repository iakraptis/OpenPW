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
using OpenRA.Graphics;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Orders
{
	// Targeting mode after the Unload button: a confirming click on explored ground sends every selected transport there
	// to unload (PwUnloadAt; Shift queues it); the other button cancels (TargetingButtons). The cursor is the ring of
	// loading and unloading.
	public class UnloadAtOrderGenerator : IOrderGenerator
	{
		readonly Actor[] transports;

		public UnloadAtOrderGenerator(Actor[] transports)
		{
			this.transports = transports;
		}

		IEnumerable<Actor> Live => transports.Where(t => !t.IsDead && !t.Disposed && t.IsInWorld);

		bool CanTarget(World world, CPos cell) => world.Map.Contains(cell) && Live.Any(t => t.Owner.Shroud.IsExplored(cell));

		MouseButton IOrderGenerator.ActionButton => TargetingButtons.Confirm;

		IEnumerable<Order> IOrderGenerator.Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (TargetingButtons.IsCancel(mi))
			{
				world.CancelInputMode();
				yield break;
			}

			if (!TargetingButtons.IsConfirm(mi) || !CanTarget(world, cell))
				yield break;

			world.CancelInputMode();
			var queued = mi.Modifiers.HasModifier(Modifiers.Shift);
			foreach (var t in Live)
				yield return new Order(PwUnloadAt.OrderID, t, Target.FromCell(world, cell), queued);
		}

		void IOrderGenerator.Tick(World world)
		{
			if (!Live.Any())
				world.CancelInputMode();
		}

		void IOrderGenerator.SelectionChanged(World world, IEnumerable<Actor> selected) { }

		IEnumerable<IRenderable> IOrderGenerator.Render(WorldRenderer wr, World world) { yield break; }

		IEnumerable<IRenderable> IOrderGenerator.RenderAboveShroud(WorldRenderer wr, World world) { yield break; }

		IEnumerable<IRenderable> IOrderGenerator.RenderAnnotations(WorldRenderer wr, World world) { yield break; }

		string IOrderGenerator.GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi) =>
			CanTarget(world, cell) ? "deploy" : "generic-blocked";

		bool IOrderGenerator.HandleKeyPress(KeyInput e) { return false; }

		void IOrderGenerator.Deactivate() { }
	}
}
