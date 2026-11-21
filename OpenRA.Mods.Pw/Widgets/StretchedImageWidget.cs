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
using System.Numerics;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets
{
	// Draws a chrome image stretched to fill the widget bounds (stock Image widgets draw at native size only), so the
	// small original icons (the resource icons) scale with the interface bar, and the main menu with the window.
	// GetImageName picks the image each frame (a button's state). Shows a tooltip when given one.
	// Ported from OpenSYW.
	public class StretchedImageWidget : Widget
	{
		public string ImageCollection = "";
		public string ImageName = "";
		public string TooltipTemplate;
		public string TooltipContainer;
		public Func<string> GetTooltipText;
		public Func<string> GetImageName;

		readonly Lazy<TooltipContainerWidget> tooltipContainer;

		[ObjectCreator.UseCtor]
		public StretchedImageWidget()
		{
			tooltipContainer = new Lazy<TooltipContainerWidget>(() => Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
			GetImageName = () => ImageName;
		}

		protected StretchedImageWidget(StretchedImageWidget other)
			: base(other)
		{
			ImageCollection = other.ImageCollection;
			ImageName = other.ImageName;
			TooltipTemplate = other.TooltipTemplate;
			TooltipContainer = other.TooltipContainer;
			GetTooltipText = other.GetTooltipText;
			GetImageName = other.GetImageName;
			tooltipContainer = new Lazy<TooltipContainerWidget>(() => Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));
		}

		public override Widget Clone() { return new StretchedImageWidget(this); }

		public override void Draw()
		{
			var sprite = ChromeProvider.GetImage(ImageCollection, GetImageName());
			if (sprite == null)
				return;

			var b = RenderBounds;
			var scale = new Vector3(b.Width / sprite.Size.X, b.Height / sprite.Size.Y, 1);
			Game.Renderer.RgbaSpriteRenderer.DrawSprite(sprite, new Vector3(b.X, b.Y, 0), scale);
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
