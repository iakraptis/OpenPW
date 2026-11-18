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
	// One install run: the game folder, the output folder (the player's content folder unless --out is given) and the
	// mod folder in the repository, where generated YAML is kept. Every written file goes through here and lands in the
	// manifest.
	public sealed class InstallContext
	{
		public readonly GameFolder Game;
		public readonly string Output;
		public readonly string ModFolder;
		public readonly bool WriteYaml;

		// Our own test and battle maps (TestMaps): for the developers' runs only, not the players' (PwContentLogic).
		public readonly bool TestMaps;
		public readonly SortedSet<string> Written = new(StringComparer.Ordinal);
		public readonly List<string> Warnings = new();

		public InstallContext(GameFolder game, string output, string modFolder, bool writeYaml, bool testMaps = true)
		{
			Game = game;
			Output = Path.GetFullPath(output);
			ModFolder = Path.GetFullPath(modFolder);
			WriteYaml = writeYaml;
			TestMaps = testMaps;
		}

		public void WriteFile(string relative, byte[] data)
		{
			File.WriteAllBytes(OutputPath(relative), data);
			Written.Add(relative);
		}

		public void WriteText(string relative, string text)
		{
			File.WriteAllText(OutputPath(relative), text.Replace("\r\n", "\n"));
			Written.Add(relative);
		}

		public string OutputPath(string relative)
		{
			var path = Path.Combine(Output, relative.Replace('/', Path.DirectorySeparatorChar));
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			return path;
		}

		// YAML the mod loads, generated from game data but kept in git. Checked against the repository copy; with
		// --write-yaml it is written there.
		public void RepoYaml(string relative, string text)
		{
			text = text.Replace("\r\n", "\n");
			var path = Path.Combine(ModFolder, relative.Replace('/', Path.DirectorySeparatorChar));
			var current = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : null;
			if (current == text)
				return;

			if (WriteYaml)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				File.WriteAllText(path, text);
				Console.WriteLine($"  updated {relative}");
			}
			else
				Warnings.Add($"{relative} differs from what the game data gives (run with --write-yaml to update it)");
		}

		// Written last: the mod treats the content as installed once this exists.
		public void WriteManifest(bool merge)
		{
			var path = Path.Combine(Output, PwContent.ManifestFile);
			if (merge && File.Exists(path))
				foreach (var line in File.ReadAllLines(path).Where(l => !l.StartsWith('#') && l.Length > 0))
					Written.Add(line);

			var lines = new List<string> { $"# OpenPW content {PwContent.Version}, converted from {Game.Path}" };
			lines.AddRange(Written);
			File.WriteAllText(path, string.Join("\n", lines) + "\n");
		}
	}
}
