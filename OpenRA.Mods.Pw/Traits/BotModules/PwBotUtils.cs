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
using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	// What the bot modules know about actor types, from their traits only: no faction's type names, so a new faction is
	// played as soon as its rules use the same traits (Builder, BerryGatherer, AcceptsBerries, ProvidesPopulation, ...).
	public static class PwBotUtils
	{
		public static IEnumerable<Actor> Owned(World world, Player player) =>
			world.Actors.Where(a => a.Owner == player && !a.IsDead);

		// Workers: they gather berries (and usually build).
		public static bool IsWorker(ActorInfo info) => info.HasTraitInfo<BerryGathererInfo>();

		public static bool IsBuilding(ActorInfo info) => info.HasTraitInfo<BuildingInfo>();

		// A drop-off for berries: the main building (Sanctuary), which the base is centred on.
		public static bool IsDropOff(ActorInfo info) => info.HasTraitInfo<AcceptsBerriesInfo>();

		// A building that is there for population (House, Nest: 10): no training queue, no drop-off. Other buildings give
		// 1 at most (the Tyrano Nisdos, which are tech).
		public static bool IsHouse(ActorInfo info) =>
			info.TraitInfos<ProvidesPopulationInfo>().Sum(p => p.Amount) > 1 && !info.HasTraitInfo<ProductionQueueInfo>() && !IsDropOff(info);

		// A building that fights (tower, trap).
		public static bool IsDefense(ActorInfo info) => IsBuilding(info) && info.HasTraitInfo<AttackBaseInfo>();

		// A unit that fights: armed, not a worker, not a building.
		public static bool IsArmy(ActorInfo info) =>
			info.HasTraitInfo<AttackBaseInfo>() && !IsWorker(info) && !IsBuilding(info) && info.HasTraitInfo<IPositionableInfo>();

		public static bool IsResearch(ActorInfo info) => info.HasTraitInfo<PwUpgradeInfo>();

		public static bool IsItem(ActorInfo info) => info.HasTraitInfo<PwItemInfo>();

		public static int Cost(ActorInfo info) => info.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;

		// Finished, or not a construction site at all.
		public static bool IsComplete(Actor actor) => actor.TraitOrDefault<UnderConstruction>()?.IsComplete ?? true;

		public static int Cash(Player player)
		{
			var resources = player.PlayerActor.Trait<PlayerResources>();
			return resources.Cash + resources.Resources;
		}

		// The base centre: the middle of the main building (drop-off), else of any building; null with no buildings.
		public static CPos? BaseCenter(IEnumerable<Actor> owned)
		{
			var main = owned.FirstOrDefault(a => a.IsInWorld && IsDropOff(a.Info))
				?? owned.FirstOrDefault(a => a.IsInWorld && IsBuilding(a.Info));
			if (main == null)
				return null;

			var footprint = main.Info.TraitInfo<BuildingInfo>().Dimensions;
			return main.Location + new CVec(footprint.X / 2, footprint.Y / 2);
		}

		// The berry bush with berries nearest a place, if one is within maxCells.
		public static CPos? NearestBush(World world, CPos center, int maxCells)
		{
			var bush = world.ActorsHavingTrait<BerryBush>().Where(b => b.IsInWorld && !b.Trait<BerryBush>().IsEmpty)
				.MinByOrDefault(b => (b.Location - center).LengthSquared);
			return bush != null && (bush.Location - center).LengthSquared <= maxCells * maxCells ? bush.Location : null;
		}

		// The base's edge: distance cells from the base centre toward the nearest enemy building (or the map centre while
		// none is known); null when the base centre is there.
		public static CPos? EdgeToward(World world, Player player, CPos center, int distance)
		{
			var enemy = world.ActorsHavingTrait<Building>()
				.Where(a => !a.IsDead && a.IsInWorld && !a.Owner.NonCombatant && player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy)
				.MinByOrDefault(a => (a.Location - center).LengthSquared);
			var bounds = world.Map.Bounds;
			var toward = enemy?.Location ?? new CPos(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
			var offset = toward - center;
			var length = offset.Length;
			if (length == 0)
				return null;

			return center + new CVec(offset.X * distance / length, offset.Y * distance / length);
		}

		// Starts a research the bot needs (to unlock an ability or a merge), from whichever of its queues offers it,
		// queued behind whatever the queue is making (the unit builder only adds to idle queues).
		public static void Research(World world, Player player, IBot bot, string upgrade, string reason)
		{
			var queues = world.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == player && !q.Actor.IsDead && q.Trait.Enabled && IsComplete(q.Actor)).ToList();
			if (queues.Any(q => q.Trait.AllQueued().Any(i => i.Item == upgrade)))
				return;

			foreach (var q in queues)
			{
				var item = q.Trait.BuildableItems().FirstOrDefault(i => i.Name == upgrade);
				if (item != null && Cash(player) >= Cost(item))
				{
					AIUtils.BotDebug("{0}: researching {1} for {2}.", player, upgrade, reason);
					bot.QueueOrder(Order.StartProduction(q.Actor, upgrade, 1));
					return;
				}
			}
		}
	}
}
