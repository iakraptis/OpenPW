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
	// The caster goes into range of the target (a unit or a place; none for a spell on itself), turns to it and casts
	// the spell (PwSpell.Release pays the kill points and makes the effect).
	public class CastSpell : Activity
	{
		const int MaxMoveAttempts = 3;

		readonly PwSpell spell;
		readonly Target target;
		int moveAttempts;

		public CastSpell(Actor self, PwSpell spell, Target target)
		{
			this.spell = spell;
			this.target = target;
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling || !spell.IsReady(self) || target.Type == TargetType.Invalid)
				return true;

			var onSelf = spell.Info.Target == PwSpellTarget.Self;
			if (spell.Info.Target == PwSpellTarget.Unit && !spell.CanTarget(self, target.Actor))
				return true;

			if (!onSelf && !target.IsInRange(self.CenterPosition, spell.Info.Range))
			{
				if (moveAttempts++ >= MaxMoveAttempts)
					return true;

				QueueChild(self.Trait<IMove>().MoveWithinRange(target, spell.Info.Range));
				return false;
			}

			var facing = self.TraitOrDefault<IFacing>();
			if (facing != null && !onSelf)
				facing.Facing = (target.CenterPosition - self.CenterPosition).Yaw;

			spell.Release(self, target);
			return true;
		}
	}
}
