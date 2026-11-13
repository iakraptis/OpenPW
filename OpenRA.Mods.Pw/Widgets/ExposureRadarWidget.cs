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

using System.Reflection;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Widgets
{
	// The exposed players' markers (PwExposure) on the minimap: a red dot per unit or building the viewer can't see,
	// drawn over the RadarWidget named by Radar (a sibling), with its own map rectangle and scale.
	public class ExposureRadarWidget : Widget
	{
		static readonly FieldInfo RadarMapRect = typeof(RadarWidget).GetField("mapRect", BindingFlags.NonPublic | BindingFlags.Instance);
		static readonly FieldInfo RadarScale = typeof(RadarWidget).GetField("previewScale", BindingFlags.NonPublic | BindingFlags.Instance);

		public string Radar = "RADAR";

		readonly World world;
		readonly PwExposure exposure;
		RadarWidget radar;

		[ObjectCreator.UseCtor]
		public ExposureRadarWidget(World world)
		{
			this.world = world;
			exposure = world.WorldActor.TraitOrDefault<PwExposure>();
		}

		public override void Initialize(WidgetArgs args)
		{
			base.Initialize(args);
			radar = Parent.Get<RadarWidget>(Radar);
		}

		public override void Draw()
		{
			if (exposure == null || !exposure.Enabled || RadarMapRect == null || RadarScale == null)
				return;

			var mapRect = (Rectangle)RadarMapRect.GetValue(radar);
			var scale = (float)RadarScale.GetValue(radar);
			var bounds = world.Map.Bounds;
			var size = System.Math.Max(2f, scale + 1);
			var r = Game.Renderer.RgbaColorRenderer;
			foreach (var a in exposure.Marked(world.RenderPlayer))
			{
				var uv = a.Location.ToMPos(world.Map);
				var x = mapRect.X + scale * (uv.U - bounds.Left + 0.5f) - size / 2;
				var y = mapRect.Y + scale * (uv.V - bounds.Top + 0.5f) - size / 2;
				r.FillRect(new float3(x, y, 0), new float3(x + size, y + size, 0), exposure.Info.MarkerColor);
			}
		}

		public override bool HandleMouseInput(MouseInput mi) { return false; }
	}
}
