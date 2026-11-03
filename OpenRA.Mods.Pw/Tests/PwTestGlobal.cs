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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Activities;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Scripting;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Tests
{
	// Lua helpers for the test maps (mods/pw/maps/*-test/test.lua). Orders are resolved directly on the actor's traits,
	// so a script can give the mod's own orders (gathering, construction) that the stock Lua API doesn't expose.
	[ScriptGlobal("PwTest")]
	public class PwTestGlobal : ScriptGlobal
	{
		public PwTestGlobal(ScriptContext context)
			: base(context) { }

		static void Resolve(Actor actor, Order order)
		{
			foreach (var t in actor.TraitsImplementing<IResolveOrder>().ToArray())
				t.ResolveOrder(actor, order);
		}

		[Desc("Issue an order targeting an actor (PwGather on a bush).")]
		public void OrderOn(Actor actor, string order, Actor target) =>
			Resolve(actor, new Order(order, actor, Target.FromActor(target), false));

		[Desc("Order a worker onto a berry bush (the real gather order).")]
		public void Gather(Actor worker, Actor bush) => OrderOn(worker, BerryGatherer.OrderID, bush);

		[Desc("Whether a worker is running its gathering loop.")]
		public bool Gathering(Actor actor) => actor.CurrentActivity is GatherBerries;

		[Desc("Berries a worker is carrying, or -1 for anything else.")]
		public int Carried(Actor worker) => worker.TraitOrDefault<BerryGatherer>()?.Carried ?? -1;

		[Desc("Berries left in a bush, or -1 for anything else.")]
		public int Berries(Actor bush) => bush.TraitOrDefault<BerryBush>()?.Remaining ?? -1;

		[Desc("Set the berries left in a bush (to test emptying and regrowth quickly).")]
		public void SetBerries(Actor bush, int amount) => bush.Trait<BerryBush>().SetRemaining(amount);

		[Desc("Every unit a building's own training queue offers, prerequisites aside, comma separated.")]
		public string Trainable(Actor producer) =>
			string.Join(",", producer.TraitsImplementing<ProductionQueue>().SelectMany(q => q.AllItems()).Select(a => a.Name));

		[Desc("Select one actor, as a click would.")]
		public void Select(Actor actor) => actor.World.Selection.Combine(actor.World, new[] { actor }, false, true);

		[Desc("Order a worker to construct a building at a cell (the real build order).")]
		public void Construct(Actor builder, string building, CPos cell) =>
			Resolve(builder, new Order(Builder.OrderID, builder, Target.FromCell(builder.World, cell), false) { TargetString = building });

		[Desc("Start training a unit at a building, as a click on its icon would (Lua's Build waits for every unit it ",
			"asked for, so it can't test refused units).")]
		public void Train(Actor producer, string unit) =>
			Resolve(producer, new Order("StartProduction", producer, false) { TargetString = unit, ExtraData = 1 });

		[Desc("Whether the player meets an actor type's prerequisites (its tech is available).")]
		public bool Available(Player player, string actorType)
		{
			var buildable = player.World.Map.Rules.Actors[actorType].TraitInfoOrDefault<BuildableInfo>();
			return buildable == null || player.PlayerActor.Trait<TechTree>().HasPrerequisites(buildable.Prerequisites);
		}

		[Desc("Population in use: units plus everything in the training queues.")]
		public int PopulationUsed(Player player) => PwPopulation.Used(player);

		[Desc("Population limit: what the player's finished buildings provide.")]
		public int PopulationLimit(Player player) => PwPopulation.Capacity(player);

		[Desc("A unit's level, or -1 if it doesn't level.")]
		public int Level(Actor actor) => actor.TraitOrDefault<PwLevels>()?.Level ?? -1;

		[Desc("A unit's experience towards its next level, or -1 if it doesn't level.")]
		public int Experience(Actor actor) => actor.TraitOrDefault<PwLevels>()?.Experience ?? -1;

		[Desc("Give a unit experience, as kills would.")]
		public void GiveExperience(Actor actor, int amount) => actor.TraitOrDefault<PwLevels>()?.GiveExperience(amount);

		[Desc("Whether the player has researched an upgrade level.")]
		public bool Researched(Player player, string upgrade) =>
			player.PlayerActor.TraitOrDefault<PwUpgrades>()?.IsResearched(upgrade) ?? false;

		[Desc("Complete an upgrade level at once, as if researched.")]
		public void CompleteUpgrade(Player player, string upgrade) =>
			player.PlayerActor.Trait<PwUpgrades>().Complete(player.World.Map.Rules.Actors[upgrade]);

		[Desc("How many items a building's queues hold.")]
		public int QueueLength(Actor producer) => producer.TraitsImplementing<ProductionQueue>().Sum(q => q.AllQueued().Count());

		[Desc("Order a unit to pick up an item (the real pick-up order).")]
		public void PickUp(Actor unit, Actor item) => OrderOn(unit, PwItemUser.OrderID, item);

		[Desc("Whether a unit has a voice (Select, Action, Attack, Die, Work, Built).")]
		public bool HasVoice(Actor actor, string voice) => actor.TraitsImplementing<IVoiced>().Any(v => v.HasVoice(actor, voice));

		[Desc("Whether a right-click on the target would give this order (the order's targeter accepts it), as the ",
			"mouse does; orders given through Lua skip this check.")]
		public bool CanOrder(Actor actor, string order, Actor target)
		{
			var t = Target.FromActor(target);
			foreach (var o in actor.TraitsImplementing<IIssueOrder>().SelectMany(i => i.Orders))
			{
				var modifiers = TargetModifiers.None;
				string cursor = null;
				if (o.OrderID == order && o.CanTarget(actor, t, ref modifiers, ref cursor))
					return true;
			}

			return false;
		}

		[Desc("Meat a unit carries, or -1 if it can't carry any.")]
		public int CarriedMeat(Actor actor) => actor.TraitOrDefault<PwMeatEater>()?.Meat ?? -1;

		[Desc("Meat left in a piece of meat on the ground, or -1 for anything else.")]
		public int MeatAmount(Actor meat) => meat.TraitOrDefault<PwMeat>()?.Amount ?? -1;

		[Desc("A unit's mana, or -1 without any.")]
		public int Mana(Actor actor) => actor.TraitOrDefault<PwMana>()?.Mana ?? -1;

		[Desc("Whether the building has finished construction.")]
		public bool Finished(Actor actor) => actor.TraitOrDefault<UnderConstruction>()?.IsComplete ?? true;

		[Desc("Turns a unit's hunting on or off, as the Hunt button does.")]
		public void Hunt(Actor actor, bool on) => actor.World.IssueOrder(PwHunter.Toggle(actor, on));

		[Desc("Selects these actors (replacing the selection), as a drag box does.")]
		public void SelectAll(Actor[] actors) => actors[0].World.Selection.Combine(actors[0].World, actors, false, false);

		[Desc("Sets the meat a unit carries.")]
		public void SetMeat(Actor actor, int amount) => actor.TraitOrDefault<PwMeatEater>()?.SetMeat(amount);

		[Desc("Makes the units meet and share their meat, as the Share meat button does (the first takes the order).")]
		public void ShareMeat(Actor[] units) => units[0].World.IssueOrder(PwMeatEater.Share(units));

		[Desc("Whether the player can see the actor (fog and stealth: a hidden trap needs a detector near it).")]
		public bool CanSee(Player player, Actor actor) => actor.CanBeViewedByPlayer(player);

		[Desc("Whether a unit's hunting is on.")]
		public bool Hunting(Actor actor) => actor.TraitOrDefault<PwHunter>()?.Hunting ?? false;

		[Desc("Tyrano: the units meet and merge, as the merge button does (the first takes the order).")]
		public void Merge(Actor[] units) => Resolve(units[0], PwMerge.Merge(units));

		[Desc("Tyrano: a merged unit lets its parts go again (the Split button).")]
		public void Split(Actor merged) => Resolve(merged, PwMerged.Split(merged));

		[Desc("How many units a merged unit holds (-1 if it isn't one).")]
		public int MergedParts(Actor merged) => merged.TraitOrDefault<PwMerged>()?.Parts.Count ?? -1;

		[Desc("Tyrano: disguise a unit as its wild creature, or turn it back (the Disguise button).")]
		public void Disguise(Actor actor, bool on) => Resolve(actor, PwDisguise.Toggle(actor, on));

		[Desc("Whether a unit is disguised.")]
		public bool Disguised(Actor actor) => actor.TraitOrDefault<PwDisguise>()?.Disguised ?? false;

		[Desc("The internal name of the player the actor appears to belong to (a disguised unit: the Creeps).")]
		public string AppearsOwnedBy(Actor actor) =>
			actor.EffectiveOwner != null && actor.EffectiveOwner.Disguised ? actor.EffectiveOwner.Owner.InternalName : actor.Owner.InternalName;
	}
}
