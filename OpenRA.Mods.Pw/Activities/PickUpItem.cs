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

using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Activities
{
	// Walk to an item and pick it up: the item goes, and the unit becomes the item's type (Transform keeps health,
	// selection and control group; PwLevels carries the level and experience over).
	public class PickUpItem : Activity
	{
		const int MaxMoveAttempts = 3;
		static readonly WDist Reach = new(1024 + 512);

		readonly Actor item;
		readonly IMove move;
		int moveAttempts;

		public PickUpItem(Actor self, Actor item)
		{
			this.item = item;
			move = self.Trait<IMove>();
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling || !PwItemUser.CanPickUp(self, item))
				return true;

			item.Trait<PwItem>().Claimant = self;
			var target = Target.FromActor(item);
			if (!target.IsInRange(self.CenterPosition, Reach))
			{
				if (moveAttempts++ >= MaxMoveAttempts)
					return true;

				QueueChild(move.MoveWithinRange(target, WDist.FromCells(1)));
				return false;
			}

			item.Trait<PwItem>().Taken = true;
			var becomes = item.Info.TraitInfo<PwItemInfo>().Becomes;
			self.World.AddFrameEndTask(w => item.Dispose());

			var facing = self.TraitOrDefault<IFacing>()?.Facing ?? WAngle.Zero;
			QueueChild(new Transform(becomes) { Facing = facing, SkipMakeAnims = true });
			return false;
		}

		protected override void OnLastRun(Actor self)
		{
			// Given up (cancelled, unreachable): another unit may go for it.
			if (!item.IsDead && item.TraitOrDefault<PwItem>() is PwItem i && i.Claimant == self)
				i.Claimant = null;
		}
	}
}
