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
	// - map.yaml: title, tileset, players and a spawn at each start position.
	// The map gets a one-cell border, so original cell (x, y) is OpenRA cell (x + 1, y + 1).
	public static class Maps
	{
		const int Border = 1;
		const int GridSize = 256;

		public static void Export(InstallContext c)
		{
			foreach (var file in c.Game.MapFiles())
			{
				try
				{
					ExportMap(c, new TrcArchive(file));
				}
				catch (Exception e) when (e is InvalidDataException or FileNotFoundException)
				{
					c.Warnings.Add($"{Path.GetFileName(file)}: {e.Message}");
				}
			}
		}

		static void ExportMap(InstallContext c, TrcArchive jw2)
		{
			var pMap = jw2.Read("P_MAP");
			var title = Encoding.GetEncoding(949).GetString(pMap, 0, Math.Max(0, Array.IndexOf(pMap, (byte)0, 0, 0x20)));
			var tileset = BitConverter.ToInt32(pMap, 0x160);
			var width = BitConverter.ToInt32(pMap, 0x164);
			var height = BitConverter.ToInt32(pMap, 0x168);
			if (tileset < 0 || tileset >= Terrain.Tilesets.Length || width <= 0 || width > GridSize || height <= 0 || height > GridSize)
				throw new InvalidDataException($"unexpected map header (tileset {tileset}, {width}x{height})");

			var map1 = jw2.Read("MAP1");
			var map2 = jw2.Read("MAP2");
			var players = jw2.Read("P_SCENA")[4];
			var starts = StartPositions(jw2.Read("P_PLAYE"));
			var objects = MapObjects(jw2.Read("MAP3"), Terrain.Tilesets[tileset].Name, width, height);

			var slug = Regex.Replace(title.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
			var folder = $"maps/{slug}";
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
						writer.Write(inside ? MovementTemplate(Cell(map1, x - Border, y - Border)) : (ushort)4);
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
						writer.Write(Cell(map1, x, y));
						writer.Write(Cell(map2, x, y));
					}
				}

				writer.Flush();
				c.WriteFile($"{folder}/terrain.bin", stream.ToArray());
			}

			c.WriteText($"{folder}/map.yaml", MapYaml(title, Terrain.Tilesets[tileset].Id, mapWidth, mapHeight, width, height, players, starts, objects));
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

		static string MapYaml(string title, string tileset, int mapWidth, int mapHeight, int width, int height, int players,
			List<(int X, int Y)> starts, List<(string Actor, int X, int Y)> objects)
		{
			var multi = Enumerable.Range(0, players).Select(i => $"Multi{i}").ToList();
			var s = new StringBuilder();
			s.Append("MapFormat: 12\n\nRequiresMod: pw\n\n");
			s.Append($"Title: {title}\n\nAuthor: Wizard Soft\n\nTileset: {tileset}\n\n");
			s.Append($"MapSize: {mapWidth},{mapHeight}\n\nBounds: {Border},{Border},{width},{height}\n\n");
			s.Append("Visibility: Lobby\n\nCategories: Conquest\n\nPlayers:\n");
			s.Append("\tPlayerReference@Neutral:\n\t\tName: Neutral\n\t\tOwnsWorld: True\n\t\tNonCombatant: True\n\t\tFaction: Random\n");
			s.Append("\tPlayerReference@Creeps:\n\t\tName: Creeps\n\t\tNonCombatant: True\n\t\tFaction: Random\n");
			s.Append($"\t\tEnemies: {string.Join(", ", multi)}\n");
			foreach (var m in multi)
				s.Append($"\tPlayerReference@{m}:\n\t\tName: {m}\n\t\tPlayable: True\n\t\tFaction: Random\n\t\tEnemies: Creeps\n");

			s.Append("\nActors:\n");
			for (var i = 0; i < starts.Count; i++)
				s.Append($"\tActor{i}: mpspawn\n\t\tOwner: Neutral\n\t\tLocation: {starts[i].X},{starts[i].Y}\n");

			for (var i = 0; i < objects.Count; i++)
				s.Append($"\tObject{i}: {objects[i].Actor}\n\t\tOwner: Neutral\n\t\tLocation: {objects[i].X},{objects[i].Y}\n");

			return s.ToString();
		}
	}
}
