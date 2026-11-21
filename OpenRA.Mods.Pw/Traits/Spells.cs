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
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	public enum PwSpellTarget { Unit, Ground, Self }

	[Desc("A spell (JW2_11 records, docs/game/rules.md): cast from the command panel (order PwSpell, TargetString = Name) ",
		"on a unit, a place or the caster itself, once the caster has MinLevel and its owner Prerequisites (record ",
		"0x160, 0x15c). It costs Cost kill points (Demon: health, PwKillPoints; the caster needs more than that) and ",
		"ManaCost mana (PwMana). Its effect grows with the caster's spell power (PwLevels.SpellPower). What it does, in ",
		"any mix: Damage (or Heal) every StepInterval ticks for Steps steps, to the target unit or, with a Radius, to ",
		"the units around the target (at a random point within Scatter of it each step); Shield; PetrifyTicks of ",
		"stone; Teleport the caster; a jump portal (PortalTicks); an Illusion of the target unit; Resurrect the dead; ",
		"Summon units; KillsCaster.")]
	public class PwSpellInfo : TraitInfo, Requires<IHealthInfo>
	{
		[FieldLoader.Require]
		[Desc("Identifies the spell in its order; also the fluent label spell-<Name> and tooltip spell-<Name>-desc.")]
		public readonly string Name = null;

		public readonly PwSpellTarget Target = PwSpellTarget.Ground;

		[Desc("Whose units it can be aimed at (Target: Unit).")]
		public readonly PlayerRelationship TargetRelationships = PlayerRelationship.Enemy | PlayerRelationship.Neutral;

		[Desc("Whose units its area effects reach.")]
		public readonly PlayerRelationship AffectsRelationships = PlayerRelationship.Enemy | PlayerRelationship.Neutral;

		[Desc("While the caster is idle, it heals on its own (Cure): the unit last cast on while it is hurt and in Range,",
			"otherwise the most hurt one it can target, Heal HP for ManaPerStep mana every tick.")]
		public readonly bool AutoHeal = false;

		[Desc("Kill points spent (spell record 0x1e0).")]
		public readonly int Cost = 0;

		[Desc("Mana spent (record 0x1e4).")]
		public readonly int ManaCost = 0;

		[Desc("Mana spent on each step (Cure: it goes on while the mana lasts).")]
		public readonly int ManaPerStep = 0;

		[Desc("Lowest caster level that can cast it (record 0x160).")]
		public readonly int MinLevel = 0;

		[Desc("Research its owner needs (record 0x15c).")]
		public readonly string[] Prerequisites = Array.Empty<string>();

		public readonly WDist Range = WDist.FromCells(6);

		[Desc("Damage per step (record 0x1e8, raised by spell power).")]
		public readonly int Damage = 0;

		[Desc("Health given back per step (record 0x1e8 when positive, raised by spell power).")]
		public readonly int Heal = 0;

		[Desc("Steps (1 = one blow).")]
		public readonly int Steps = 1;

		[Desc("Ticks between steps. Spell art advances one frame per tick, so a blow on every frame is 1 (record 0x8bc ",
			"lists the frames that hit); a jump portal throws every 4.")]
		public readonly int StepInterval = 1;

		[Desc("Ticks before the first step: the effect frame that hits (record 0x8bc), one frame per tick.")]
		public readonly int Delay = 0;

		[Desc("Each step also waits a random 0 to RandomDelay - 1 ticks (Meteor: each meteor falls after a random ",
			"1 to 0x1f0 ticks).")]
		public readonly int RandomDelay = 0;

		[Desc("The effect art is played again each time it ends until this many ticks have passed (Noxious gas: its ",
			"cloud loops for its duration, record 0x1f4). 0: once.")]
		public readonly int EffectTicks = 0;

		[Desc("Radius of the area (record 0x1f8). Zero: the target unit only.")]
		public readonly WDist Radius = WDist.Zero;

		[Desc("Each step lands at a random point this far from the target (Meteor).")]
		public readonly WDist Scatter = WDist.Zero;

		[Desc("Reach only living units (PwLivingInfo), not machines or buildings.")]
		public readonly bool LivingOnly = false;

		[Desc("Damage a shield (PwShield) soaks up for each unit it reaches (raised by spell power).")]
		public readonly int Shield = 0;

		[Desc("Ticks the shield lasts; 0 until used up.")]
		public readonly int ShieldTicks = 0;

		[Desc("Ticks the target unit is turned to stone (raised by spell power): it can't move or attack.")]
		public readonly int PetrifyTicks = 0;

		[Desc("Condition the target's ExternalCondition grants while it is stone.")]
		public readonly string PetrifyCondition = "petrified";

		[Desc("The caster moves to the target place at once.")]
		public readonly bool Teleport = false;

		[Desc("Ticks a jump portal stays open at the target place (raised by spell power): enemy units that come within ",
			"Radius are thrown PortalThrow away.")]
		public readonly int PortalTicks = 0;

		public readonly WDist PortalThrow = WDist.FromCells(12);

		[Desc("A copy of the target unit appears beside it, its owner the caster's. It is granted IllusionCondition, with ",
			"which the rules take its firepower away and make it vanish after a while (^Unit).")]
		public readonly bool Illusion = false;

		[Desc("Condition the copy's ExternalCondition grants.")]
		public readonly string IllusionCondition = "illusion";

		[Desc("The living units that died within Radius of the target place in the last ResurrectAge ticks come back, ",
			"the caster's own, with ResurrectHealth percent of their health (PwGraveyard).")]
		public readonly bool Resurrect = false;

		public readonly int ResurrectAge = 1500;

		public readonly int ResurrectHealth = 50;

		[ActorReference]
		[Desc("Units raised around the caster, Summon[i] for the i-th (repeating).")]
		public readonly string[] Summon = Array.Empty<string>();

		public readonly int SummonCount = 0;

		[Desc("The caster dies casting it (Suicide, Meteor).")]
		public readonly bool KillsCaster = false;

		[Desc("Image of the effect (sequence EffectSequence).")]
		public readonly string Image = null;

		[Desc("Image whose icon sequence is the button.")]
		public readonly string Icon = null;

		public readonly string EffectSequence = "effect";

		public readonly string EffectPalette = "effect";

		public readonly string Sound = null;

		[VoiceReference]
		public readonly string Voice = "Action";

		public override object Create(ActorInitializer init) { return new PwSpell(this); }
	}

	public class PwSpell : IResolveOrder, IOrderVoice, ITick
	{
		public const string OrderID = "PwSpell";

		public readonly PwSpellInfo Info;
		Actor autoHealTarget;

		// AutoHeal: the unit the spell was last cast on, healed first.
		Actor preferred;

		public PwSpell(PwSpellInfo info)
		{
			Info = info;
		}

		public static Order Cast(Actor self, string name, Target target) =>
			new(OrderID, self, target, false) { TargetString = name };

		public bool IsUnlocked(Actor self) => !self.Disposed
			&& (self.TraitOrDefault<PwLevels>()?.Level ?? 0) >= Info.MinLevel
			&& (Info.Prerequisites.Length == 0 || self.Owner.PlayerActor.Trait<TechTree>().HasPrerequisites(Info.Prerequisites));

		// Kill points must stay above zero; mana must cover the cost.
		public bool IsReady(Actor self) => IsUnlocked(self) && self.Trait<IHealth>().HP > Info.Cost
			&& (Info.ManaCost + Info.ManaPerStep == 0 || (self.TraitOrDefault<PwMana>()?.Mana ?? 0) >= Math.Max(Info.ManaCost, Info.ManaPerStep));

		public bool CanTarget(Actor self, Actor target) =>
			target != null && !target.IsDead && target.IsInWorld && target.Info.HasTraitInfo<IHealthInfo>()
			&& !target.Info.HasTraitInfo<BuildingInfo>() && Info.TargetRelationships.HasRelationship(self.Owner.RelationshipWith(target.Owner));

		static int Power(Actor self, int value) => value + value * (self.TraitOrDefault<PwLevels>()?.SpellPower ?? 0) / 100;

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderID || order.TargetString != Info.Name || !IsReady(self))
				return;

			var target = Info.Target == PwSpellTarget.Self ? Target.FromActor(self) : order.Target;
			if (Info.Target == PwSpellTarget.Unit && (target.Type != TargetType.Actor || !CanTarget(self, target.Actor)))
				return;

			if (Info.Target == PwSpellTarget.Ground && target.Type == TargetType.Invalid)
				return;

			self.QueueActivity(order.Queued, new CastSpell(self, this, target));
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order) =>
			order.OrderString == OrderID && order.TargetString == Info.Name ? Info.Voice : null;

		void ITick.Tick(Actor self)
		{
			if (!Info.AutoHeal || !self.IsIdle || !IsReady(self))
			{
				autoHealTarget = null;
				return;
			}

			bool Hurt(Actor a) => CanTarget(self, a) && a.TraitOrDefault<IHealth>() is IHealth health && health.HP < health.MaxHP;
			if (preferred != null && (!Hurt(preferred)
				|| (preferred.CenterPosition - self.CenterPosition).HorizontalLengthSquared > Info.Range.LengthSquared))
				preferred = null;

			var target = preferred ?? self.World.FindActorsInCircle(self.CenterPosition, Info.Range)
				.Where(Hurt)
				.OrderBy(a => (long)a.Trait<IHealth>().HP * 100 / a.Trait<IHealth>().MaxHP)
				.FirstOrDefault();

			if (target == null)
			{
				autoHealTarget = null;
				return;
			}

			// The cue when it turns to a new patient, only where the viewer can see it.
			var viewer = self.World.RenderPlayer;
			if (autoHealTarget != target && (viewer == null || target.CanBeViewedByPlayer(viewer)))
			{
				autoHealTarget = target;
				if (Info.Sound != null)
					Game.Sound.Play(SoundType.World, Info.Sound, target.CenterPosition);

				if (Info.Image != null)
					ShowEffect(self.World, target, target.CenterPosition);
			}

			var mana = self.TraitOrDefault<PwMana>();
			if (mana?.Spend(Info.ManaPerStep) != true)
				return;

			var health = target.Trait<IHealth>();
			var heal = Power(self, Info.Heal);
			health.InflictDamage(target, self, new Damage(-Math.Min(heal, health.MaxHP - health.HP)), true);
		}

		// Called by CastSpell once the caster is in range, facing the target.
		public void Release(Actor self, Target target)
		{
			if (!IsReady(self))
				return;

			if (Info.Cost > 0)
				self.Trait<IHealth>().InflictDamage(self, self, new Damage(Info.Cost), true);

			if (Info.ManaCost > 0)
				self.Trait<PwMana>().Spend(Info.ManaCost);

			var pos = target.CenterPosition;
			var victim = Info.Target == PwSpellTarget.Unit ? target.Actor : null;
			if (Info.AutoHeal && victim != null)
				preferred = victim;

			var world = self.World;
			if (Info.Sound != null)
				Game.Sound.Play(SoundType.World, Info.Sound, pos);

			if (Info.Image != null && Info.PortalTicks == 0)
			{
				ShowEffect(world, victim, pos);

				// Looped for EffectTicks: again each time the art ends.
				var sequence = world.Map.Sequences.GetSequence(Info.Image, Info.EffectSequence);
				var length = Math.Max(1, sequence.Length * sequence.Tick / 40);
				for (var t = length; t < Info.EffectTicks; t += length)
				{
					var delay = t;
					world.AddFrameEndTask(w => w.Add(new OpenRA.Effects.DelayedAction(delay, () => ShowEffect(w, victim, pos))));
				}
			}

			if (victim != null && Info.PetrifyTicks > 0)
			{
				var stone = victim.TraitsImplementing<ExternalCondition>()
					.FirstOrDefault(c => c.Info.Condition == Info.PetrifyCondition && c.CanGrantCondition(self));
				stone?.GrantCondition(victim, self, Power(self, Info.PetrifyTicks));
			}

			if (Info.Shield > 0)
				foreach (var a in Reached(self, victim, pos))
					a.TraitOrDefault<PwShield>()?.Grant(Power(self, Info.Shield), Info.ShieldTicks);

			if (Info.Damage != 0 || Info.Heal != 0)
			{
				var damage = Power(self, Info.Damage);
				var heal = Power(self, Info.Heal);
				for (var step = 0; step < Info.Steps; step++)
				{
					// A blow without delay lands at once (Suicide: before the caster dies).
					var delay = Info.Delay + step * Info.StepInterval + (Info.RandomDelay > 0 ? world.SharedRandom.Next(Info.RandomDelay) : 0);
					if (delay == 0)
						Step(self, victim, pos, damage, heal);
					else
						world.AddFrameEndTask(w => w.Add(new OpenRA.Effects.DelayedAction(delay, () => Step(self, victim, pos, damage, heal))));
				}
			}

			if (Info.Teleport)
				world.AddFrameEndTask(w => TeleportCaster(self, pos));

			if (Info.PortalTicks > 0)
				OpenPortal(self, pos, Power(self, Info.PortalTicks));

			if (Info.Illusion && victim != null)
				world.AddFrameEndTask(w => MakeIllusion(self, victim, w));

			if (Info.Resurrect)
				world.AddFrameEndTask(w => RaiseDead(self, pos, w));

			if (Info.SummonCount > 0 && Info.Summon.Length > 0)
				world.AddFrameEndTask(w => Raise(self, w));

			if (Info.KillsCaster)
				world.AddFrameEndTask(w => { if (!self.IsDead) self.Kill(self); });
		}

		void ShowEffect(World world, Actor victim, WPos pos)
		{
			var image = Info.Image;
			world.AddFrameEndTask(w => w.Add(victim != null
				? new SpriteEffect(victim, w, image, Info.EffectSequence, Info.EffectPalette)
				: new SpriteEffect(pos, w, image, Info.EffectSequence, Info.EffectPalette)));
		}

		// The units an effect reaches: the target unit, or those within the radius of the place.
		IEnumerable<Actor> Reached(Actor self, Actor victim, WPos pos)
		{
			if (Info.Radius == WDist.Zero)
				return victim != null && !victim.IsDead && victim.IsInWorld ? new[] { victim } : Enumerable.Empty<Actor>();

			return self.World.FindActorsInCircle(pos, Info.Radius)
				.Where(a => !a.IsDead && a.IsInWorld && a.TraitOrDefault<IHealth>() != null
					&& (!Info.LivingOnly || a.Info.HasTraitInfo<PwLivingInfo>())
					&& Info.AffectsRelationships.HasRelationship(self.Owner.RelationshipWith(a.Owner)))
				.ToList();
		}

		void Step(Actor self, Actor victim, WPos pos, int damage, int heal)
		{
			// Cure goes on while the caster lives and has the mana.
			if (Info.ManaPerStep > 0 && (self.IsDead || self.TraitOrDefault<PwMana>()?.Spend(Info.ManaPerStep) != true))
				return;

			if (Info.Scatter > WDist.Zero)
			{
				var r = self.World.SharedRandom;
				var s = Info.Scatter.Length;
				pos += new WVec(r.Next(-s, s + 1), r.Next(-s, s + 1), 0);
				if (Info.Image != null)
					ShowEffect(self.World, null, pos);
			}

			foreach (var a in Reached(self, victim, pos))
			{
				// A caster that is gone (killed meanwhile) can't take the credit.
				var by = self.Disposed ? a : self;
				if (damage > 0)
					a.InflictDamage(by, new Damage(damage));

				var health = a.Trait<IHealth>();
				if (heal > 0 && health.HP < health.MaxHP)
					health.InflictDamage(a, by, new Damage(-Math.Min(heal, health.MaxHP - health.HP)), true);
			}
		}

		static void TeleportCaster(Actor self, WPos pos)
		{
			if (self.IsDead || !self.IsInWorld)
				return;

			var cell = self.World.Map.CellContaining(pos);
			var positionable = self.Trait<IPositionable>();
			if (self.Info.HasTraitInfo<AircraftInfo>())
				positionable.SetPosition(self, self.World.Map.CenterOfCell(cell) + new WVec(0, 0, self.CenterPosition.Z));
			else if (FreeCell(self, positionable, cell) is CPos free)
				positionable.SetPosition(self, free);
			else
				return;

			self.CancelActivity();
		}

		static CPos? FreeCell(Actor self, IPositionable positionable, CPos near) =>
			self.World.Map.FindTilesInCircle(near, 4).Where(c => positionable.CanEnterCell(c)).Select(c => (CPos?)c).FirstOrDefault();

		// The portal throws the enemies that walk into it away, each step, while it is open.
		void OpenPortal(Actor self, WPos pos, int ticks)
		{
			var world = self.World;
			var owner = self.Owner;
			for (var t = 0; t < ticks; t += Info.StepInterval)
			{
				var delay = t + 1;
				var show = t % 40 == 0 && Info.Image != null;
				world.AddFrameEndTask(w => w.Add(new OpenRA.Effects.DelayedAction(delay, () =>
				{
					if (show)
						w.Add(new SpriteEffect(pos, w, Info.Image, Info.EffectSequence, Info.EffectPalette));

					foreach (var a in w.FindActorsInCircle(pos, Info.Radius).ToList())
					{
						if (a.IsDead || !a.IsInWorld || a.Info.HasTraitInfo<BuildingInfo>() || a.TraitOrDefault<Mobile>() is not Mobile mobile
							|| !Info.AffectsRelationships.HasRelationship(owner.RelationshipWith(a.Owner)))
							continue;

						var r = w.SharedRandom;
						var angle = new WAngle(r.Next(1024));
						var away = w.Map.CellContaining(pos + new WVec(0, -Info.PortalThrow.Length, 0).Rotate(WRot.FromYaw(angle)));
						if (FreeCell(a, mobile, away) is CPos cell)
						{
							a.CancelActivity();
							mobile.SetPosition(a, cell);
						}
					}
				})));
			}
		}

		// A harmless copy of the target, the caster's own, which vanishes after a while.
		void MakeIllusion(Actor self, Actor original, World w)
		{
			if (original.IsDead || !original.IsInWorld)
				return;

			var mobile = original.Info.TraitInfoOrDefault<MobileInfo>();
			var cell = w.Map.FindTilesInAnnulus(original.Location, 1, 3)
				.Where(c => mobile == null || mobile.CanEnterCell(w, null, c))
				.Select(c => (CPos?)c)
				.FirstOrDefault();
			if (cell == null)
				return;

			var copy = w.CreateActor(original.Info.Name,
			[
				new LocationInit(cell.Value),
				new CenterPositionInit(w.Map.CenterOfCell(cell.Value) + new WVec(0, 0, original.CenterPosition.Z)),
				new OwnerInit(self.Owner),
				new FacingInit(original.TraitOrDefault<IFacing>()?.Facing ?? WAngle.Zero),
			]);
			copy.TraitsImplementing<ExternalCondition>()
				.FirstOrDefault(c => c.Info.Condition == Info.IllusionCondition && c.CanGrantCondition(self))
				?.GrantCondition(copy, self);
		}

		// The recent dead around the place rise, the caster's own.
		void RaiseDead(Actor self, WPos pos, World w)
		{
			if (self.IsDead || !self.IsInWorld)
				return;

			foreach (var grave in w.WorldActor.Trait<PwGraveyard>().Take(pos, Info.Radius, w.WorldTick - Info.ResurrectAge))
			{
				var mobile = w.Map.Rules.Actors[grave.Type].TraitInfoOrDefault<MobileInfo>();
				var cell = w.Map.FindTilesInCircle(grave.Cell, 3)
					.Where(c => mobile == null || mobile.CanEnterCell(w, null, c))
					.Select(c => (CPos?)c)
					.FirstOrDefault();
				if (cell == null)
					continue;

				w.CreateActor(grave.Type,
				[
					new LocationInit(cell.Value),
					new OwnerInit(self.Owner),
					new FacingInit(grave.Facing),
					new HealthInit(Info.ResurrectHealth),
				]);
			}
		}

		// The raised units appear on free cells around the caster, as its owner's.
		void Raise(Actor self, World w)
		{
			if (self.IsDead || !self.IsInWorld)
				return;

			var taken = new HashSet<CPos>();
			for (var i = 0; i < Info.SummonCount; i++)
			{
				var type = Info.Summon[i % Info.Summon.Length];
				var mobile = w.Map.Rules.Actors[type].TraitInfoOrDefault<MobileInfo>();
				var cell = w.Map.FindTilesInAnnulus(self.Location, 1, 4)
					.Where(c => !taken.Contains(c) && (mobile == null || mobile.CanEnterCell(w, null, c)))
					.Select(c => (CPos?)c)
					.FirstOrDefault();
				if (cell == null)
					return;

				taken.Add(cell.Value);
				w.CreateActor(type,
				[
					new LocationInit(cell.Value),
					new OwnerInit(self.Owner),
					new FacingInit(self.TraitOrDefault<IFacing>()?.Facing ?? WAngle.Zero),
				]);
			}
		}
	}

	[Desc("Soaks up damage (Shield, Mass temper: PwSpell.Shield): while its pool lasts, hits do nothing and drain it; the ",
		"hit that empties it is absorbed too (C, Pw.exe FUN_00457d4b, unit flag 0x100). Grants Condition meanwhile.")]
	public class PwShieldInfo : TraitInfo
	{
		[GrantedConditionReference]
		public readonly string Condition = "shielded";

		public override object Create(ActorInitializer init) { return new PwShield(init.Self, this); }
	}

	public class PwShield : IDamageModifier, ITick
	{
		readonly Actor actor;
		readonly PwShieldInfo info;
		int ticks, token = Actor.InvalidConditionToken;

		public PwShield(Actor actor, PwShieldInfo info)
		{
			this.actor = actor;
			this.info = info;
		}

		public int Pool { get; private set; }

		// A new shield replaces a weaker one; ticks 0 lasts until used up.
		public void Grant(int amount, int duration)
		{
			if (amount < Pool)
				return;

			Pool = amount;
			ticks = duration;
			if (token == Actor.InvalidConditionToken)
				token = actor.GrantCondition(info.Condition);
		}

		void End()
		{
			Pool = 0;
			if (token != Actor.InvalidConditionToken)
				token = actor.RevokeCondition(token);
		}

		int IDamageModifier.GetDamageModifier(Actor attacker, Damage damage)
		{
			if (Pool <= 0 || damage == null || damage.Value <= 0)
				return 100;

			Pool -= damage.Value;
			if (Pool <= 0)
				End();

			return 0;
		}

		void ITick.Tick(Actor self)
		{
			if (Pool > 0 && ticks > 0 && --ticks == 0)
				End();
		}
	}

	[Desc("Attach to the world: the living units that died lately, which Resurrect (PwSpell) brings back.")]
	public class PwGraveyardInfo : TraitInfo
	{
		[Desc("Deaths kept, the oldest forgotten first.")]
		public readonly int Capacity = 200;

		public override object Create(ActorInitializer init) { return new PwGraveyard(this); }
	}

	public class PwGraveyard
	{
		public readonly struct Grave
		{
			public readonly string Type;
			public readonly CPos Cell;
			public readonly WPos Position;
			public readonly WAngle Facing;
			public readonly int Tick;

			public Grave(string type, CPos cell, WPos position, WAngle facing, int tick)
			{
				Type = type;
				Cell = cell;
				Position = position;
				Facing = facing;
				Tick = tick;
			}
		}

		readonly PwGraveyardInfo info;
		readonly List<Grave> graves = [];

		public PwGraveyard(PwGraveyardInfo info)
		{
			this.info = info;
		}

		public void Bury(Actor dead)
		{
			graves.Add(new Grave(dead.Info.Name, dead.Location, dead.CenterPosition,
				dead.TraitOrDefault<IFacing>()?.Facing ?? WAngle.Zero, dead.World.WorldTick));
			if (graves.Count > info.Capacity)
				graves.RemoveAt(0);
		}

		// The graves dug no earlier than the tick (the AI looks where to raise the dead).
		public IEnumerable<Grave> Recent(int since) => graves.Where(g => g.Tick >= since);

		// The graves within the radius of the place, dug no earlier than the tick; they are emptied.
		public List<Grave> Take(WPos pos, WDist radius, int since)
		{
			var taken = graves.Where(g => g.Tick >= since && (g.Position - pos).HorizontalLengthSquared <= radius.LengthSquared).ToList();
			graves.RemoveAll(taken.Contains);
			return taken;
		}
	}
}
