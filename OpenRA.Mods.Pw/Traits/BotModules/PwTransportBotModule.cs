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
using OpenRA.Mods.Common.Pathfinder;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("AI air transport (the Revolver), after OpenSYW's TransportBotModule: one trip at a time, it gathers idle",
		"soldiers beside the base, flies them to open ground near an enemy building and sends them to attack it. The",
		"target is an enemy building they can't walk to if there is one, otherwise (Raids) the least defended one.",
		"Transports are aircraft with Cargo, passengers armed units with a fitting Passenger type: no type names. It",
		"orders a transport when it has none and a trip is wanted. Every step has a timeout, so a lost transport or a",
		"blocked landing never stalls it.")]
	public class PwTransportBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Fewest idle soldiers worth a trip.")]
		public readonly int MinimumPassengers = 4;

		[Desc("Raid buildings the army can walk to as well (not only unreachable ones).")]
		public readonly bool Raids = true;

		[Desc("Army units the bot has before it orders a transport for raids.")]
		public readonly int RaidArmy = 10;

		[Desc("Ticks between plans while no trip is running.")]
		public readonly int ScanInterval = PwTime.Ticks(100);

		[Desc("Ticks a step (flying to the pickup, loading, travelling, unloading) may take before the trip is dropped.")]
		public readonly int StepTimeout = PwTime.Ticks(1500);

		[Desc("Ticks to wait after a trip (or a dropped one) before planning the next.")]
		public readonly int Cooldown = PwTime.Ticks(750);

		[Desc("How far from the base soldiers are picked up, and from the target to look for a landing, in cells.")]
		public readonly int BaseRadius = 16;
		public readonly int SearchRadius = 12;

		[Desc("Landings with the fewest armed enemies (towers, units) within this many cells are chosen.")]
		public readonly int DangerRadius = 8;

		public override object Create(ActorInitializer init) { return new PwTransportBotModule(init.Self, this); }
	}

	public class PwTransportBotModule : ConditionalTrait<PwTransportBotModuleInfo>, IBotTick
	{
		enum Phase { Idle, ToPickup, Loading, Travelling, Unloading }

		// Ticks between progress checks of a running trip.
		const int StepInterval = 25 * PwTime.TicksPerOriginal;

		// Orders are repeated this often while a step waits (the squad manager may have sent a passenger elsewhere).
		const int ReorderInterval = 150 * PwTime.TicksPerOriginal;

		readonly World world;
		readonly Player player;
		IPathFinder pathFinder;

		Phase phase;
		int ticks, stepStarted, nextPlan, lastOrders;
		Actor transport, target;
		CPos pickup, drop;
		List<Actor> passengers = [];

		public PwTransportBotModule(Actor self, PwTransportBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		protected override void Created(Actor self)
		{
			pathFinder = world.WorldActor.Trait<IPathFinder>();
			base.Created(self);
		}

		static bool IsTransport(ActorInfo info) => info.HasTraitInfo<CargoInfo>() && info.HasTraitInfo<AircraftInfo>();

		bool Alive(Actor a) => a != null && !a.IsDead && !a.Disposed && a.Owner == player;

		// The transport of a running trip (PwDetectorBotModule doesn't take it as a detector).
		public static bool Uses(Player player, Actor actor) =>
			player.PlayerActor.TraitsImplementing<PwTransportBotModule>().Any(m => !m.IsTraitDisabled && m.transport == actor);

		void IBotTick.BotTick(IBot bot)
		{
			ticks++;
			if (phase == Phase.Idle)
			{
				if (ticks >= nextPlan && ticks % Info.ScanInterval == 0)
					Plan(bot);

				return;
			}

			if (ticks % StepInterval == 0)
				Step(bot);
		}

		void Plan(IBot bot)
		{
			var owned = PwBotUtils.Owned(world, player).Where(a => a.IsInWorld).ToList();
			var main = owned.FirstOrDefault(a => PwBotUtils.IsDropOff(a.Info)) ?? owned.FirstOrDefault(a => PwBotUtils.IsBuilding(a.Info));
			if (main == null)
				return;

			var home = main.Location;
			var soldiers = owned.Where(a => a.IsIdle && PwBotUtils.IsArmy(a.Info) && a.Info.HasTraitInfo<PassengerInfo>()
					&& a.Info.HasTraitInfo<MobileInfo>() && (a.Location - home).LengthSquared <= Info.BaseRadius * Info.BaseRadius)
				.ToList();
			var locomotor = soldiers.Select(s => s.Trait<Mobile>().Locomotor).FirstOrDefault();

			var enemies = world.ActorsHavingTrait<Building>()
				.Where(a => !a.IsDead && a.IsInWorld && !a.Owner.NonCombatant && a.Info.HasTraitInfo<HealthInfo>()
					&& player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy)
				.ToList();
			if (enemies.Count == 0 || locomotor == null)
				return;

			// Out of walking reach first; otherwise a raid on the least defended building.
			var armed = ArmedEnemies();
			var unreachable = enemies.Where(e => Landing(e, locomotor, armed) is CPos l && !pathFinder.PathExistsForLocomotor(locomotor, home, l))
				.OrderBy(e => (e.Location - home).LengthSquared).FirstOrDefault();
			var raid = Info.Raids ? enemies.OrderBy(e => Danger(armed, e.Location)).ThenBy(e => (e.Location - home).LengthSquared).FirstOrDefault() : null;
			target = unreachable ?? raid;
			if (target == null)
				return;

			// Not the detectors PwDetectorBotModule keeps (posted Revolvers).
			var transports = owned.Where(a => IsTransport(a.Info) && a.Trait<Cargo>().IsEmpty() && !PwDetectorBotModule.Holds(player, a)).ToList();
			if (transports.Count == 0)
			{
				var army = owned.Count(a => PwBotUtils.IsArmy(a.Info));
				if (unreachable != null || army >= Info.RaidArmy)
					OrderTransport(bot);

				target = null;
				return;
			}

			var landing = Landing(target, locomotor, armed);
			var start = OpenGround(home, locomotor, 3, Info.BaseRadius).Cast<CPos?>().FirstOrDefault();
			if (landing == null || start == null || soldiers.Count < Info.MinimumPassengers)
			{
				target = null;
				return;
			}

			pickup = start.Value;
			drop = landing.Value;
			Start(bot, transports.MinBy(t => (t.Location - pickup).LengthSquared), soldiers);
		}

		// The safest open ground near the building (fewest armed enemies around), nearest of equally safe ones.
		CPos? Landing(Actor building, Locomotor locomotor, List<CPos> armed) =>
			OpenGround(building.Location, locomotor, 3, Info.SearchRadius).Take(20)
				.OrderBy(c => Danger(armed, c)).Cast<CPos?>().FirstOrDefault();

		// Walkable cells without a building, minRadius to maxRadius cells from center, nearest first.
		IEnumerable<CPos> OpenGround(CPos center, Locomotor locomotor, int minRadius, int maxRadius)
		{
			foreach (var cell in world.Map.FindTilesInAnnulus(center, minRadius, maxRadius))
				if (locomotor.MovementCostForCell(cell) != PathGraph.MovementCostForUnreachableCell
					&& !world.ActorMap.GetActorsAt(cell).Any(a => a.TraitOrDefault<Building>() != null))
					yield return cell;
		}

		// Where the enemy's towers and armed units stand (not wild creatures).
		List<CPos> ArmedEnemies() =>
			world.ActorsHavingTrait<Armament>().Where(a => !a.IsDead && a.IsInWorld && !a.Owner.NonCombatant
				&& player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy).Select(a => a.Location).ToList();

		int Danger(List<CPos> armed, CPos cell) =>
			armed.Count(a => (a - cell).LengthSquared <= Info.DangerRadius * Info.DangerRadius);

		// One transport from any queue that has one, unless one is being made.
		void OrderTransport(IBot bot)
		{
			var queues = world.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == player && !q.Actor.IsDead && q.Trait.Enabled && PwBotUtils.IsComplete(q.Actor)).ToList();
			if (queues.Any(q => q.Trait.AllQueued().Any(i => IsTransport(world.Map.Rules.Actors[i.Item]))))
				return;

			foreach (var q in queues)
			{
				var aircraft = q.Trait.BuildableItems().FirstOrDefault(IsTransport);
				if (aircraft != null && PwBotUtils.Cash(player) >= PwBotUtils.Cost(aircraft) && PwPopulation.HasRoomFor(player, aircraft))
				{
					AIUtils.BotDebug("{0}: ordering transport {1}.", player, aircraft.Name);
					bot.QueueOrder(Order.StartProduction(q.Actor, aircraft.Name, 1));
					return;
				}
			}
		}

		void Start(IBot bot, Actor t, List<Actor> soldiers)
		{
			transport = t;
			var cargo = t.Info.TraitInfo<CargoInfo>();
			var room = t.TraitOrDefault<PwTransportCapacity>()?.Capacity(t) ?? cargo.MaxWeight;
			passengers = [];
			foreach (var s in soldiers.OrderBy(s => (s.Location - pickup).LengthSquared))
			{
				var p = s.Info.TraitInfo<PassengerInfo>();
				if (!cargo.Types.Contains(p.CargoType) || p.Weight > room)
					continue;

				passengers.Add(s);
				room -= p.Weight;
			}

			if (passengers.Count < Info.MinimumPassengers)
			{
				target = null;
				return;
			}

			AIUtils.BotDebug("{0}: {1} carries {2} soldiers from {3} to {4} for {5}.", player, t.Info.Name, passengers.Count,
				pickup, drop, target.Info.Name);
			bot.QueueOrder(new Order("Move", transport, Target.FromCell(world, pickup), false));
			Enter(Phase.ToPickup);
		}

		void Enter(Phase next)
		{
			phase = next;
			stepStarted = ticks;
			lastOrders = 0;
		}

		void Finish()
		{
			phase = Phase.Idle;
			nextPlan = ticks + Info.Cooldown;
			transport = null;
			target = null;
			passengers.Clear();
		}

		bool Near(CPos cell) => (transport.Location - cell).LengthSquared <= 4;

		bool Reorder()
		{
			if (ticks - lastOrders < ReorderInterval)
				return false;

			lastOrders = ticks;
			return true;
		}

		void Step(IBot bot)
		{
			if (!Alive(transport))
			{
				AIUtils.BotDebug("{0}: transport lost, trip dropped.", player);
				Finish();
				return;
			}

			var timedOut = ticks - stepStarted > Info.StepTimeout;
			var cargo = transport.Trait<Cargo>();
			passengers.RemoveAll(p => !Alive(p));

			switch (phase)
			{
				case Phase.ToPickup:
					if (Near(pickup))
						Enter(Phase.Loading);
					else if (timedOut || passengers.Count == 0)
						Release(bot);
					else if (transport.IsIdle && Reorder())
						bot.QueueOrder(new Order("Move", transport, Target.FromCell(world, pickup), false));

					return;

				case Phase.Loading:
				{
					// Everyone aboard (or, after the timeout, whoever made it): off to the target.
					var waiting = passengers.Where(p => p.IsInWorld).ToList();
					if (waiting.Count == 0 || timedOut)
					{
						if (cargo.IsEmpty())
						{
							Release(bot);
							return;
						}

						bot.QueueOrder(new Order("Move", transport, Target.FromCell(world, drop), false));
						Enter(Phase.Travelling);
						return;
					}

					if (Reorder())
						foreach (var p in waiting)
							bot.QueueOrder(new Order("EnterTransport", p, Target.FromActor(transport), false));

					return;
				}

				case Phase.Travelling:
					if (Near(drop) || timedOut)
					{
						bot.QueueOrder(new Order("Unload", transport, false));
						Enter(Phase.Unloading);
					}
					else if (transport.IsIdle && Reorder())
						bot.QueueOrder(new Order("Move", transport, Target.FromCell(world, drop), false));

					return;

				case Phase.Unloading:
					if (cargo.IsEmpty())
					{
						var attackAt = target != null && !target.IsDead ? target.Location : drop;
						foreach (var p in passengers.Where(p => p.IsInWorld))
							bot.QueueOrder(new Order("AttackMove", p, Target.FromCell(world, attackAt), false));

						bot.QueueOrder(new Order("Move", transport, Target.FromCell(world, pickup), false));
						AIUtils.BotDebug("{0}: landed {1} soldiers near {2}.", player, passengers.Count, attackAt);
						Finish();
					}
					else if (timedOut)
					{
						AIUtils.BotDebug("{0}: could not unload, trip dropped.", player);
						Finish();
					}
					else if (transport.IsIdle && Reorder())
						bot.QueueOrder(new Order("Unload", transport, false));

					return;
			}
		}

		// Ends the trip before departure: anyone already aboard gets off again at home.
		void Release(IBot bot)
		{
			if (!transport.Trait<Cargo>().IsEmpty())
				bot.QueueOrder(new Order("Unload", transport, false));

			AIUtils.BotDebug("{0}: trip dropped before departure.", player);
			Finish();
		}
	}
}
