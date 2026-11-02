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
using OpenRA.Mods.Pw.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("AI base building for factions whose workers build (Builder; there is no construction yard queue). After",
		"OpenSYW's PeasantBaseBuilderBotModule. Every DecisionInterval it finishes abandoned sites first, then builds a",
		"house when the population is nearly full, otherwise the building furthest below its share of the base; it",
		"finds a site around the base centre (the drop-off, Sanctuary) and sends a worker with the PwBuildAt order the",
		"build palette issues. What a worker can build comes from its Builder list and every type's role from its traits",
		"(houses: more than 1 ProvidesPopulation and nothing else; drop-offs: AcceptsBerries; defences: armed buildings,",
		"which guard the main building, the berries and the base's edge), so it plays any faction;",
		"BuildingShares and BuildingLimits only tune it.")]
	public class PwBaseBuilderBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between building decisions.")]
		public readonly int DecisionInterval = 60;

		[Desc("How many buildings may be under construction at once.")]
		public readonly int MaxConcurrentConstructions = 1;

		[Desc("Workers the bot needs before it builds anything but houses.")]
		public readonly int MinimumWorkers = 4;

		[Desc("Cash kept back after paying for a building (not for houses), so training goes on.")]
		public readonly int CashReserve = 200;

		[Desc("A house is built when the free population is this or less (and no house is being built).")]
		public readonly int PopulationMargin = 3;

		[Desc("Share of a building type in the base (relative weights; 0 never builds it). Types not listed use",
			"DefaultShare, defences DefenseShare.")]
		public readonly Dictionary<string, int> BuildingShares = new();

		public readonly int DefaultShare = 10;
		public readonly int DefenseShare = 6;

		[Desc("Most buildings of a type. Types not listed: DefenseLimit for defences, 1 for the others (a destroyed one",
			"is built again).")]
		public readonly Dictionary<string, int> BuildingLimits = new();

		public readonly int DefenseLimit = 3;

		[Desc("Towers guard three spots: the main building, the berry bushes nearest the base and the base's edge toward",
			"the nearest enemy. Once the bot has its workforce and a production building, a spot without a tower gets one",
			"before anything but houses; further towers go to the least guarded spot. The tower is the defence (armed",
			"building) with the longest reach. False: towers only by their share, around the base.")]
		public readonly bool GuardSpots = true;

		[Desc("A defence within this many cells of a spot guards it.")]
		public readonly int GuardRadius = 5;

		[Desc("Search annulus around the base centre, in cells.")]
		public readonly int MinBaseRadius = 3;
		public readonly int MaxBaseRadius = 16;

		[Desc("Free cells kept around each building so units can pass and exits stay clear.")]
		public readonly int Spacing = 1;

		[Desc("How many of the nearest valid sites to choose from at random.")]
		public readonly int SiteChoices = 6;

		public override object Create(ActorInitializer init) { return new PwBaseBuilderBotModule(init.Self, this); }
	}

	public class PwBaseBuilderBotModule : ConditionalTrait<PwBaseBuilderBotModuleInfo>, IBotTick
	{
		readonly World world;
		readonly Player player;
		TechTree techTree;
		int ticks;

		// The berries the next building needs (its cost and CashReserve) while the bot can't afford it, else 0:
		// PwUnitBuilderBotModule holds this back from army units and research, so the base keeps growing.
		public int SavingFor { get; private set; }

		public PwBaseBuilderBotModule(Actor self, PwBaseBuilderBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			techTree = player.PlayerActor.Trait<TechTree>();
			base.Created(self);
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (++ticks % Info.DecisionInterval != 0)
				return;

			var owned = PwBotUtils.Owned(world, player).ToList();
			var free = owned.Where(a => a.IsInWorld && a.Info.HasTraitInfo<BuilderInfo>() && !IsConstructing(a)).ToList();
			if (free.Count == 0)
				return;

			var sites = owned.Where(a => a.IsInWorld && !PwBotUtils.IsComplete(a)).ToList();

			// Finish what is already paid for: a site whose worker was killed or pulled away.
			var abandoned = sites.FirstOrDefault(s => free.Any(b => b.Trait<Builder>().CanResume(b, s)));
			if (abandoned != null)
			{
				var worker = free.Where(b => b.Trait<Builder>().CanResume(b, abandoned)).MinBy(b => (b.Location - abandoned.Location).LengthSquared);
				AIUtils.BotDebug("{0}: resuming {1} at {2}.", player, abandoned.Info.Name, abandoned.Location);
				bot.QueueOrder(new Order(Builder.ResumeOrderID, worker, Target.FromActor(abandoned), false));
				return;
			}

			SavingFor = 0;
			if (sites.Count >= Info.MaxConcurrentConstructions)
				return;

			var center = BaseCenter(owned, free);
			foreach (var type in Choose(owned, free))
			{
				var info = world.Map.Rules.Actors[type];
				var reserve = PwBotUtils.IsHouse(info) ? 0 : Info.CashReserve;
				if (PwBotUtils.Cash(player) < PwBotUtils.Cost(info) + reserve)
				{
					SavingFor = PwBotUtils.Cost(info) + reserve;
					return;
				}

				// A type with no room (a large building in a cramped base) mustn't block the others.
				var worker = free.Where(b => b.Trait<Builder>().CanBuild(type))
					.OrderBy(b => b.IsIdle ? 0 : 1).ThenBy(b => (b.Location - center).LengthSquared).FirstOrDefault();
				var site = worker == null ? null
					: PwBotUtils.IsDefense(info) && Info.GuardSpots ? GuardedSpots(owned, center).OrderBy(g => g.Towers)
						.Select(g => FindSite(worker, info, g.Spot, 1, Info.GuardRadius)).FirstOrDefault(c => c != null)
					: FindSite(worker, info, center, Info.MinBaseRadius, Info.MaxBaseRadius);
				if (site == null)
				{
					AIUtils.BotDebug("{0}: no site for {1}.", player, type);
					continue;
				}

				AIUtils.BotDebug("{0}: building {1} at {2}.", player, type, site.Value);
				bot.QueueOrder(new Order(Builder.OrderID, worker, Target.FromCell(world, site.Value), false) { TargetString = type });
				return;
			}
		}

		// Walking to a site to build it (inside one, the worker is out of the world).
		static bool IsConstructing(Actor worker)
		{
			for (var activity = worker.CurrentActivity; activity != null; activity = activity.NextActivity)
				if (activity is ConstructBuilding)
					return true;

			return false;
		}

		// The types to build, best first: a house when the population is nearly full, else the type furthest below its
		// share; only types a free worker can build now (prerequisites met, under the limit).
		IEnumerable<string> Choose(List<Actor> owned, List<Actor> free)
		{
			var counts = owned.Where(a => PwBotUtils.IsBuilding(a.Info)).GroupBy(a => a.Info.Name).ToDictionary(g => g.Key, g => g.Count());
			int Count(string type) => counts.TryGetValue(type, out var n) ? n : 0;

			var buildable = free.SelectMany(b => b.Trait<Builder>().Info.Types).Distinct()
				.Where(t => world.Map.Rules.Actors.ContainsKey(t))
				.Select(t => world.Map.Rules.Actors[t])
				.Where(i =>
				{
					var prerequisites = i.TraitInfoOrDefault<BuildableInfo>()?.Prerequisites;
					return prerequisites == null || prerequisites.Length == 0 || techTree.HasPrerequisites(prerequisites);
				})
				.ToList();

			// More population: the cheapest house, unless one is going up already.
			var room = PwPopulation.Capacity(player) - PwPopulation.Used(player);
			var houseGoingUp = owned.Any(a => PwBotUtils.IsHouse(a.Info) && !PwBotUtils.IsComplete(a));
			if (room <= Info.PopulationMargin && !houseGoingUp)
			{
				var house = buildable.Where(PwBotUtils.IsHouse).MinByOrDefault(PwBotUtils.Cost);
				if (house != null)
					yield return house.Name;
			}

			// Economy first: nothing else until the workforce is there.
			if (owned.Count(a => PwBotUtils.IsWorker(a.Info)) < Info.MinimumWorkers)
				yield break;

			// A tower for every guard spot without one, once a production building stands (the first thing to defend).
			var tower = buildable.Where(PwBotUtils.IsDefense).OrderByDescending(Reach).ThenBy(PwBotUtils.Cost).FirstOrDefault();
			var producing = owned.Any(a => PwBotUtils.IsComplete(a) && a.Info.HasTraitInfo<ProductionQueueInfo>() && !PwBotUtils.IsDropOff(a.Info));
			if (Info.GuardSpots && tower != null && producing && GuardedSpots(owned, BaseCenter(owned, free)).Any(g => g.Towers == 0))
				yield return tower.Name;

			// The type whose (count + 1) / share is smallest is furthest below its share.
			var byShare = buildable
				.Where(i => !PwBotUtils.IsHouse(i))
				.Select(i => (Info: i, Share: Share(i), Limit: Limit(i)))
				.Where(t => t.Share > 0 && Count(t.Info.Name) < t.Limit)
				.OrderBy(t => (Count(t.Info.Name) + 1) * 1000 / t.Share)
				.ThenByDescending(t => t.Share);

			foreach (var t in byShare)
				yield return t.Info.Name;
		}

		// How far a defence strikes: its longest weapon.
		int Reach(ActorInfo info) => info.TraitInfos<ArmamentInfo>()
			.Select(a => world.Map.Rules.Weapons.TryGetValue(a.Weapon.ToLowerInvariant(), out var w) ? w.Range.Length : 0)
			.DefaultIfEmpty(0).Max();

		// The spots towers guard, each with the number of the bot's defences (finished or not) within GuardRadius: the
		// main building, the berry bush nearest the base centre, and the base's edge toward the nearest enemy building
		// (or the map centre while none is known).
		List<(CPos Spot, int Towers)> GuardedSpots(List<Actor> owned, CPos center)
		{
			var spots = new List<CPos> { center };

			var bush = world.ActorsHavingTrait<BerryBush>().Where(b => b.IsInWorld && !b.Trait<BerryBush>().IsEmpty)
				.MinByOrDefault(b => (b.Location - center).LengthSquared);
			if (bush != null && (bush.Location - center).LengthSquared <= Info.MaxBaseRadius * Info.MaxBaseRadius * 4)
				spots.Add(bush.Location);

			var enemy = world.ActorsHavingTrait<Building>()
				.Where(a => !a.IsDead && a.IsInWorld && !a.Owner.NonCombatant && player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy)
				.MinByOrDefault(a => (a.Location - center).LengthSquared);
			var bounds = world.Map.Bounds;
			var toward = enemy?.Location ?? new CPos(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
			var offset = toward - center;
			var length = offset.Length;
			if (length > 0)
			{
				var edge = Info.MaxBaseRadius * 2 / 3;
				spots.Add(center + new CVec(offset.X * edge / length, offset.Y * edge / length));
			}

			var defences = owned.Where(a => PwBotUtils.IsDefense(a.Info)).Select(a => a.Location).ToList();
			return spots.ConvertAll(s => (s, defences.Count(d => (d - s).LengthSquared <= Info.GuardRadius * Info.GuardRadius)));
		}

		int Share(ActorInfo info) =>
			Info.BuildingShares.TryGetValue(info.Name, out var share) ? share
			: PwBotUtils.IsDefense(info) ? Info.DefenseShare : Info.DefaultShare;

		int Limit(ActorInfo info) =>
			Info.BuildingLimits.TryGetValue(info.Name, out var limit) ? limit
			: PwBotUtils.IsDefense(info) ? Info.DefenseLimit : 1;

		// The main building (drop-off), else any building, else the workers.
		static CPos BaseCenter(List<Actor> owned, List<Actor> workers)
		{
			var main = owned.FirstOrDefault(a => a.IsInWorld && PwBotUtils.IsDropOff(a.Info))
				?? owned.FirstOrDefault(a => a.IsInWorld && PwBotUtils.IsBuilding(a.Info));
			if (main == null)
				return workers[0].Location;

			var footprint = main.Info.TraitInfo<BuildingInfo>().Dimensions;
			return main.Location + new CVec(footprint.X / 2, footprint.Y / 2);
		}

		CPos? FindSite(Actor worker, ActorInfo info, CPos center, int minRadius, int maxRadius)
		{
			var buildingInfo = info.TraitInfo<BuildingInfo>();
			var valid = new List<CPos>();
			foreach (var cell in world.Map.FindTilesInAnnulus(center, minRadius, maxRadius))
			{
				if (!Builder.CanPlace(world, worker, info, buildingInfo, cell) || !Clear(buildingInfo, cell))
					continue;

				valid.Add(cell);
				if (valid.Count >= Info.SiteChoices)
					break;
			}

			return valid.Count == 0 ? null : valid.Random(world.LocalRandom);
		}

		// Spacing free cells around the footprint: no other building with health, and no berry bush (workers gather from
		// every side of it); trees and other map objects may stand next to it.
		bool Clear(BuildingInfo info, CPos topLeft)
		{
			var s = Info.Spacing;
			foreach (var c in info.Tiles(topLeft))
			{
				for (var dx = -s; dx <= s; dx++)
				{
					for (var dy = -s; dy <= s; dy++)
					{
						var n = c + new CVec(dx, dy);
						if (world.Map.Contains(n) && world.ActorMap.GetActorsAt(n).Any(a =>
							a.TraitOrDefault<Building>() != null && (a.Info.HasTraitInfo<HealthInfo>() || a.Info.HasTraitInfo<BerryBushInfo>())))
							return false;
					}
				}
			}

			return true;
		}
	}
}
