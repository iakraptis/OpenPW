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
using OpenRA.Mods.Common.Widgets;

namespace OpenRA.Mods.Pw.Widgets
{
	// A command panel slot: a button that also takes the right mouse button, which ButtonWidget ignores. OnRightClick
	// runs when the right button is released over the slot, also while the slot is disabled: a unit that can't be queued
	// any more (no berries, no room) can still be taken out of the queue. With no OnRightClick, a right click passes
	// through as before.
	public class SlotButtonWidget : ButtonWidget
	{
		public Action OnRightClick;
		bool rightPressed;

		[ObjectCreator.UseCtor]
		public SlotButtonWidget(ModData modData)
			: base(modData) { }

		protected SlotButtonWidget(SlotButtonWidget other)
			: base(other)
		{
			OnRightClick = other.OnRightClick;
		}

		public override SlotButtonWidget Clone() { return new SlotButtonWidget(this); }

		public override bool HandleMouseInput(MouseInput mi)
		{
			if (mi.Button != MouseButton.Right || OnRightClick == null)
				return base.HandleMouseInput(mi);

			if (mi.Event == MouseInputEvent.Down)
			{
				if (!TakeMouseFocus(mi))
					return false;

				rightPressed = true;
				Game.Sound.PlayNotification(ModRules, null, "Sounds", ClickSound, null);
				return true;
			}

			if (mi.Event == MouseInputEvent.Up && HasMouseFocus)
			{
				var released = rightPressed && RenderBounds.Contains(mi.Location);
				rightPressed = false;
				if (released)
					OnRightClick();

				return YieldMouseFocus(mi);
			}

			return rightPressed;
		}
	}
}
