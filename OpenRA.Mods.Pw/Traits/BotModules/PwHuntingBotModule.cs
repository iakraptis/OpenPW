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

using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("AI hunting: turns hunting (PwHunter, the Hunt button) on for the bot's army units near its base, so the ones",
		"waiting there hunt the wild creatures around it for experience and meat, and off for those further out (an",
		"attack wave isn't led off after dinosaurs). Workers keep to the berries unless HuntWithWorkers.")]
	public class PwHuntingBotModuleInfo : ConditionalTraitInfo
	{
		[Desc("Ticks between checks.")]
		public readonly int ScanInterval = PwTime.Ticks(50);

		[Desc("Units within this many cells of the main building hunt; further out they don't.")]
		public readonly int HuntRadius = 14;

		[Desc("Workers hunt too.")]
		public readonly bool HuntWithWorkers = false;

		public override object Create(ActorInitializer init) { return new PwHuntingBotModule(init.Self, this); }
	}

	public class PwHuntingBotModule : ConditionalTrait<PwHuntingBotModuleInfo>, IBotTick
	{
		readonly World world;
		readonly Player player;
		int ticks;

		public PwHuntingBotModule(Actor self, PwHuntingBotModuleInfo info)
			: base(info)
		{
			world = self.World;
			player = self.Owner;
		}

		void IBotTick.BotTick(IBot bot)
		{
			if (++ticks % Info.ScanInterval != 0)
				return;

			var owned = PwBotUtils.Owned(world, player).Where(a => a.IsInWorld).ToList();
			var main = owned.FirstOrDefault(a => PwBotUtils.IsDropOff(a.Info)) ?? owned.FirstOrDefault(a => PwBotUtils.IsBuilding(a.Info));
			if (main == null)
				return;

			var radius = WDist.FromCells(Info.HuntRadius).Length;
			foreach (var unit in owned)
			{
				if (!(PwBotUtils.IsArmy(unit.Info) || (Info.HuntWithWorkers && PwBotUtils.IsWorker(unit.Info))))
					continue;

				var hunter = unit.TraitOrDefault<PwHunter>();
				if (hunter == null || hunter.IsTraitDisabled)
					continue;

				var near = (unit.CenterPosition - main.CenterPosition).HorizontalLength <= radius;
				if (near != hunter.Hunting)
					bot.QueueOrder(PwHunter.Toggle(unit, near));
			}
		}
	}
}
