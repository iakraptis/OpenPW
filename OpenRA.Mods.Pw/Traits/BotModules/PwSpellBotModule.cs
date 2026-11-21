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
using OpenRA.Mods.Pw.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("AI spells (PwSpell): every caster of the bot casts its spells when they are ready, with the order the spell",
		"button issues. What a spell is for comes from what it does, not its name: a blow on an enemy unit, the one it",
		"kills or the dearest; an area blow (or a portal) where most enemies stand; healing on the most hurt ally; a",
		"shield on an ally under fire, or over a group of them; a copy of the dearest unit near a fight; raising the",
		"dead where most fell; a teleport home for a caster about to die. Spells locked behind research get it",
		"researched. Unarmed casters follow the bot's main group of units.")]
	public class PwSpellBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("The spells (PwSpell.Name) the bot casts; empty: all of them.")]
		public readonly HashSet<string> Spells = [];

		[Desc("Ticks between checks.")]
		public readonly int ScanInterval = 20;

		[Desc("How far beyond a spell's range a caster looks for a target, in cells (it walks up to cast).")]
		public readonly int ExtraRange = 3;

		[Desc("Enemies (or allies, for a shield) an area spell must reach.")]
		public readonly int MinAreaTargets = 3;

		[Desc("Enemies an area spell that kills its caster (Meteor) must reach.")]
		public readonly int MinSacrificeTargets = 6;

		[Desc("Graves a raising spell must reach.")]
		public readonly int MinGraves = 2;

		[Desc("Allies are healed below this share of their health, in percent.")]
		public readonly int HealBelow = 60;

		[Desc("A caster with a teleport flees home below this share of its health, in percent, with enemies near.")]
		public readonly int FleeBelow = 35;

		[Desc("Cells around a unit where an enemy makes it 'in a fight'.")]
		public readonly int FightRadius = 7;

		[Desc("Strike wild creatures too (not only other players' units).")]
		public readonly bool TargetWild = false;

		public override object Create(ActorInitializer init) { return new PwSpellBotModule(init.Self, this); }
	}

	public class PwSpellBotModule : ConditionalTrait<PwSpellBotModuleInfo>, IBotTick
	{
		readonly World world;
		readonly Player player;
		int ticks;

		public PwSpellBotModule(Actor self, PwSpellBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (++ticks % Info.ScanInterval != 0)
				return;

			var casters = world.ActorsWithTrait<PwSpell>()
				.Where(c => c.Actor.Owner == player && !c.Actor.IsDead && c.Actor.IsInWorld)
				.GroupBy(c => c.Actor).ToList();

			var cast = new HashSet<Actor>();
			foreach (var caster in casters)
			{
				var self = caster.Key;
				if (Casting(self) || Harmless(self))
					continue;

				foreach (var spell in caster.Select(c => c.Trait).Where(s => !s.Info.AutoHeal && (Info.Spells.Count == 0 || Info.Spells.Contains(s.Info.Name))))
				{
					if (!spell.IsUnlocked(self))
					{
						foreach (var upgrade in spell.Info.Prerequisites.Where(p => world.Map.Rules.Actors.TryGetValue(p, out var u)
							&& PwBotUtils.IsResearch(u)))
							PwBotUtils.Research(world, player, bot, upgrade, "a spell");

						continue;
					}

					if (!spell.IsReady(self))
						continue;

					var target = Choose(self, spell.Info);
					if (target.Type == TargetType.Invalid)
						continue;

					AIUtils.BotDebug("{0}: {1} casts {2}.", player, self.Info.Name, spell.Info.Name);
					bot.QueueOrder(PwSpell.Cast(self, spell.Info.Name, target));
					cast.Add(self);
					break;
				}
			}

			foreach (var caster in casters.Select(c => c.Key).Where(c => !cast.Contains(c)))
				Follow(bot, caster);
		}

		Target Choose(Actor self, PwSpellInfo spell)
		{
			var reach = spell.Range + WDist.FromCells(Info.ExtraRange);
			if (spell.Teleport)
				return Flee(self);

			if (spell.Resurrect)
				return Graves(self, spell, reach);

			if (spell.Illusion)
				return InFight(self) ? Unit(self, spell, reach, a => !Harmless(a), Cost) : Target.Invalid;

			if (spell.Heal > 0)
			{
				return Unit(self, spell, reach, a => HealthShare(a) < Info.HealBelow && !Harmless(a),
					a => -HealthShare(a));
			}

			if (spell.Shield > 0 && spell.Radius == WDist.Zero)
			{
				return Unit(self, spell, reach, a => PwBotUtils.IsArmy(a.Info) && !Harmless(a)
					&& (a.TraitOrDefault<PwShield>()?.Pool ?? 1) == 0 && HealthShare(a) < 100 && InFight(a), Cost);
			}

			if (spell.Shield > 0)
			{
				return Area(self, spell, reach, Info.MinAreaTargets, a => PwBotUtils.IsArmy(a.Info) && !Harmless(a)
					&& (a.TraitOrDefault<PwShield>()?.Pool ?? 1) == 0, requireFight: true);
			}

			if (spell.SummonCount > 0)
				return InFight(self) ? Target.FromActor(self) : Target.Invalid;

			if (spell.Damage <= 0 && spell.PetrifyTicks <= 0 && spell.PortalTicks <= 0)
				return Target.Invalid;

			if (spell.Target == PwSpellTarget.Unit)
			{
				// The one the blow kills, otherwise the dearest.
				return Unit(self, spell, reach, a => Enemy(a) && !Harmless(a),
					a => (spell.Damage > 0 && a.Trait<IHealth>().HP <= spell.Damage ? 100000 : 0) + Cost(a));
			}

			if (spell.Target == PwSpellTarget.Ground)
			{
				var needed = spell.KillsCaster ? Info.MinSacrificeTargets : Info.MinAreaTargets;
				return Area(self, spell, reach, needed, a => Enemy(a) && !Harmless(a) && !PwBotUtils.IsBuilding(a.Info));
			}

			return Target.Invalid;
		}

		// The best unit the spell can be cast on within reach.
		Target Unit(Actor self, PwSpellInfo spell, WDist reach, System.Func<Actor, bool> wanted, System.Func<Actor, int> score)
		{
			var spellTrait = self.TraitsImplementing<PwSpell>().First(s => s.Info == spell);
			var best = Visible(self.CenterPosition, reach)
				.Where(a => spellTrait.CanTarget(self, a) && wanted(a))
				.MaxByOrDefault(score);
			return best != null ? Target.FromActor(best) : Target.Invalid;
		}

		// The place within reach whose radius takes in the most of the wanted units, if enough.
		Target Area(Actor self, PwSpellInfo spell, WDist reach, int needed, System.Func<Actor, bool> wanted, bool requireFight = false)
		{
			var radius = spell.Radius + new WDist(spell.Scatter.Length / 2);
			var units = Visible(self.CenterPosition, reach + radius).Where(wanted).ToList();
			if (units.Count < needed)
				return Target.Invalid;

			var (centre, count) = units
				.Where(c => (c.CenterPosition - self.CenterPosition).HorizontalLengthSquared <= reach.LengthSquared)
				.Select(c => (Centre: c, Count: units.Count(a => (a.CenterPosition - c.CenterPosition).HorizontalLengthSquared <= radius.LengthSquared)))
				.Where(c => !requireFight || InFight(c.Centre))
				.MaxByOrDefault(c => c.Count);

			return centre != null && count >= needed ? Target.FromPos(centre.CenterPosition) : Target.Invalid;
		}

		// Raising the dead: the grave within reach with the most graves around it.
		Target Graves(Actor self, PwSpellInfo spell, WDist reach)
		{
			var since = world.WorldTick - spell.ResurrectAge;
			var graves = world.WorldActor.Trait<PwGraveyard>().Recent(since)
				.Where(g => (g.Position - self.CenterPosition).HorizontalLengthSquared <= (reach + spell.Radius).LengthSquared).ToList();
			var (grave, count) = graves
				.Where(g => (g.Position - self.CenterPosition).HorizontalLengthSquared <= reach.LengthSquared)
				.Select(g => (Grave: g, Count: graves.Count(o => (o.Position - g.Position).HorizontalLengthSquared <= spell.Radius.LengthSquared)))
				.MaxByOrDefault(g => g.Count);

			return count >= Info.MinGraves ? Target.FromPos(grave.Position) : Target.Invalid;
		}

		// A caster about to die, in a fight, goes home.
		Target Flee(Actor self)
		{
			if (HealthShare(self) >= Info.FleeBelow || !InFight(self))
				return Target.Invalid;

			var home = PwBotUtils.Owned(world, player).FirstOrDefault(a => a.IsInWorld && PwBotUtils.IsDropOff(a.Info));
			if (home == null || (home.CenterPosition - self.CenterPosition).HorizontalLengthSquared < WDist.FromCells(10).LengthSquared)
				return Target.Invalid;

			return Target.FromPos(home.CenterPosition + new WVec(0, WDist.FromCells(3).Length, 0));
		}

		// An unarmed caster with nothing to cast keeps up with the bot's main group of units.
		void Follow(IBot bot, Actor caster)
		{
			if (caster.Info.HasTraitInfo<AttackBaseInfo>() || (!caster.IsIdle && !caster.Info.HasTraitInfo<AircraftInfo>()))
				return;

			if (caster.Info.HasTraitInfo<AircraftInfo>() && caster.CurrentActivity != null && caster.CurrentActivity is not FlyIdle)
				return;

			var army = PwBotUtils.Owned(world, player).Where(a => a.IsInWorld && PwBotUtils.IsArmy(a.Info)).ToList();
			if (army.Count == 0)
				return;

			var near = WDist.FromCells(5).LengthSquared;
			var group = army.MaxBy(a => army.Count(o => (o.CenterPosition - a.CenterPosition).HorizontalLengthSquared <= near));
			if ((group.CenterPosition - caster.CenterPosition).HorizontalLengthSquared <= WDist.FromCells(4).LengthSquared)
				return;

			bot.QueueOrder(new Order("Move", caster, Target.FromCell(world, group.Location), false));
		}

		IEnumerable<Actor> Visible(WPos centre, WDist range) =>
			world.FindActorsInCircle(centre, range)
				.Where(a => !a.IsDead && a.IsInWorld && a.TraitOrDefault<IHealth>() != null && a.CanBeViewedByPlayer(player));

		bool Enemy(Actor a) => player.RelationshipWith(a.Owner) == PlayerRelationship.Enemy && (Info.TargetWild || !a.Owner.NonCombatant);

		// An enemy unit near it.
		bool InFight(Actor unit) =>
			Visible(unit.CenterPosition, WDist.FromCells(Info.FightRadius)).Any(a => Enemy(a) && !PwBotUtils.IsBuilding(a.Info) && !Harmless(a));

		// An illusion (the Fake spell's copies): it can't hurt.
		static bool Harmless(Actor a) => a.TraitsImplementing<FirepowerMultiplier>().Any(f => !f.IsTraitDisabled && f.Info.Modifier == 0);

		static int HealthShare(Actor a)
		{
			var health = a.Trait<IHealth>();
			return health.HP * 100 / health.MaxHP;
		}

		static int Cost(Actor a) => PwBotUtils.Cost(a.Info);

		static bool Casting(Actor caster)
		{
			for (var activity = caster.CurrentActivity; activity != null; activity = activity.NextActivity)
				if (activity is CastSpell)
					return true;

			return false;
		}
	}
}
