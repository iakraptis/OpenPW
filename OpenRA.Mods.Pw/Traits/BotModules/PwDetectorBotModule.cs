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
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.BotModules.Squads;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("AI detectors (units that see invisible ones, DetectCloaked: the Revolver, TwinRhamphos, DeathEye, Pixie): it",
		"trains them and posts one between the main building and its berry bushes and one at the base's edge toward the",
		"nearest enemy. After EscortDelay one more goes with every attack wave (it guards the squad's centre unit). A",
		"faction whose detector can't be trained (the TwinRhamphos is merged: PwMergeBotModule) uses those it gets. Its",
		"detectors are kept from the other modules (PwTransportBotModule doesn't fly the Revolvers it posts).")]
	public class PwDetectorBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between checks.")]
		public readonly int ScanInterval = 50;

		[Desc("Ticks after which attack waves get a detector.")]
		public readonly int EscortDelay = 9000;

		[Desc("How far the edge post is from the base centre, in cells.")]
		public readonly int EdgeDistance = 10;

		[Desc("A posted detector further than this many cells from its post is sent back.")]
		public readonly int PostRadius = 3;

		[Desc("Berry bushes further than this many cells from the base centre don't move the home post.")]
		public readonly int MaxBushDistance = 20;

		[Desc("Cash kept back after paying for a detector.")]
		public readonly int CashReserve = 100;

		public override object Create(ActorInitializer init) { return new PwDetectorBotModule(init.Self, this); }
	}

	public class PwDetectorBotModule : ConditionalTrait<PwDetectorBotModuleInfo>, IBotTick
	{
		enum Role { Home, Edge, Escort }

		readonly World world;
		readonly Player player;
		readonly Dictionary<Actor, Role> roles = new();

		// The unit each escort was last told to guard.
		readonly Dictionary<Actor, Actor> guarding = new();
		int ticks;

		public PwDetectorBotModule(Actor self, PwDetectorBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		public static bool IsDetector(ActorInfo info) =>
			info.HasTraitInfo<DetectCloakedInfo>() && !PwBotUtils.IsBuilding(info) && info.HasTraitInfo<IPositionableInfo>();

		// A detector this module keeps (posted or escorting): other modules leave it alone.
		public static bool Holds(Player player, Actor actor) =>
			player.PlayerActor.TraitsImplementing<PwDetectorBotModule>().Any(m => !m.IsTraitDisabled && m.roles.ContainsKey(actor));

		void IBotTick.BotTick(IBot bot)
		{
			if (++ticks % Info.ScanInterval != 0)
				return;

			foreach (var gone in roles.Keys.Where(a => a.IsDead || a.Disposed || a.Owner != player).ToList())
			{
				roles.Remove(gone);
				guarding.Remove(gone);
			}

			var owned = PwBotUtils.Owned(world, player).ToList();
			var center = PwBotUtils.BaseCenter(owned);
			if (center == null)
				return;

			var wanted = new List<Role> { Role.Home, Role.Edge };
			if (ticks >= Info.EscortDelay)
				wanted.Add(Role.Escort);

			// New detectors take the first role without one.
			foreach (var detector in owned.Where(a => a.IsInWorld && IsDetector(a.Info) && !roles.ContainsKey(a)
				&& !PwTransportBotModule.Uses(player, a) && (a.TraitOrDefault<Cargo>()?.IsEmpty() ?? true)))
			{
				var role = wanted.Where(r => !roles.ContainsValue(r)).Cast<Role?>().FirstOrDefault();
				if (role == null)
					break;

				roles[detector] = role.Value;
			}

			if (roles.Count < wanted.Count)
				Train(bot, wanted.Count - roles.Count);

			foreach (var (detector, role) in roles.ToList())
			{
				if (!detector.IsInWorld)
					continue;

				if (role == Role.Escort && Escort(bot, detector))
					continue;

				var post = role == Role.Edge ? PwBotUtils.EdgeToward(world, player, center.Value, Info.EdgeDistance) ?? center.Value
					: HomePost(center.Value);
				guarding.Remove(detector);
				if ((detector.Location - post).LengthSquared > Info.PostRadius * Info.PostRadius && !Moving(detector))
				{
					AIUtils.BotDebug("{0}: posting {1} ({2}) at {3}.", player, detector.Info.Name, role, post);
					bot.QueueOrder(new Order("Move", detector, Target.FromCell(world, post), false));
				}
			}
		}

		// Between the main building and the berry bushes nearest it, to cover both.
		CPos HomePost(CPos center)
		{
			var bush = PwBotUtils.NearestBush(world, center, Info.MaxBushDistance);
			return bush == null ? center : new CPos((center.X + bush.Value.X) / 2, (center.Y + bush.Value.Y) / 2);
		}

		// Guards the centre unit of the bot's biggest attack squad; false when there is no attack (the escort waits at
		// the home post).
		bool Escort(IBot bot, Actor escort)
		{
			var squad = player.PlayerActor.TraitsImplementing<SquadManagerBotModule>().Where(m => !m.IsTraitDisabled)
				.SelectMany(m => m.Squads)
				.Where(s => (s.Type == SquadType.Assault || s.Type == SquadType.Rush) && s.IsValid)
				.MaxByOrDefault(s => s.Units.Count);
			if (squad == null)
				return false;

			// Guarding follows the unit: a new order only when the one it guards left the squad, or the escort lost it.
			if (guarding.TryGetValue(escort, out var current) && squad.Units.Contains(current) && !escort.IsIdle)
				return true;

			var leader = squad.CenterUnit();
			if (leader == null)
				return false;

			AIUtils.BotDebug("{0}: {1} escorts the attack ({2}).", player, escort.Info.Name, leader.Info.Name);
			guarding[escort] = leader;
			bot.QueueOrder(new Order("Guard", escort, Target.FromActor(leader), false));
			return true;
		}

		// Flying or walking somewhere (an aircraft hovering or landed in place has no move activity).
		static bool Moving(Actor actor)
		{
			for (var activity = actor.CurrentActivity; activity != null; activity = activity.NextActivity)
				if (activity is Fly || activity is Move || activity is Land)
					return true;

			return false;
		}

		// One detector at a time, from whichever queue trains one, behind what it is making. A race whose detector is
		// only merged (the Tyrano TwinRhamphos) trains its parts instead, as many as the missing detectors take;
		// PwMergeBotModule merges them (Medium keeps those parts out of the army mix and the squads).
		void Train(IBot bot, int missing)
		{
			var queues = world.ActorsWithTrait<ProductionQueue>()
				.Where(q => q.Actor.Owner == player && !q.Actor.IsDead && q.Actor.IsInWorld && q.Trait.Enabled && PwBotUtils.IsComplete(q.Actor))
				.ToList();
			var queued = queues.SelectMany(q => q.Trait.AllQueued()).Select(i => i.Item).ToList();
			if (queued.Any(i => world.Map.Rules.Actors.TryGetValue(i, out var a) && IsDetector(a)))
				return;

			var buildable = queues.SelectMany(q => q.Trait.BuildableItems().Select(i => (Queue: q.Actor, Item: i))).ToList();
			var detector = buildable.Where(b => IsDetector(b.Item)).MinByOrDefault(b => PwBotUtils.Cost(b.Item));
			if (detector.Item != null)
			{
				Start(bot, detector.Queue, detector.Item);
				return;
			}

			// The parts of a merged detector that the bot has (and is training) fewer of than the missing ones need.
			var parts = world.Map.Rules.Actors.Values.Where(IsDetector).Select(i => i.TraitInfoOrDefault<PwMergedInfo>())
				.Where(m => m != null && m.From.Length > 0 && m.From.All(f => buildable.Any(b => b.Item.Name == f)))
				.SelectMany(m => m.From.GroupBy(f => f).Select(g => (Type: g.Key, Needed: g.Count() * missing)));
			foreach (var (type, needed) in parts)
			{
				var have = PwBotUtils.Owned(world, player).Count(a => a.Info.Name == type) + queued.Count(i => i == type);
				if (have >= needed)
					continue;

				var part = buildable.First(b => b.Item.Name == type);
				Start(bot, part.Queue, part.Item);
				return;
			}
		}

		void Start(IBot bot, Actor queue, ActorInfo item)
		{
			if (PwBotUtils.Cash(player) < PwBotUtils.Cost(item) + Info.CashReserve || !PwPopulation.HasRoomFor(player, item))
				return;

			AIUtils.BotDebug("{0}: training {1} for a detector at {2}.", player, item.Name, queue.Info.Name);
			bot.QueueOrder(Order.StartProduction(queue, item.Name, 1));
		}
	}
}
