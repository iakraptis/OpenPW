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
	[Desc("AI defence against anything that attacks it, wild creatures included (the stock squad manager is set to leave",
		"them alone so its attacks go for the other players): when one of the bot's buildings or units is hurt by an",
		"enemy, the armed units nearest the victim attack the attacker. Hostile wild creatures (with an AttackBase) that",
		"come near the base or its workers are attacked before they strike.")]
	public class PwDefenseBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Armed units within this many cells of the victim answer an attack.")]
		public readonly int DefenseRadius = 14;

		[Desc("Most units sent at one attacker.")]
		public readonly int MaxDefenders = 6;

		[Desc("Ticks before the same attacker is answered again.")]
		public readonly int ResponseInterval = 75;

		[Desc("Ticks between scans for hostile wild creatures near the base and its workers.")]
		public readonly int ScanInterval = 50;

		[Desc("Hostile wild creatures within this many cells of the bot's buildings or workers are attacked first.")]
		public readonly int ClearRadius = 8;

		public override object Create(ActorInitializer init) { return new PwDefenseBotModule(init.Self, this); }
	}

	public class PwDefenseBotModule : ConditionalTrait<PwDefenseBotModuleInfo>, IBotTick, IBotRespondToAttack
	{
		readonly World world;
		readonly Player player;

		// When each attacker was last answered.
		readonly Dictionary<Actor, int> answered = [];
		int ticks;

		public PwDefenseBotModule(Actor self, PwDefenseBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		void IBotRespondToAttack.RespondToAttack(IBot bot, Actor self, AttackInfo e)
		{
			var attacker = e.Attacker;
			if (attacker == null || attacker.IsDead || !attacker.IsInWorld || e.Damage.Value <= 0
				|| player.RelationshipWith(attacker.Owner) != PlayerRelationship.Enemy)
				return;

			Answer(bot, attacker, self.CenterPosition);
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (++ticks % Info.ScanInterval != 0)
				return;

			foreach (var gone in answered.Keys.Where(a => a.IsDead || a.Disposed).ToList())
				answered.Remove(gone);

			// Hostile wild creatures near what the bot must keep (its buildings and workers).
			var kept = PwBotUtils.Owned(world, player)
				.Where(a => a.IsInWorld && (PwBotUtils.IsBuilding(a.Info) || PwBotUtils.IsWorker(a.Info)))
				.Select(a => a.CenterPosition).ToList();
			if (kept.Count == 0)
				return;

			var clear = WDist.FromCells(Info.ClearRadius).Length;
			var hostiles = world.ActorsHavingTrait<AttackBase>()
				.Where(a => !a.IsDead && a.IsInWorld && a.Owner.NonCombatant && player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy
					&& !world.FogObscures(a) && kept.Any(k => (a.CenterPosition - k).Length <= clear));

			foreach (var hostile in hostiles)
				Answer(bot, hostile, hostile.CenterPosition);
		}

		// The armed units nearest the trouble attack the attacker (at most MaxDefenders, once per ResponseInterval).
		void Answer(IBot bot, Actor attacker, WPos where)
		{
			if (answered.TryGetValue(attacker, out var last) && ticks - last < Info.ResponseInterval)
				return;

			var radius = WDist.FromCells(Info.DefenseRadius).Length;
			var defenders = PwBotUtils.Owned(world, player)
				.Where(a => a.IsInWorld && PwBotUtils.IsArmy(a.Info) && (a.CenterPosition - where).Length <= radius
					&& a.TraitsImplementing<AttackBase>().Any(ab => !ab.IsTraitDisabled && ab.HasAnyValidWeapons(Target.FromActor(attacker))))
				.OrderBy(a => (a.CenterPosition - where).LengthSquared)
				.Take(Info.MaxDefenders)
				.ToList();
			if (defenders.Count == 0)
				return;

			answered[attacker] = ticks;
			AIUtils.BotDebug("{0}: {1} defenders attack {2}.", player, defenders.Count, attacker.Info.Name);
			foreach (var d in defenders)
				bot.QueueOrder(new Order("Attack", d, Target.FromActor(attacker), false));
		}
	}
}
