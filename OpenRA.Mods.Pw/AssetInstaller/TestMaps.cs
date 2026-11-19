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
		// The duels' map (Battles).
		const string DuelMap = "2-one-vs-one";

		static readonly (string Faction, string Main, string Worker)[] Races =
		{
			("primitive", "primitive-sanctuary", "primitive-buildman"),
			("tyrano", "tyrano-tyranonest", "tyrano-dinos"),
			("demon", "demon-demonden", "demon-insect"),
			("elf", "elf-heaven", "elf-wizard"),
		};

		static readonly string[] Colors = { "E02020", "2050E0", "20C020", "E0C020" };

		// Battles of Easy AIs, each starting as a player of its race does (rules/world.yaml StartingUnits): its main
		// building and four workers, named after its race. bot-test (the four races on (4) The X Cross) is a test with
		// checks; the battle-* maps only report (run_tests.py --battles runs them, tools/battle_stats.py collects them):
		// the four races on (4) Four Ways, and every pairing of two races on (2) One vs One.
		static readonly (string Name, string Title, string BaseMap, bool Checks, int[] Races)[] Battles =
			new (string Name, string Title, string BaseMap, bool Checks, int[] Races)[]
			{
				("bot-test", "Test: Bots", "4-the-x-cross", true, new[] { 0, 1, 2, 3 }),
				("battle-four-ways", "Battle: Four Ways", "4-four-ways", false, new[] { 0, 1, 2, 3 }),
			}
			.Concat(
				from a in Enumerable.Range(0, Races.Length)
				from b in Enumerable.Range(a + 1, Races.Length - a - 1)
				select ($"battle-{Faction(a)}-{Faction(b)}", $"Battle: {BotName(a)} vs {BotName(b)}", DuelMap, false, new[] { a, b }))
			.ToArray();

		static string Faction(int race) => Races[race].Faction;

		static string BotName(int race) => char.ToUpperInvariant(Races[race].Faction[0]) + Races[race].Faction[1..];

		public static void Export(InstallContext c)
		{
			var script = Encoding.UTF8.GetString(Template("MapTemplates/bot-test/test.lua")).Replace("\r\n", "\n");
			var testlib = File.ReadAllBytes(Path.Combine(c.ModFolder, "maps/economy-test/testlib.lua"));
			foreach (var battle in Battles)
			{
				var baseFolder = c.OutputPath($"maps/{battle.BaseMap}");
				if (!File.Exists(Path.Combine(baseFolder, "map.yaml")))
				{
					c.Warnings.Add($"{battle.Name}: no converted map {battle.BaseMap}");
					continue;
				}

				foreach (var file in Directory.GetFiles(baseFolder).Where(f => Path.GetFileName(f) != "map.yaml"))
					c.WriteFile($"maps/{battle.Name}/{Path.GetFileName(file)}", File.ReadAllBytes(file));

				c.WriteText($"maps/{battle.Name}/test.lua", Script(script, battle.Name, battle.Checks, battle.Races));
				c.WriteFile($"maps/{battle.Name}/testlib.lua", testlib);
				var source = File.ReadAllText(Path.Combine(baseFolder, "map.yaml")).Replace("\r\n", "\n");
				c.WriteText($"maps/{battle.Name}/map.yaml", BattleYaml(source, battle.Title, battle.Races));
			}

			ExportPathing(c, testlib);
		}

		// pathing-test: a group of Powermen filing over a narrow ramp of (2) One vs One (MapTemplates/pathing-test). The
		// map's objects stay; its wild creatures don't (they would fight the group).
		static void ExportPathing(InstallContext c, byte[] testlib)
		{
			const string Name = "pathing-test";
			var baseFolder = c.OutputPath($"maps/{DuelMap}");
			if (!File.Exists(Path.Combine(baseFolder, "map.yaml")))
			{
				c.Warnings.Add($"{Name}: no converted map {DuelMap}");
				return;
			}

			foreach (var file in Directory.GetFiles(baseFolder).Where(f => Path.GetFileName(f) != "map.yaml"))
				c.WriteFile($"maps/{Name}/{Path.GetFileName(file)}", File.ReadAllBytes(file));

			c.WriteText($"maps/{Name}/test.lua", Encoding.UTF8.GetString(Template($"MapTemplates/{Name}/test.lua")).Replace("\r\n", "\n"));
			c.WriteFile($"maps/{Name}/testlib.lua", testlib);

			var source = File.ReadAllText(Path.Combine(baseFolder, "map.yaml")).Replace("\r\n", "\n");
			var lines = source.Split('\n');
			string Field(string field) => lines.First(l => l.StartsWith(field + ":", System.StringComparison.Ordinal));
			var s = new StringBuilder("MapFormat: 12\n\nRequiresMod: pw\n\nTitle: Test: Pathing\n\nAuthor: OpenPW\n\n");
			s.Append($"{Field("Tileset")}\n\n{Field("MapSize")}\n\n{Field("Bounds")}\n\nVisibility: MissionSelector\n\nCategories: Test\n\n");
			s.Append("Players:\n");
			s.Append("\tPlayerReference@Neutral:\n\t\tName: Neutral\n\t\tOwnsWorld: True\n\t\tNonCombatant: True\n\t\tFaction: Random\n");
			s.Append("\tPlayerReference@Multi0:\n\t\tName: Multi0\n\t\tPlayable: True\n\t\tLockFaction: True\n\t\tNonCombatant: True\n\t\tFaction: primitive\n");
			s.Append("\tPlayerReference@Group:\n\t\tName: Group\n\t\tFaction: primitive\n\t\tLockFaction: True\n");
			s.Append("\nActors:\n\tSpawn0: mpspawn\n\t\tOwner: Neutral\n\t\tLocation: 40,40\n");
			for (var i = 0; i < lines.Length; i++)
			{
				if (!lines[i].StartsWith("\tObject", System.StringComparison.Ordinal))
					continue;

				s.Append(lines[i]).Append('\n');
				while (i + 1 < lines.Length && lines[i + 1].StartsWith("\t\t", System.StringComparison.Ordinal))
					s.Append(lines[++i]).Append('\n');
			}

			s.Append(@"
Rules:
	player:
		Shroud:
			FogCheckboxEnabled: False
			FogCheckboxLocked: True
			ExploredMapCheckboxEnabled: True
			ExploredMapCheckboxLocked: True
		-ConquestVictoryConditions:
	world:
		-SpawnStartingUnits:
		LuaScript:
			Scripts: testlib.lua, test.lua
		MapOptions:
			GameSpeed: test
			GameSpeedDropdownLocked: true
".Replace("\r\n", "\n"));
			c.WriteText($"maps/{Name}/map.yaml", s.ToString());
		}

		// The template with this battle's settings: its bots, whether it checks, its name.
		static string Script(string template, string name, bool checks, int[] races)
		{
			var bots = string.Join(", ", races.Select(r => "\"" + BotName(r) + "\""));
			var lua = Regex.Replace(template, "^Bots = .*$", "Bots = { " + bots + " }", RegexOptions.Multiline);
			lua = Regex.Replace(lua, "^Checks = .*$", "Checks = " + (checks ? "true" : "false"), RegexOptions.Multiline);
			return Regex.Replace(lua, "^BattleName = .*$", "BattleName = \"" + name + "\"", RegexOptions.Multiline);
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

		static string BattleYaml(string source, string title, int[] races)
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

			var bots = races.Select(BotName).ToArray();
			var s = new StringBuilder($"MapFormat: 12\n\nRequiresMod: pw\n\nTitle: {title}\n\nAuthor: OpenPW\n\n");
			s.Append($"{Field("Tileset")}\n\n{Field("MapSize")}\n\n{Field("Bounds")}\n\nVisibility: MissionSelector\n\nCategories: Test\n\n");
			s.Append("Players:\n");
			s.Append("\tPlayerReference@Neutral:\n\t\tName: Neutral\n\t\tOwnsWorld: True\n\t\tNonCombatant: True\n\t\tFaction: Random\n");
			s.Append($"\tPlayerReference@Creeps:\n\t\tName: Creeps\n\t\tNonCombatant: True\n\t\tFaction: Random\n\t\tEnemies: {string.Join(", ", bots)}\n");

			// The local player only watches.
			s.Append("\tPlayerReference@Multi0:\n\t\tName: Multi0\n\t\tPlayable: True\n\t\tLockFaction: True\n\t\tNonCombatant: True\n\t\tFaction: primitive\n");
			for (var i = 0; i < bots.Length; i++)
			{
				var enemies = bots.Where(b => b != bots[i]).Append("Creeps");
				s.Append($"\tPlayerReference@{bots[i]}:\n\t\tName: {bots[i]}\n\t\tBot: easy\n\t\tFaction: {Faction(races[i])}\n\t\tLockFaction: True\n");
				s.Append($"\t\tColor: {Colors[races[i]]}\n\t\tEnemies: {string.Join(", ", enemies)}\n");
			}

			// Each bot's start: what StartingUnits gives a player of its race (rules/world.yaml), at its spawn point.
			s.Append("\nActors:\n");
			for (var i = 0; i < bots.Length && i < starts.Count; i++)
			{
				var (x, y) = starts[i];
				var (_, main, worker) = Races[races[i]];
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
