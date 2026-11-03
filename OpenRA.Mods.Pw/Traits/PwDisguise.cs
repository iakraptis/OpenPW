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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("Tyrano disguise (the help file's Special Abilities: \"Tyranos have the ability to revert to their original ",
		"forms\"): once researched, the command panel's button (order PwDisguise) turns the unit into a wild creature of ",
		"its kind and back. While disguised it grants Condition, which the rules use to draw the creature ",
		"(PwWithDisguiseBody), switch its attacks off and add the Wild target type that nothing attacks on its own; and it ",
		"appears as the Creeps' (colours, relationship). Health, level and experience stay as they are.")]
	public class PwDisguiseInfo : TraitInfo
	{
		[GrantedConditionReference]
		[Desc("Condition granted while disguised.")]
		public readonly string Condition = "disguised";

		[Desc("Needed to disguise (the Disguise research).")]
		public readonly string[] Prerequisites = Array.Empty<string>();

		[Desc("The player the disguised unit appears to belong to.")]
		public readonly string DisguiseOwner = "Creeps";

		[VoiceReference]
		public readonly string Voice = "Action";

		public override object Create(ActorInitializer init) { return new PwDisguise(init.Self, this); }
	}

	public class PwDisguise : IResolveOrder, IOrderVoice, IEffectiveOwner
	{
		public const string OrderID = "PwDisguise";

		public readonly PwDisguiseInfo Info;
		readonly Player disguiseOwner;
		int token = Actor.InvalidConditionToken;

		public PwDisguise(Actor self, PwDisguiseInfo info)
		{
			Info = info;
			disguiseOwner = self.World.Players.FirstOrDefault(p => p.InternalName == info.DisguiseOwner);
		}

		public bool Disguised { get; private set; }

		bool IEffectiveOwner.Disguised => Disguised && disguiseOwner != null;
		Player IEffectiveOwner.Owner => disguiseOwner;

		public bool IsAvailable(Actor self) =>
			disguiseOwner != null && self.Owner.PlayerActor.Trait<TechTree>().HasPrerequisites(Info.Prerequisites);

		// The order the Disguise button sends: ExtraData 1 disguises, 0 turns back.
		public static Order Toggle(Actor self, bool on) => new(OrderID, self, false) { ExtraData = on ? 1u : 0u };

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString != OrderID)
				return;

			var on = order.ExtraData != 0;
			if (on == Disguised || (on && !IsAvailable(self)))
				return;

			// A disguised unit stops what it was doing (its attack is off).
			if (on)
				self.CancelActivity();

			var before = on ? self.Owner : disguiseOwner;
			Disguised = on;
			if (on)
				token = self.GrantCondition(Info.Condition);
			else if (token != Actor.InvalidConditionToken)
				token = self.RevokeCondition(token);

			var after = on ? disguiseOwner : self.Owner;
			foreach (var notify in self.TraitsImplementing<INotifyEffectiveOwnerChanged>())
				notify.OnEffectiveOwnerChanged(self, before, after);
		}

		string IOrderVoice.VoicePhraseForOrder(Actor self, Order order) =>
			order.OrderString == OrderID ? Info.Voice : null;
	}
}
