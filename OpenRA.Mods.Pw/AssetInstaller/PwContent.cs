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
	// Where the converted content lives (mounted by mods/pw/mod.yaml as "content") and how a run is done.
	public static class PwContent
	{
		// Bump when the converted output changes in a way older content cannot be used with.
		public const string Version = "v1";
		public const string ManifestFile = "install-manifest.txt";

		public static string Folder => Path.Combine(Platform.SupportDir, "Content", "pw");

		public static IReadOnlyList<string> Run(string gamePath, string output, string modFolder, IEnumerable<Stage> stages,
			bool writeYaml, bool mergeManifest, Action<Stage> progress = null, bool testMaps = true)
		{
			var context = new InstallContext(new GameFolder(gamePath), output, modFolder, writeYaml, testMaps);
			foreach (var stage in stages)
			{
				progress?.Invoke(stage);
				stage.Run(context);
			}

			context.WriteManifest(mergeManifest);
			return context.Warnings;
		}

		// Folders where a copy of the game commonly sits: "Primitive Wars" and similar names at the root of every fixed
		// drive, in Games and Program Files, in the user's Desktop, Downloads and Documents, and next to OpenPW itself
		// (an installed copy, or a development checkout with the game beside the repository).
		public static IEnumerable<string> Candidates()
		{
			// The Korean release is Jurassic War 2 (docs/game/versions.md); its installer's own folder is "Pw".
			var names = new[]
			{
				"Primitive Wars", "PrimitiveWars", "Primitive Wars - Legend of the Land", "Legend of the Land", "PW",
				"Jurassic War 2", "Jurassic War II", "Jurassic_War_2", "Jw2"
			};

			// Where an installer put it: Wizard Soft's games write HKLM\SOFTWARE\WIZARD SOFT\<game>\InstallPath (the
			// Korean release: "Jurassic era war 2").
			foreach (var path in RegistryInstallPaths())
				yield return path;

			var bases = new List<string>();

			foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
			{
				bases.Add(drive.RootDirectory.FullName);
				bases.Add(Path.Combine(drive.RootDirectory.FullName, "Games"));
			}

			var special = new[]
			{
				Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles,
				Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.UserProfile
			};

			foreach (var folder in special)
			{
				var path = Environment.GetFolderPath(folder);
				if (!string.IsNullOrEmpty(path))
					bases.Add(path);
			}

			var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			if (!string.IsNullOrEmpty(profile))
				bases.Add(Path.Combine(profile, "Downloads"));

			// The engine folder and three levels above it: an installed copy (OpenPW\), the portable zip (OpenPW\game\,
			// unzipped beside the game) and a development checkout (primwars\engine\, the game beside the repository).
			// Absolute: a development launch gives the engine folder as "..".
			var upFolder = Path.GetFullPath(Platform.EngineDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			for (var up = 0; up < 4 && !string.IsNullOrEmpty(upFolder); up++)
			{
				bases.Add(upFolder);
				upFolder = Path.GetDirectoryName(upFolder);
			}

			foreach (var candidate in bases.Distinct(StringComparer.OrdinalIgnoreCase).SelectMany(b => names.Select(n => Path.Combine(b, n))))
				yield return candidate;
		}

		static IEnumerable<string> RegistryInstallPaths()
		{
			if (!OperatingSystem.IsWindows())
				yield break;

			var paths = new List<string>();
			foreach (var key in new[] { @"SOFTWARE\WIZARD SOFT", @"SOFTWARE\WOW6432Node\WIZARD SOFT" })
			{
				try
				{
					using var wizard = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(key);
					foreach (var game in wizard?.GetSubKeyNames() ?? [])
					{
						using var sub = wizard.OpenSubKey(game);
						if (sub?.GetValue("InstallPath") is string path && !string.IsNullOrWhiteSpace(path))
							paths.Add(path.TrimEnd('\\', '/'));
					}
				}
				catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
				{
				}
			}

			foreach (var path in paths)
				yield return path;
		}

		public static string Detect()
		{
			foreach (var candidate in Candidates())
			{
				try
				{
					if (GameFolder.IsGameFolder(candidate))
						return candidate;
				}
				catch (Exception e) when (e is IOException or UnauthorizedAccessException)
				{
				}
			}

			return null;
		}
	}
}
