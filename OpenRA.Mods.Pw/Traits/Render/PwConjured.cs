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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Pw.Effects;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits.Render
{
	[Desc("A conjured building (the Elves'): while it is being built it fades in, from MinAlpha to whole as the work goes on;",
		"destroyed, it fades out where it stood while sparks play over it, instead of vanishing at once.")]
	public class PwConjuredInfo : TraitInfo, Requires<RenderSpritesInfo>
	{
		[Desc("How much of the building shows when its construction starts (0 to 1).")]
		public readonly float MinAlpha = 0.25f;

		[Desc("Ticks the destroyed building takes to fade out.")]
		public readonly int FadeOutTicks = PwTime.Ticks(48);

		[SequenceReference]
		[Desc("The building's sequence shown while it fades out.")]
		public readonly string Sequence = "idle";

		[Desc("The sparks played over it as it fades out.")]
		public readonly string SparksImage = "spell-elf-build";

		[SequenceReference(nameof(SparksImage))]
		public readonly string SparksSequence = "effect";

		[PaletteReference]
		public readonly string SparksPalette = "effect";

		public override object Create(ActorInitializer init) { return new PwConjured(this); }
	}

	public class PwConjured : IRenderModifier, INotifyKilled
	{
		readonly PwConjuredInfo info;

		public PwConjured(PwConjuredInfo info)
		{
			this.info = info;
		}

		IEnumerable<IRenderable> IRenderModifier.ModifyRender(Actor self, WorldRenderer wr, IEnumerable<IRenderable> r)
		{
			var construction = self.TraitOrDefault<UnderConstruction>();
			if (construction == null || construction.IsComplete)
				return r;

			var alpha = info.MinAlpha + (1f - info.MinAlpha) * construction.Progress;
			return r.Select(a => !a.IsDecoration && a is IModifyableRenderable m ? m.WithAlpha(alpha) : a);
		}

		IEnumerable<Rectangle> IRenderModifier.ModifyScreenBounds(Actor self, WorldRenderer wr, IEnumerable<Rectangle> bounds) => bounds;

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			var rs = self.Trait<RenderSprites>();
			var image = rs.GetImage(self);
			var palette = rs.Info.Palette ?? rs.Info.PlayerPalette + self.Owner.InternalName;
			var pos = self.CenterPosition;
			self.World.AddFrameEndTask(w =>
			{
				w.Add(new FadingSprite(w, pos, image, info.Sequence, palette, info.FadeOutTicks));
				w.Add(new SpriteEffect(pos, w, info.SparksImage, info.SparksSequence, info.SparksPalette));
			});
		}
	}
}
