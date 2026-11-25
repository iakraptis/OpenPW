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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenRA.Mods.Pw.AssetInstaller
{
	// The campaign scenarios of JW2_06.TRC (docs/formats/campaign.md), each into maps/<folder>/ of the content folder: the
	// map as Maps.cs converts it, every placed unit and building with its owner, the players, the briefing, the
	// narration MP3s, and mission.lua with the scenario script (players, teams, locations, triggers), which
	// mods/pw/scripts/campaign.lua runs. The missions are listed in mods/pw/missions/pw.yaml.
	public static class Campaign
	{
		const int Players = 8;
		const int Wildlife = 8;

		// P_SCRIP is loaded to 0x8ec420 (docs/formats/campaign.md); the code addresses its parts absolutely.
		const int ScriptBase = 0x8ec420;
		const int TriggerSize = 0x1c4a;
		const int Teams = 32;
		const int TeamsAt = 0x924d68 - ScriptBase;
		const int TeamSize = 0x9c;
		const int Locations = 100;
		const int LocationsAt = 0x928a58 - ScriptBase;
		const int LocationSize = 0x50;
		const int MoreTriggersAt = 0x92a99c - ScriptBase;

		// Action 1, a transmission: shown for (text length / 2 + 80) ticks (FUN_00405170 case 1).
		const int TransmissionAction = 1;
		const int ObjectivesAction = 13;
		const int ChangeTypeAction = 19;

		// The scenarios converted so far: prefix (race letter and number, as the archive names them) and map folder.
		static readonly (string Prefix, string Folder)[] Missions =
		{
			("P01", "primitive-01"),
			("P02", "primitive-02"),
			("P03", "primitive-03"),
			("P04", "primitive-04"),
			("P05", "primitive-05"),
			("P06", "primitive-06"),
			("P07", "primitive-07"),
			("P08", "primitive-08"),
			("P09", "primitive-09"),
			("P10", "primitive-10"),
			("P11", "primitive-11"),
			("P12", "primitive-12"),
		};

		// Unit types the mod has no actor for yet: Alien4 (.chr 164, which no scenario places).
		static readonly HashSet<int> Unsupported = [164];

		static readonly string[] Colors = { "E02020", "2050E0", "20C020", "E0C020", "20C0C0", "C020C0", "E08020", "A0A0A0" };

		public static void Export(InstallContext c)
		{
			var archive = c.Game.Archive("Jw2_06.trc");
			var image = Maps.ImageNames(c);
			var scenarios = Scenarios(archive);
			foreach (var (prefix, folder) in Missions)
			{
				if (!scenarios.TryGetValue(prefix, out var entries))
				{
					c.Warnings.Add($"campaign: no scenario {prefix} in JW2_06.TRC");
					continue;
				}

				byte[] Section(string name)
				{
					var entry = entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
					if (entry.Name == null)
						throw new FileNotFoundException($"campaign {prefix}: no {name} section");

					return archive.Read(entry.Index);
				}

				ExportMission(c, prefix, folder, Section, image);
			}
		}

		// The archive holds the scenarios one after another, each starting with its <prefix>_S.MP3 narration.
		static Dictionary<string, List<TrcArchive.Entry>> Scenarios(TrcArchive archive)
		{
			var scenarios = new Dictionary<string, List<TrcArchive.Entry>>();
			List<TrcArchive.Entry> current = null;
			foreach (var e in archive.Entries)
			{
				if (Regex.IsMatch(e.Name, @"^[A-Z]\d\d_S\.MP3$", RegexOptions.IgnoreCase))
					scenarios[e.Name[..3].ToUpperInvariant()] = current = [];

				current?.Add(e);
			}

			return scenarios;
		}

		static void ExportMission(InstallContext c, string prefix, string folder, Func<string, byte[]> section, Func<int, string> image)
		{
			var map = Maps.Read(section);
			var scenario = section("P_SCENA");
			var playe = section("P_PLAYE");
			var availability = section("P_UB");
			var script = section("P_SCRIP");

			var all = Maps.PlacedUnits(section("OBC"), image);
			var units = all.Where(u => !Unsupported.Contains(u.Type) && u.Owner >= 0 && u.Owner <= Wildlife).ToList();
			if (units.Count < all.Count)
				c.Warnings.Add($"campaign {prefix}: left out {all.Count - units.Count} units of types the mod does not have yet");

			// The players: the local one (P_PLAYE +0) and every one that owns something, by the race most of it belongs to.
			var human = BitConverter.ToInt32(playe, 0);
			var owners = units.Where(u => u.Owner < Players).Select(u => u.Owner).Append(human).Distinct().Order().ToList();
			var races = owners.ToDictionary(o => o, o => units.Where(u => u.Owner == o && Units.Race(u.Type) != "wild")
				.GroupBy(u => Units.Race(u.Type)).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "primitive");
			var names = new Dictionary<int, string>();
			foreach (var o in owners)
			{
				var name = char.ToUpperInvariant(races[o][0]) + races[o][1..];
				names[o] = names.ContainsValue(name) ? $"{name}{o}" : name;
			}

			names[Wildlife] = "Creeps";

			// Alliances: P_PLAYE 0x234, a bit mask per player.
			bool Allied(int a, int b) => a != b && (BitConverter.ToInt32(playe, 0x234 + 4 * a) & (1 << b)) != 0;

			var header = Text(scenario, 8, 0x800);
			var title = Field(header, "Title") ?? prefix;
			var objective = Field(header, "Objective") ?? title;
			var number = int.Parse(prefix[1..], CultureInfo.InvariantCulture);
			var race = races[human];
			var raceTitle = char.ToUpperInvariant(race[0]) + race[1..];
			var briefing = $"{Lines(header).FirstOrDefault()}\nTitle: {title}\nObjective: {objective}\n\n{Story(Text(scenario, 0x808, 0x1000))}";
			var ending = Story(Text(scenario, 0x1808, 0x1000));

			var s = new StringBuilder(Maps.Header(map, $"{raceTitle} {number}: {title}", "Wizard Soft", "MissionSelector", "Campaign"));
			s.Append("Players:\n");
			s.Append("\tPlayerReference@Neutral:\n\t\tName: Neutral\n\t\tOwnsWorld: True\n\t\tNonCombatant: True\n\t\tFaction: Random\n");
			s.Append("\tPlayerReference@Creeps:\n\t\tName: Creeps\n\t\tNonCombatant: True\n\t\tFaction: Random\n");
			s.Append(CultureInfo.InvariantCulture, $"\t\tEnemies: {string.Join(", ", owners.Select(o => names[o]))}\n");
			foreach (var o in owners)
			{
				s.Append(CultureInfo.InvariantCulture, $"\tPlayerReference@{names[o]}:\n\t\tName: {names[o]}\n");
				if (o == human)
					s.Append("\t\tPlayable: True\n\t\tAllowBots: False\n\t\tRequired: True\n\t\tLockSpawn: True\n\t\tLockTeam: True\n\t\tLockColor: True\n");
				else
					s.Append("\t\tBot: campaign\n");

				s.Append(CultureInfo.InvariantCulture, $"\t\tFaction: {races[o]}\n\t\tLockFaction: True\n\t\tColor: {Colors[o]}\n");
				var allies = owners.Where(x => Allied(o, x)).Select(x => names[x]).ToList();
				if (allies.Count > 0)
					s.Append(CultureInfo.InvariantCulture, $"\t\tAllies: {string.Join(", ", allies)}\n");

				s.Append(CultureInfo.InvariantCulture,
					$"\t\tEnemies: {string.Join(", ", owners.Where(x => x != o && !Allied(o, x)).Select(x => names[x]).Append("Creeps"))}\n");
			}

			// The camera starts over the human's units, where the one spawn point is (the engine wants one per playable
			// player; the units are all placed, SpawnStartingUnits is off).
			var humanUnits = units.Where(u => u.Owner == human).ToList();
			var camera = humanUnits.Count > 0
				? (X: (int)humanUnits.Average(u => u.X), Y: (int)humanUnits.Average(u => u.Y))
				: (X: map.Width / 2, Y: map.Height / 2);

			s.Append("\nActors:\n");
			s.Append(CultureInfo.InvariantCulture, $"\tSpawn0: mpspawn\n\t\tOwner: Neutral\n\t\tLocation: {camera.X},{camera.Y}\n");
			Maps.AppendObjects(s, map);
			foreach (var u in units)
			{
				s.Append(CultureInfo.InvariantCulture, $"\tUnit{u.Slot}: {u.Actor}\n\t\tOwner: {names[u.Owner]}\n\t\tLocation: {u.X},{u.Y}\n");
				if (u.Type < 96 || u.Type >= 160)
					s.Append(CultureInfo.InvariantCulture, $"\t\tFacing: {u.Facing}\n");
			}

			// Unit availability, P_UB: a byte per player and type. A type nobody of its race may build is taken off the build
			// menus.
			var disabled = units.Select(u => u.Type).Concat(Enumerable.Range(0, 160)).Distinct()
				.Where(t => Units.Race(t) != "wild" && image(t) != null
					&& owners.Any(o => races[o] == Units.Race(t))
					&& owners.Where(o => races[o] == Units.Race(t)).All(o => availability[o * 170 + t] == 0))
				.Order().Select(image).ToList();

			// The computer players play their original scripts (JW2_16 <prefix>_<player>.txt) with the campaign AI
			// (rules/campaign-ai.yaml, PwCampaignBotModule): what they train, and how many wait before they attack. The
			// Primitive and Demon scripts number the player with two digits (P01_02.txt), the Elf and Tyrano ones with one
			// (E01_2.txt).
			var scripts = c.Game.Archive("Jw2_16.trc");
			string ScriptName(int player) =>
				scripts.Contains($"{prefix}_{player:00}.txt") ? $"{prefix}_{player:00}.txt" : $"{prefix}_{player}.txt";
			var ai = owners.Where(o => o != human).ToDictionary(o => names[o], o => AiScript(scripts, ScriptName(o), races[o], image));

			s.Append("\nRules: pw|rules/campaign-ai.yaml, pw|rules/campaign.yaml\n");
			s.Append("\tplayer:\n\t\t# The script decides victory and defeat (scripts/campaign.lua).\n\t\t-ConquestVictoryConditions:\n");
			s.Append("\t\tPwCampaignBotModule@campaign:\n");
			AppendPlans(s, "Train", ai, p => p.Train);
			AppendPlans(s, "Settings", ai, p => p.Settings);
			AppendPlans(s, "LaterTrain", ai, p => p.LaterTrain);
			AppendPlans(s, "Later", ai, p => p.Later);
			if (ai.Any(a => a.Value.LaterAfter >= 0))
			{
				s.Append("\t\t\tLaterAfter:\n");
				foreach (var (player, plan) in ai.Where(a => a.Value.LaterAfter >= 0))
					s.Append(CultureInfo.InvariantCulture, $"\t\t\t\t{player}: {plan.LaterAfter}\n");
			}

			s.Append("\tworld:\n\t\t-SpawnStartingUnits:\n\t\tLuaScript:\n\t\t\tScripts: pw|scripts/campaign.lua, mission.lua\n");

			// Every unit counts towards losing, not only the buildings (the short game): some missions start with an army
			// and no base (P07, P09), and would otherwise be lost on the first tick.
			s.Append("\t\tMapOptions:\n\t\t\tShortGameCheckboxEnabled: False\n\t\t\tShortGameCheckboxLocked: True\n\t\t\tShortGameCheckboxVisible: False\n");
			s.Append(CultureInfo.InvariantCulture, $"\t\tMissionData:\n\t\t\tBriefing: {YamlLine(briefing)}\n");
			s.Append("\t\tObjectivesPanel:\n\t\t\tPanelName: MISSION_OBJECTIVES\n");
			foreach (var actor in disabled)
				s.Append(CultureInfo.InvariantCulture, $"\t{actor}:\n\t\tBuildable:\n\t\t\tPrerequisites: ~disabled\n");

			// The narrations of the intro and the ending story, as hidden music tracks of the map: music is streamed, a
			// sound would be decoded whole and stall the game. PwRaceMusic plays "intro" first; the script plays "outro".
			s.Append("\nMusic:\n");
			s.Append("\tintro:\n\t\tTitle: Briefing\n\t\tExtension: mp3\n\t\tHidden: true\n");
			s.Append("\toutro:\n\t\tTitle: Ending\n\t\tExtension: mp3\n\t\tHidden: true\n");
			c.WriteFile($"maps/{folder}/intro.mp3", section($"{prefix}_S.MP3"));
			c.WriteFile($"maps/{folder}/outro.mp3", section($"{prefix}_E.MP3"));
			Maps.WriteTerrain(c, $"maps/{folder}", map);
			c.WriteText($"maps/{folder}/map.yaml", s.ToString());

			var placed = units.Select(u => u.Slot).ToHashSet();
			c.WriteText($"maps/{folder}/mission.lua", MissionLua(prefix, script, names, human, owners, playe, objective, ending,
				camera, placed, image));
		}

		// The unit tiers SET_NO_BASE_UNIT, _MIDDLE_ and _HIGH_ set, by race (Pw.exe tables 0x5476f4, 0x547734, 0x547794).
		static readonly Dictionary<string, Dictionary<string, int[]>> Tiers = new()
		{
			{ "NO_BASE_UNIT", new() { { "primitive", [1, 3] }, { "elf", [17, 19] }, { "tyrano", [33, 34, 36] }, { "demon", [49, 50, 54] } } },
			{ "NO_MIDDLE_UNIT", new() { { "primitive", [2, 4, 7, 6] }, { "elf", [18, 20, 21, 23] }, { "tyrano", [35, 37, 40, 39, 42] }, { "demon", [57, 55, 56] } } },
			{ "NO_HIGH_UNIT", new() { { "primitive", [8, 9] }, { "elf", [25, 26, 27] }, { "tyrano", [43, 44] }, { "demon", [53, 58] } } },
		};

		// A computer player's script as the campaign AI plays it: the start settings and wanted units, and the later
		// ones after a number of attacks.
		sealed class AiPlan
		{
			public readonly Dictionary<string, int> Train = [];
			public readonly Dictionary<string, int> Settings = [];
			public readonly Dictionary<string, int> LaterTrain = [];
			public readonly Dictionary<string, int> Later = [];
			public int LaterAfter = -1;
		}

		// An AI script (docs/formats/text.md). The main block runs again and again: its GO_IF_UNDER_RELCNT <label> 2 runs
		// the start block on the first tick, its GO_IF_OVER_ATTCNT <label> <n> the later block once n attacks have
		// been made. A script without them is its own start block. UNUSE(D)_SCRIPT turns the player's AI off.
		static AiPlan AiScript(TrcArchive scripts, string name, string race, Func<int, string> image)
		{
			var plan = new AiPlan();
			if (!scripts.Contains(name))
				return plan;

			var blocks = new Dictionary<string, List<string[]>> { { "", [] } };
			var block = blocks[""];
			foreach (var raw in Encoding.GetEncoding(949).GetString(scripts.Read(name)).Split('\n'))
			{
				var words = raw.Split(';')[0].Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
				if (words.Length == 0)
					continue;

				if (words[0].StartsWith(':'))
					blocks[words[0][1..]] = block = [];
				else
					block.Add(words);
			}

			int Arg(string[] words, int i) => words.Length > i && int.TryParse(words[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

			void Apply(List<string[]> lines, Dictionary<string, int> train, Dictionary<string, int> settings)
			{
				foreach (var words in lines)
				{
					var command = words[0];
					if (command is "UNUSE_SCRIPT" or "UNUSED_SCRIPT" or "UNUNSED_SCRIPT")
						settings["PASSIVE"] = 1;
					else if (command == "SET_UNIT_MAKE" && image(Arg(words, 1)) is string unit)
						train[unit] = Arg(words, 2);
					else if (command.StartsWith("SET_", StringComparison.Ordinal) && Tiers.TryGetValue(command[4..], out var tiers))
						foreach (var type in tiers.GetValueOrDefault(race) ?? [])
							train[image(type)] = Arg(words, 1);
					else if (command.StartsWith("SET_", StringComparison.Ordinal))
						settings[command[4..]] = Arg(words, 1);
					else if (command.StartsWith("ADD_", StringComparison.Ordinal))
					{
						settings[command] = Arg(words, 1);
						settings[command + "_MAX"] = Arg(words, 2);
					}
				}
			}

			var main = blocks[""];
			var start = main.FirstOrDefault(w => w[0] == "GO_IF_UNDER_RELCNT");
			var later = main.FirstOrDefault(w => w[0] == "GO_IF_OVER_ATTCNT");
			Apply(main.Where(w => !w[0].StartsWith("GO", StringComparison.Ordinal)).ToList(), plan.Train, plan.Settings);
			if (start != null && blocks.TryGetValue(start[1], out var startBlock))
				Apply(startBlock, plan.Train, plan.Settings);

			if (later != null && blocks.TryGetValue(later[1], out var laterBlock))
			{
				plan.LaterAfter = Arg(later, 2);
				foreach (var (type, count) in plan.Train)
					plan.LaterTrain[type] = count;

				Apply(laterBlock, plan.LaterTrain, plan.Later);
			}

			return plan;
		}

		static void AppendPlans(StringBuilder s, string field, Dictionary<string, AiPlan> ai, Func<AiPlan, Dictionary<string, int>> values)
		{
			var listed = ai.Where(a => values(a.Value).Count > 0).ToList();
			if (listed.Count == 0)
				return;

			s.Append(CultureInfo.InvariantCulture, $"\t\t\t{field}:\n");
			foreach (var (player, plan) in listed)
			{
				s.Append(CultureInfo.InvariantCulture, $"\t\t\t\t{player}:\n");
				foreach (var (key, value) in values(plan))
					s.Append(CultureInfo.InvariantCulture, $"\t\t\t\t\t{key}: {value.ToString(CultureInfo.InvariantCulture)}\n");
			}
		}

		static string MissionLua(string prefix, byte[] script, Dictionary<int, string> names, int human, List<int> owners, byte[] playe,
			string objective, string ending, (int X, int Y) camera, HashSet<int> placed, Func<int, string> image)
		{
			static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);
			var s = new StringBuilder($"-- Generated by install-assets (OpenRA.Mods.Pw/AssetInstaller/Campaign.cs) from JW2_06.TRC scenario {prefix};\n" +
				"-- run by scripts/campaign.lua. Locations are pixel rectangles of the original map; teams list placed units.\n\n");
			s.Append("Mission = {\n");
			s.Append(CultureInfo.InvariantCulture, $"\tHuman = {Lua(names[human])},\n");
			s.Append(CultureInfo.InvariantCulture,
				$"\tPlayers = {{ {string.Join(", ", names.OrderBy(n => n.Key).Select(n => $"[{Int(n.Key)}] = {Lua(n.Value)}"))} }},\n");

			// P_PLAYE 0x144: starting berries per player.
			s.Append(CultureInfo.InvariantCulture,
				$"\tCash = {{ {string.Join(", ", owners.Select(o => $"{names[o]} = {Int(BitConverter.ToInt32(playe, 0x144 + 4 * o))}"))} }},\n");
			s.Append(CultureInfo.InvariantCulture, $"\tObjective = {Lua(objective)},\n\tEnding = {Lua(ending)},\n");
			s.Append(CultureInfo.InvariantCulture, $"\tCamera = {{ {Int(camera.X)}, {Int(camera.Y)} }},\n");

			s.Append("\tLocations = {\n");
			for (var i = 0; i < Locations; i++)
			{
				var o = LocationsAt + i * LocationSize + 0x24;
				var rect = Enumerable.Range(0, 4).Select(k => BitConverter.ToInt32(script, o + 4 * k)).ToArray();
				if (rect.Any(v => v != 0))
					s.Append(CultureInfo.InvariantCulture, $"\t\t[{Int(i)}] = {{ {string.Join(", ", rect.Select(Int))} }},\n");
			}

			// The unit types that action 19 changes from and to, by number.
			var changed = new SortedSet<int>();
			for (var i = 0; i < (script.Length > MoreTriggersAt ? 256 : 32); i++)
			{
				var t = i < 32 ? 4 + i * TriggerSize : MoreTriggersAt + (i - 32) * TriggerSize;
				var action = t + 0x8a6;
				if (script[t + 0x1bce] != 0 && BitConverter.ToInt32(script, action) == ChangeTypeAction)
				{
					changed.Add(BitConverter.ToInt32(script, action + 8));
					changed.Add(BitConverter.ToInt32(script, action + 0x25c));
				}
			}

			s.Append("\t},\n\tTypes = {\n");
			foreach (var type in changed.Where(t => t >= 0 && t < 170 && image(t) != null))
				s.Append(CultureInfo.InvariantCulture, $"\t\t[{Int(type)}] = {Lua(image(type))},\n");

			s.Append("\t},\n\tTeams = {\n");
			for (var i = 0; i < Teams; i++)
			{
				var o = TeamsAt + i * TeamSize;
				if (script[o + 0x7c] == 0)
					continue;

				var members = Enumerable.Range(0, 20).Select(k => BitConverter.ToInt32(script, o + 0x2c + 4 * k))
					.Where(placed.Contains).Select(slot => Lua($"Unit{slot}"));
				s.Append(CultureInfo.InvariantCulture, $"\t\t[{Int(i)}] = {{ {string.Join(", ", members)} }},\n");
			}

			// The first 32 triggers, then the teams and locations, then 224 more (T01 has an older script without them).
			s.Append("\t},\n\tTriggers = {\n");
			var count = script.Length > MoreTriggersAt ? 256 : 32;
			for (var i = 0; i < count; i++)
			{
				var t = i < 32 ? 4 + i * TriggerSize : MoreTriggersAt + (i - 32) * TriggerSize;
				if (script[t + 0x1bce] == 0)
					continue;

				var condition = t + 2;
				var action = t + 0x8a6;
				var conditionArgs = new[] { 8, 0x11c, 0x230, 0x344 }.Select(k => BitConverter.ToInt32(script, condition + k));
				var actionArgs = new[] { 8, 0x25c, 0x4b0, 0x704, 0x958 }.Select(k => BitConverter.ToInt32(script, action + k));
				var actionType = BitConverter.ToInt32(script, action);
				s.Append(CultureInfo.InvariantCulture, $"\t\t[{Int(i)}] = {{ Name = {Lua(Text(script, t + 0x1b4a, 0x80))},\n");
				s.Append(CultureInfo.InvariantCulture,
					$"\t\t\tCondition = {{ Type = {Int(BitConverter.ToInt32(script, condition))}, {string.Join(", ", conditionArgs.Select(Int))} }},\n");
				s.Append(CultureInfo.InvariantCulture, $"\t\t\tAction = {{ Type = {Int(actionType)}, {string.Join(", ", actionArgs.Select(Int))} }}");

				// Only these actions show their text; the others keep stale text from the editor.
				if (actionType == TransmissionAction || actionType == ObjectivesAction)
				{
					var raw = Text(script, action + 0x58, 0x800);
					var (speaker, message) = Transmission(raw);
					s.Append(CultureInfo.InvariantCulture, $",\n\t\t\tSpeaker = {Lua(speaker)}, Text = {Lua(message)}, Duration = {Int(raw.Length / 2 + 80)}");
				}

				s.Append(" },\n");
			}

			s.Append("\t},\n}\n");
			return s.ToString();
		}

		// A null-ended CP949 string.
		static string Text(byte[] data, int offset, int max)
		{
			var end = Array.IndexOf(data, (byte)0, offset, max);
			return Encoding.GetEncoding(949).GetString(data, offset, (end < 0 ? offset + max : end) - offset);
		}

		static IEnumerable<string> Lines(string text) =>
			text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

		// "Objective : Obstruct the Skeletons and" with its continuation lines, up to the next field or blank line.
		static string Field(string header, string name)
		{
			var lines = header.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).ToList();
			var i = lines.FindIndex(l => l.StartsWith(name, StringComparison.OrdinalIgnoreCase) && l.Contains(':'));
			if (i < 0)
				return null;

			var parts = new List<string> { lines[i][(lines[i].IndexOf(':') + 1)..].Trim() };
			for (var j = i + 1; j < lines.Count && lines[j].Length > 0 && !Regex.IsMatch(lines[j], @"^\w+\s*:"); j++)
				parts.Add(lines[j]);

			return Clean(string.Join(" ", parts));
		}

		// The intro and ending stories: lines wrapped by hand, paragraphs split by blank lines, "#..." and "@" marker lines.
		static string Story(string text)
		{
			var paragraphs = new List<string>();
			var current = new List<string>();
			foreach (var line in text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Append(""))
			{
				if (line.StartsWith('#') || line == "@")
					continue;

				if (line.Length > 0)
					current.Add(line);
				else if (current.Count > 0)
				{
					paragraphs.Add(Clean(string.Join(" ", current)));
					current.Clear();
				}
			}

			return string.Join("\n\n", paragraphs);
		}

		// "Powerman - Chief [Jarrmyn], We cannot ...": the speaker and the message, the names unbracketed.
		static (string Speaker, string Message) Transmission(string raw)
		{
			var text = Clean(string.Join(" ", Lines(raw)));
			var dash = text.IndexOf(" - ", StringComparison.Ordinal);
			return dash > 0 && dash < 40 ? (text[..dash].Trim(), text[(dash + 3)..].Trim()) : ("", text);
		}

		static string Clean(string text) => Regex.Replace(text.Replace("[", "").Replace("]", ""), @"\s+", " ").Trim();

		// One MiniYaml line: line breaks as \n (MissionData turns them back), no comment marks.
		static string YamlLine(string text) => text.Replace("#", "").Replace("\n", "\\n").Trim();

		static string Lua(string text)
		{
			var s = new StringBuilder("\"");
			foreach (var ch in text)
			{
				s.Append(ch switch
				{
					'\\' => "\\\\",
					'"' => "\\\"",
					'\n' => "\\n",
					'\r' => "",
					_ => ch.ToString(),
				});
			}

			return s.Append('"').ToString();
		}
	}
}
