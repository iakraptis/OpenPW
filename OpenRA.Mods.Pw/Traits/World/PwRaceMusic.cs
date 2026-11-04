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
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Plays the local player's race music the way Pw.exe does (docs/game/rules.md, Music): the race's start tune ",
		"once, then its battle tracks in turn. Picking a track in the music player hands over to MusicPlaylist; ",
		"observers hear the plain playlist. List it after MusicPlaylist.")]
	public class PwRaceMusicInfo : TraitInfo, Requires<MusicPlaylistInfo>
	{
		[Desc("The tune played at the start of a game, by faction.")]
		public readonly Dictionary<string, string> Start = new();

		[Desc("The tracks played in turn after the start tune, by faction.")]
		public readonly Dictionary<string, string[]> Battle = new();

		public override object Create(ActorInitializer init) { return new PwRaceMusic(this); }
	}

	public class PwRaceMusic : IPostWorldLoaded, INotifyGameLoaded
	{
		readonly PwRaceMusicInfo info;
		MusicPlaylist playlist;
		World world;
		MusicInfo[] battle;
		int next;

		public PwRaceMusic(PwRaceMusicInfo info)
		{
			this.info = info;
		}

		void IPostWorldLoaded.PostWorldLoaded(World world, WorldRenderer wr)
		{
			if (!world.IsLoadingGameSave)
				Begin(world);
		}

		void INotifyGameLoaded.GameLoaded(World world)
		{
			Begin(world);
		}

		void Begin(World world)
		{
			this.world = world;
			playlist = world.WorldActor.Trait<MusicPlaylist>();
			var faction = world.LocalPlayer?.Faction.InternalName;
			if (faction == null || !info.Battle.TryGetValue(faction, out var names))
				return;

			var tracks = new List<MusicInfo>();
			foreach (var name in names)
				if (Track(name) is MusicInfo track)
					tracks.Add(track);

			battle = tracks.ToArray();
			if (info.Start.TryGetValue(faction, out var start) && Track(start) is MusicInfo tune)
				playlist.Play(tune, PlayNext);
			else
				PlayNext();
		}

		MusicInfo Track(string name)
		{
			return world.Map.Rules.Music.TryGetValue(name, out var track) && track.Exists ? track : null;
		}

		void PlayNext()
		{
			if (battle.Length == 0 || world.IsGameOver)
				return;

			var track = battle[next];
			next = (next + 1) % battle.Length;
			playlist.Play(track, PlayNext);
		}
	}
}
