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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	public class PwHomeInit : ValueActorInit<CPos>, ISingleInstanceInit
	{
		public PwHomeInit(CPos value)
			: base(value) { }
	}

	[Desc("A wild creature that comes back a long while after it dies, where it was first placed (PwRespawner on the ",
		"world keeps the timers).")]
	public class PwRespawnsInfo : TraitInfo
	{
		[Desc("Ticks from death to return. Each creature sets the original's (Pw.exe 0x460b69): its fall, its body ",
			"(.chr 0x13d0) and its type's wait (.chr 0x18c), counted from the death so the mod's longer bodies don't ",
			"delay it.")]
		public readonly int Delay = PwTime.Ticks(1690);

		public override object Create(ActorInitializer init) { return new PwRespawns(init, this); }
	}

	public class PwRespawns : INotifyKilled
	{
		readonly PwRespawnsInfo info;
		readonly CPos? home;

		public PwRespawns(ActorInitializer init, PwRespawnsInfo info)
		{
			this.info = info;
			home = init.GetOrDefault<PwHomeInit>()?.Value ?? init.GetOrDefault<LocationInit>()?.Value;
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (home != null)
				self.World.WorldActor.TraitOrDefault<PwRespawner>()?.Schedule(self.Info.Name, self.ActorID, self.Owner, home.Value, info.Delay);
		}
	}

	[Desc("Attach to the world: brings dead wild creatures (PwRespawns) back when their time comes, on their home cell ",
		"or the nearest free one.")]
	public class PwRespawnerInfo : TraitInfo<PwRespawner> { }

	public class PwRespawner : ITick
	{
		readonly List<(string Actor, uint Dead, Player Owner, CPos Home, int Due)> pending = [];
		int now;

		public void Schedule(string actor, uint dead, Player owner, CPos home, int delay)
		{
			pending.Add((actor, dead, owner, home, now + delay));
		}

		// The dead creature was raised by Resurrect (the original's raised unit leaves the dead for good).
		public void Cancel(uint dead) => pending.RemoveAll(p => p.Dead == dead);

		void ITick.Tick(Actor self)
		{
			now++;
			for (var i = pending.Count - 1; i >= 0; i--)
			{
				var (actor, _, owner, home, due) = pending[i];
				if (now < due)
					continue;

				pending.RemoveAt(i);
				var world = self.World;
				var info = world.Map.Rules.Actors[actor];
				var mobile = info.TraitInfoOrDefault<MobileInfo>();
				var cell = world.Map.FindTilesInCircle(home, 4)
					.FirstOrDefault(c => mobile == null || mobile.CanEnterCell(world, null, c));
				if (!world.Map.Contains(cell))
					cell = home;

				world.AddFrameEndTask(w => w.CreateActor(actor,
				[
					new LocationInit(cell),
					new OwnerInit(owner),
					new PwHomeInit(home),
				]));
			}
		}
	}
}
