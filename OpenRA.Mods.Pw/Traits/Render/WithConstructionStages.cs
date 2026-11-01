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

namespace OpenRA.Mods.Pw.Traits.Render
{
	[Desc("While the building is under construction, the body shows the construction stage for the work done ",
		"(the original's stage frames, .chr group 0), then its normal sequence.")]
	public class WithConstructionStagesInfo : TraitInfo, Requires<WithSpriteBodyInfo>, Requires<UnderConstructionInfo>
	{
		[SequenceReference]
		[Desc("Sequence with one frame per construction stage.")]
		public readonly string Sequence = "make";

		public override object Create(ActorInitializer init) { return new WithConstructionStages(this); }
	}

	public class WithConstructionStages : INotifyCreated, ITick
	{
		readonly WithConstructionStagesInfo info;
		WithSpriteBody body;
		UnderConstruction underConstruction;
		bool showingStages;

		public WithConstructionStages(WithConstructionStagesInfo info)
		{
			this.info = info;
		}

		void INotifyCreated.Created(Actor self)
		{
			body = self.Trait<WithSpriteBody>();
			underConstruction = self.Trait<UnderConstruction>();
			if (underConstruction.IsComplete)
				return;

			showingStages = true;
			body.DefaultAnimation.PlayFetchIndex(info.Sequence, () =>
			{
				var length = body.DefaultAnimation.CurrentSequence.Length;
				return Math.Clamp((int)(underConstruction.Progress * length), 0, length - 1);
			});
		}

		void ITick.Tick(Actor self)
		{
			if (showingStages && underConstruction.IsComplete)
			{
				showingStages = false;
				body.CancelCustomAnimation(self);
			}
		}
	}
}
