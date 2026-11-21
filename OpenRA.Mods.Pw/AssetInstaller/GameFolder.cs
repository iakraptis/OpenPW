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
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenRA.Mods.Pw.AssetInstaller
{
	// A copy of Primitive Wars: a folder holding the Jw2_*.trc archives the installer reads. That is an installed game
	// (with Pw.exe) or the files of the CD copied to a folder (no Pw.exe: Setup.exe unpacks it from Jw2_13.trc), whose
	// archives are the same (docs/formats/trc.md). Only read.
	public sealed class GameFolder
	{
		// The archives the installer reads (Archive below).
		public static readonly string[] RequiredArchives =
		[
			"Jw2_01.trc", "Jw2_02.trc", "Jw2_03.trc", "Jw2_05.trc", "Jw2_06.trc", "Jw2_07.trc", "Jw2_09.trc", "Jw2_11.trc",
			"Jw2_12.trc", "Jw2_15.trc", "Jw2_16.trc"
		];

		public readonly string Path;
		readonly Dictionary<string, TrcArchive> archives = new(StringComparer.OrdinalIgnoreCase);

		public GameFolder(string path)
		{
			Path = System.IO.Path.GetFullPath(path);
			var missing = MissingArchives(Path);
			if (missing.Count > 0)
				throw new DirectoryNotFoundException($"{Path} is not a Primitive Wars folder (missing {string.Join(", ", missing)})");
		}

		public static bool IsGameFolder(string path) => MissingArchives(path).Count == 0;

		public static List<string> MissingArchives(string path)
		{
			return RequiredArchives.Where(a => !File.Exists(System.IO.Path.Combine(path, a))).ToList();
		}

		// Jw2_03.trc and the others, opened once per run.
		public TrcArchive Archive(string name)
		{
			if (!archives.TryGetValue(name, out var archive))
				archives[name] = archive = new TrcArchive(System.IO.Path.Combine(Path, name));

			return archive;
		}

		// The skirmish maps: Maps/*.jw2 and Maps/Rank/*.jw2.
		public IEnumerable<string> MapFiles()
		{
			var maps = System.IO.Path.Combine(Path, "Maps");
			if (!Directory.Exists(maps))
				return Enumerable.Empty<string>();

			return Directory.EnumerateFiles(maps, "*.*", SearchOption.AllDirectories)
				.Where(f => string.Equals(System.IO.Path.GetExtension(f), ".jw2", StringComparison.OrdinalIgnoreCase))
				.OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
		}
	}
}
