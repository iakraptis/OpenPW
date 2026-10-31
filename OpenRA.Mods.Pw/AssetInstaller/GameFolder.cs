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
	// An original Primitive Wars installation: the folder holding Pw.exe and the Jw2_*.trc archives. Only read.
	public sealed class GameFolder
	{
		public readonly string Path;
		readonly Dictionary<string, TrcArchive> archives = new(StringComparer.OrdinalIgnoreCase);

		public GameFolder(string path)
		{
			Path = System.IO.Path.GetFullPath(path);
			if (!IsGameFolder(Path))
				throw new DirectoryNotFoundException($"{Path} is not a Primitive Wars folder (no Pw.exe and Jw2_03.trc)");
		}

		public static bool IsGameFolder(string path)
		{
			return File.Exists(System.IO.Path.Combine(path, "Pw.exe")) && File.Exists(System.IO.Path.Combine(path, "Jw2_03.trc"));
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
