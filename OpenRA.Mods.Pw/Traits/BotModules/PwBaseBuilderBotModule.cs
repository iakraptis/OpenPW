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

		[Desc("Defences built by the berry bushes nearest the base, each type until this many of it stand within",
			"GuardRadius of them (the Primitive Trap). They come after the guard spots' towers and don't count as one.")]
		public readonly Dictionary<string, int> BushDefenses = new();

		[Desc("Ticks after which the bot builds a second main building (drop-off) by berry bushes no drop-off of its own",
			"serves, the nearest group of them; 0: never. Its main building becomes a guard spot too.")]
		public readonly int ExpansionDelay = 0;

		[Desc("Bushes at least this many cells from every drop-off of the bot, and at most ExpansionMaxDistance from its",
			"main building, may get the second base.")]
		public readonly int ExpansionMinDistance = 16;
		public readonly int ExpansionMaxDistance = 50;

		[Desc("Bushes with an enemy building within this many cells are left alone.")]
		public readonly int ExpansionEnemyRadius = 15;

		[Desc("Bushes within this many cells of each other make one group (more bushes: a better site).")]
		public readonly int ExpansionClusterRadius = 6;

		public override object Create(ActorInitializer init) { return new PwBaseBuilderBotModule(init.Self, this); }
	}

	public class PwBaseBuilderBotModule : ConditionalTrait<PwBaseBuilderBotModuleInfo>, IBotTick
	{
		readonly World world;
		readonly Player player;
		TechTree techTree;
		IPathFinder pathFinder;
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
			pathFinder = world.WorldActor.Trait<IPathFinder>();
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
			foreach (var (type, spot, minRadius, maxRadius) in Choose(owned, free))
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
					: spot != null ? FindSite(worker, info, spot.Value, minRadius, maxRadius)
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
		// share; only types a free worker can build now (prerequisites met, under the limit). A type that goes to a
		// particular place (bush defences, the second base) comes with it and the search radii around it; the others
		// with none (around the base, or the guard spots for defences).
		IEnumerable<(string Type, CPos? Spot, int MinRadius, int MaxRadius)> Choose(List<Actor> owned, List<Actor> free)
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
					yield return (house.Name, null, 0, 0);
			}

			// Economy first: nothing else until the workforce is there.
			if (owned.Count(a => PwBotUtils.IsWorker(a.Info)) < Info.MinimumWorkers)
				yield break;

			// A tower for every guard spot without one, once a production building stands (the first thing to defend).
			var center = BaseCenter(owned, free);
			var tower = buildable.Where(PwBotUtils.IsDefense).Where(i => !Info.BushDefenses.ContainsKey(i.Name))
				.OrderByDescending(Reach).ThenBy(PwBotUtils.Cost).FirstOrDefault();
			var producing = owned.Any(a => PwBotUtils.IsComplete(a) && a.Info.HasTraitInfo<ProductionQueueInfo>() && !PwBotUtils.IsDropOff(a.Info));
			if (Info.GuardSpots && tower != null && producing && GuardedSpots(owned, center).Any(g => g.Towers == 0))
				yield return (tower.Name, null, 0, 0);

			// The bush defences, by the berry bushes nearest the base.
			var bush = PwBotUtils.NearestBush(world, center, Info.MaxBaseRadius * 2);
			if (producing && bush != null)
			{
				foreach (var (type, wanted) in Info.BushDefenses)
				{
					var near = owned.Count(a => a.Info.Name == type && (a.Location - bush.Value).LengthSquared <= Info.GuardRadius * Info.GuardRadius);
					if (near < wanted && buildable.Any(i => i.Name == type))
						yield return (type, bush, 1, Info.GuardRadius);
				}
			}

			// The second base: a main building by the nearest berries no drop-off serves, once its time has come.
			if (Info.ExpansionDelay > 0 && ticks >= Info.ExpansionDelay && producing
				&& owned.Count(a => PwBotUtils.IsDropOff(a.Info)) < 2)
			{
				var main = buildable.Where(PwBotUtils.IsDropOff).MinByOrDefault(PwBotUtils.Cost);
				var site = main == null ? null : ExpansionSpot(owned, free, center);
				if (site != null)
					yield return (main.Name, site, 1, Info.ExpansionClusterRadius + 2);
			}

			// The type whose (count + 1) / share is smallest is furthest below its share.
			var byShare = buildable
				.Where(i => !PwBotUtils.IsHouse(i))
				.Select(i => (Info: i, Share: Share(i), Limit: Limit(i)))
				.Where(t => t.Share > 0 && Count(t.Info.Name) < t.Limit)
				.OrderBy(t => (Count(t.Info.Name) + 1) * 1000 / t.Share)
				.ThenByDescending(t => t.Share);

			foreach (var t in byShare)
				yield return (t.Info.Name, null, 0, 0);
		}

		// The middle of the group of berry bushes for the second base: bushes with berries no drop-off of the bot serves
		// (ExpansionMinDistance), not too far from the base, without an enemy building near, that a worker can walk to.
		// Each bush counts with the bushes around it: the nearest, biggest group wins.
		CPos? ExpansionSpot(List<Actor> owned, List<Actor> free, CPos center)
		{
			var dropOffs = owned.Where(a => PwBotUtils.IsDropOff(a.Info)).Select(a => a.Location).ToList();
			var enemies = world.ActorsHavingTrait<Building>()
				.Where(a => !a.IsDead && a.IsInWorld && !a.Owner.NonCombatant && player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy)
				.Select(a => a.Location).ToList();
			var locomotor = free.Select(w => w.TraitOrDefault<Mobile>()?.Locomotor).FirstOrDefault(l => l != null);
			var bushes = world.ActorsHavingTrait<BerryBush>().Where(b => b.IsInWorld && !b.Trait<BerryBush>().IsEmpty)
				.Select(b => b.Location).ToList();

			static int Square(int cells) => cells * cells;
			var candidates = bushes.Where(b =>
				dropOffs.All(d => (b - d).LengthSquared >= Square(Info.ExpansionMinDistance))
				&& (b - center).LengthSquared <= Square(Info.ExpansionMaxDistance)
				&& !enemies.Any(e => (b - e).LengthSquared <= Square(Info.ExpansionEnemyRadius))).ToList();

			var (_, group) = candidates
				.Select(b => (Bush: b, Group: candidates.Where(o => (o - b).LengthSquared <= Square(Info.ExpansionClusterRadius)).ToList()))
				.OrderBy(g => (g.Bush - center).Length - g.Group.Count * Info.ExpansionClusterRadius)
				.FirstOrDefault(g => locomotor == null || pathFinder.PathExistsForLocomotor(locomotor, center, g.Bush));
			if (group == null)
				return null;

			return new CPos(group.Sum(c => c.X) / group.Count, group.Sum(c => c.Y) / group.Count);
		}

		// How far a defence strikes: its longest weapon.
		int Reach(ActorInfo info) => info.TraitInfos<ArmamentInfo>()
			.Select(a => world.Map.Rules.Weapons.TryGetValue(a.Weapon.ToLowerInvariant(), out var w) ? w.Range.Length : 0)
			.DefaultIfEmpty(0).Max();

		// The spots towers guard, each with the number of the bot's defences (finished or not) within GuardRadius: the
		// main building, the berry bush nearest the base centre, and the base's edge toward the nearest enemy building
		// (or the map centre while none is known); with ExpansionDelay, also every other main building of the bot (the
		// second base). Bush defences don't count.
		List<(CPos Spot, int Towers)> GuardedSpots(List<Actor> owned, CPos center)
		{
			var spots = new List<CPos> { center };

			var bush = PwBotUtils.NearestBush(world, center, Info.MaxBaseRadius * 2);
			if (bush != null)
				spots.Add(bush.Value);

			var edge = PwBotUtils.EdgeToward(world, player, center, Info.MaxBaseRadius * 2 / 3);
			if (edge != null)
				spots.Add(edge.Value);

			if (Info.ExpansionDelay > 0)
			{
				var main = owned.FirstOrDefault(a => a.IsInWorld && PwBotUtils.IsDropOff(a.Info));
				spots.AddRange(owned.Where(a => a != main && a.IsInWorld && PwBotUtils.IsDropOff(a.Info) && PwBotUtils.IsComplete(a))
					.Select(a => a.Location + new CVec(a.Info.TraitInfo<BuildingInfo>().Dimensions.X / 2, a.Info.TraitInfo<BuildingInfo>().Dimensions.Y / 2)));
			}

			var defences = owned.Where(a => PwBotUtils.IsDefense(a.Info) && !Info.BushDefenses.ContainsKey(a.Info.Name)).Select(a => a.Location).ToList();
			return spots.ConvertAll(s => (s, defences.Count(d => (d - s).LengthSquared <= Info.GuardRadius * Info.GuardRadius)));
		}

		int Share(ActorInfo info) =>
			Info.BuildingShares.TryGetValue(info.Name, out var share) ? share
			: PwBotUtils.IsDefense(info) ? Info.DefenseShare : Info.DefaultShare;

		int Limit(ActorInfo info) =>
			Info.BuildingLimits.TryGetValue(info.Name, out var limit) ? limit
			: PwBotUtils.IsDefense(info) ? Info.DefenseLimit : 1;

		// The main building (drop-off), else any building, else the workers.
		static CPos BaseCenter(List<Actor> owned, List<Actor> workers) => PwBotUtils.BaseCenter(owned) ?? workers[0].Location;

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
