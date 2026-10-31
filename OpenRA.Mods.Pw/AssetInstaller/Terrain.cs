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

namespace OpenRA.Mods.Pw.AssetInstaller
{
	// The three tilesets (docs/formats/trt.md). PwTerrainRenderer reads them raw, so they are copied unchanged.
	public static class Terrain
	{
		// Index = the tileset number in P_MAP 0x160; Jw2_03.trc holds each at entry index * 15.
		public static readonly (string Source, string Name, string Id)[] Tilesets =
		{
			("Jungle.trt", "jungle", "JUNGLE"),
			("Dungeon.trt", "dungeon", "DUNGEON"),
			("Ruin.trt", "ruin", "RUIN"),
		};

		public static void ExportTilesets(InstallContext c)
		{
			var archive = c.Game.Archive("Jw2_03.trc");
			for (var i = 0; i < Tilesets.Length; i++)
				c.WriteFile($"tilesets/{Tilesets[i].Name}.trt", archive.Read(i * 15));
		}
	}
}
