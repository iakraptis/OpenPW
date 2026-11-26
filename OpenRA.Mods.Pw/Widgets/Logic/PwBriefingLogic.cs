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

using System.Linq;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets.Logic
{
	// Opens a campaign mission's briefing (PwBriefing, PwBriefingWidget) when the game starts, once, the game paused
	// (a game with no other human) until it closes; the race music waits for it (PwRaceMusic.EndBriefing). Not in a
	// replay, a loaded game or a headless run.
	public class PwBriefingLogic : ChromeLogic
	{
		readonly World world;

		[ObjectCreator.UseCtor]
		public PwBriefingLogic(World world)
		{
			this.world = world;
		}

		public static bool WillShow(World world) =>
			!Game.IsHeadless && world.LocalPlayer != null && !world.IsReplay && !world.IsLoadingGameSave
			&& world.WorldActor.Info.TraitInfoOrDefault<PwBriefingInfo>()?.Pages.Count > 0;

		public override void Tick()
		{
			var briefing = world.WorldActor.TraitOrDefault<PwBriefing>();
			if (briefing == null || briefing.Shown)
				return;

			briefing.Shown = true;
			if (!WillShow(world))
				return;

			var alone = world.LobbyInfo.NonBotClients.Count() == 1;
			var wasPaused = world.Paused;
			if (alone)
				world.SetPauseState(true);

			// After this tick: the UI is iterating its widgets now.
			var widget = new PwBriefingWidget(world, world.WorldActor.Info.TraitInfo<PwBriefingInfo>(), () =>
			{
				if (alone)
					world.SetPauseState(wasPaused);

				world.WorldActor.TraitOrDefault<PwRaceMusic>()?.EndBriefing();
			});
			Game.RunAfterTick(() => Ui.Root.AddChild(widget));
		}
	}
}
