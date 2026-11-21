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
using OpenRA.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Traits;

namespace OpenRA.Mods.Pw.Activities
{
	// The units sharing their meat (the Share meat button): they meet, then the meat they carry is split evenly.
	public class MeatShareGroup
	{
		// Cells from the meeting point that count as arrived.
		const int Near = 3;

		public readonly CPos MeetingPoint;
		public readonly int Deadline;
		readonly List<Actor> members;
		readonly HashSet<Actor> arrived = [];

		public bool Done { get; private set; }

		public MeatShareGroup(IEnumerable<Actor> members, CPos meetingPoint, int deadline)
		{
			this.members = members.ToList();
			MeetingPoint = meetingPoint;
			Deadline = deadline;
		}

		IEnumerable<Actor> Present => members.Where(m => !m.IsDead && m.IsInWorld);

		public void Leave(Actor member) => members.Remove(member);

		public bool Arrive(Actor member)
		{
			if ((member.Location - MeetingPoint).LengthSquared <= Near * Near)
				arrived.Add(member);

			return arrived.Contains(member);
		}

		// Everyone still in the group is there (or the time is up): the meat they carry, split evenly (the remainder
		// to the first ones).
		public void TryShare(World world)
		{
			var present = Present.ToList();
			if (Done || (world.WorldTick < Deadline && present.Any(m => !arrived.Contains(m))))
				return;

			Done = true;
			var sharing = present.Where(arrived.Contains).ToList();
			if (sharing.Count < 2)
				return;

			var eaters = sharing.ConvertAll(m => m.Trait<PwMeatEater>());
			var total = eaters.Sum(e => e.Meat);
			for (var i = 0; i < eaters.Count; i++)
				eaters[i].SetMeat(total / eaters.Count + (i < total % eaters.Count ? 1 : 0));
		}
	}

	// A member walks to the meeting point and waits there until the group shares.
	public class ShareMeat : Activity
	{
		readonly MeatShareGroup group;
		readonly IMove move;
		bool moved;

		public ShareMeat(Actor self, MeatShareGroup group)
		{
			this.group = group;
			move = self.Trait<IMove>();
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling)
			{
				group.Leave(self);
				return true;
			}

			if (group.Done)
				return true;

			if (!group.Arrive(self) && !moved)
			{
				moved = true;
				QueueChild(move.MoveTo(group.MeetingPoint, 2));
				return false;
			}

			// Walked as far as it could: it waits where it is (if that's not near the meeting point, it is left out when
			// the time is up).
			group.Arrive(self);

			group.TryShare(self.World);
			return group.Done;
		}
	}
}
