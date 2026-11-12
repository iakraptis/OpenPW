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
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets
{
	// A coloured frame around the widget's bounds (a command slot that is on, such as the Hunt toggle), animated: the
	// frame glows softly up and down, and a light with a fading tail runs around it clockwise. Mouse input passes
	// through it.
	public class OutlineWidget : Widget
	{
		public Color Color = Color.Gold;

		[Desc("Colour of the light running around the frame.")]
		public Color LightColor = Color.FromArgb(255, 252, 230);

		[Desc("Width of the frame in pixels.")]
		public int Thickness = 2;

		[Desc("Milliseconds the light takes to go around once, and for one glow of the frame.")]
		public int Period = 1600;

		[Desc("Length of the light and its tail, as a share of the frame's length.")]
		public float TailShare = 0.35f;

		public OutlineWidget()
		{
			IgnoreMouseOver = true;
		}

		protected OutlineWidget(OutlineWidget other)
			: base(other)
		{
			Color = other.Color;
			LightColor = other.LightColor;
			Thickness = other.Thickness;
			Period = other.Period;
			TailShare = other.TailShare;
			IgnoreMouseOver = true;
		}

		public override Widget Clone() { return new OutlineWidget(this); }

		public override void Draw()
		{
			var b = RenderBounds;
			var half = Thickness / 2f;
			var left = b.Left + half;
			var top = b.Top + half;
			var right = b.Right - half;
			var bottom = b.Bottom - half;
			var phase = Game.RunTime % Period / (float)Period;
			var renderer = Game.Renderer.RgbaColorRenderer;

			// The frame, glowing between 65% and full strength.
			var glow = 0.825f + 0.175f * (float)Math.Sin(2 * Math.PI * phase);
			renderer.DrawRect(new float3(left, top, 0), new float3(right, bottom, 0), Thickness,
				Color.FromArgb((int)(Color.A * glow), Color.R, Color.G, Color.B));

			// The running light: its head at the phase's share of the way round (clockwise from the top left corner), its
			// tail fading behind it, drawn in short steps so it bends round the corners.
			var w = right - left;
			var h = bottom - top;
			var length = 2 * (w + h);
			if (length <= 0)
				return;

			float3 At(float d)
			{
				d = (d % length + length) % length;
				if (d < w)
					return new float3(left + d, top, 0);
				if (d < w + h)
					return new float3(right, top + d - w, 0);
				if (d < 2 * w + h)
					return new float3(right - (d - w - h), bottom, 0);

				return new float3(left, bottom - (d - 2 * w - h), 0);
			}

			var head = phase * length;
			var tail = TailShare * length;
			const float Step = 2f;
			for (var d = 0f; d < tail; d += Step)
			{
				var fade = 1 - d / tail;
				var color = Color.FromArgb((int)(255 * fade * fade), LightColor.R, LightColor.G, LightColor.B);
				renderer.DrawLine(At(head - d - Step), At(head - d), Thickness + 1, color);
			}
		}
	}
}
