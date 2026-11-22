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

using OpenRA.Scripting;

namespace OpenRA.Mods.Pw.Scripting
{
	[ScriptGlobal("PwTime")]
	public class PwTimeGlobal : ScriptGlobal
	{
		public PwTimeGlobal(ScriptContext context)
			: base(context) { }

		[Desc("World ticks in one original tick (tickrate-plan.md): the campaign's and the tests' times are original ticks.")]
		public int TicksPerOriginal => PwTime.TicksPerOriginal;

		[Desc("The world ticks of an original duration.")]
		public int Ticks(int originalTicks) => PwTime.Ticks(originalTicks);

		[Desc("The original ticks since the game started (DateTime.GameTime in original ticks).")]
		public int Now => Context.World.WorldTick / PwTime.TicksPerOriginal;
	}
}
