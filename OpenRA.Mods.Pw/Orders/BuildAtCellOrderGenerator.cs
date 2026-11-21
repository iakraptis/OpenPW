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
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Pw.Traits;
using OpenRA.Orders;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Pw.Orders
{
	// Placement mode after a build palette button: shows the footprint under the cursor (white where it fits, red
	// where it does not) and sends the chosen worker to build there on a confirming click; the other button cancels
	// (TargetingButtons).
	// Ported from OpenSYW.
	public class BuildAtCellOrderGenerator : IOrderGenerator
	{
		readonly string worldDefaultCursor = ChromeMetrics.Get<string>("WorldDefaultCursor");
		readonly Actor builder;
		readonly string actorType;
		readonly ActorInfo actorInfo;
		readonly BuildingInfo buildingInfo;

		public BuildAtCellOrderGenerator(World world, Actor builder, string actorType)
		{
			this.builder = builder;
			this.actorType = actorType;
			actorInfo = world.Map.Rules.Actors[actorType];
			buildingInfo = actorInfo.TraitInfo<BuildingInfo>();
		}

		bool BuilderGone => builder.IsDead || builder.Disposed || !builder.IsInWorld;

		MouseButton IOrderGenerator.ActionButton => TargetingButtons.Confirm;

		IEnumerable<Order> IOrderGenerator.Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if (TargetingButtons.IsCancel(mi))
			{
				world.CancelInputMode();
				yield break;
			}

			if (!TargetingButtons.IsConfirm(mi))
				yield break;

			if (BuilderGone)
			{
				world.CancelInputMode();
				yield break;
			}

			if (!Builder.CanPlace(world, builder, actorInfo, buildingInfo, cell))
				yield break;

			world.CancelInputMode();
			yield return new Order(Builder.OrderID, builder, Target.FromCell(world, cell), false)
			{
				TargetString = actorType,
				SuppressVisualFeedback = true,
			};
		}

		void IOrderGenerator.Tick(World world)
		{
			if (BuilderGone)
				world.CancelInputMode();
		}

		void IOrderGenerator.SelectionChanged(World world, IEnumerable<Actor> selected) { }

		IEnumerable<IRenderable> IOrderGenerator.Render(WorldRenderer wr, World world) { yield break; }

		IEnumerable<IRenderable> IOrderGenerator.RenderAboveShroud(WorldRenderer wr, World world) { yield break; }

		IEnumerable<IRenderable> IOrderGenerator.RenderAnnotations(WorldRenderer wr, World world)
		{
			if (BuilderGone)
				yield break;

			var cell = wr.Viewport.ViewToWorld(Viewport.LastMousePos);
			var tiles = buildingInfo.Tiles(cell).ToList();
			if (tiles.Count == 0)
				yield break;

			var color = Builder.CanPlace(world, builder, actorInfo, buildingInfo, cell) ? Color.White : Color.Red;
			var half = new WVec(512, 512, 0);
			var topLeft = world.Map.CenterOfCell(new CPos(tiles.Min(t => t.X), tiles.Min(t => t.Y))) - half;
			var bottomRight = world.Map.CenterOfCell(new CPos(tiles.Max(t => t.X), tiles.Max(t => t.Y))) + half;
			var corners = new[]
			{
				topLeft, new WPos(bottomRight.X, topLeft.Y, topLeft.Z), bottomRight, new WPos(topLeft.X, bottomRight.Y, topLeft.Z),
			};

			yield return new PolygonAnnotationRenderable(corners, topLeft, 2, color);
		}

		string IOrderGenerator.GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			return Builder.CanPlace(world, builder, actorInfo, buildingInfo, cell) ? worldDefaultCursor : "move-blocked";
		}

		bool IOrderGenerator.HandleKeyPress(KeyInput e) { return false; }

		void IOrderGenerator.Deactivate() { }
	}
}
