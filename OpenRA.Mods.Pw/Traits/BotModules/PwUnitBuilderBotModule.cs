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
using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("AI training and research for buildings that each train their own list (PwProductionQueue). Every idle queue",
		"of the bot's finished buildings gets one item at a time: workers until MinimumWorkers, research when the bot",
		"is rich, otherwise an army unit picked at random by weight. Nothing is named: workers, army units and research",
		"are told apart by their traits, so every faction is played the same way. Units past the population limit are",
		"not started (PwBaseBuilderBotModule builds houses for more).")]
	public class PwUnitBuilderBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between checks of the queues.")]
		public readonly int ScanInterval = PwTime.Ticks(30);

		[Desc("Workers trained before anything else.")]
		public readonly int MinimumWorkers = 6;

		[Desc("Workers kept at most: between MinimumWorkers and this, they take WorkerWeight in the army mix.")]
		public readonly int MaximumWorkers = 12;

		[Desc("Weight of a worker in the mix once MinimumWorkers is reached.")]
		public readonly int WorkerWeight = 15;

		[Desc("Weight of an army unit not listed in UnitWeights.")]
		public readonly int DefaultUnitWeight = 10;

		[Desc("Weights of units in the mix by type (0 never trains it). Units not listed use DefaultUnitWeight.")]
		public readonly Dictionary<string, int> UnitWeights = [];

		[Desc("Most units of a type the bot keeps.")]
		public readonly Dictionary<string, int> UnitLimits = [];

		[Desc("Cash kept back after paying for an army unit (workers and research may use it).")]
		public readonly int CashReserve = 0;

		[Desc("Army units the bot has before it holds back cash for the next building (PwBaseBuilderBotModule.SavingFor)",
			"from army units and research.")]
		public readonly int ArmyBeforeSaving = 6;

		[Desc("Research starts only with at least this much cash, the cheapest available first.")]
		public readonly int ResearchCash = 1500;

		[Desc("Research that comes before army units once the bot has PriorityResearchArmy of them, at any cash: each",
			"entry names an upgrade without its level (every level, in turn), earlier entries first.")]
		public readonly string[] PriorityResearch = Array.Empty<string>();

		[Desc("Army units the bot has before PriorityResearch.")]
		public readonly int PriorityResearchArmy = 4;

		[Desc("Train transports (units that carry others): off by default, the bot doesn't load them yet.")]
		public readonly bool TrainTransports = false;

		public override object Create(ActorInitializer init) { return new PwUnitBuilderBotModule(init.Self, this); }
	}

	public class PwUnitBuilderBotModule : ConditionalTrait<PwUnitBuilderBotModuleInfo>, IBotTick
	{
		readonly World world;
		readonly Player player;
		int ticks;

		public PwUnitBuilderBotModule(Actor self, PwUnitBuilderBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (++ticks % Info.ScanInterval != 0)
				return;

			var queues = world.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == player && !q.Actor.IsDead && q.Actor.IsInWorld && q.Trait.Enabled
					&& PwBotUtils.IsComplete(q.Actor) && !q.Trait.AllQueued().Any())
				.ToList();
			if (queues.Count == 0)
				return;

			var counts = PwBotUtils.Owned(world, player).GroupBy(a => a.Info.Name).ToDictionary(g => g.Key, g => g.Count());
			int Count(string type) => counts.TryGetValue(type, out var n) ? n : 0;
			var workers = PwBotUtils.Owned(world, player).Count(a => PwBotUtils.IsWorker(a.Info));

			// Workers first, from any queue that has them, one at a time.
			var workerQueue = queues.FirstOrDefault(q => q.Trait.BuildableItems().Any(i => PwBotUtils.IsWorker(i)));
			if (workers < Info.MinimumWorkers && workerQueue.Trait != null)
			{
				var worker = workerQueue.Trait.BuildableItems().First(i => PwBotUtils.IsWorker(i));
				if (Affordable(worker, 0))
					Start(bot, workerQueue.Actor, worker);

				return;
			}

			foreach (var queue in queues)
			{
				var item = Choose(queue.Trait.BuildableItems().ToList(), workers, Count);
				if (item == null)
					continue;

				Start(bot, queue.Actor, item);

				// The cash just spent isn't taken yet: one new item per check.
				return;
			}
		}

		ActorInfo Choose(List<ActorInfo> items, int workers, Func<string, int> count)
		{
			var cash = PwBotUtils.Cash(player);

			// Once there is an army to defend with, the next building comes before more units and research.
			var army = PwBotUtils.Owned(world, player).Count(a => PwBotUtils.IsArmy(a.Info));
			var saving = army < Info.ArmyBeforeSaving ? 0 : player.PlayerActor.TraitsImplementing<PwBaseBuilderBotModule>()
				.Where(m => !m.IsTraitDisabled).Sum(m => m.SavingFor);

			// The research that matters most, once there is an army to protect the base meanwhile.
			if (army >= Info.PriorityResearchArmy)
			{
				foreach (var name in Info.PriorityResearch)
				{
					var research = items.FirstOrDefault(i => PwBotUtils.IsResearch(i)
						&& (i.Name == name || i.Name.StartsWith(name + "-", StringComparison.Ordinal)));
					if (research != null && Affordable(research, saving))
						return research;
				}
			}

			// Research when rich: the cheapest first (the next level of each upgrade follows).
			if (cash >= Info.ResearchCash + saving)
			{
				var research = items.Where(PwBotUtils.IsResearch).OrderBy(PwBotUtils.Cost).FirstOrDefault();
				if (research != null && Affordable(research, saving))
					return research;
			}

			var choices = new List<(ActorInfo Item, int Weight)>();
			foreach (var item in items)
			{
				// Items lie on the ground until a unit picks them up: the bot doesn't buy them yet.
				if (PwBotUtils.IsResearch(item) || PwBotUtils.IsItem(item))
					continue;

				if (Info.UnitLimits.TryGetValue(item.Name, out var limit) && count(item.Name) >= limit)
					continue;

				int weight;
				if (PwBotUtils.IsWorker(item))
					weight = workers < Info.MaximumWorkers ? Info.WorkerWeight : 0;
				else if (PwBotUtils.IsArmy(item))
					weight = Info.UnitWeights.TryGetValue(item.Name, out var w) ? w : Info.DefaultUnitWeight;

				// Unarmed casters (Unicorn, ManaSpread): only those given a weight (PwSpellBotModule casts their spells).
				else if (item.HasTraitInfo<PwSpellInfo>())
					weight = Info.UnitWeights.TryGetValue(item.Name, out var w) ? w : 0;
				else if (item.HasTraitInfo<CargoInfo>())
					weight = Info.TrainTransports ? (Info.UnitWeights.TryGetValue(item.Name, out var w) ? w : Info.DefaultUnitWeight) : 0;
				else
					weight = 0;

				if (weight > 0 && Affordable(item, PwBotUtils.IsWorker(item) ? 0 : Info.CashReserve + saving))
					choices.Add((item, weight));
			}

			if (choices.Count == 0)
				return null;

			var roll = world.LocalRandom.Next(choices.Sum(c => c.Weight));
			foreach (var (item, weight) in choices)
			{
				if (roll < weight)
					return item;

				roll -= weight;
			}

			return null;
		}

		// Enough cash (above the reserve) and room in the population.
		bool Affordable(ActorInfo item, int reserve) =>
			PwBotUtils.Cash(player) >= PwBotUtils.Cost(item) + reserve && PwPopulation.HasRoomFor(player, item);

		void Start(IBot bot, Actor producer, ActorInfo item)
		{
			AIUtils.BotDebug("{0}: training {1} at {2}.", player, item.Name, producer.Info.Name);
			bot.QueueOrder(Order.StartProduction(producer, item.Name, 1));
		}
	}
}
