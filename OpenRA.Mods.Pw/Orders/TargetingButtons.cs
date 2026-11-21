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
	// The mouse buttons of PW's targeting modes (abilities and spells, worker build placement, Patrol, Unload, and
	// Attack through PwAttackMoveOrderGenerator): the left button confirms and the right one cancels, under every mouse
	// control style. That is the engine's SupportPower (its ConfirmOrder would confirm with the right button under the
	// default Modern style).
	//
	// A mode confirms on the button's release, not its press: a mode ends when it confirms, so the release of a press
	// that confirmed would reach the default order generator, which can give the selected units another order with it.
	static class TargetingButtons
	{
		public const MouseActionType ActionType = MouseActionType.SupportPower;

		public static MouseButton Confirm => Game.Settings.Game.ResolveActionButton(ActionType);

		public static bool IsConfirm(MouseInput mi) => mi.Button == Confirm && mi.Event == MouseInputEvent.Up;

		public static bool IsCancel(MouseInput mi) =>
			mi.Button == Game.Settings.Game.ResolveCancelButton(ActionType) && mi.Event == MouseInputEvent.Up;
	}
}
