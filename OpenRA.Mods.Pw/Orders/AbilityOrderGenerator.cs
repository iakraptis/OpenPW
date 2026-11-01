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
	// Targeting mode after an ability button (the Chief's Fall out): a left click on a valid unit gives the order to the
	// caster; a right click cancels.
	public class AbilityOrderGenerator : IOrderGenerator
	{
		readonly Actor caster;
		readonly string order;
		readonly Func<Actor, Actor, bool> canTarget;

		public AbilityOrderGenerator(Actor caster, string order, Func<Actor, Actor, bool> canTarget)
		{
			this.caster = caster;
			this.order = order;
			this.canTarget = canTarget;
		}

		bool CasterGone => caster.IsDead || caster.Disposed || !caster.IsInWorld;

		Actor TargetAt(World world, MouseInput mi) =>
			world.ScreenMap.ActorsAtMouse(mi).Select(a => a.Actor).FirstOrDefault(a => canTarget(caster, a));

		IEnumerable<Order> IOrderGenerator.Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (mi.Button == MouseButton.Right && mi.Event == MouseInputEvent.Up)
			{
				world.CancelInputMode();
				yield break;
			}

			if (mi.Button != MouseButton.Left || mi.Event != MouseInputEvent.Down || CasterGone)
				yield break;

			var target = TargetAt(world, mi);
			if (target == null)
				yield break;

			world.CancelInputMode();
			yield return new Order(order, caster, Target.FromActor(target), mi.Modifiers.HasModifier(Modifiers.Shift));
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
			return TargetAt(world, mi) != null ? "ability" : "generic-blocked";
		}

		bool IOrderGenerator.HandleKeyPress(KeyInput e) { return false; }

		void IOrderGenerator.Deactivate() { }
	}
}
