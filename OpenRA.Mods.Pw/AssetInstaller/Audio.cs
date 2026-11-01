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
	// The global sounds (JW2_05, 33 WAVs, most with masked names) as sounds/ui-<nn>.wav by entry number: 0 the button
	// click, 6 victory, 9-16 "under attack" (two per race: Primitive, Elf, Tyrano, Demon), 20-28 the buildings' select
	// and destroyed sounds (.chr 0x244c, 0x2458). notifications/pw.yaml and the building voice sets use them.
	// The "under attack" pair of each race is also written as <race>-under-attack-<1|2>.wav, so the notification can
	// pick the player's race by prefix.
	public static class Audio
	{
		static readonly string[] Races = { "primitive", "elf", "tyrano", "demon" };

		public static void Export(InstallContext c)
		{
			var archive = c.Game.Archive("Jw2_05.trc");
			foreach (var entry in archive.Entries)
			{
				var data = archive.Read(entry.Index);
				if (data.Length <= 12 || data[0] != 'R' || data[1] != 'I' || data[2] != 'F' || data[3] != 'F')
					continue;

				c.WriteFile($"sounds/ui-{entry.Index:D2}.wav", data);
				if (entry.Index >= 9 && entry.Index <= 16)
					c.WriteFile($"sounds/{Races[(entry.Index - 9) / 2]}-under-attack-{(entry.Index - 9) % 2 + 1}.wav", data);
			}
		}
	}
}
