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
