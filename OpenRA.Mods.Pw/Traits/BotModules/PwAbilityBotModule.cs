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
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("AI unit abilities: the Fall out of every caster (PwFallOut, the Chief) goes on the strongest enemy unit",
		"near it that isn't struck yet, with the order the ability button issues, as soon as it is ready. While the bot",
		"has a caster whose ability is still locked, it researches the upgrade that unlocks it first.")]
	public class PwAbilityBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between checks.")]
		public readonly int ScanInterval = 25;

		[Desc("How far beyond its range a caster looks for a target, in cells (it walks up to cast).")]
		public readonly int ExtraRange = 3;

		[Desc("Strike wild creatures too (not only other players' units).")]
		public readonly bool TargetWild = false;

		public override object Create(ActorInitializer init) { return new PwAbilityBotModule(init.Self, this); }
	}

	public class PwAbilityBotModule : ConditionalTrait<PwAbilityBotModuleInfo>, IBotTick
	{
		readonly World world;
		readonly Player player;
		int ticks;

		public PwAbilityBotModule(Actor self, PwAbilityBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (++ticks % Info.ScanInterval != 0)
				return;

			var casters = world.ActorsWithTrait<PwFallOut>()
				.Where(c => c.Actor.Owner == player && !c.Actor.IsDead && c.Actor.IsInWorld).ToList();

			foreach (var (caster, fallOut) in casters.Select(c => (c.Actor, c.Trait)))
			{
				if (!fallOut.IsUnlocked(caster))
				{
					Research(bot, fallOut.Info.Upgrade);
					continue;
				}

				if (!fallOut.IsReady(caster) || Casting(caster))
					continue;

				var range = fallOut.Info.Range + WDist.FromCells(Info.ExtraRange);
				var target = world.FindActorsInCircle(caster.CenterPosition, range)
					.Where(a => PwFallOut.CanTarget(caster, a) && player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy
						&& (Info.TargetWild || !a.Owner.NonCombatant)
						&& !a.Trait<PwFallOutVictim>().Active && a.TraitOrDefault<IHealth>() is IHealth h && h.HP > 1)
					.OrderByDescending(a => a.Trait<IHealth>().HP)
					.FirstOrDefault();

				if (target == null)
					continue;

				AIUtils.BotDebug("{0}: {1} casts Fall out on {2}.", player, caster.Info.Name, target.Info.Name);
				bot.QueueOrder(new Order(PwFallOut.OrderID, caster, Target.FromActor(target), false));
			}
		}

		static bool Casting(Actor caster)
		{
			for (var activity = caster.CurrentActivity; activity != null; activity = activity.NextActivity)
				if (activity is CastFallOut)
					return true;

			return false;
		}

		// Starts the research that unlocks an ability, from whichever of the bot's queues offers it.
		void Research(IBot bot, string upgrade)
		{
			var queues = world.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == player && !q.Actor.IsDead && q.Trait.Enabled && PwBotUtils.IsComplete(q.Actor)).ToList();
			if (queues.Any(q => q.Trait.AllQueued().Any(i => i.Item == upgrade)))
				return;

			foreach (var q in queues)
			{
				var item = q.Trait.BuildableItems().FirstOrDefault(i => i.Name == upgrade);

				// Queued behind whatever the queue is making (the unit builder only adds to idle queues).
				if (item != null && PwBotUtils.Cash(player) >= PwBotUtils.Cost(item))
				{
					AIUtils.BotDebug("{0}: researching {1} for an ability.", player, upgrade);
					bot.QueueOrder(Order.StartProduction(q.Actor, upgrade, 1));
					return;
				}
			}
		}
	}
}
