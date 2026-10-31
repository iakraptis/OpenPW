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
			bool writeYaml, bool mergeManifest, Action<Stage> progress = null)
		{
			var context = new InstallContext(new GameFolder(gamePath), output, modFolder, writeYaml);
			foreach (var stage in stages)
			{
				progress?.Invoke(stage);
				stage.Run(context);
			}

			context.WriteManifest(mergeManifest);
			return context.Warnings;
		}
	}
}
