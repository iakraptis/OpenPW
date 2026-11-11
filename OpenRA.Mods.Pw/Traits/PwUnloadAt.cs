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

using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("Unloading at a place: the command panel's Unload button, then a click on the ground (order PwUnloadAt). The",
		"transport goes there (a flyer lands) and lets its passengers out.")]
	public class PwUnloadAtInfo : TraitInfo, Requires<CargoInfo>
	{
		public override object Create(ActorInitializer init) { return new PwUnloadAt(); }
	}

	public class PwUnloadAt : IResolveOrder, IOrderVoice
	{
		public const string OrderID = "PwUnloadAt";

		Cargo cargo;

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderID || order.Target.Type != TargetType.Terrain)
				return;

			cargo ??= self.Trait<Cargo>();
			self.QueueActivity(order.Queued, new UnloadCargo(self, order.Target, cargo.Info.LoadRange));
			self.ShowTargetLines();
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			cargo ??= self.Trait<Cargo>();
			return order.OrderString == OrderID && !cargo.IsEmpty() ? cargo.Info.UnloadVoice : null;
		}
	}
}
