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
	[TraitLocation(SystemActors.Player)]
	[Desc("The campaign's computer players, as simple as their original scripts (JW2_16, docs/formats/campaign.md): they",
		"train the units their script names (SET_UNIT_MAKE) and, once enough of them wait (SET_ATTACK_COUNTER), send them",
		"to attack together. The units the mission placed stay where they are and fight what comes to them. Each mission",
		"sets Train and AttackSize for its players in its map rules (AssetInstaller/Campaign.cs).")]
	public class PwCampaignBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between checks.")]
		public readonly int ScanInterval = 30;

		[Desc("By player (internal name): the units to train and how many of each to have waiting for the next attack.")]
		public readonly Dictionary<string, Dictionary<string, int>> Train = new();

		[Desc("By player (internal name): units waiting before they attack together.")]
		public readonly Dictionary<string, int> AttackSize = new();

		[Desc("Units waiting before they attack, for a player AttackSize does not list.")]
		public readonly int DefaultAttackSize = 10;

		public override object Create(ActorInitializer init) { return new PwCampaignBotModule(init.Self, this); }
	}

	public class PwCampaignBotModule : ConditionalTrait<PwCampaignBotModuleInfo>, IBotTick
	{
		readonly World world;
		readonly Player player;
		readonly HashSet<Actor> attacking = new();
		HashSet<Actor> placed;
		int ticks;

		public PwCampaignBotModule(Actor self, PwCampaignBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		void IBotTick.BotTick(IBot bot)
		{
			// What the player owns when the game starts is the mission's placed army: it is never sent.
			placed ??= PwBotUtils.Owned(world, player).ToHashSet();
			if (++ticks % Info.ScanInterval != 0)
				return;

			attacking.RemoveWhere(a => a.IsDead || !a.IsInWorld);
			var waiting = PwBotUtils.Owned(world, player)
				.Where(a => a.IsInWorld && PwBotUtils.IsArmy(a.Info) && !placed.Contains(a) && !attacking.Contains(a))
				.ToList();

			var full = !Train(bot, waiting);

			// Attackers that have run out of orders go for the nearest enemy again.
			var idle = attacking.Where(a => a.IsIdle).ToList();
			if (idle.Count > 0)
				AttackMove(bot, idle);

			// A full group attacks; so does a smaller one when the population is full (this AI builds no houses), as more
			// can only be trained once some have died.
			var size = Info.AttackSize.TryGetValue(player.InternalName, out var s) ? s : Info.DefaultAttackSize;
			if (waiting.Count >= size || (full && waiting.Count > 0))
			{
				AIUtils.BotDebug("{0}: campaign attack with {1} units.", player, waiting.Count);
				attacking.UnionWith(waiting);
				AttackMove(bot, waiting);
			}
		}

		// One unit per check from an idle queue, the type furthest below its count first. False when a unit is due but
		// the population has no room for any of them.
		bool Train(IBot bot, List<Actor> waiting)
		{
			if (!Info.Train.TryGetValue(player.InternalName, out var train))
				return true;

			var queues = world.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == player && !q.Actor.IsDead && q.Actor.IsInWorld && q.Trait.Enabled
					&& PwBotUtils.IsComplete(q.Actor))
				.ToList();

			var due = false;
			foreach (var (type, count) in train.OrderBy(t => waiting.Count(a => a.Info.Name == t.Key) - t.Value))
			{
				if (waiting.Count(a => a.Info.Name == type) >= count)
					continue;

				foreach (var queue in queues)
				{
					var item = queue.Trait.BuildableItems().FirstOrDefault(i => i.Name == type);
					if (item == null)
						continue;

					due = true;
					if (!PwPopulation.HasRoomFor(player, item))
						continue;

					if (!queue.Trait.AllQueued().Any() && PwBotUtils.Cash(player) >= PwBotUtils.Cost(item))
						bot.QueueOrder(Order.StartProduction(queue.Actor, type, 1));

					return true;
				}
			}

			return !due;
		}

		// To the enemy building (or else unit) nearest the group, wild creatures left out.
		void AttackMove(IBot bot, List<Actor> group)
		{
			var from = group[0].CenterPosition;
			var enemies = world.Actors.Where(a => !a.IsDead && a.IsInWorld && !a.Owner.NonCombatant
				&& player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy && a.Info.HasTraitInfo<HealthInfo>()).ToList();
			var target = enemies.OrderBy(a => PwBotUtils.IsBuilding(a.Info) ? 0 : 1)
				.ThenBy(a => (a.CenterPosition - from).LengthSquared).FirstOrDefault();
			if (target == null)
				return;

			foreach (var unit in group)
				bot.QueueOrder(new Order("AttackMove", unit, Target.FromCell(world, target.Location), false));
		}
	}
}
