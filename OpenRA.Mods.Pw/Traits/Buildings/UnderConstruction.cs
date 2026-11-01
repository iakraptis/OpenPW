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
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	// Added only by Builder when a worker places a site. Map and editor buildings have no worker coming, so without
	// this init they start finished.
	public class UnderConstructionInit : RuntimeFlagInit, ISingleInstanceInit { }

	[Desc("A building placed by a worker (Builder) starts at low HP. The worker walks up and goes inside it ",
		"(ConstructBuilding removes it from the world); the building grows while the worker is inside and is finished ",
		"after Duration ticks (.chr 0x18c), then lets the worker out beside it. If the site is destroyed meanwhile, the ",
		"worker is lost with it. Map and editor placed buildings start finished. Ported from OpenSYW.")]
	public class UnderConstructionInfo : TraitInfo, Requires<HealthInfo>
	{
		[FieldLoader.Require]
		[Desc("Ticks of work to finish the building (the original build time).")]
		public readonly int Duration = 0;

		[GrantedConditionReference]
		[Desc("Condition granted while unfinished: disable production, attacks and so on with it.")]
		public readonly string Condition = "constructing";

		[Desc("How close the worker must come to go inside.")]
		public readonly WDist EnterRange = WDist.FromCells(2);

		public override object Create(ActorInitializer init) { return new UnderConstruction(init, this); }
	}

	public class UnderConstruction : ITick, INotifyRemovedFromWorld
	{
		public readonly UnderConstructionInfo Info;
		readonly Health health;
		int worked;
		int conditionToken = Actor.InvalidConditionToken;

		public bool IsComplete { get; private set; }

		// The worker inside, building it, or null.
		public Actor Builder { get; private set; }

		public bool HasBuilder => Builder != null && !Builder.IsDead && !Builder.Disposed;

		// 0 when placed, 1 when finished.
		public float Progress => IsComplete ? 1f : (float)worked / Info.Duration;

		public UnderConstruction(ActorInitializer init, UnderConstructionInfo info)
		{
			Info = info;
			health = init.Self.Trait<Health>();
			if (init.GetOrDefault<UnderConstructionInit>() == null || info.Duration <= 0)
			{
				IsComplete = true;
				return;
			}

			if (!string.IsNullOrEmpty(info.Condition))
				conditionToken = init.Self.GrantCondition(info.Condition);
		}

		public bool TryAssignBuilder(Actor worker)
		{
			if (IsComplete || (HasBuilder && Builder != worker))
				return false;

			Builder = worker;
			return true;
		}

		void ITick.Tick(Actor self)
		{
			if (IsComplete || !HasBuilder)
				return;

			worked++;

			// Grow HP with the work done: from the 1 HP it was placed with to full.
			var targetHp = (int)((long)health.MaxHP * worked / Info.Duration);
			if (targetHp > health.HP)
				self.InflictDamage(self, new Damage(health.HP - targetHp));

			if (worked >= Info.Duration)
				Complete(self);
		}

		void Complete(Actor self)
		{
			IsComplete = true;
			if (health.HP < health.MaxHP)
				self.InflictDamage(self, new Damage(health.HP - health.MaxHP));

			if (conditionToken != Actor.InvalidConditionToken)
				conditionToken = self.RevokeCondition(conditionToken);

			ReleaseBuilder(self);
		}

		// The worker comes out on a free cell beside the building and announces it (.chr sound group 8).
		void ReleaseBuilder(Actor self)
		{
			if (Builder == null)
				return;

			var worker = Builder;
			Builder = null;
			self.World.AddFrameEndTask(w =>
			{
				if (worker.IsDead || worker.Disposed)
					return;

				var cell = FreeCellBeside(self, worker);
				w.Add(worker);
				worker.Trait<IPositionable>().SetPosition(worker, cell);
				foreach (var voiced in worker.TraitsImplementing<IVoiced>())
					voiced.PlayVoice(worker, "Built", worker.Owner.Faction.InternalName);
			});
		}

		static CPos FreeCellBeside(Actor building, Actor worker)
		{
			var positionable = worker.Trait<IPositionable>();
			var candidates = Util.AdjacentCells(building.World, Target.FromActor(building))
				.OrderBy(c => (c - building.Location).LengthSquared);

			foreach (var cell in candidates)
				if (positionable.CanEnterCell(cell))
					return cell;

			return building.Location;
		}

		// Destroyed with the worker inside: there is no outside to put it, so it is lost with the building.
		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			if (Builder != null && !Builder.IsDead && !Builder.Disposed)
				Builder.Dispose();

			Builder = null;
		}
	}
}
