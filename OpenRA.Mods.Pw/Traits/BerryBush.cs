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
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("A berry bush: workers gather from it (BerryGatherer). Its art shows how much is left: the frames of Sequence ",
		"stand for at least 1,500, 1,000, 500 and less (Pw.exe FUN_0045bb14). Unlike the original, an empty bush stays ",
		"and slowly grows its berries back.")]
	public class BerryBushInfo : TraitInfo, Requires<WithSpriteBodyInfo>
	{
		[Desc("Berries in a full bush (4,000 in the original maps).")]
		public readonly int Amount = 4000;

		[Desc("Amounts at which the art moves on to the next frame, from full to almost empty.")]
		public readonly int[] Stages = { 1500, 1000, 500 };

		[Desc("Berries grown back every RegrowInterval ticks, up to Amount.")]
		public readonly int RegrowAmount = 1;

		[Desc("Ticks between regrowths (30 ticks: about 2 seconds at the default speed, a full trip in 25 seconds).")]
		public readonly int RegrowInterval = 30;

		[SequenceReference]
		public readonly string Sequence = "idle";

		public override object Create(ActorInitializer init) { return new BerryBush(this); }
	}

	public class BerryBush : INotifyCreated, ITick
	{
		readonly BerryBushInfo info;
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

		// Takes up to amount berries.
		public int Take(int amount)
		{
			var taken = Math.Min(amount, Remaining);
			Remaining -= taken;
			return taken;
		}
	}

	[Desc("Workers deliver gathered berries here (the main buildings).")]
	public class AcceptsBerriesInfo : TraitInfo<AcceptsBerries> { }

	public class AcceptsBerries { }
}
