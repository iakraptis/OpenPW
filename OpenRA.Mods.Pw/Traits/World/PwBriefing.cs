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
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("A campaign mission's briefing (the original's Brief.mnt): shown before the mission starts, the game paused,",
		"page by page, each its picture (a file of the map) and text under the header, while the map's \"intro\" music",
		"track (the narration) plays; CONTINUE turns the page, and after the last one the mission starts",
		"(PwBriefingLogic, PwBriefingWidget). AssetInstaller/Campaign.cs writes it for each mission.")]
	public class PwBriefingInfo : TraitInfo
	{
		[Desc("The lines at the top: the stage, title and objective (\\n between lines).")]
		public readonly string Header = "";

		[Desc("The pages in order: \"<picture file>|<text>\" (\\n between paragraphs).")]
		public readonly Dictionary<int, string> Pages = [];

		public IEnumerable<(string Picture, string Text)> PageList =>
			Pages.OrderBy(p => p.Key).Select(p =>
			{
				var split = p.Value.IndexOf('|');
				return split < 0 ? ("", Unescape(p.Value)) : (p.Value[..split], Unescape(p.Value[(split + 1)..]));
			});

		public static string Unescape(string text) => text.Replace("\\n", "\n");

		public override object Create(ActorInitializer init) { return new PwBriefing(); }
	}

	public class PwBriefing
	{
		// Shown once a game (not again when the player widgets are rebuilt).
		public bool Shown;
	}
}
