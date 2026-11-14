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
using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Activities
{
	// Back and forth between two places until another order: each leg is an attack-move (the unit fights every enemy it
	// meets, buildings included, then goes on), as the map scripts' Patrol does.
	public class Patrol : Activity
	{
		readonly IMove move;
		readonly CPos[] points;
		readonly Color lineColor;

		// The end the unit is heading for (first the clicked place), and whether a leg has started.
		int heading = 1;
		bool started;

		public Patrol(Actor self, CPos from, CPos to, Color lineColor)
		{
			move = self.Trait<IMove>();
			points = new[] { from, to };
			this.lineColor = lineColor;
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling)
				return true;

			// The leg before ended (arrived, or as near as it gets): turn round.
			if (started)
				heading = 1 - heading;

			started = true;
			var cell = points[heading];
			QueueChild(new AttackMoveActivity(self, () => move.MoveTo(cell, 2)));
			return false;
		}

		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
		{
			yield return new TargetLineNode(Target.FromCell(self.World, points[heading]), lineColor);
			yield return new TargetLineNode(Target.FromCell(self.World, points[1 - heading]), lineColor);
		}
	}
}
