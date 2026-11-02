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

using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("The unit's voices: the voice set named like its image (voices/units.yaml, written by the installer from the ",
		".chr sounds), so every unit gets its own without naming it. Units without sounds stay silent.")]
	public class PwVoicedInfo : TraitInfo
	{
		[Desc("Voice set to use instead of the image's.")]
		public readonly string VoiceSet = null;

		public readonly float Volume = 1f;

		public override object Create(ActorInitializer init) { return new PwVoiced(init.Self, this); }
	}

	public class PwVoiced : IVoiced
	{
		readonly PwVoicedInfo info;
		readonly string voiceSet;

		public PwVoiced(Actor self, PwVoicedInfo info)
		{
			this.info = info;
			var set = info.VoiceSet ?? self.Info.TraitInfoOrDefault<RenderSpritesInfo>()?.Image ?? self.Info.Name;
			voiceSet = self.World.Map.Rules.Voices.ContainsKey(set.ToLowerInvariant()) ? set.ToLowerInvariant() : null;
		}

		string IVoiced.VoiceSet => voiceSet;

		bool IVoiced.PlayVoice(Actor self, string phrase, string variant)
		{
			// The engine throws on a phrase the set doesn't have (a tower given an order has no Action voice).
			if (phrase == null || !((IVoiced)this).HasVoice(self, phrase))
				return false;

			return Game.Sound.PlayPredefined(SoundType.World, self.World.Map.Rules, null, self, voiceSet, phrase, variant, true, WPos.Zero, info.Volume, true);
		}

		bool IVoiced.PlayVoiceLocal(Actor self, string phrase, string variant, float volume)
		{
			if (phrase == null || !((IVoiced)this).HasVoice(self, phrase))
				return false;

			return Game.Sound.PlayPredefined(SoundType.World, self.World.Map.Rules, null, self, voiceSet, phrase, variant, false, self.CenterPosition, volume, true);
		}

		bool IVoiced.HasVoice(Actor self, string voice)
		{
			return voiceSet != null && self.World.Map.Rules.Voices[voiceSet].Voices.ContainsKey(voice);
		}

		// Plays a voice heard where the unit is (attack, work), if the unit has it.
		public static void PlayLocal(Actor self, string voice)
		{
			foreach (var voiced in self.TraitsImplementing<IVoiced>())
				if (voiced.HasVoice(self, voice))
					voiced.PlayVoiceLocal(self, voice, self.Owner.Faction.InternalName, 1f);
		}
	}

	[Desc("Plays the unit's Attack voice when it attacks, one time in OneIn (.chr 0x2420).")]
	public class PwAttackVoiceInfo : TraitInfo
	{
		public readonly int OneIn = 2;

		public override object Create(ActorInitializer init) { return new PwAttackVoice(this); }
	}

	public class PwAttackVoice : INotifyAttack
	{
		readonly PwAttackVoiceInfo info;

		public PwAttackVoice(PwAttackVoiceInfo info)
		{
			this.info = info;
		}

		void INotifyAttack.PreparingAttack(Actor self, in Target target, Armament a, Barrel barrel) { }

		void INotifyAttack.Attacking(Actor self, in Target target, Armament a, Barrel barrel)
		{
			if (info.OneIn > 0 && self.World.LocalRandom.Next(info.OneIn) == 0)
				PwVoiced.PlayLocal(self, "Attack");
		}
	}
}
