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
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Pw.Effects;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[Desc("A unit that leaves a body (Pw.exe 0x460ac0, the dead units' update): it plays its die animation facing the ",
		"way it fell, then lies on the last frame for Ticks (the original's .chr 0x13d0: 320 for most ground units, 480 ",
		"for the big Tyranosaurs, 0 for flyers and machines, which vanish once they have fallen; the mod doubles it, ",
		"about 45 seconds at the default speed). While it falls or lies, ",
		"Resurrect (PwSpell) can raise it (PwGraveyard).")]
	public class PwBodyInfo : ConditionalTraitInfo, Requires<RenderSpritesInfo>
	{
		[SequenceReference]
		public readonly string Sequence = "die";

		[PaletteReference(nameof(IsPlayerPalette))]
		public readonly string Palette = "player";

		public readonly bool IsPlayerPalette = true;

		[Desc("Ticks the body lies after the die animation.")]
		public readonly int Ticks = 640;

		[Desc("Its art ends with nothing to leave lying (flyers, the Wizard): once fallen, an unseen mark that Resurrect ",
			"can raise, shown as a grey dot only to a player who owns a Resurrect caster (a mod addition: in the original ",
			"these leave no body, so Resurrect can't reach them).")]
		public readonly bool Marker = false;

		[Desc("Resurrect can raise it. The original lets it raise any unit (0x475104 checks only that it is a unit, ",
			"dead and seen).")]
		public readonly bool Raisable = true;

		public override object Create(ActorInitializer init) { return new PwBody(init.Self, this); }
	}

	public class PwBody : ConditionalTrait<PwBodyInfo>, INotifyKilled
	{
		readonly RenderSprites rs;

		public PwBody(Actor self, PwBodyInfo info)
			: base(info)
		{
			rs = self.Trait<RenderSprites>();
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (IsTraitDisabled || !self.IsInWorld)
				return;

			var world = self.World;
			var image = rs.GetImage(self);
			var sequence = world.Map.Sequences.GetSequence(image, Info.Sequence);
			var lifetime = Math.Max(1, sequence.Length * Math.Max(1, sequence.Tick / 40)) + Info.Ticks;
			var pos = self.CenterPosition;
			var facing = self.TraitOrDefault<IFacing>()?.Facing ?? WAngle.Zero;
			var palette = Info.IsPlayerPalette ? Info.Palette + self.Owner.InternalName : Info.Palette;

			var grave = Info.Raisable ? world.WorldActor.TraitOrDefault<PwGraveyard>()?.Bury(self, lifetime) : null;
			world.AddFrameEndTask(w => w.Add(new PwBodyEffect(w, pos, facing, image, Info.Sequence, palette, lifetime, grave, Info.Marker)));
		}
	}
}
