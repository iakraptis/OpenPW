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

using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets.Logic
{
	// Shows the victory or defeat screen (PwEndScreenWidget) when the local player wins or loses, after the objectives'
	// GameOverDelay (when the engine swaps the interface bar for the observer widgets). The screen goes on the UI root,
	// above the menus, so it outlives the player widgets this logic belongs to. Victory shows the race's picture
	// (chrome collection end-<race>), defeat the plain one (end-defeat).
	public class PwEndScreenLogic : ChromeLogic
	{
		readonly World world;
		bool shown;

		[ObjectCreator.UseCtor]
		public PwEndScreenLogic(World world)
		{
			this.world = world;
		}

		public override void Tick()
		{
			var player = world.LocalPlayer;
			if (shown || player == null || player.WinState == WinState.Undefined)
				return;

			shown = true;
			var won = player.WinState == WinState.Won;
			var race = player.Faction.InternalName;
			var collection = won && ChromeProvider.Collections.ContainsKey($"end-{race}") ? $"end-{race}" : "end-defeat";
			if (won && collection == "end-defeat")
				return;

			var elapsed = world.WorldTick;
			var delay = player.PlayerActor.Info.TraitInfoOrDefault<MissionObjectivesInfo>()?.GameOverDelay ?? 0;
			Game.RunAfterDelay(delay, () =>
			{
				if (Game.IsCurrentWorld(world))
					Ui.Root.AddChild(new PwEndScreenWidget(world, collection, elapsed));
			});
		}
	}
}
