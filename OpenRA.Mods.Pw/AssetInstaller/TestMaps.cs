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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenRA.Mods.Pw.AssetInstaller
{
	// Test maps that play on a converted original map, so they can only be written by an install (after Maps.Export):
	// the terrain, map objects and wild creatures come from the converted map, the players and starting bases from here,
	// the script from an embedded template (AssetInstaller/MapTemplates/<map>), the shared test library from the
	// repository's test maps. run_tests.py finds them in the content folder.
	public static class TestMaps
	{
		// bot-test: four Normal AIs, one of each race, all against all on (4) The X Cross, each starting as a player
		// of its race does (rules/world.yaml StartingUnits): its main building and four workers. Each bot is named
		// after its race.
		const string BotTest = "bot-test";
		const string BotTestBase = "4-the-x-cross";
		static readonly (string Faction, string Main, string Worker)[] Races =
		{
			("primitive", "primitive-sanctuary", "primitive-buildman"),
			("tyrano", "tyrano-tyranonest", "tyrano-dinos"),
			("demon", "demon-demonden", "demon-insect"),
			("elf", "elf-heaven", "elf-wizard"),
		};

		static readonly string[] Colors = { "E02020", "2050E0", "20C020", "E0C020" };

		static string BotName((string Faction, string Main, string Worker) race) =>
			char.ToUpperInvariant(race.Faction[0]) + race.Faction[1..];

		public static void Export(InstallContext c)
		{
			var baseFolder = c.OutputPath($"maps/{BotTestBase}");
			if (!File.Exists(Path.Combine(baseFolder, "map.yaml")))
			{
				c.Warnings.Add($"{BotTest}: no converted map {BotTestBase}");
				return;
			}

			foreach (var file in Directory.GetFiles(baseFolder).Where(f => Path.GetFileName(f) != "map.yaml"))
				c.WriteFile($"maps/{BotTest}/{Path.GetFileName(file)}", File.ReadAllBytes(file));

			c.WriteFile($"maps/{BotTest}/test.lua", Template($"MapTemplates/{BotTest}/test.lua"));
			c.WriteFile($"maps/{BotTest}/testlib.lua", File.ReadAllBytes(Path.Combine(c.ModFolder, "maps/economy-test/testlib.lua")));
			c.WriteText($"maps/{BotTest}/map.yaml", BotTestYaml(File.ReadAllText(Path.Combine(baseFolder, "map.yaml")).Replace("\r\n", "\n")));
		}

		static byte[] Template(string name)
		{
			// The resource names keep the folder separator they were built with (a backslash on Windows).
			var assembly = typeof(TestMaps).Assembly;
			var resource = assembly.GetManifestResourceNames().FirstOrDefault(n => n.Replace('\\', '/') == name)
				?? throw new FileNotFoundException($"No embedded template {name}.");
			using var stream = assembly.GetManifestResourceStream(resource);
			using var memory = new MemoryStream();
			stream.CopyTo(memory);
			return memory.ToArray();
		}

		static string BotTestYaml(string source)
		{
			var lines = source.Split('\n');
			string Field(string name) => lines.First(l => l.StartsWith(name + ":", System.StringComparison.Ordinal));

			// The start positions (the converted map's spawn points) and its map objects and creatures.
			var starts = Regex.Matches(source, @"\tActor\d+: mpspawn\n\t\tOwner: Neutral\n\t\tLocation: (\d+),(\d+)")
				.Select(m => (X: Exts.ParseInt32Invariant(m.Groups[1].Value), Y: Exts.ParseInt32Invariant(m.Groups[2].Value))).ToList();
			var actors = new List<string>();
			for (var i = 0; i < lines.Length; i++)
			{
				if (!lines[i].StartsWith("\tObject", System.StringComparison.Ordinal) && !lines[i].StartsWith("\tCreature", System.StringComparison.Ordinal))
					continue;

				actors.Add(lines[i]);
				while (i + 1 < lines.Length && lines[i + 1].StartsWith("\t\t", System.StringComparison.Ordinal))
					actors.Add(lines[++i]);
			}

			var bots = Races.Select(BotName).ToArray();
			var s = new StringBuilder("MapFormat: 12\n\nRequiresMod: pw\n\nTitle: Test: Bots\n\nAuthor: OpenPW\n\n");
			s.Append($"{Field("Tileset")}\n\n{Field("MapSize")}\n\n{Field("Bounds")}\n\nVisibility: MissionSelector\n\nCategories: Test\n\n");
			s.Append("Players:\n");
			s.Append("\tPlayerReference@Neutral:\n\t\tName: Neutral\n\t\tOwnsWorld: True\n\t\tNonCombatant: True\n\t\tFaction: Random\n");
			s.Append($"\tPlayerReference@Creeps:\n\t\tName: Creeps\n\t\tNonCombatant: True\n\t\tFaction: Random\n\t\tEnemies: {string.Join(", ", bots)}\n");

			// The local player only watches.
			s.Append("\tPlayerReference@Multi0:\n\t\tName: Multi0\n\t\tPlayable: True\n\t\tLockFaction: True\n\t\tNonCombatant: True\n\t\tFaction: primitive\n");
			for (var i = 0; i < bots.Length; i++)
			{
				var enemies = bots.Where(b => b != bots[i]).Append("Creeps");
				s.Append($"\tPlayerReference@{bots[i]}:\n\t\tName: {bots[i]}\n\t\tBot: normal\n\t\tFaction: {Races[i].Faction}\n\t\tLockFaction: True\n");
				s.Append($"\t\tColor: {Colors[i]}\n\t\tEnemies: {string.Join(", ", enemies)}\n");
			}

			// Each bot's start: what StartingUnits gives a player of its race (rules/world.yaml), at its spawn point.
			s.Append("\nActors:\n");
			for (var i = 0; i < bots.Length && i < starts.Count; i++)
			{
				var (x, y) = starts[i];
				var (_, main, worker) = Races[i];
				s.Append($"\t{bots[i]}Main: {main}\n\t\tOwner: {bots[i]}\n\t\tLocation: {x},{y}\n");
				for (var w = 0; w < 4; w++)
					s.Append($"\t{bots[i]}Worker{w}: {worker}\n\t\tOwner: {bots[i]}\n\t\tLocation: {x + w},{y + 5}\n");
			}

			// The spawn points stay: the watching player is given one (it gets no starting units, SpawnStartingUnits is off).
			for (var i = 0; i < starts.Count; i++)
				s.Append($"\tSpawn{i}: mpspawn\n\t\tOwner: Neutral\n\t\tLocation: {starts[i].X},{starts[i].Y}\n");

			foreach (var line in actors)
				s.Append(line).Append('\n');

			s.Append(@"
Rules:
	player:
		# Fog off and the map explored: the bots are tested, not scouting.
		Shroud:
			FogCheckboxEnabled: False
			FogCheckboxLocked: True
			ExploredMapCheckboxEnabled: True
			ExploredMapCheckboxLocked: True
		# The match runs on after a side is wiped out; the script decides.
		-ConquestVictoryConditions:
	world:
		-SpawnStartingUnits:
		LuaScript:
			Scripts: testlib.lua, test.lua
		# 5x speed.
		MapOptions:
			GameSpeed: test
			GameSpeedDropdownLocked: true
".Replace("\r\n", "\n"));
			return s.ToString();
		}
	}
}
