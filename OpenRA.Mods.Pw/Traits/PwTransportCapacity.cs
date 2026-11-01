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
using System.Reflection;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("A transport whose room grows with the transport upgrade (PwUpgrade.Transport). Cargo.MaxWeight is the room ",
		"with every upgrade; until they are researched the missing room is held as ballast weight.")]
	public class PwTransportCapacityInfo : TraitInfo, Requires<CargoInfo>
	{
		[Desc("Room before upgrades (.chr 0x188 on a transport).")]
		public readonly int Capacity = 10;

		public override object Create(ActorInitializer init) { return new PwTransportCapacity(this); }
	}

	public class PwTransportCapacity : ITick
	{
		// The engine's Cargo has a fixed capacity and only counts its passengers' weight (a private field set when the
		// transport is created), so the ballast goes there. Checked against the engine release in use (release-20250330).
		static readonly FieldInfo TotalWeight = typeof(Cargo).GetField("totalWeight", BindingFlags.NonPublic | BindingFlags.Instance)
			?? throw new InvalidOperationException("Cargo.totalWeight not found: PwTransportCapacity needs updating for this engine.");

		readonly PwTransportCapacityInfo info;
		Cargo cargo;
		int ballast;

		public PwTransportCapacity(PwTransportCapacityInfo info)
		{
			this.info = info;
		}

		public int Capacity(Actor self) => info.Capacity + PwUpgrades.Bonus(self, u => u.Transport);

		void ITick.Tick(Actor self)
		{
			cargo ??= self.Trait<Cargo>();
			var wanted = Math.Max(0, cargo.Info.MaxWeight - Capacity(self));
			if (wanted == ballast)
				return;

			TotalWeight.SetValue(cargo, (int)TotalWeight.GetValue(cargo) + wanted - ballast);
			ballast = wanted;
		}
	}
}
