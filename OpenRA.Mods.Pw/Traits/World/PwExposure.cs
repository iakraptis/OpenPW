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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Mod change (not in the original): a player without a finished main building (AcceptsBerries) for Delay ticks",
		"is exposed. Every enemy then sees a red marker on each of their units and buildings it can't see itself: in the",
		"fog, in the shroud, and invisible ones out of detection (the marker shows where, the unit stays untargetable).",
		"A finished main building hides them again and restarts the count. A lobby checkbox; off in campaign missions.")]
	public class PwExposureInfo : TraitInfo, ILobbyOptions
	{
		[Desc("Ticks without a main building before the player is exposed: 5 minutes at the default speed (71 ms).")]
		public readonly int Delay = 4225;

		[Desc("Also in maps for the mission selector (the campaign keeps the original rules; the test maps opt in).")]
		public readonly bool InMissions = false;

		public readonly Color MarkerColor = Color.Red;

		[Desc("Marker side in screen pixels, for units and for buildings.")]
		public readonly int UnitMarkerSize = 6;
		public readonly int BuildingMarkerSize = 10;

		[FluentReference]
		public readonly string CheckboxLabel = "checkbox-no-hq-reveal.label";

		[FluentReference]
		public readonly string CheckboxDescription = "checkbox-no-hq-reveal.description";

		public readonly bool CheckboxEnabled = true;
		public readonly bool CheckboxLocked = false;
		public readonly bool CheckboxVisible = true;
		public readonly int CheckboxDisplayOrder = 0;

		[FluentReference("player")]
		[Desc("To everyone else when a player is exposed.")]
		public readonly string ExposedNotification = "notification-no-hq-exposed";

		[FluentReference]
		[Desc("To the exposed player.")]
		public readonly string SelfExposedNotification = "notification-no-hq-self-exposed";

		[FluentReference("player")]
		[Desc("To everyone else when an exposed player has a main building again.")]
		public readonly string HiddenNotification = "notification-no-hq-hidden";

		IEnumerable<LobbyOption> ILobbyOptions.LobbyOptions(MapPreview map)
		{
			yield return new LobbyBooleanOption(map, "nohqreveal", CheckboxLabel, CheckboxDescription,
				CheckboxVisible, CheckboxDisplayOrder, CheckboxEnabled, CheckboxLocked);
		}

		public override object Create(ActorInitializer init) { return new PwExposure(init.Self, this); }
	}

	public class PwExposure : ITick, IRenderAnnotations
	{
		public readonly PwExposureInfo Info;
		readonly World world;
		readonly Dictionary<Player, int> withoutHQ = [];
		readonly HashSet<Player> withHQ = [];

		public bool Enabled { get; }

		public PwExposure(Actor self, PwExposureInfo info)
		{
			Info = info;
			world = self.World;
			Enabled = world.LobbyInfo.GlobalSettings.OptionOrDefault("nohqreveal", info.CheckboxEnabled)
				&& (info.InMissions || !world.Map.Visibility.HasFlag(MapVisibility.MissionSelector));
		}

		static bool Counts(Player p) => !p.NonCombatant && !p.Spectating && p.WinState != WinState.Lost;

		public static bool IsMainBuilding(Actor a) =>
			a.IsInWorld && !a.IsDead && a.Info.HasTraitInfo<AcceptsBerriesInfo>()
			&& (a.TraitOrDefault<UnderConstruction>()?.IsComplete ?? true);

		// Ticks the player has been without a main building (0 with one).
		public int TicksWithoutHQ(Player p) => withoutHQ.TryGetValue(p, out var t) ? t : 0;

		// Ticks left before the player is exposed: -1 with a main building (or when the rule is off), 0 once exposed.
		public int TicksLeft(Player p)
		{
			if (!Enabled || !withoutHQ.TryGetValue(p, out var t) || t == 0)
				return -1;

			return t >= Info.Delay ? 0 : Info.Delay - t;
		}

		// The players counting down or exposed.
		public IEnumerable<Player> PlayersWithoutHQ => withoutHQ.Keys;

		// "4m 12s": ticks at the game's speed, rounded up to the second.
		public static string FormatTime(int ticks, int timestep)
		{
			var s = (ticks * timestep + 999) / 1000;
			return $"{s / 60}m {s % 60}s";
		}

		public bool IsExposed(Player p) => Enabled && TicksWithoutHQ(p) >= Info.Delay;

		// The viewer sees this actor only as a marker: its exposed enemy's, and not visible to the viewer itself.
		public bool IsMarked(Actor a, Player viewer) =>
			viewer != null && a.IsInWorld && !a.IsDead && a.Owner != viewer && IsExposed(a.Owner)
			&& viewer.RelationshipWith(a.Owner) == PlayerRelationship.Enemy
			&& a.Info.HasTraitInfo<MustBeDestroyedInfo>() && !a.CanBeViewedByPlayer(viewer);

		public IEnumerable<Actor> Marked(Player viewer)
		{
			if (!Enabled || viewer == null || withoutHQ.Count == 0)
				yield break;

			foreach (var a in world.ActorsHavingTrait<MustBeDestroyed>())
				if (IsMarked(a, viewer))
					yield return a;
		}

		void ITick.Tick(Actor self)
		{
			if (!Enabled)
				return;

			withHQ.Clear();
			foreach (var a in world.ActorsHavingTrait<AcceptsBerries>())
				if (IsMainBuilding(a))
					withHQ.Add(a.Owner);

			foreach (var p in world.Players)
			{
				if (!Counts(p) || withHQ.Contains(p))
				{
					if (withoutHQ.Remove(p, out var was) && was >= Info.Delay && Counts(p))
						Notify(p, Info.HiddenNotification, null);

					continue;
				}

				var t = TicksWithoutHQ(p) + 1;
				withoutHQ[p] = t;
				if (t == Info.Delay)
					Notify(p, Info.ExposedNotification, Info.SelfExposedNotification);
			}
		}

		void Notify(Player p, string others, string own)
		{
			var local = world.LocalPlayer;
			if (local == p)
			{
				if (own != null)
					TextNotificationsManager.AddSystemLine(own);
			}
			else if (local == null || local.RelationshipWith(p) == PlayerRelationship.Enemy)
				TextNotificationsManager.AddSystemLine(others, "player", p.ResolvedPlayerName);
		}

		IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
		{
			foreach (var a in Marked(world.RenderPlayer))
			{
				var size = a.Info.HasTraitInfo<BuildingInfo>() ? Info.BuildingMarkerSize : Info.UnitMarkerSize;
				yield return new PwMarkerRenderable(a.CenterPosition, size, Info.MarkerColor);
			}
		}

		bool IRenderAnnotations.SpatiallyPartitionable => false;
	}

	// A filled square with a dark edge, a fixed size on screen, drawn above the shroud.
	public class PwMarkerRenderable : IRenderable, IFinalizedRenderable
	{
		readonly int size;
		readonly Color color;

		public PwMarkerRenderable(WPos pos, int size, Color color)
		{
			Pos = pos;
			this.size = size;
			this.color = color;
		}

		public WPos Pos { get; }
		public int ZOffset => 0;
		public bool IsDecoration => true;

		public IRenderable WithZOffset(int newOffset) { return this; }
		public IRenderable OffsetBy(in WVec vec) { return new PwMarkerRenderable(Pos + vec, size, color); }
		public IRenderable AsDecoration() { return this; }
		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }

		public void Render(WorldRenderer wr)
		{
			var c = wr.Viewport.WorldToViewPx(new Vector3(wr.ScreenPosition(Pos), 0));
			var h = size / 2;
			var r = Game.Renderer.RgbaColorRenderer;
			r.FillRect(new Vector3(c.X - h - 1, c.Y - h - 1, 0), new Vector3(c.X + h + 1, c.Y + h + 1, 0), Color.Black);
			r.FillRect(new Vector3(c.X - h, c.Y - h, 0), new Vector3(c.X + h, c.Y + h, 0), color);
		}

		public void RenderDebugGeometry(WorldRenderer wr) { }
		public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }
	}
}
