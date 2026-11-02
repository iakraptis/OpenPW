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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("AI berry gathering: sends idle workers (BerryGatherer) to the bush with berries nearest a drop-off, a few per ",
		"bush. The gather order loops on its own (gather, deliver, back), so only idle workers need an order: new ones, ",
		"and builders back from a construction site.")]
	public class PwGathererBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between checks for idle workers.")]
		public readonly int ScanInterval = 40;

		[Desc("Workers sent to one bush before the next bush is preferred (each one more counts as this many cells further).")]
		public readonly int CrowdPenalty = 6;

		[Desc("Bushes further than this from every drop-off are left alone, in cells.")]
		public readonly int MaxBushDistance = 30;

		[Desc("Bushes with a hostile wild creature (one that attacks) within this many cells are left alone.")]
		public readonly int DangerRadius = 5;

		public override object Create(ActorInitializer init) { return new PwGathererBotModule(init.Self, this); }
	}

	public class PwGathererBotModule : ConditionalTrait<PwGathererBotModuleInfo>, IBotTick
	{
		readonly World world;
		readonly Player player;

		// The bush each worker was last sent to, to spread the next ones.
		readonly Dictionary<Actor, Actor> sentTo = new();
		int ticks;

		public PwGathererBotModule(Actor self, PwGathererBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (++ticks % Info.ScanInterval != 0)
				return;

			foreach (var gone in sentTo.Keys.Where(w => w.IsDead || w.Disposed).ToList())
				sentTo.Remove(gone);

			var idle = PwBotUtils.Owned(world, player)
				.Where(a => a.IsInWorld && a.IsIdle && PwBotUtils.IsWorker(a.Info))
				.ToList();
			if (idle.Count == 0)
				return;

			var dropOffs = PwBotUtils.Owned(world, player)
				.Where(a => a.IsInWorld && PwBotUtils.IsDropOff(a.Info) && PwBotUtils.IsComplete(a))
				.Select(a => a.CenterPosition).ToList();
			if (dropOffs.Count == 0)
				return;

			// Where hostile wild creatures stand (PwDefenseBotModule clears those near the base).
			var danger = WDist.FromCells(Info.DangerRadius).Length;
			var predators = world.ActorsHavingTrait<AttackBase>()
				.Where(a => !a.IsDead && a.IsInWorld && a.Owner.NonCombatant && player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy)
				.Select(a => a.CenterPosition).ToList();

			var maxDistance = WDist.FromCells(Info.MaxBushDistance).Length;
			var bushes = world.ActorsHavingTrait<BerryBush>()
				.Where(b => b.IsInWorld && !b.Trait<BerryBush>().IsEmpty && !predators.Any(p => (p - b.CenterPosition).Length <= danger))
				.Select(b => (Bush: b, Distance: dropOffs.Min(d => (b.CenterPosition - d).Length)))
				.Where(b => b.Distance <= maxDistance)
				.ToList();
			if (bushes.Count == 0)
				return;

			var crowd = WDist.FromCells(Info.CrowdPenalty).Length;
			foreach (var worker in idle)
			{
				var bush = bushes
					.OrderBy(b => b.Distance + crowd * sentTo.Values.Count(s => s == b.Bush))
					.First().Bush;

				sentTo[worker] = bush;
				bot.QueueOrder(new Order(BerryGatherer.OrderID, worker, Target.FromActor(bush), false));
			}
		}
	}
}
