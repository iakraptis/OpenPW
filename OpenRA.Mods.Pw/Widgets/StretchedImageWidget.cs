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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets
{
	// Draws a chrome image stretched to fill the widget bounds (stock Image widgets draw at native size only), so the
	// small original icons (the resource icons) scale with the interface bar. Shows a tooltip when given one.
	// Ported from OpenSYW.
	public class StretchedImageWidget : Widget
	{
		public string ImageCollection = "";
		public string ImageName = "";
		public string TooltipTemplate;
		public string TooltipContainer;
		public Func<string> GetTooltipText;

		readonly Lazy<TooltipContainerWidget> tooltipContainer;

		[ObjectCreator.UseCtor]
		public StretchedImageWidget()
		{
			tooltipContainer = new Lazy<TooltipContainerWidget>(() => Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		protected StretchedImageWidget(StretchedImageWidget other)
			: base(other)
		{
			ImageCollection = other.ImageCollection;
			ImageName = other.ImageName;
			TooltipTemplate = other.TooltipTemplate;
			TooltipContainer = other.TooltipContainer;
			GetTooltipText = other.GetTooltipText;
			tooltipContainer = new Lazy<TooltipContainerWidget>(() => Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		public override Widget Clone() { return new StretchedImageWidget(this); }

		public override void Draw()
		{
			var sprite = ChromeProvider.GetImage(ImageCollection, ImageName);
			if (sprite == null)
				return;

			var b = RenderBounds;
			var scale = new float3(b.Width / sprite.Size.X, b.Height / sprite.Size.Y, 1);
			Game.Renderer.RgbaSpriteRenderer.DrawSprite(sprite, new float3(b.X, b.Y, 0), scale);
		}

		public override void MouseEntered()
		{
			if (TooltipContainer == null || GetTooltipText == null)
				return;

			tooltipContainer.Value.SetTooltip(TooltipTemplate, new WidgetArgs() { { "getText", GetTooltipText } });
		}

		public override void MouseExited()
		{
			if (TooltipContainer == null)
				return;

			tooltipContainer.Value.RemoveTooltip();
		}
	}
}
