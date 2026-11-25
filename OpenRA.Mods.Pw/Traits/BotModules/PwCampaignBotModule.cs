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
using OpenRA.Mods.Common.Warheads;
using OpenRA.Mods.Pw.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("The campaign's computer players, after the original AI running their scripts (JW2_16; docs/formats/text.md",
		"has the commands as Pw.exe runs them). Each player has wanted counts of unit types (SET_UNIT_MAKE, or a count",
		"for every unit of a tier) and trains up to them, builds the building that trains a wanted unit (and its",
		"prerequisites) when it has none, houses for its population and workers for its berries. It attacks when the",
		"attack interval has passed, when its army reaches the attack counter, or when its army's strength (HP, attack",
		"and armour) is a share of the enemy's whole army (winable) or of the enemy units at the enemy's base (attack",
		"back). An attack takes some of the units at home, gathers part of the way to the enemy, then goes for it. A",
		"share of the units at home hunts. Units in the mission script's teams can be kept out of all this. Settings",
		"are by player and come from the missions' map rules (AssetInstaller/Campaign.cs); a later set replaces them",
		"once the player has made a number of attacks (the scripts' GO_IF_OVER_ATTCNT). Only the campaign maps load it",
		"(rules/campaign-ai.yaml).")]
	public class PwCampaignBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between checks.")]
		public readonly int ScanInterval = PwTime.Ticks(30);

		[Desc("By player (internal name): the units it wants, and how many of each.")]
		public readonly Dictionary<string, Dictionary<string, int>> Train = [];

		[Desc("By player: its script's settings, by command without SET_ (ATTACK_COUNTER, ATTACK_INTERVAL, ATTACK_WINABLE,",
			"ATTACK_BACK, ATTACK_UNIT, ATTACK_UNIT_PS, ATTACK_MOVE, HUNT, NO_SUPPLY_BY_UNIT, NO_BUILDER_BERY; PASSIVE 1",
			"for UNUSE_SCRIPT, no AI at all; others are kept but not played). Settings left out take the original's",
			"defaults.")]
		public readonly Dictionary<string, Dictionary<string, int>> Settings = [];

		[Desc("By player: after this many attacks the Later settings and LaterTrain apply.")]
		public readonly Dictionary<string, int> LaterAfter = [];

		[Desc("By player: the settings that change after LaterAfter attacks. ADD_<setting> adds to a setting at each",
			"attack from then on, up to ADD_<setting>_MAX.")]
		public readonly Dictionary<string, Dictionary<string, int>> Later = [];

		[Desc("By player: the wanted units after LaterAfter attacks.")]
		public readonly Dictionary<string, Dictionary<string, int>> LaterTrain = [];

		[Desc("By player: placed units (map actor names) the mission script moves itself; they never join attacks",
			"or hunt (ON_EXCEPT_TRIGGER_TEAM).")]
		public readonly Dictionary<string, HashSet<string>> Kept = [];

		[Desc("Buildings go between these many cells from the base centre.")]
		public readonly int MinBaseRadius = 3;

		public readonly int MaxBaseRadius = 14;

		[Desc("Free cells kept around a new building.")]
		public readonly int Spacing = 1;

		[Desc("Most workers (the original's cap, before NO_BUILDER_BERY's share).")]
		public readonly int MaxWorkers = 15;

		[Desc("Berry bushes this many cells from the base count for the workers wanted.")]
		public readonly int BushRadius = 12;

		[Desc("Checks an attack waits at its gathering point for its stragglers (the original's 8).")]
		public readonly int GatherChecks = 8;

		[Desc("A unit this many cells from the gathering point has arrived.")]
		public readonly int GatherRadius = 4;

		[Desc("Enemy units this many cells from the enemy's main building are its base's defenders (ATTACK_BACK; the",
			"original's 640 pixels).")]
		public readonly int DefendersRadius = 20;

		[Desc("How far around a target or gathering point it looks for a cell its units can reach.")]
		public readonly int ReachRadius = 8;

		[Desc("Population at which it attacks whatever its settings (the original's 144).")]
		public readonly int FullPopulation = 144;

		public override object Create(ActorInitializer init) { return new PwCampaignBotModule(init.Self, this); }
	}

	public class PwCampaignBotModule : ConditionalTrait<PwCampaignBotModuleInfo>, IBotTick
	{
		// The original's defaults (Pw.exe FUN_00419410). ATTACK_UNIT -1: every unit at home.
		static readonly Dictionary<string, int> Defaults = new()
		{
			{ "ATTACK_COUNTER", 0 }, { "ATTACK_INTERVAL", 0 }, { "ATTACK_WINABLE", 120 }, { "ATTACK_BACK", 120 },
			{ "ATTACK_UNIT", -1 }, { "ATTACK_UNIT_PS", 100 }, { "ATTACK_MOVE", 70 }, { "HUNT", 20 },
			{ "NO_SUPPLY_BY_UNIT", 125 }, { "NO_BUILDER_BERY", 0 }, { "PASSIVE", 0 },
		};

		// ATTACK_INTERVAL counts in steps of 22 original ticks.
		static readonly int IntervalStep = PwTime.Ticks(22);

		readonly World world;
		readonly Player player;
		readonly HashSet<Actor> attacking = [];
		readonly Dictionary<string, int> added = [];
		HashSet<Actor> kept;
		TechTree techTree;
		int ticks, attacks, lastInterval;

		// The attack gathering on its way (the original forms one at a time).
		List<Actor> squad;
		CPos gatherAt, targetCell;
		int gatherChecks;

		public PwCampaignBotModule(Actor self, PwCampaignBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		string Name => player.InternalName;

		// Its decisions: in the chat with bot debugging on, and always in debug.log (headless campaign runs).
		static void Note(string format, params object[] args)
		{
			AIUtils.BotDebug(format, args);
			Log.Write("debug", string.Format(System.Globalization.CultureInfo.InvariantCulture, format, args));
		}

		bool InLaterPhase => Info.LaterAfter.TryGetValue(Name, out var after) && attacks >= after;

		int Setting(string name)
		{
			var value = InLaterPhase && Info.Later.TryGetValue(Name, out var later) && later.TryGetValue(name, out var l) ? l
				: Info.Settings.TryGetValue(Name, out var settings) && settings.TryGetValue(name, out var s) ? s
				: Defaults[name];

			if (added.TryGetValue(name, out var extra))
			{
				var max = Info.Later.TryGetValue(Name, out var lat) && lat.TryGetValue($"ADD_{name}_MAX", out var m) ? m : int.MaxValue;
				value = System.Math.Min(max, value + extra);
			}

			return value;
		}

		Dictionary<string, int> Wanted()
		{
			var train = InLaterPhase && Info.LaterTrain.TryGetValue(Name, out var later) ? later
				: Info.Train.TryGetValue(Name, out var t) ? t : [];
			return new Dictionary<string, int>(train);
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (kept == null)
			{
				techTree = player.PlayerActor.Trait<TechTree>();
				var named = world.WorldActor.Trait<SpawnMapActors>().Actors;
				kept = Info.Kept.TryGetValue(Name, out var names)
					? names.Where(named.ContainsKey).Select(n => named[n]).ToHashSet() : [];
			}

			// UNUSE_SCRIPT: the original runs no AI for the player; it keeps what the mission placed.
			if (++ticks % Info.ScanInterval != 0 || Setting("PASSIVE") != 0)
				return;

			var owned = PwBotUtils.Owned(world, player).Where(a => a.IsInWorld).ToList();
			attacking.RemoveWhere(a => a.IsDead || !a.IsInWorld);
			squad?.RemoveAll(a => a.IsDead || !a.IsInWorld);

			var army = owned.Where(a => PwBotUtils.IsArmy(a.Info) && !kept.Contains(a)).ToList();
			var home = army.Where(a => !attacking.Contains(a) && (squad == null || !squad.Contains(a))).ToList();

			var wanted = Wanted();
			AddWorkers(owned, wanted);
			Train(bot, owned, wanted);
			Build(bot, owned, wanted);
			Hunt(bot, home);

			// Attackers that have run out of orders go for the nearest enemy again.
			var idle = attacking.Where(a => a.IsIdle).ToList();
			if (idle.Count > 0)
				AttackMove(bot, idle, Target(idle[0].Location));

			if (squad != null)
				Gather(bot);
			else if (home.Count > 0 && ShouldAttack(owned, army))
				Launch(bot, owned, home);
		}

		// Workers: NO_BUILDER_BERY percent of the bushes near the base (at most MaxWorkers), as wanted units.
		void AddWorkers(List<Actor> owned, Dictionary<string, int> wanted)
		{
			var share = Setting("NO_BUILDER_BERY");
			var center = PwBotUtils.BaseCenter(owned);
			if (share <= 0 || center == null)
				return;

			var worker = owned.FirstOrDefault(a => PwBotUtils.IsWorker(a.Info))?.Info.Name ?? Trainable(owned).FirstOrDefault(PwBotUtils.IsWorker)?.Name;
			if (worker == null)
				return;

			var bushes = world.ActorsHavingTrait<BerryBush>().Count(b => b.IsInWorld && !b.Trait<BerryBush>().IsEmpty
				&& (b.Location - center.Value).LengthSquared <= Info.BushRadius * Info.BushRadius);
			wanted[worker] = System.Math.Max(wanted.GetValueOrDefault(worker), System.Math.Min(Info.MaxWorkers, bushes) * share / 100);
		}

		static IEnumerable<ActorInfo> Trainable(List<Actor> owned) =>
			owned.SelectMany(a => a.TraitsImplementing<ProductionQueue>()).Where(q => q.Enabled).SelectMany(q => q.BuildableItems());

		// One unit per check from an idle queue, the type furthest below its wanted count first (counting every unit
		// the player has of it, as the original).
		void Train(IBot bot, List<Actor> owned, Dictionary<string, int> wanted)
		{
			var queues = owned.SelectMany(a => a.TraitsImplementing<ProductionQueue>().Where(q => q.Enabled && PwBotUtils.IsComplete(a)))
				.ToList();
			var queued = queues.SelectMany(q => q.AllQueued()).GroupBy(i => i.Item).ToDictionary(g => g.Key, g => g.Count());
			int Have(string type) => owned.Count(a => a.Info.Name == type) + queued.GetValueOrDefault(type);

			foreach (var (type, count) in wanted.Where(w => Have(w.Key) < w.Value).OrderBy(w => Have(w.Key) - w.Value))
			{
				foreach (var queue in queues)
				{
					var item = queue.BuildableItems().FirstOrDefault(i => i.Name == type);
					if (item == null || queue.AllQueued().Any() || !PwPopulation.HasRoomFor(player, item))
						continue;

					if (PwBotUtils.Cash(player) >= PwBotUtils.Cost(item))
						bot.QueueOrder(Order.StartProduction(queue.Actor, type, 1));

					return;
				}
			}
		}

		// One building at a time: a site left unfinished first, then a house when the population needs one, then the
		// building that trains a wanted unit it can't train (or that building's missing prerequisite).
		void Build(IBot bot, List<Actor> owned, Dictionary<string, int> wanted)
		{
			var workers = owned.Where(a => a.Info.HasTraitInfo<BuilderInfo>() && !IsConstructing(a)).ToList();
			if (workers.Count == 0)
				return;

			var sites = owned.Where(a => !PwBotUtils.IsComplete(a)).ToList();
			var abandoned = sites.FirstOrDefault(s => workers.Any(w => w.Trait<Builder>().CanResume(w, s)));
			if (abandoned != null)
			{
				var worker = workers.Where(w => w.Trait<Builder>().CanResume(w, abandoned)).MinBy(w => (w.Location - abandoned.Location).LengthSquared);
				bot.QueueOrder(new Order(Builder.ResumeOrderID, worker, OpenRA.Traits.Target.FromActor(abandoned), false));
				return;
			}

			if (sites.Count > 0)
				return;

			var buildable = workers.SelectMany(w => w.Trait<Builder>().Info.Types).Distinct().ToList();
			foreach (var type in Needed(owned, wanted, buildable))
			{
				var info = world.Map.Rules.Actors[type];
				if (PwBotUtils.Cash(player) < PwBotUtils.Cost(info))
					return;

				var center = PwBotUtils.BaseCenter(owned) ?? workers[0].Location;
				var worker = workers.Where(w => w.Trait<Builder>().CanBuild(type))
					.OrderBy(w => w.IsIdle ? 0 : 1).ThenBy(w => (w.Location - center).LengthSquared).First();
				var site = FindSite(worker, info, center);
				if (site == null)
					continue;

				Note("{0}: campaign builds {1} at {2}.", player, type, site.Value);
				bot.QueueOrder(new Order(Builder.OrderID, worker, OpenRA.Traits.Target.FromCell(world, site.Value), false) { TargetString = type });
				return;
			}
		}

		IEnumerable<string> Needed(List<Actor> owned, Dictionary<string, int> wanted, List<string> buildable)
		{
			var rules = world.Map.Rules.Actors;
			bool Has(string type) => owned.Any(a => a.Info.Name == type);
			bool Allowed(string type) => buildable.Contains(type) && rules.TryGetValue(type, out var i)
				&& i.TraitInfoOrDefault<BuildableInfo>()?.Prerequisites.Contains("~disabled") != true;

			// Houses: NO_SUPPLY_BY_UNIT percent of the population used, up to 160 (Pw.exe FUN_0041e670).
			var supply = System.Math.Min(160, PwPopulation.Used(player) * Setting("NO_SUPPLY_BY_UNIT") / 100);
			if (PwPopulation.Capacity(player) < supply)
			{
				var house = buildable.FirstOrDefault(t => Allowed(t) && PwBotUtils.IsHouse(rules[t]));
				if (house != null && Ready(house))
					yield return house;
			}

			var trainable = Trainable(owned).Select(i => i.Name).ToHashSet();
			foreach (var (unit, count) in wanted)
			{
				if (trainable.Contains(unit) || owned.Count(a => a.Info.Name == unit) >= count || !rules.TryGetValue(unit, out var info))
					continue;

				var buildableInfo = info.TraitInfoOrDefault<BuildableInfo>();
				if (buildableInfo == null || buildableInfo.Prerequisites.Contains("~disabled"))
					continue;

				// Its trainer, or the unit's own prerequisite buildings, then whatever those need first.
				var producers = buildable.Where(t => rules[t].TraitInfos<ProductionQueueInfo>().Any(q => buildableInfo.Queue.Contains(q.Type)));
				var producer = producers.FirstOrDefault(Has) ?? producers.FirstOrDefault(Allowed);
				var chain = new List<string>();
				if (producer != null && !Has(producer))
					chain.Add(producer);

				chain.AddRange(buildableInfo.Prerequisites.Where(p => rules.ContainsKey(p) && !Has(p) && Allowed(p)));
				foreach (var type in chain)
				{
					var first = FirstMissing(type, owned, buildable, 0);
					if (first != null)
						yield return first;
				}
			}
		}

		// The type itself when its prerequisites are met, else the first missing one it can build (depth-first).
		string FirstMissing(string type, List<Actor> owned, List<string> buildable, int depth)
		{
			if (Ready(type))
				return type;

			var rules = world.Map.Rules.Actors;
			if (depth > 8 || !rules.TryGetValue(type, out var info))
				return null;

			foreach (var p in info.TraitInfoOrDefault<BuildableInfo>()?.Prerequisites ?? [])
			{
				if (!rules.ContainsKey(p) || owned.Any(a => a.Info.Name == p && PwBotUtils.IsComplete(a)) || !buildable.Contains(p))
					continue;

				var first = FirstMissing(p, owned, buildable, depth + 1);
				if (first != null)
					return first;
			}

			return null;
		}

		bool Ready(string type)
		{
			var prerequisites = world.Map.Rules.Actors[type].TraitInfoOrDefault<BuildableInfo>()?.Prerequisites ?? [];
			return prerequisites.Length == 0 || techTree.HasPrerequisites(prerequisites);
		}

		static bool IsConstructing(Actor worker)
		{
			for (var activity = worker.CurrentActivity; activity != null; activity = activity.NextActivity)
				if (activity is ConstructBuilding)
					return true;

			return false;
		}

		CPos? FindSite(Actor worker, ActorInfo info, CPos center)
		{
			var buildingInfo = info.TraitInfo<BuildingInfo>();
			var valid = world.Map.FindTilesInAnnulus(center, Info.MinBaseRadius, Info.MaxBaseRadius)
				.Where(c => Builder.CanPlace(world, worker, info, buildingInfo, c) && Clear(buildingInfo, c)).Take(8).ToList();
			return valid.Count == 0 ? null : valid.Random(world.LocalRandom);
		}

		// No other building or berry bush within Spacing cells of the footprint.
		bool Clear(BuildingInfo info, CPos topLeft)
		{
			var s = Info.Spacing;
			foreach (var c in info.Tiles(topLeft))
				for (var dx = -s; dx <= s; dx++)
					for (var dy = -s; dy <= s; dy++)
					{
						var n = c + new CVec(dx, dy);
						if (world.Map.Contains(n) && world.ActorMap.GetActorsAt(n).Any(a => a.TraitOrDefault<Building>() != null
							&& (a.Info.HasTraitInfo<HealthInfo>() || a.Info.HasTraitInfo<BerryBushInfo>())))
							return false;
					}

			return true;
		}

		// HUNT percent of the units at home hunt; the others don't.
		void Hunt(IBot bot, List<Actor> home)
		{
			var hunters = home.Where(a => a.TraitOrDefault<PwHunter>() is PwHunter h && !h.IsTraitDisabled).ToList();
			var wanted = hunters.Count * Setting("HUNT") / 100;
			var on = hunters.Count(a => a.Trait<PwHunter>().Hunting);
			foreach (var unit in hunters)
			{
				var hunting = unit.Trait<PwHunter>().Hunting;
				if (!hunting && on < wanted && unit.IsIdle)
				{
					bot.QueueOrder(PwHunter.Toggle(unit, true));
					on++;
				}
				else if (hunting && on > wanted)
				{
					bot.QueueOrder(PwHunter.Toggle(unit, false));
					on--;
				}
			}
		}

		// Pw.exe FUN_0041c470: the interval, the counter (or a full population), then the strength shares.
		bool ShouldAttack(List<Actor> owned, List<Actor> army)
		{
			var interval = Setting("ATTACK_INTERVAL");
			if (interval > 0 && world.WorldTick - lastInterval >= interval * IntervalStep)
			{
				lastInterval = world.WorldTick;
				return true;
			}

			var counter = Setting("ATTACK_COUNTER");
			if ((counter > 0 && army.Count >= counter) || PwPopulation.Used(player) > Info.FullPopulation)
				return true;

			var strength = army.Sum(Strength);
			var enemy = EnemyPlayer(owned);
			if (enemy == null)
				return false;

			var enemyArmy = PwBotUtils.Owned(world, enemy).Where(a => a.IsInWorld && PwBotUtils.IsArmy(a.Info)).ToList();
			var winable = Setting("ATTACK_WINABLE");
			if (winable > 0 && strength * 100 / (enemyArmy.Sum(Strength) + 1) >= winable)
				return true;

			var back = Setting("ATTACK_BACK");
			var enemyBase = PwBotUtils.BaseCenter(PwBotUtils.Owned(world, enemy));
			if (back <= 0 || enemyBase == null)
				return false;

			var radius = Info.DefendersRadius * Info.DefendersRadius;
			var defenders = enemyArmy.Where(a => (a.Location - enemyBase.Value).LengthSquared <= radius).Sum(Strength);
			return strength * 100 / (defenders + 1) >= back;
		}

		// HP, attack and armour, as the original sums them (FUN_0041c7e0).
		static int Strength(Actor a)
		{
			var hp = a.TraitOrDefault<IHealth>()?.HP ?? 0;
			var armour = a.Info.TraitInfoOrDefault<PwArmorInfo>()?.Value ?? 0;
			var attack = a.TraitsImplementing<Armament>().Select(w => w.Weapon.Warheads.OfType<DamageWarhead>().Sum(d => d.Damage))
				.DefaultIfEmpty(0).Max();
			return hp + armour + attack;
		}

		// The enemy whose nearest building (or unit) is nearest the base.
		Player EnemyPlayer(List<Actor> owned)
		{
			var center = PwBotUtils.BaseCenter(owned);
			if (center == null)
				return null;

			return world.Actors.Where(a => !a.IsDead && a.IsInWorld && !a.Owner.NonCombatant
					&& player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy && a.Info.HasTraitInfo<HealthInfo>())
				.MinByOrDefault(a => (a.Location - center.Value).LengthSquared)?.Owner;
		}

		// An attack: the larger of ATTACK_UNIT units and ATTACK_UNIT_PS percent of those at home, sent to gather
		// ATTACK_MOVE percent of the way to the enemy.
		void Launch(IBot bot, List<Actor> owned, List<Actor> home)
		{
			var center = PwBotUtils.BaseCenter(owned) ?? home[0].Location;
			var target = Target(center);
			if (target == null)
				return;

			var units = Setting("ATTACK_UNIT");
			var share = home.Count * Setting("ATTACK_UNIT_PS") / 100;
			var size = units < 0 ? home.Count : System.Math.Max(units, share);
			if (size <= 0)
				return;

			squad = home.OrderBy(a => (a.Location - center).LengthSquared).Take(size).ToList();
			targetCell = target.Value;
			var move = Setting("ATTACK_MOVE");
			gatherAt = new CPos(center.X + (targetCell.X - center.X) * move / 100, center.Y + (targetCell.Y - center.Y) * move / 100);
			var walker = squad.FirstOrDefault(a => a.TraitOrDefault<Mobile>() != null);
			if (walker != null)
				gatherAt = Reachable(walker, walker.Trait<Mobile>(), gatherAt) ?? walker.Location;

			gatherChecks = 0;
			attacks++;

			// ADD_ settings grow at each attack once the later settings apply.
			if (InLaterPhase && Info.Later.TryGetValue(Name, out var later))
				foreach (var (key, value) in later.Where(l => l.Key.StartsWith("ADD_", System.StringComparison.Ordinal)
					&& !l.Key.EndsWith("_MAX", System.StringComparison.Ordinal)))
					added[key[4..]] = added.GetValueOrDefault(key[4..]) + value;

			Note("{0}: campaign attack {1} with {2} units, gathering at {3}.", player, attacks, squad.Count, gatherAt);
			foreach (var unit in squad)
			{
				if (unit.TraitOrDefault<PwHunter>()?.Hunting == true)
					bot.QueueOrder(PwHunter.Toggle(unit, false));

				bot.QueueOrder(new Order("AttackMove", unit, OpenRA.Traits.Target.FromCell(world, gatherAt), false));
			}
		}

		// The gathering squad goes on once everyone has arrived, or after GatherChecks checks.
		void Gather(IBot bot)
		{
			var radius = Info.GatherRadius * Info.GatherRadius;
			if (squad.Count > 0 && ++gatherChecks <= Info.GatherChecks && squad.Any(a => (a.Location - gatherAt).LengthSquared > radius))
				return;

			attacking.UnionWith(squad);
			AttackMove(bot, squad, Target(gatherAt) ?? targetCell);
			squad = null;
		}

		// The cell at the middle of the enemy building (or else unit) nearest a cell, wild creatures left out.
		CPos? Target(CPos from)
		{
			var enemies = world.Actors.Where(a => !a.IsDead && a.IsInWorld && !a.Owner.NonCombatant
				&& player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy && a.Info.HasTraitInfo<HealthInfo>());
			return enemies.OrderBy(a => PwBotUtils.IsBuilding(a.Info) ? 0 : 1).ThenBy(a => (a.Location - from).LengthSquared)
				.Select(a => (CPos?)world.Map.CellContaining(a.CenterPosition)).FirstOrDefault();
		}

		// The cell nearest the given one that a ground unit can stand on and reach. A move ordered onto a cell it can
		// never reach (a building's own cell, water, a cliff) searches the whole map for a path, again and again, and
		// with a large attack that stalled the game. Null: none within ReachRadius.
		CPos? Reachable(Actor unit, Mobile mobile, CPos near)
		{
			var pathFinder = world.WorldActor.Trait<PathFinder>();
			foreach (var cell in world.Map.FindTilesInCircle(near, Info.ReachRadius))
				if (mobile.CanEnterCell(cell, null, BlockedByActor.Immovable)
					&& pathFinder.PathMightExistForLocomotorBlockedByImmovable(mobile.Locomotor, unit.Location, cell))
					return cell;

			return null;
		}

		// Each ground unit goes to the reachable cell nearest the target (worked out once per locomotor); a unit with
		// none in reach gets no order. Flyers go to the target itself.
		void AttackMove(IBot bot, List<Actor> group, CPos? cell)
		{
			if (cell == null)
				return;

			var byLocomotor = new Dictionary<Locomotor, CPos?>();
			foreach (var unit in group)
			{
				var to = cell;
				if (unit.TraitOrDefault<Mobile>() is Mobile mobile)
				{
					if (!byLocomotor.TryGetValue(mobile.Locomotor, out to))
						byLocomotor[mobile.Locomotor] = to = Reachable(unit, mobile, cell.Value);

					if (to == null)
						continue;
				}

				bot.QueueOrder(new Order("AttackMove", unit, OpenRA.Traits.Target.FromCell(world, to.Value), false));
			}
		}
	}
}
