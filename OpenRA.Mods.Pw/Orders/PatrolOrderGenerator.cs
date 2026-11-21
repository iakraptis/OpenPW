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

namespace OpenRA.Mods.Pw.Orders
{
	// Targeting mode after the Patrol button: a confirming click on explored ground sends every selected unit that can
	// patrol back and forth between where it is and there (PwPatrol; Shift queues it); the other button cancels
	// (TargetingButtons). The cursor is the attack-move one, a patrol being attack-moves.
	public class PatrolOrderGenerator : IOrderGenerator
	{
		readonly Actor[] units;

		public PatrolOrderGenerator(Actor[] units)
		{
			this.units = units;
		}

		IEnumerable<Actor> Live => units.Where(u => !u.IsDead && !u.Disposed && u.IsInWorld && u.TraitOrDefault<PwPatrol>() != null);

		bool CanTarget(World world, CPos cell) => world.Map.Contains(cell) && Live.Any(u => u.Owner.Shroud.IsExplored(cell));

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

			var queued = mi.Modifiers.HasModifier(Modifiers.Shift);
			if (!queued)
				world.CancelInputMode();

			foreach (var u in Live)
				yield return PwPatrol.Patrol(u, cell, queued);
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
			CanTarget(world, cell) ? "attackmove" : "attackmove-blocked";

		bool IOrderGenerator.HandleKeyPress(KeyInput e) { return false; }

		void IOrderGenerator.Deactivate() { }
	}
}
