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
using System.Text;
using System.Text.RegularExpressions;

namespace OpenRA.Mods.Pw.AssetInstaller
{
	// The skirmish maps (docs/formats/map.md), each into maps/<slug>/ of the content folder:
	// - map.bin: the movement terrain per cell (tileset templates 1 Clear, 2 Ramp, 3 Water, 4 Cliff) from MAP1's flags;
	// - terrain.bin: "PWT1", u16 width, u16 height, u16 origin x, u16 origin y, then per cell (row by row) the original
	//   u32 MAP1 and u32 MAP2 words, which PwTerrainRenderer draws;
	// - map.yaml: title, tileset, players, a spawn at each start position, the map objects and the wild creatures.
	// The map gets a one-cell border, so original cell (x, y) is OpenRA cell (x + 1, y + 1).
	public static class Maps
	{
		public const int Border = 1;
		const int GridSize = 256;

		// Balance: hostile creatures that start too close to a player's start, moved away on the converted map (and the
		// test maps built on it): map, creature types, fewest cells from every start position.
		static readonly Dictionary<string, (string[] Types, int Distance)> KeepAway = new()
		{
			// The velociraptors lay 19 to 29 cells from the bases and raided them early.
			["2-one-vs-one"] = (new[] { "wild-velociraptor" }, 30),
		};

		// A map's terrain and objects, read from its sections: a skirmish .jw2 file or a campaign scenario (Campaign.cs).
		public sealed class Source
		{
			public string Title;
			public int Tileset, Width, Height;
			public byte[] Map1, Map2;
			public List<(string Actor, int X, int Y)> Objects;
		}

		// A live slot of the OBC unit table: a placed unit, building or creature, at its OpenRA cell.
		public sealed record Placed(int Slot, int Type, int Owner, string Actor, int X, int Y, int Facing);

		public static void Export(InstallContext c)
		{
			var image = ImageNames(c);
			foreach (var file in c.Game.MapFiles())
			{
				try
				{
					ExportMap(c, new TrcArchive(file), image);
				}
				catch (Exception e) when (e is InvalidDataException or FileNotFoundException)
				{
					c.Warnings.Add($"{Path.GetFileName(file)}: {e.Message}");
				}
			}

			TestMaps.Export(c);
		}

		// The actor (image) name of each unit type of JW2_09.TRC (Units.ImageName); null for the empty types.
		public static Func<int, string> ImageNames(InstallContext c)
		{
			var types = c.Game.Archive("Jw2_09.trc");
			var images = new Dictionary<int, string>();
			return type =>
			{
				if (!images.TryGetValue(type, out var image))
				{
					var data = type < types.Entries.Count ? types.Read(type) : Array.Empty<byte>();
					var chr = data.Length < ChrFile.RecordSize + 0x400 ? null : new ChrFile(data);
					images[type] = image = chr == null || chr.Frames.All(g => g.Count == 0) ? null : Units.ImageName(type, chr);
				}

				return image;
			};
		}

		public static Source Read(Func<string, byte[]> section)
		{
			var pMap = section("P_MAP");
			var tileset = BitConverter.ToInt32(pMap, 0x160);
			var width = BitConverter.ToInt32(pMap, 0x164);
			var height = BitConverter.ToInt32(pMap, 0x168);
			if (tileset < 0 || tileset >= Terrain.Tilesets.Length || width <= 0 || width > GridSize || height <= 0 || height > GridSize)
				throw new InvalidDataException($"unexpected map header (tileset {tileset}, {width}x{height})");

			return new Source
			{
				Title = Encoding.GetEncoding(949).GetString(pMap, 0, Math.Max(0, Array.IndexOf(pMap, (byte)0, 0, 0x20))),
				Tileset = tileset,
				Width = width,
				Height = height,
				Map1 = section("MAP1"),
				Map2 = section("MAP2"),
				Objects = MapObjects(section("MAP3"), Terrain.Tilesets[tileset].Name, width, height),
			};
		}

