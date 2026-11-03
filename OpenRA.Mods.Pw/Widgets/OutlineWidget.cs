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

using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets
{
	// A coloured frame around the widget's bounds (a command slot that is on, such as the Hunt toggle). Mouse input
	// passes through it.
	public class OutlineWidget : Widget
	{
		public Color Color = Color.Gold;

		[Desc("Width of the frame in pixels.")]
		public int Thickness = 2;

		public OutlineWidget()
		{
			IgnoreMouseOver = true;
		}

		protected OutlineWidget(OutlineWidget other)
			: base(other)
		{
			Color = other.Color;
			Thickness = other.Thickness;
			IgnoreMouseOver = true;
		}

		public override Widget Clone() { return new OutlineWidget(this); }

		public override void Draw()
		{
			var b = RenderBounds;
			var half = Thickness / 2f;
			Game.Renderer.RgbaColorRenderer.DrawRect(new float3(b.Left + half, b.Top + half, 0),
				new float3(b.Right - half, b.Bottom - half, 0), Thickness, Color);
		}
	}
}
