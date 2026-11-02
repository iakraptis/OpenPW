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
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Activities
{
	// The units merging into one (PwMerge): they meet, then all of them become the merged unit there. If one is lost
	// or doesn't arrive in time, nothing happens.
	public class MergeGroup
	{
		// Cells from the meeting point that count as arrived.
		const int Near = 2;

		public readonly CPos MeetingPoint;
		public readonly int Deadline;
		readonly string into;
		readonly List<Actor> parts;
		readonly HashSet<Actor> arrived = new();

		public bool Done { get; private set; }

		public MergeGroup(IEnumerable<Actor> parts, string into, CPos meetingPoint, int deadline)
		{
			this.parts = parts.ToList();
			this.into = into;
			MeetingPoint = meetingPoint;
			Deadline = deadline;
		}

		public void Leave() => Done = true;

		public bool Arrive(Actor part)
		{
			if ((part.Location - MeetingPoint).LengthSquared <= Near * Near)
				arrived.Add(part);

			return arrived.Contains(part);
		}

		public void TryMerge(World world)
		{
			if (Done)
				return;

			if (parts.Any(p => p.IsDead || !p.IsInWorld) || world.WorldTick >= Deadline)
			{
				Done = true;
				return;
			}

			if (parts.Any(p => !arrived.Contains(p)))
				return;

			Done = true;
			world.AddFrameEndTask(w =>
			{
				if (parts.Any(p => p.IsDead || !p.IsInWorld))
					return;

				// It appears where the first of them stands (a flyer in the air), with their mean health and lowest
				// level, facing as the first one does.
				var lead = parts[0];
				var health = parts.Sum(p => p.Trait<IHealth>().HP * 100L / p.Trait<IHealth>().MaxHP) / parts.Count;
				var level = parts.Min(p => p.TraitOrDefault<PwLevels>()?.Level ?? 0);
				var init = new TypeDictionary
				{
					new LocationInit(lead.Location),
					new CenterPositionInit(lead.CenterPosition),
					new OwnerInit(lead.Owner),
					new FacingInit(lead.TraitOrDefault<IFacing>()?.Facing ?? WAngle.Zero),
					new HealthInit((int)health),
					new PwLevelInit(level),
				};

				var selected = parts.Any(w.Selection.Contains);
				foreach (var p in parts)
				{
					p.CancelActivity();
					w.Selection.Remove(p);
					w.Remove(p);
				}

				var merged = w.CreateActor(into, init);
				merged.Trait<PwMerged>().Store(parts);
				if (selected)
					w.Selection.Add(merged);
			});
		}
	}

	// A part walks to the meeting point and waits there until the group merges.
	public class MergeUnits : Activity
	{
		readonly MergeGroup group;
		readonly IMove move;
		bool moved;

		public MergeUnits(Actor self, MergeGroup group)
		{
			this.group = group;
			move = self.Trait<IMove>();
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling)
			{
				group.Leave();
				return true;
			}

			if (group.Done)
				return true;

			if (!group.Arrive(self) && !moved)
			{
				moved = true;
				QueueChild(move.MoveTo(group.MeetingPoint, 1));
				return false;
			}

			group.Arrive(self);
			group.TryMerge(self.World);
			return group.Done;
		}
	}
}
