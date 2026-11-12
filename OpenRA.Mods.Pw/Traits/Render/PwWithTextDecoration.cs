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
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits.Render
{
	[Desc("WithTextDecoration that stays visible when the camera is zoomed out past the engine's normal minimum. The",
		"engine hides every decoration there (SelectionDecorationsBase), and the mod lets the player zoom out 25% further",
		"(InterfaceBarLogic), so the hunting H vanished when zoomed out.")]
	public class PwWithTextDecorationInfo : WithTextDecorationInfo
	{
		public override object Create(ActorInitializer init) { return new PwWithTextDecoration(init.Self, this); }
	}

	public class PwWithTextDecoration : WithTextDecoration, IRenderAnnotations
	{
		ISelectionDecorations container;

		public PwWithTextDecoration(Actor self, PwWithTextDecorationInfo info)
			: base(self, info) { }

		// Only where the engine leaves it out (zoomed out past the normal minimum), so it is never drawn twice.
		IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
		{
			if (wr.Viewport.Zoom >= wr.Viewport.MinZoom || self.World.FogObscures(self))
				return Enumerable.Empty<IRenderable>();

			if (Info.RequiresSelection && !self.World.Selection.Contains(self))
				return Enumerable.Empty<IRenderable>();

			container ??= self.TraitOrDefault<ISelectionDecorations>();
			return container == null ? Enumerable.Empty<IRenderable>() : ((IDecoration)this).RenderDecoration(self, wr, container);
		}

		bool IRenderAnnotations.SpatiallyPartitionable => true;
	}
}
