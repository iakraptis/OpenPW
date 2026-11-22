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
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("A worker that builds (the Buildman's build list, .chr 0x240): ordered through the build palette ",
		"(PwBuildAt) it pays, places the site and walks there to build it; right-clicking an unfinished site of its ",
		"own (PwResumeBuild) sends it to continue. Ported from OpenSYW.")]
	public class BuilderInfo : TraitInfo
	{
		[Desc("Buildings this worker can build.")]
		public readonly string[] Types = System.Array.Empty<string>();

		[VoiceReference]
		public readonly string Voice = "Action";

		[Desc("The worker remains in the world and builds from EnterRange instead of entering the building.")]
		public readonly bool WorksFromRange = false;

		[Desc("Whether it can be ordered onto damaged friendly buildings to repair them (the Demon Insect can't, as in the",
			"original).")]
		public readonly bool Repairs = true;

		[Desc("Cost to fully repair a building as a percent of its value (OpenRA default: 20).")]
		public readonly int RepairPercent = 20;

		[Desc("Maximum HP repaired per step (OpenRA default: 7).")]
		public readonly int RepairStep = 7;

		[Desc("Ticks between repair steps (OpenRA default: 24).")]
		public readonly int RepairInterval = PwTime.Ticks(24);

		[Desc("How close the worker must come to repair a building.")]
		public readonly WDist RepairRange = WDist.FromCells(1);

		[GrantedConditionReference]
		[Desc("Condition granted while working from range (the Elf Wizard's casting body); none by default.")]
		public readonly string WorkCondition = null;

		public override object Create(ActorInitializer init) { return new Builder(this); }
	}

	public class Builder : IIssueOrder, IResolveOrder, IOrderVoice
	{
		public const string OrderID = "PwBuildAt";
		public const string ResumeOrderID = "PwResumeBuild";
		public const string RepairOrderID = "PwRepairBuilding";

		public readonly BuilderInfo Info;

		public Builder(BuilderInfo info)
		{
			Info = info;
		}

		public bool CanBuild(string actorType) { return System.Array.IndexOf(Info.Types, actorType) >= 0; }

		public bool CanResume(Actor self, Actor site)
		{
			if (site == null || site.IsDead || !site.IsInWorld || site.Owner != self.Owner || !CanBuild(site.Info.Name))
				return false;

			var underConstruction = site.TraitOrDefault<UnderConstruction>();
			return underConstruction != null && !underConstruction.IsComplete && !underConstruction.HasBuilder;
		}

		public bool CanRepair(Actor self, Actor target)
		{
			if (!Info.Repairs || target == null || target.IsDead || target.Disposed || !target.IsInWorld
				|| !target.AppearsFriendlyTo(self) || !target.Info.HasTraitInfo<BuildingInfo>()
				|| !target.Info.HasTraitInfo<IHealthInfo>() || target.GetDamageState() == DamageState.Undamaged)
				return false;

			var underConstruction = target.TraitOrDefault<UnderConstruction>();
			return underConstruction == null || underConstruction.IsComplete;
		}

		// The worker's footprint check: free, buildable cells for this building, and for a main building (AcceptsBerries)
		// a gap of BushClearance cells to every berry bush.
		public static bool CanPlace(World world, Actor builder, ActorInfo actorInfo, BuildingInfo buildingInfo, CPos cell)
		{
			if (!world.CanPlaceBuilding(cell, actorInfo, buildingInfo, builder))
				return false;

			var clearance = actorInfo.TraitInfoOrDefault<AcceptsBerriesInfo>()?.BushClearance ?? 0;
			return clearance <= 0 || !NearBush(world, buildingInfo.Tiles(cell).ToArray(), clearance);
		}

		// Whether a berry bush has a cell within clearance cells (diagonals included) of the footprint.
		static bool NearBush(World world, CPos[] footprint, int clearance)
		{
			var minX = footprint.Min(c => c.X) - clearance;
			var maxX = footprint.Max(c => c.X) + clearance;
			var minY = footprint.Min(c => c.Y) - clearance;
			var maxY = footprint.Max(c => c.Y) + clearance;
			var actors = world.ActorMap.ActorsInBox(world.Map.CenterOfCell(new CPos(minX - 1, minY - 1)), world.Map.CenterOfCell(new CPos(maxX + 1, maxY + 1)));
			foreach (var bush in actors.Where(a => a.Info.HasTraitInfo<BerryBushInfo>()))
				foreach (var (bushCell, _) in bush.OccupiesSpace.OccupiedCells())
					if (footprint.Any(f => Math.Max(Math.Abs(f.X - bushCell.X), Math.Abs(f.Y - bushCell.Y)) <= clearance))
						return true;

			return false;
		}

		IEnumerable<IOrderTargeter> IIssueOrder.Orders
		{
			get
			{
				yield return new ResumeTargeter(this);
				yield return new RepairTargeter(this);
			}
		}

		Order IIssueOrder.IssueOrder(Actor self, IOrderTargeter order, in Target target, bool queued)
		{
			return order.OrderID == ResumeOrderID || order.OrderID == RepairOrderID
				? new Order(order.OrderID, self, target, queued)
				: null;
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order)
		{
			return order.OrderString == OrderID || order.OrderString == ResumeOrderID || order.OrderString == RepairOrderID
				? Info.Voice
				: null;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == RepairOrderID)
			{
				if (order.Target.Type == TargetType.Actor && CanRepair(self, order.Target.Actor))
					self.QueueActivity(order.Queued, new RepairBuildingActivity(order.Target.Actor, this));

				return;
			}

			if (order.OrderString == ResumeOrderID)
			{
				if (order.Target.Type == TargetType.Actor && CanResume(self, order.Target.Actor))
					self.QueueActivity(order.Queued, new ConstructBuilding(order.Target.Actor, this));

				return;
			}

			if (order.OrderString != OrderID || !CanBuild(order.TargetString))
				return;

			var world = self.World;
			if (!world.Map.Rules.Actors.TryGetValue(order.TargetString, out var actorInfo))
				return;

			var buildingInfo = actorInfo.TraitInfoOrDefault<BuildingInfo>();
			var cell = world.Map.CellContaining(order.Target.CenterPosition);
			if (buildingInfo == null || !CanPlace(world, self, actorInfo, buildingInfo, cell))
				return;

			var buildable = actorInfo.TraitInfoOrDefault<BuildableInfo>();
			var techTree = self.Owner.PlayerActor.Trait<TechTree>();
			if (buildable != null && buildable.Prerequisites.Length > 0 && !techTree.HasPrerequisites(buildable.Prerequisites))
				return;

			var cost = actorInfo.TraitInfoOrDefault<ValuedInfo>()?.Cost ?? 0;
			if (!self.Owner.PlayerActor.Trait<PlayerResources>().TakeCash(cost, true))
				return;

			var faction = self.Owner.Faction.InternalName;
			world.AddFrameEndTask(w =>
			{
				var site = w.CreateActor(order.TargetString,
				[
					new LocationInit(cell),
					new OwnerInit(self.Owner),
					new FactionInit(faction),
					new HealthInit(1),
					new UnderConstructionInit(),
				]);

				self.QueueActivity(false, new ConstructBuilding(site, this));
			});
		}

		sealed class ResumeTargeter : UnitOrderTargeter
		{
			readonly Builder builder;

			public ResumeTargeter(Builder builder)
				: base(ResumeOrderID, 7, "repair", false, true)
			{
				this.builder = builder;
			}

			public override bool CanTargetActor(Actor self, Actor target, TargetModifiers modifiers, ref string cursor)
			{
				return builder.CanResume(self, target);
			}

			public override bool CanTargetFrozenActor(Actor self, FrozenActor target, TargetModifiers modifiers, ref string cursor)
			{
				return false;
			}
		}

		sealed class RepairTargeter : UnitOrderTargeter
		{
			readonly Builder builder;

			public RepairTargeter(Builder builder)
				: base(RepairOrderID, 7, "repair", false, true)
			{
				this.builder = builder;
			}

			public override bool CanTargetActor(Actor self, Actor target, TargetModifiers modifiers, ref string cursor) =>
				builder.CanRepair(self, target);

			public override bool CanTargetFrozenActor(Actor self, FrozenActor target, TargetModifiers modifiers, ref string cursor) =>
				false;
		}
	}
}
