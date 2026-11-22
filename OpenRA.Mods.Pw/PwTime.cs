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

namespace OpenRA.Mods.Pw
{
	// The original game's ticks and the world's (tickrate-plan.md). The rules hold world ticks; docs/game holds the
	// original's. A value the code takes from the original (Pw.exe) goes through here.
	public static class PwTime
	{
		// World ticks in one original tick.
		public const int TicksPerOriginal = 1;

		// An original duration in world ticks.
		public static int Ticks(int originalTicks) => originalTicks * TicksPerOriginal;
	}
}
