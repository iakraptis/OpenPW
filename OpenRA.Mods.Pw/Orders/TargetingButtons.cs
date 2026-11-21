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

namespace OpenRA.Mods.Pw.Orders
{
	// The mouse buttons of PW's targeting modes (abilities and spells, worker build placement, Patrol, Unload). They
	// follow the player's mouse control style as the engine's ConfirmOrder: the right button confirms under the default
	// Modern style, the left one under Classic, and the other button cancels.
	//
	// A mode confirms on the button's release, not its press. A mode ends when it confirms, so the release of a press
	// that confirmed would reach the default order generator, and a right release there gives the selected units a
	// contextual order (move, attack) that replaces the one just given.
	static class TargetingButtons
	{
		const MouseActionType ActionType = MouseActionType.ConfirmOrder;

		public static MouseButton Confirm => Game.Settings.Game.ResolveActionButton(ActionType);

		public static bool IsConfirm(MouseInput mi) => mi.Button == Confirm && mi.Event == MouseInputEvent.Up;

		public static bool IsCancel(MouseInput mi) =>
			mi.Button == Game.Settings.Game.ResolveCancelButton(ActionType) && mi.Event == MouseInputEvent.Up;
	}
}
