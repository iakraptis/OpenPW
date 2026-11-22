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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("Tyrano merging (docs/game/rules.md): this unit can combine with others into the type Into, by the recipe in ",
		"Into's PwMerged.From. The command panel's button sends order PwMerge to the ingredients, which meet and become ",
		"one Into (MergeUnits).")]
	public class PwMergeInfo : TraitInfo, Requires<IMoveInfo>
	{
		[ActorReference(typeof(PwMergedInfo))]
		[FieldLoader.Require]
		[Desc("The merged type.")]
		public readonly string Into = null;

		[VoiceReference]
		public readonly string Voice = "Action";

		public override object Create(ActorInitializer init) { return new PwMerge(this); }
	}

	public class PwMerge : IResolveOrder, IOrderVoice
	{
		public const string OrderID = "PwMerge";

		// Ticks the units have to meet before the merge is given up.
		const int MeetTimeout = 400 * PwTime.TicksPerOriginal;

		public readonly PwMergeInfo Info;

		public PwMerge(PwMergeInfo info)
		{
			Info = info;
		}

		// Sent to one ingredient, the others in ExtraActors.
		public static Order Merge(Actor[] units) => new(OrderID, units[0], false) { ExtraActors = units.Skip(1).ToArray() };

		// The recipe's ingredients from these units (each type of PwMerged.From as many times as it is listed), or null.
		public static Actor[] Pick(IEnumerable<Actor> units, string into, PwMergedInfo recipe)
		{
			var left = units.Where(a => !a.IsDead && a.IsInWorld && a.Info.TraitInfoOrDefault<PwMergeInfo>()?.Into == into).ToList();
			var picked = new List<Actor>();
			foreach (var type in recipe.From)
			{
				var unit = left.FirstOrDefault(a => a.Info.Name == type);
				if (unit == null)
					return null;

				picked.Add(unit);
				left.Remove(unit);
			}

			return picked.ToArray();
		}

		public static bool CanMerge(Player owner, PwMergedInfo recipe) =>
			owner.PlayerActor.Trait<TechTree>().HasPrerequisites(recipe.Prerequisites);

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderID)
				return;

			var rules = self.World.Map.Rules;
			var recipe = rules.Actors[Info.Into].TraitInfo<PwMergedInfo>();
			var units = new[] { self }.Concat(order.ExtraActors ?? Array.Empty<Actor>()).Where(a => a.Owner == self.Owner).Distinct();
			var parts = Pick(units, Info.Into, recipe);
			if (parts == null || !CanMerge(self.Owner, recipe))
				return;

			// They meet at the one nearest their middle.
			var middle = new WPos(parts.Sum(p => p.CenterPosition.X) / parts.Length, parts.Sum(p => p.CenterPosition.Y) / parts.Length, 0);
			var meeting = parts.MinBy(p => (p.CenterPosition - middle).HorizontalLengthSquared).Location;
			var group = new MergeGroup(parts, Info.Into, meeting, self.World.WorldTick + MeetTimeout);
			foreach (var p in parts)
				p.QueueActivity(false, new MergeUnits(p, group));
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order) =>
			order.OrderString == OrderID ? Info.Voice : null;
	}

	[Desc("A merged Tyrano unit (TwinVelocis, TwinRhamphos, Mutant): made from the units of From (PwMerge), which it keeps ",
		"and lets go again when split (order PwSplit, the command panel's button; C, Pw.exe 0x45ff60). It starts with ",
		"their mean health and lowest level; split, they come back with its health share and their own levels.")]
	public class PwMergedInfo : TraitInfo
	{
		[ActorReference(typeof(PwMergeInfo))]
		[FieldLoader.Require]
		[Desc("The units it is made from, a type once per unit.")]
		public readonly string[] From = Array.Empty<string>();

		[Desc("Needed to merge (the result's own requirements in the original: a building or a research).")]
		public readonly string[] Prerequisites = Array.Empty<string>();

		[Desc("The image whose icon sequence is the merge button.")]
		public readonly string Image = "command-merge";

		[VoiceReference]
		public readonly string Voice = "Action";

		public override object Create(ActorInitializer init) { return new PwMerged(); }
	}

	public class PwMerged : IResolveOrder, IOrderVoice, INotifyKilled, INotifyActorDisposing
	{
		public const string OrderID = "PwSplit";

		readonly List<Actor> parts = [];

		public IReadOnlyList<Actor> Parts => parts;

		public static Order Split(Actor self) => new(OrderID, self, false);

		// The units it was made from, taken out of the world (as a transport keeps its passengers).
		public void Store(IEnumerable<Actor> units) => parts.AddRange(units);

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderID || parts.Count == 0)
				return;

			var share = self.Trait<IHealth>().HP * 100L / self.Trait<IHealth>().MaxHP;
			var released = parts.ToList();
			parts.Clear();
			self.World.AddFrameEndTask(w =>
			{
				if (self.IsDead || !self.IsInWorld)
				{
					foreach (var p in released)
						p.Dispose();

					return;
				}

				var selected = w.Selection.Contains(self);
				var taken = new HashSet<CPos>();
				foreach (var p in released)
				{
					Place(self, p, taken);
					w.Add(p);
					var health = p.Trait<IHealth>();
					var hp = (int)Math.Max(1, health.MaxHP * share / 100);
					if (hp != health.HP)
						health.InflictDamage(p, p, new Damage(health.HP - hp), true);

					if (selected)
						w.Selection.Add(p);
				}

				self.Dispose();
			});
		}

		// Flyers where it is; ground units on free cells around it.
		static void Place(Actor self, Actor part, HashSet<CPos> taken)
		{
			var positionable = part.Trait<IPositionable>();
			if (part.Info.HasTraitInfo<AircraftInfo>())
			{
				positionable.SetPosition(part, self.CenterPosition);
				return;
			}

			var cell = self.World.Map.FindTilesInCircle(self.Location, 5)
				.Where(c => !taken.Contains(c) && positionable.CanEnterCell(c))
				.Select(c => (CPos?)c)
				.FirstOrDefault() ?? self.Location;

			taken.Add(cell);
			positionable.SetPosition(part, cell);
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order) =>
			order.OrderString == OrderID ? self.Info.TraitInfo<PwMergedInfo>().Voice : null;

		// The units inside go with it.
		void INotifyKilled.Killed(Actor self, AttackInfo e) => DisposeParts();

		void INotifyActorDisposing.Disposing(Actor self) => DisposeParts();

		void DisposeParts()
		{
			foreach (var p in parts)
				p.Dispose();

			parts.Clear();
		}
	}
}
