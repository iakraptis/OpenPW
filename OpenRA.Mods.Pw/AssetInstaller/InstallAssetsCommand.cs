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
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OpenRA.Mods.Pw.AssetInstaller
{
	// utility.cmd pw --install-assets [GAME_FOLDER] [--out FOLDER] [--only STAGE,...] [--write-yaml] [--list]
	// Converts an original Primitive Wars installation into the mod's content. GAME_FOLDER defaults to a "Primitive Wars"
	// folder next to the repository; the output goes to the player's content folder unless --out is given. The original
	// files are only read.
	sealed class InstallAssetsCommand : IUtilityCommand
	{
		string IUtilityCommand.Name => "--install-assets";

		bool IUtilityCommand.ValidateArguments(string[] args) => true;

		[Desc("[GAME_FOLDER]", "[--out FOLDER] [--only STAGE,...] [--write-yaml] [--list]",
			"Convert an original Primitive Wars installation into the mod's content.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			var modFolder = utility.ModData.Manifest.Package.Name;
			var options = args.Skip(1).ToList();
			if (options.Contains("--list"))
			{
				foreach (var stage in Stages.All)
					Console.WriteLine($"{stage.Name,-12} {stage.Description}");

				return;
			}

			var output = TakeValue(options, "--out") ?? PwContent.Folder;
			var only = TakeValue(options, "--only")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			var writeYaml = options.Remove("--write-yaml");
			var gamePath = options.FirstOrDefault(o => !o.StartsWith("--", StringComparison.Ordinal))
				?? Path.Combine(modFolder, "..", "..", "..", "Primitive Wars");

			var unknown = only?.Where(name => Stages.All.All(s => s.Name != name)).ToList();
			if (unknown?.Count > 0)
				throw new ArgumentException($"Unknown stage(s): {string.Join(", ", unknown)}. Use --list to see them.");

			Console.WriteLine($"Primitive Wars folder: {Path.GetFullPath(gamePath)}");
			Console.WriteLine($"Writing to: {Path.GetFullPath(output)}");

			var total = Stopwatch.StartNew();
			var watch = new Stopwatch();
			string running = null;
			void Report()
			{
				if (running != null)
					Console.WriteLine($"  {running,-12} {watch.ElapsedMilliseconds,6} ms");
			}

			var warnings = PwContent.Run(gamePath, output, modFolder, Stages.All.Where(s => only == null || only.Contains(s.Name)),
				writeYaml, mergeManifest: only != null, stage =>
				{
					Report();
					running = stage.Name;
					watch.Restart();
				});
			Report();

			foreach (var warning in warnings)
				Console.WriteLine($"WARNING: {warning}");

			Console.WriteLine($"Done in {total.Elapsed.TotalSeconds:0.0} s.");
		}

		static string TakeValue(List<string> options, string name)
		{
			var i = options.IndexOf(name);
			if (i < 0)
				return null;

			if (i + 1 >= options.Count)
				throw new ArgumentException($"{name} needs a value.");

			var value = options[i + 1];
			options.RemoveRange(i, 2);
			return value;
		}
	}
}
