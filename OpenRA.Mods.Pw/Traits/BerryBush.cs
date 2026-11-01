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
	[Desc("A berry bush: workers gather from it (BerryGatherer) until it is empty, then it disappears. Its art shows ",
		"how much is left: the frames of Sequence stand for at least 1,500, 1,000, 500 and less (Pw.exe FUN_0045bb14).")]
	public class BerryBushInfo : TraitInfo, Requires<WithSpriteBodyInfo>
	{
		[Desc("Berries in a full bush (4,000 in the original maps).")]
		public readonly int Amount = 4000;

		[Desc("Amounts at which the art moves on to the next frame, from full to almost empty.")]
		public readonly int[] Stages = { 1500, 1000, 500 };

		[SequenceReference]
		public readonly string Sequence = "idle";

		public override object Create(ActorInitializer init) { return new BerryBush(this); }
	}

	public class BerryBush : INotifyCreated
	{
		readonly BerryBushInfo info;

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

		// Takes up to amount berries; the bush disappears when it is empty.
		public int Take(Actor self, int amount)
		{
			var taken = Math.Min(amount, Remaining);
			Remaining -= taken;
			if (Remaining <= 0 && !self.Disposed)
				self.World.AddFrameEndTask(w => self.Dispose());

			return taken;
		}
	}

	[Desc("Workers deliver gathered berries here (the main buildings).")]
	public class AcceptsBerriesInfo : TraitInfo<AcceptsBerries> { }

	public class AcceptsBerries { }
}
