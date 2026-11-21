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
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Pw.Orders
{
	// The engine's attack-move mode (the command panel's Attack) on PW's targeting buttons: a left click gives the order,
	// a right click cancels (TargetingButtons). The engine's takes the right button under the default Modern style.
	public class PwAttackMoveOrderGenerator : AttackMoveOrderGenerator
	{
		protected override MouseActionType ActionType => TargetingButtons.ActionType;

		public PwAttackMoveOrderGenerator(World world, IEnumerable<Actor> subjects)
			: base(world, subjects) { }
	}
}
