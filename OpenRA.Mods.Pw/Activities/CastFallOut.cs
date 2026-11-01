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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Activities
{
	// The Chief walks into range of the target, spends the mana and strikes it with Fall out.
	public class CastFallOut : Activity
	{
		const int MaxMoveAttempts = 3;

		readonly Actor target;
		readonly PwFallOut fallOut;
		int moveAttempts;

		public CastFallOut(Actor self, Actor target)
		{
			this.target = target;
			fallOut = self.Trait<PwFallOut>();
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling || !PwFallOut.CanTarget(self, target) || !fallOut.IsReady(self))
				return true;

			var t = Target.FromActor(target);
			if (!t.IsInRange(self.CenterPosition, fallOut.Info.Range))
			{
				if (moveAttempts++ >= MaxMoveAttempts)
					return true;

				QueueChild(self.Trait<IMove>().MoveWithinRange(t, fallOut.Info.Range));
				return false;
			}

			var facing = self.TraitOrDefault<IFacing>();
			if (facing != null)
				facing.Facing = (target.CenterPosition - self.CenterPosition).Yaw;

			if (self.Trait<PwMana>().Spend(fallOut.Info.Cost))
			{
				target.Trait<PwFallOutVictim>().Strike(target);
				Game.Sound.Play(SoundType.World, fallOut.Info.Sound, target.CenterPosition);
			}

			return true;
		}
	}
}
