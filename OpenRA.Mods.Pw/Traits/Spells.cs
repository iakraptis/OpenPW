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
using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	public enum PwSpellTarget { Unit, Ground, Self }

	[Desc("A Demon spell (JW2_11 records, docs/game/rules.md): cast from the command panel (order PwSpell, TargetString = ",
		"Name) on an enemy unit, a place or the caster itself, once the caster has MinLevel. It costs Cost kill points ",
		"(PwKillPoints: health; the caster needs more than that), and its effect grows with the caster's spell power ",
		"(PwLevels.SpellPower). What it does: Damage to the units within Radius every StepInterval ticks for Steps steps; ",
		"PetrifyTicks of stone for the target unit; Summon units around the caster; KillsCaster.")]
	public class PwSpellInfo : TraitInfo, Requires<IHealthInfo>
	{
		[FieldLoader.Require]
		[Desc("Identifies the spell in its order; also the fluent label spell-<Name> and tooltip spell-<Name>-desc.")]
		public readonly string Name = null;

		public readonly PwSpellTarget Target = PwSpellTarget.Ground;

		[Desc("Kill points spent (spell record 0x1e0).")]
		public readonly int Cost = 0;

		[Desc("Lowest caster level that can cast it.")]
		public readonly int MinLevel = 0;

		public readonly WDist Range = WDist.FromCells(6);

		[Desc("Damage per step to each unit within Radius (record 0x1e8, raised by spell power).")]
		public readonly int Damage = 0;

		[Desc("Damage steps (1 = one blow).")]
		public readonly int Steps = 1;

		[Desc("Ticks between steps (the effect's frame delay, record 0x21c).")]
		public readonly int StepInterval = 4;

		[Desc("Ticks before the first step (the effect frame that hits, record 0x8bc, times StepInterval).")]
		public readonly int Delay = 0;

		[Desc("Radius of the damage (record 0x1f8).")]
		public readonly WDist Radius = WDist.Zero;

		[Desc("Hurt only living units (PwLivingInfo), not machines or buildings.")]
		public readonly bool LivingOnly = false;

		[Desc("Hurt the caster's own and allied units too.")]
		public readonly bool AffectsAllies = false;

		[Desc("Ticks the target unit is turned to stone (raised by spell power): it can't move or attack.")]
		public readonly int PetrifyTicks = 0;

		[Desc("Condition the target's ExternalCondition grants while it is stone.")]
		public readonly string PetrifyCondition = "petrified";

		[ActorReference]
		[Desc("Units raised around the caster, Summon[i] for the i-th (repeating).")]
		public readonly string[] Summon = Array.Empty<string>();

		public readonly int SummonCount = 0;

		[Desc("The caster dies casting it (Suicide).")]
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

	public class PwSpell : IResolveOrder, IOrderVoice
	{
		public const string OrderID = "PwSpell";

		public readonly PwSpellInfo Info;

		public PwSpell(PwSpellInfo info)
		{
			Info = info;
		}

		public static Order Cast(Actor self, string name, Target target) =>
			new(OrderID, self, target, false) { TargetString = name };

		public bool IsUnlocked(Actor self) => (self.TraitOrDefault<PwLevels>()?.Level ?? 0) >= Info.MinLevel;

		// Casting takes its cost from the caster's kill points, which must stay above zero.
		public bool IsReady(Actor self) => IsUnlocked(self) && self.Trait<IHealth>().HP > Info.Cost;

		public static bool CanTarget(Actor self, Actor target) =>
			target != null && !target.IsDead && target.IsInWorld && target.Info.HasTraitInfo<IHealthInfo>()
			&& !target.Info.HasTraitInfo<BuildingInfo>() && self.Owner.RelationshipWith(target.Owner) != PlayerRelationship.Ally;

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

		// Called by CastSpell once the caster is in range, facing the target.
		public void Release(Actor self, Target target)
		{
			if (!IsReady(self))
				return;

			var health = self.Trait<IHealth>();
			if (Info.Cost > 0)
				health.InflictDamage(self, self, new Damage(Info.Cost), true);

			var pos = target.CenterPosition;
			var world = self.World;
			if (Info.Sound != null)
				Game.Sound.Play(SoundType.World, Info.Sound, pos);

			if (Info.Image != null)
			{
				var victim = Info.Target == PwSpellTarget.Unit ? target.Actor : null;
				var image = Info.Image;
				world.AddFrameEndTask(w => w.Add(victim != null
					? new SpriteEffect(victim, w, image, Info.EffectSequence, Info.EffectPalette)
					: new SpriteEffect(pos, w, image, Info.EffectSequence, Info.EffectPalette)));
			}

			if (Info.PetrifyTicks > 0 && target.Type == TargetType.Actor)
			{
				var stone = target.Actor.TraitsImplementing<ExternalCondition>()
					.FirstOrDefault(c => c.Info.Condition == Info.PetrifyCondition && c.CanGrantCondition(self));
				stone?.GrantCondition(target.Actor, self, Power(self, Info.PetrifyTicks));
			}

			if (Info.Damage != 0 && Info.Radius > WDist.Zero)
			{
				// A blow without delay lands at once (Suicide: before the caster dies).
				var damage = Power(self, Info.Damage);
				for (var step = 0; step < Info.Steps; step++)
				{
					var delay = Info.Delay + step * Info.StepInterval;
					if (delay == 0)
						Strike(self, pos, damage);
					else
						world.AddFrameEndTask(w => w.Add(new OpenRA.Effects.DelayedAction(delay, () => Strike(self, pos, damage))));
				}
			}

			if (Info.SummonCount > 0 && Info.Summon.Length > 0)
				world.AddFrameEndTask(w => Raise(self, w));

			if (Info.KillsCaster)
				world.AddFrameEndTask(w => { if (!self.IsDead) self.Kill(self); });
		}

		// One step of damage to what stands within the radius (the caster too, for a spell that hurts allies).
		void Strike(Actor self, WPos pos, int damage)
		{
			foreach (var a in self.World.FindActorsInCircle(pos, Info.Radius).ToList())
			{
				if (a.IsDead || !a.IsInWorld || a.TraitOrDefault<IHealth>() == null)
					continue;

				if (Info.LivingOnly && !a.Info.HasTraitInfo<PwLivingInfo>())
					continue;

				if (!Info.AffectsAllies && self.Owner.RelationshipWith(a.Owner) == PlayerRelationship.Ally)
					continue;

				// A caster that is gone (killed meanwhile) can't take the credit.
				a.InflictDamage(self.Disposed ? a : self, new Damage(damage));
			}
		}

		// The raised units appear on free cells around the caster, as its owner's.
		void Raise(Actor self, World w)
		{
			if (self.IsDead || !self.IsInWorld)
				return;

			var taken = new System.Collections.Generic.HashSet<CPos>();
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
				w.CreateActor(type, new TypeDictionary
				{
					new LocationInit(cell.Value),
					new OwnerInit(self.Owner),
					new FacingInit(self.TraitOrDefault<IFacing>()?.Facing ?? WAngle.Zero),
				});
			}
		}
	}
}
