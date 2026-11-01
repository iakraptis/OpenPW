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

namespace OpenRA.Mods.Pw.AssetInstaller
{
	public sealed record Stage(string Name, string Description, Action<InstallContext> Run);

	// Everything --install-assets produces, in run order.
	public static class Stages
	{
		public static readonly IReadOnlyList<Stage> All = new Stage[]
		{
			new("tilesets", "The Jungle, Dungeon and Ruin tilesets (.trt), copied as they are", Terrain.ExportTilesets),
			new("objects", "Map objects: the doodads of each tileset and the berry bushes", Objects.Export),
			new("maps", "Every skirmish map: movement terrain, original terrain cells and start positions", Maps.Export),
			new("units", "Every unit and building: sprite sheets with player colour, and their sequences", Units.Export),
			new("chrome", "Interface art: the mouse cursors, portraits, upgrade and item buttons", Chrome.Export),
			new("audio", "Interface sounds: clicks, victory, under attack, building sounds", Audio.Export),
			new("effects", "Weapon effects: projectile and impact art, and the weapon sounds", Effects.Export),
		};
	}
}
