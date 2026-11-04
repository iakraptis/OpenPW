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

namespace OpenRA.Mods.Pw.AssetInstaller
{
	// The music (JW2_15, 19 MP3s, most with masked names) as music/<name>.mp3, named by the use Pw.exe makes of each
	// entry (docs/game/rules.md, Music): 0 the menus, 3 victory, 17 defeat, 3 * race + 1 and + 2 the race's battle
	// tracks, 13 + race the race's start tune (race order Primitive, Elf, Tyrano, Demon). Entries 6, 9, 12 and 18
	// are not played by the skirmish game; they keep their number. music/pw.yaml lists them all.
	public static class Music
	{
		static readonly string[] Names =
		{
			"title", "primitive-1", "primitive-2", "victory", "elf-1", "elf-2", "track-06", "tyrano-1", "tyrano-2",
			"track-09", "demon-1", "demon-2", "track-12", "primitive-start", "elf-start", "tyrano-start",
			"demon-start", "defeat", "track-18",
		};

		public static void Export(InstallContext c)
		{
			var archive = c.Game.Archive("Jw2_15.trc");
			foreach (var entry in archive.Entries)
				if (entry.Index < Names.Length)
					c.WriteFile($"music/{Names[entry.Index]}.mp3", archive.Read(entry.Index));
		}
	}
}
