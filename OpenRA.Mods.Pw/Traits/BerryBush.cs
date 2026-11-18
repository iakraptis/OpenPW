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
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("A berry bush: workers gather from it (BerryGatherer). Its art shows how much is left: the frames of Sequence ",
		"stand for at least 1,500, 1,000, 500 and less (Pw.exe FUN_0045bb14). As in the original, only MaxGatherers ",
		"workers gather it at a time (each holds it for its gather animation). Unlike the original, an empty bush stays ",
		"and slowly grows its berries back.")]
	public class BerryBushInfo : TraitInfo, Requires<WithSpriteBodyInfo>
	{
		[Desc("Berries in a full bush (4,000 in the original maps).")]
		public readonly int Amount = 4000;

		[Desc("Amounts at which the art moves on to the next frame, from full to almost empty.")]
		public readonly int[] Stages = { 1500, 1000, 500 };

		[Desc("Berries grown back every RegrowInterval ticks, up to Amount.")]
		public readonly int RegrowAmount = 1;

		[Desc("Ticks between regrowths (60 ticks: about 4 seconds at the default speed, a worker's 12 berries in 50 ",
			"seconds; Mod change, the original's bushes never grow back).")]
		public readonly int RegrowInterval = 60;

		[Desc("Workers gathering it at once (0: no limit). The original lets one at a time: a worker holds the bush for ",
			"its gather animation (Pw.exe 0x45bb89, freed at 0x45bc0b); the others wait or go to another bush.")]
		public readonly int MaxGatherers = 1;

		[SequenceReference]
		public readonly string Sequence = "idle";

		public override object Create(ActorInitializer init) { return new BerryBush(this); }
	}

	public class BerryBush : INotifyCreated, ITick
	{
		readonly BerryBushInfo info;
		readonly List<Actor> gatherers = new();
		int regrowTicks;

		public int Remaining { get; private set; }

		public BerryBush(BerryBushInfo info)
		{
			this.info = info;
			Remaining = info.Amount;
		}

		void INotifyCreated.Created(Actor self)
		{
			var body = self.Trait<WithSpriteBody>();
			body.DefaultAnimation.PlayFetchIndex(info.Sequence, () =>
			{
				var stage = 0;
				while (stage < info.Stages.Length && Remaining < info.Stages[stage])
					stage++;

				return Math.Min(stage, body.DefaultAnimation.CurrentSequence.Length - 1);
			});
		}

		public bool IsEmpty => Remaining <= 0;

		public void SetRemaining(int amount)
		{
			Remaining = Math.Clamp(amount, 0, info.Amount);
		}

		void ITick.Tick(Actor self)
		{
			if (Remaining >= info.Amount || --regrowTicks > 0)
				return;

			regrowTicks = info.RegrowInterval;
			Remaining = Math.Min(info.Amount, Remaining + info.RegrowAmount);
		}

		// Whether the worker may gather now: it holds the bush already, or there is room. Workers that died or left the
		// world without letting go are dropped first.
		public bool HasRoom(Actor worker)
		{
			gatherers.RemoveAll(a => a.IsDead || !a.IsInWorld);
			return info.MaxGatherers <= 0 || gatherers.Contains(worker) || gatherers.Count < info.MaxGatherers;
		}

		// Holds the bush for the worker's gather animation; false when it is full.
		public bool Reserve(Actor worker)
		{
			if (!HasRoom(worker))
				return false;

			if (!gatherers.Contains(worker))
				gatherers.Add(worker);

			return true;
		}

		public void Release(Actor worker)
		{
			gatherers.Remove(worker);
		}

		public int Gatherers
		{
			get
			{
				gatherers.RemoveAll(a => a.IsDead || !a.IsInWorld);
				return gatherers.Count;
			}
		}

		// Takes up to amount berries.
		public int Take(int amount)
		{
			var taken = Math.Min(amount, Remaining);
			Remaining -= taken;
			return taken;
		}
	}

	[Desc("Workers deliver gathered berries here (the main buildings).")]
	public class AcceptsBerriesInfo : TraitInfo<AcceptsBerries>
	{
		[Desc("Cells to keep free between its footprint and any berry bush when it is placed (Builder.CanPlace): it",
			"can't be built right against the berries. 0 for none.")]
		public readonly int BushClearance = 2;
	}

	public class AcceptsBerries { }
}