		static void ExportMap(InstallContext c, TrcArchive jw2, Func<int, string> image)
		{
			var map = Read(jw2.Read);
			var players = jw2.Read("P_SCENA")[4];
			var starts = StartPositions(jw2.Read("P_PLAYE"));

			// Only wild creatures (owner 8) are placed on the skirmish maps; they become Creeps actors.
			var creatures = PlacedUnits(jw2.Read("OBC"), image).Where(u => u.Owner == 8)
				.Select(u => (u.Actor, u.X, u.Y, u.Facing)).ToList();

			var slug = Regex.Replace(map.Title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
			if (KeepAway.TryGetValue(slug, out var keepAway))
				creatures = MoveAway(creatures, keepAway.Types, keepAway.Distance, starts, map.Objects, map.Map1, map.Width, map.Height);

			var folder = $"maps/{slug}";
			WriteTerrain(c, folder, map);
			c.WriteText($"{folder}/map.yaml", MapYaml(map, players, starts, creatures));
		}

		// map.bin (movement terrain) and terrain.bin (the original cells PwTerrainRenderer draws).
		public static void WriteTerrain(InstallContext c, string folder, Source map)
		{
			var (width, height) = (map.Width, map.Height);
			var mapWidth = width + 2 * Border;
			var mapHeight = height + 2 * Border;

			static uint Cell(byte[] layer, int x, int y) => BitConverter.ToUInt32(layer, 4 * (y * GridSize + x));

			// map.bin, format 2: tiles column by column (u16 template, u8 index), then the empty resource layer.
			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream))
			{
				writer.Write((byte)2);
				writer.Write((ushort)mapWidth);
				writer.Write((ushort)mapHeight);
				writer.Write(17u);
				writer.Write(0u);
				writer.Write((uint)(3 * mapWidth * mapHeight + 17));
				for (var x = 0; x < mapWidth; x++)
				{
					for (var y = 0; y < mapHeight; y++)
					{
						var inside = x >= Border && y >= Border && x < width + Border && y < height + Border;
						writer.Write(inside ? MovementTemplate(Cell(map.Map1, x - Border, y - Border)) : (ushort)4);
						writer.Write((byte)0);
					}
				}

				writer.Write(new byte[2 * mapWidth * mapHeight]);
				writer.Flush();
				c.WriteFile($"{folder}/map.bin", stream.ToArray());
			}

			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream))
			{
				writer.Write(Encoding.ASCII.GetBytes("PWT1"));
				writer.Write((ushort)width);
				writer.Write((ushort)height);
				writer.Write((ushort)Border);
				writer.Write((ushort)Border);
				for (var y = 0; y < height; y++)
				{
					for (var x = 0; x < width; x++)
					{
						writer.Write(Cell(map.Map1, x, y));
						writer.Write(Cell(map.Map2, x, y));
					}
				}

				writer.Flush();
				c.WriteFile($"{folder}/terrain.bin", stream.ToArray());
			}
		}

		// The map.yaml fields before Players.
		public static string Header(Source map, string title, string author, string visibility, string categories)
		{
			return $"MapFormat: 12\n\nRequiresMod: pw\n\nTitle: {title}\n\nAuthor: {author}\n\nTileset: {Terrain.Tilesets[map.Tileset].Id}\n\n" +
				$"MapSize: {map.Width + 2 * Border},{map.Height + 2 * Border}\n\nBounds: {Border},{Border},{map.Width},{map.Height}\n\n" +
				$"Visibility: {visibility}\n\nCategories: {categories}\n\n";
		}

		// The map objects as Neutral actors, Object<n>.
		public static void AppendObjects(StringBuilder s, Source map)
		{
			for (var i = 0; i < map.Objects.Count; i++)
				s.Append($"\tObject{i}: {map.Objects[i].Actor}\n\t\tOwner: Neutral\n\t\tLocation: {map.Objects[i].X},{map.Objects[i].Y}\n");
		}

		// MAP1 bits 29-31: walkable, water, buildable (docs/formats/map.md).
		static ushort MovementTemplate(uint map1)
		{
			var flags = map1 >> 29;
			if ((flags & 2) != 0)
				return 3;

			if ((flags & 1) != 0)
				return (ushort)((flags & 4) != 0 ? 1 : 2);

			return 4;
		}

		// MAP3 objects (docs/formats/map.md): kind 3 doodads (code bits 0-7: doodad code >> 2, variant code & 3) and kind 1
		// berry bushes (variant bits 2-3; the second cell, bit 11, belongs to the same bush). Actors from Objects.cs.
		static List<(string Actor, int X, int Y)> MapObjects(byte[] map3, string tileset, int width, int height)
		{
			var objects = new List<(string Actor, int X, int Y)>();
			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					var value = BitConverter.ToUInt32(map3, 4 * (y * GridSize + x));
					var kind = (value >> 8) & 7;
					if (kind == 3 && (value & 0xff) >> 2 < Objects.Doodads)
						objects.Add((Objects.DoodadActor(tileset, (int)(value & 0xff) >> 2, (int)value & 3), x + Border, y + Border));
					else if (kind == 1 && (value & 0x800) == 0)
						objects.Add((Objects.BushActor((int)(value >> 2) & 3), x + Border, y + Border));
				}
			}

			return objects;
		}

		// OBC (docs/formats/map.md): the unit table, 0x1d0-byte slots, live when +0x60 is 1; the campaign's teams refer to
		// units by slot. Positions are pixels (a building's is its footprint's top-left corner, which is how its sprites are
		// placed, Units.cs), so every kind lands on cell position / 32. Facing 1-8 runs clockwise from north. Types without
		// art (the "Null" entries) are left out.
		public static List<Placed> PlacedUnits(byte[] obc, Func<int, string> image)
		{
			const int SlotSize = 0x1d0;
			var units = new List<Placed>();
			for (var slot = 0; (slot + 1) * SlotSize <= obc.Length; slot++)
			{
				var o = slot * SlotSize;
				if (BitConverter.ToInt32(obc, o + 0x60) != 1)
					continue;

				var type = BitConverter.ToInt32(obc, o);
				var actor = type >= 0 && type < 170 ? image(type) : null;
				if (actor == null)
					continue;

				var facing = Math.Clamp(BitConverter.ToInt32(obc, o + 0xa8), 1, 8);
				units.Add(new Placed(slot, type, BitConverter.ToInt32(obc, o + 4), actor,
					BitConverter.ToInt32(obc, o + 0xb8) / 32 + Border,
					BitConverter.ToInt32(obc, o + 0xbc) / 32 + Border,
					(1024 - (facing - 1) * 128) % 1024));
			}

			return units;
		}

		// Each creature of the given types closer than distance cells to a start goes to the nearest walkable free cell
		// that is at least that far from every start (and 3 cells from the other moved ones), or stays if there is none.
		static List<(string Actor, int X, int Y, int Facing)> MoveAway(List<(string Actor, int X, int Y, int Facing)> creatures,
			string[] types, int distance, List<(int X, int Y)> starts, List<(string Actor, int X, int Y)> objects, byte[] map1,
			int width, int height)
		{
			static int Squared(int x, int y, (int X, int Y) p) => (x - p.X) * (x - p.X) + (y - p.Y) * (y - p.Y);
			bool FarFromStarts(int x, int y) => starts.All(st => Squared(x, y, st) >= distance * distance);

			var taken = objects.Select(o => (o.X, o.Y)).Concat(objects.Select(o => (o.X + 1, o.Y)))
				.Concat(creatures.Select(c => (c.X, c.Y))).ToHashSet();
			var moved = new List<(int X, int Y)>();
			var result = new List<(string Actor, int X, int Y, int Facing)>();
			foreach (var c in creatures)
			{
				if (!types.Contains(c.Actor) || FarFromStarts(c.X, c.Y))
				{
					result.Add(c);
					continue;
				}

				var cell = Enumerable.Range(Border, width).SelectMany(x => Enumerable.Range(Border, height).Select(y => (X: x, Y: y)))
					.Where(p => FarFromStarts(p.X, p.Y) && !taken.Contains(p) && moved.All(m => Squared(p.X, p.Y, m) >= 9)
						&& MovementTemplate(BitConverter.ToUInt32(map1, 4 * ((p.Y - Border) * GridSize + p.X - Border))) is 1 or 2)
					.OrderBy(p => Squared(p.X, p.Y, (c.X, c.Y)))
					.Select(p => ((int X, int Y)?)p).FirstOrDefault();

				if (cell == null)
				{
					result.Add(c);
					continue;
				}

				moved.Add(cell.Value);
				taken.Add(cell.Value);
				result.Add((c.Actor, cell.Value.X, cell.Value.Y, c.Facing));
			}

			return result;
		}

		// P_PLAYE 0x2d4: (x, y) pixel pairs, ended by -1.
		static List<(int X, int Y)> StartPositions(byte[] pPlaye)
		{
			var starts = new List<(int X, int Y)>();
			for (var o = 0x2d4; o + 8 <= pPlaye.Length; o += 8)
			{
				var x = BitConverter.ToInt32(pPlaye, o);
				if (x == -1)
					break;

				starts.Add((x / 32 + Border, BitConverter.ToInt32(pPlaye, o + 4) / 32 + Border));
			}

			return starts;
		}

		static string MapYaml(Source map, int players, List<(int X, int Y)> starts, List<(string Actor, int X, int Y, int Facing)> creatures)
		{
			var multi = Enumerable.Range(0, players).Select(i => $"Multi{i}").ToList();
			var s = new StringBuilder(Header(map, map.Title, "Wizard Soft", "Lobby", "Conquest"));
			s.Append("Players:\n");
			s.Append("\tPlayerReference@Neutral:\n\t\tName: Neutral\n\t\tOwnsWorld: True\n\t\tNonCombatant: True\n\t\tFaction: Random\n");
			s.Append("\tPlayerReference@Creeps:\n\t\tName: Creeps\n\t\tNonCombatant: True\n\t\tFaction: Random\n");
			s.Append($"\t\tEnemies: {string.Join(", ", multi)}\n");
			foreach (var m in multi)
				s.Append($"\tPlayerReference@{m}:\n\t\tName: {m}\n\t\tPlayable: True\n\t\tFaction: Random\n\t\tEnemies: Creeps\n");

			s.Append("\nActors:\n");
			for (var i = 0; i < starts.Count; i++)
				s.Append($"\tActor{i}: mpspawn\n\t\tOwner: Neutral\n\t\tLocation: {starts[i].X},{starts[i].Y}\n");

			AppendObjects(s, map);
			for (var i = 0; i < creatures.Count; i++)
			{
				var (actor, x, y, facing) = creatures[i];
				s.Append($"\tCreature{i}: {actor}\n\t\tOwner: Creeps\n\t\tLocation: {x},{y}\n\t\tFacing: {facing}\n");
			}

			return s.ToString();
		}
	}
}
