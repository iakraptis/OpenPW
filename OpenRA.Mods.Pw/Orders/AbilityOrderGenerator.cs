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
using OpenRA.Graphics;
using OpenRA.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Orders
{
	// Targeting mode after an ability button (the Chief's Fall out, a Demon spell): a confirming click on a valid unit
	// (or, for a spell on a place, on explored ground) gives the order to the caster; the other button cancels. The
	// buttons follow the mouse control style (TargetingButtons).
	public class AbilityOrderGenerator : IOrderGenerator
	{
		readonly Actor caster;
		readonly string order;
		readonly Func<Actor, Actor, bool> canTarget;
		readonly bool onGround;
		readonly string targetString;

		public AbilityOrderGenerator(Actor caster, string order, Func<Actor, Actor, bool> canTarget, string targetString = null)
		{
			this.caster = caster;
			this.order = order;
			this.canTarget = canTarget;
			this.targetString = targetString;
		}

		// Aimed at a place.
		public AbilityOrderGenerator(Actor caster, string order, string targetString)
		{
			this.caster = caster;
			this.order = order;
			this.targetString = targetString;
			canTarget = (_, _) => false;
			onGround = true;
		}

		bool GroundTarget(World world, CPos cell) => onGround && world.Map.Contains(cell) && caster.Owner.Shroud.IsExplored(cell);

		bool CasterGone => caster.IsDead || caster.Disposed || !caster.IsInWorld;

		Actor TargetAt(World world, MouseInput mi) =>
			world.ScreenMap.ActorsAtMouse(mi).Select(a => a.Actor).FirstOrDefault(a => canTarget(caster, a));

		MouseButton IOrderGenerator.ActionButton => TargetingButtons.Confirm;

		IEnumerable<Order> IOrderGenerator.Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (TargetingButtons.IsCancel(mi))
			{
				world.CancelInputMode();
				yield break;
			}

			if (!TargetingButtons.IsConfirm(mi) || CasterGone)
				yield break;

			var queued = mi.Modifiers.HasModifier(Modifiers.Shift);
			if (onGround)
			{
				if (!GroundTarget(world, cell))
					yield break;

				world.CancelInputMode();
				yield return new Order(order, caster, Target.FromCell(world, cell), queued) { TargetString = targetString };
				yield break;
			}

			var target = TargetAt(world, mi);
			if (target == null)
				yield break;

			world.CancelInputMode();
			yield return new Order(order, caster, Target.FromActor(target), queued) { TargetString = targetString };
		}

		void IOrderGenerator.Tick(World world)
		{
			if (CasterGone)
				world.CancelInputMode();
		}

		void IOrderGenerator.SelectionChanged(World world, IEnumerable<Actor> selected) { }

		IEnumerable<IRenderable> IOrderGenerator.Render(WorldRenderer wr, World world) { yield break; }

		IEnumerable<IRenderable> IOrderGenerator.RenderAboveShroud(WorldRenderer wr, World world) { yield break; }

		IEnumerable<IRenderable> IOrderGenerator.RenderAnnotations(WorldRenderer wr, World world) { yield break; }

		string IOrderGenerator.GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			return (onGround ? GroundTarget(world, cell) : TargetAt(world, mi) != null) ? "ability" : "generic-blocked";
		}

		bool IOrderGenerator.HandleKeyPress(KeyInput e) { return false; }

		void IOrderGenerator.Deactivate() { }
	}
}
