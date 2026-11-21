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
using OpenRA.Mods.Common.Traits.BotModules.Squads;
using OpenRA.Mods.Pw.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("AI merging (PwMerge, Tyrano): the bot's units waiting at its base that make up a recipe of Into are merged,",
		"with the order the merge button issues, the closest ones together. Units already in an attack or defence",
		"squad are left alone (flyers of the air squad that aren't attacking are not). While the bot has an",
		"ingredient of a recipe whose research it lacks, it researches it.")]
	public class PwMergeBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("The merged types to make, each with the most the bot keeps at once (0: no limit).")]
		public readonly Dictionary<string, int> Into = [];

		[Desc("Ticks between checks.")]
		public readonly int ScanInterval = 40;

		[Desc("Only units within this many cells of the main building merge.")]
		public readonly int BaseRadius = 20;

		[Desc("The parts of one merge are at most this many cells apart.")]
		public readonly int GatherRadius = 10;

		public override object Create(ActorInitializer init) { return new PwMergeBotModule(init.Self, this); }
	}

	public class PwMergeBotModule : ConditionalTrait<PwMergeBotModuleInfo>, IBotTick
	{
		readonly World world;
		readonly Player player;
		int ticks;

		public PwMergeBotModule(Actor self, PwMergeBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (++ticks % Info.ScanInterval != 0)
				return;

			var owned = PwBotUtils.Owned(world, player).Where(a => a.IsInWorld).ToList();
			var main = owned.FirstOrDefault(a => PwBotUtils.IsDropOff(a.Info));
			if (main == null)
				return;

			// Units of a squad are on a mission, but for flyers: the squad manager puts them all in its air squad at
			// once, so those there that aren't attacking are free too (an aircraft is never idle: it hovers).
			var squads = bot.Player.PlayerActor.TraitsImplementing<SquadManagerBotModule>()
				.Where(m => !m.IsTraitDisabled).SelectMany(m => m.Squads).ToList();
			var busy = squads.Where(s => s.Type != SquadType.Air).SelectMany(s => s.Units).ToHashSet();
			var air = squads.Where(s => s.Type == SquadType.Air).SelectMany(s => s.Units).ToHashSet();
			var baseRadius = WDist.FromCells(Info.BaseRadius).Length;
			var free = owned.Where(a => a.Info.HasTraitInfo<PwMergeInfo>() && !busy.Contains(a) && !Merging(a)
				&& (!air.Contains(a) || !a.TraitsImplementing<AttackBase>().Any(t => t.IsAiming))
				&& (a.CenterPosition - main.CenterPosition).HorizontalLength <= baseRadius).ToList();

			foreach (var (into, limit) in Info.Into)
			{
				var recipe = world.Map.Rules.Actors[into].TraitInfo<PwMergedInfo>();
				var candidates = free.Where(a => a.Info.TraitInfo<PwMergeInfo>().Into == into).ToList();
				if (candidates.Count == 0)
					continue;

				if (!PwMerge.CanMerge(player, recipe))
				{
					foreach (var upgrade in recipe.Prerequisites.Where(p => world.Map.Rules.Actors.TryGetValue(p, out var u)
						&& PwBotUtils.IsResearch(u)))
						PwBotUtils.Research(world, player, bot, upgrade, "merging");

					continue;
				}

				for (var count = owned.Count(a => a.Info.Name == into); limit <= 0 || count < limit; count++)
				{
					var parts = Closest(candidates, into, recipe);
					if (parts == null)
						break;

					AIUtils.BotDebug("{0}: merging {1} into {2}.", player, string.Join(", ", parts.Select(p => p.Info.Name)), into);
					bot.QueueOrder(PwMerge.Merge(parts));
					candidates.RemoveAll(parts.Contains);
					free.RemoveAll(parts.Contains);
				}
			}
		}

		// The recipe's parts that stand closest together, around each candidate in turn; null when none are close enough.
		Actor[] Closest(List<Actor> candidates, string into, PwMergedInfo recipe)
		{
			var gather = WDist.FromCells(Info.GatherRadius).Length;
			Actor[] best = null;
			var bestSpread = long.MaxValue;
			foreach (var centre in candidates)
			{
				var near = candidates.Where(a => (a.CenterPosition - centre.CenterPosition).HorizontalLength <= gather)
					.OrderBy(a => (a.CenterPosition - centre.CenterPosition).HorizontalLengthSquared);
				var parts = PwMerge.Pick(near, into, recipe);
				if (parts == null)
					continue;

				var spread = parts.Sum(p => (p.CenterPosition - centre.CenterPosition).HorizontalLengthSquared);
				if (spread < bestSpread)
				{
					best = parts;
					bestSpread = spread;
				}
			}

			return best;
		}

		static bool Merging(Actor unit)
		{
			for (var activity = unit.CurrentActivity; activity != null; activity = activity.NextActivity)
				if (activity is MergeUnits)
					return true;

			return false;
		}
	}
}
