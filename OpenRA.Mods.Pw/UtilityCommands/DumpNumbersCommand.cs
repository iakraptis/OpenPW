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
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.UtilityCommands
{
	// Every number of every actor's traits and every weapon, with the defaults the rules don't set, one line each
	// ("actor trait field = value"), sorted: two dumps can be compared to find what a change of the tick length touched
	// (tickrate-plan.md).
	sealed class DumpNumbersCommand : IUtilityCommand
	{
		string IUtilityCommand.Name => "--dump-numbers";

		bool IUtilityCommand.ValidateArguments(string[] args) => true;

		[Desc("Print every number in the resolved rules: actor traits and weapons, defaults included.")]
		void IUtilityCommand.Run(Utility utility, string[] args)
		{
			var modData = Game.ModData = utility.ModData;
			var rules = modData.DefaultRules;
			var lines = new System.Collections.Generic.List<string>();

			foreach (var actor in rules.Actors.Values)
				foreach (var trait in actor.TraitInfos<TraitInfo>())
					Dump(lines, $"{actor.Name} {TraitName(trait)}", trait);

			foreach (var weapon in rules.Weapons)
			{
				Dump(lines, $"weapon:{weapon.Key}", weapon.Value);
				Dump(lines, $"weapon:{weapon.Key} {TraitName(weapon.Value.Projectile)}", weapon.Value.Projectile);
				var i = 0;
				foreach (var warhead in weapon.Value.Warheads)
					Dump(lines, $"weapon:{weapon.Key} warhead{i++}:{warhead.GetType().Name}", warhead);
			}

			lines.Sort(StringComparer.Ordinal);
			foreach (var line in lines)
				Console.WriteLine(line);
		}

		static string TraitName(object info)
		{
			if (info == null)
				return "-";

			var name = info.GetType().Name;
			var instance = (info as TraitInfo)?.InstanceName;
			return string.IsNullOrEmpty(instance) ? name : $"{name}@{instance}";
		}

		static void Dump(System.Collections.Generic.List<string> lines, string prefix, object info)
		{
			if (info == null)
				return;

			foreach (var field in info.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
			{
				var text = Text(field.GetValue(info));
				if (text != null)
					lines.Add($"{prefix} {field.Name} = {text}");
			}
		}

		// Numbers, distances, angles and lists of them; null for anything else.
		static string Text(object value)
		{
			switch (value)
			{
				case int or long or float or double or decimal:
					return Convert.ToString(value, CultureInfo.InvariantCulture);
				case WDist or WAngle or WVec:
					return value.ToString();
				case string:
					return null;
				case IEnumerable list:
					// A default (unset) ImmutableArray throws when enumerated.
					var isDefault = value.GetType().GetProperty("IsDefault");
					if (isDefault != null && (bool)isDefault.GetValue(value))
						return null;

					var items = list.Cast<object>().Select(Text).ToList();
					return items.Count > 0 && items.All(t => t != null) ? string.Join(", ", items) : null;
				default:
					return null;
			}
		}
	}
}
