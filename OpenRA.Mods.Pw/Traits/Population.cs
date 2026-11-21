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

using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("Population this unit takes up (.chr 0x184, docs/game/rules.md).")]
	public class UsesPopulationInfo : TraitInfo<UsesPopulation>
	{
		public readonly int Amount = 1;
	}

	public class UsesPopulation { }

	[Desc("Population this building provides (.chr 0x184: Sanctuary 9, House 10, production buildings 1).")]
	public class ProvidesPopulationInfo : ConditionalTraitInfo
	{
		public readonly int Amount = 0;

		public override object Create(ActorInitializer init) { return new ProvidesPopulation(this); }
	}

	public class ProvidesPopulation : ConditionalTrait<ProvidesPopulationInfo>
	{
		public ProvidesPopulation(ProvidesPopulationInfo info)
			: base(info) { }

		public int Amount => IsTraitDisabled ? 0 : Info.Amount;
	}

	// The population limit. Units take up population from when their training starts, so the queues can't train past
	// the limit.
	public static class PwPopulation
	{
		public static int Cost(ActorInfo unit) => unit.TraitInfoOrDefault<UsesPopulationInfo>()?.Amount ?? 0;

		public static int Capacity(Player player)
		{
			return player.World.ActorsWithTrait<ProvidesPopulation>()
				.Where(p => p.Actor.Owner == player && !p.Actor.IsDead && p.Actor.IsInWorld)
				.Sum(p => p.Trait.Amount);
		}

		// Units in the world plus everything in the player's training queues.
		public static int Used(Player player)
		{
			var rules = player.World.Map.Rules;
			var units = player.World.ActorsHavingTrait<UsesPopulation>()
				.Where(a => a.Owner == player && !a.IsDead && a.IsInWorld)
				.Sum(a => Cost(a.Info));

			var queued = player.World.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == player)
				.SelectMany(q => q.Trait.AllQueued())
				.Sum(i => Cost(rules.Actors[i.Item]));

			return units + queued;
		}

		public static bool HasRoomFor(Player player, ActorInfo unit)
		{
			var cost = Cost(unit);
			return cost == 0 || Used(player) + cost <= Capacity(player);
		}
	}

	[Desc("A building's training queue that won't start a unit past the population limit (PwPopulation), and that ",
		"researches upgrades (PwUpgrade) and lays items (PwItem) beside the building.")]
	public class PwProductionQueueInfo : ProductionQueueInfo
	{
		[FluentReference(optional: true)]
		[Desc("Shown when a unit would go past the population limit.")]
		public readonly string PopulationLimitTextNotification = null;

		public override object Create(ActorInitializer init) { return new PwProductionQueue(init, this); }
	}

	public class PwProductionQueue : ProductionQueue
	{
		public PwProductionQueue(ActorInitializer init, PwProductionQueueInfo info)
			: base(init, info) { }

		protected override void BeginProduction(ProductionItem item, bool hasPriority)
		{
			// An upgrade level is researched once: not again, and not in two queues at the same time.
			var unit = Actor.World.Map.Rules.Actors[item.Item];
			if (unit.HasTraitInfo<PwUpgradeInfo>())
			{
				var upgrades = Actor.Owner.PlayerActor.Trait<PwUpgrades>();
				var queued = Actor.World.ActorsWithTrait<ProductionQueue>()
					.Any(q => q.Actor.Owner == Actor.Owner && q.Trait.AllQueued().Any(i => i.Item == item.Item));
				if (upgrades.IsResearched(item.Item) || queued)
					return;
			}

			if (!PwPopulation.HasRoomFor(Actor.Owner, unit))
			{
				TextNotificationsManager.AddTransientLine(Actor.Owner, ((PwProductionQueueInfo)Info).PopulationLimitTextNotification);
				return;
			}

			base.BeginProduction(item, hasPriority);
		}

		// A finished upgrade is recorded on the player, and a finished item laid beside the building (even on an occupied
		// cell), instead of producing an actor through an exit.
		protected override bool BuildUnit(ActorInfo unit)
		{
			var isUpgrade = unit.HasTraitInfo<PwUpgradeInfo>();
			if (!isUpgrade && !unit.HasTraitInfo<PwItemInfo>())
				return base.BuildUnit(unit);

			var done = Queue.FirstOrDefault(i => i.Done && i.Item == unit.Name);
			if (done == null || !Actor.IsInWorld || Actor.IsDead)
				return false;

			if (isUpgrade)
				Actor.Owner.PlayerActor.Trait<PwUpgrades>().Complete(unit);
			else
			{
				var cell = PwItemPlacement.Cell(Actor);
				Actor.World.AddFrameEndTask(w => w.CreateActor(unit.Name,
				[
					new OwnerInit(Actor.Owner),
					new LocationInit(cell),
				]));
			}

			EndProduction(done);
			return true;
		}
	}
}
