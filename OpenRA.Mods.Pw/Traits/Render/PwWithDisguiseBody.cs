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

using OpenRA.Mods.Common.Traits.Render;

namespace OpenRA.Mods.Pw.Traits.Render
{
	[Desc("An infantry body drawn from another image: a disguised Tyrano unit's wild creature (PwDisguise). Its ",
		"sequences have the same names as the unit's own (stand, walk, attack).")]
	public class PwWithDisguiseBodyInfo : WithInfantryBodyInfo
	{
		[FieldLoader.Require]
		[Desc("The image to draw (a wild creature's).")]
		public readonly string Image = null;

		public override object Create(ActorInitializer init) { return new PwWithDisguiseBody(init, this); }
	}

	public class PwWithDisguiseBody : WithInfantryBody
	{
		public PwWithDisguiseBody(ActorInitializer init, PwWithDisguiseBodyInfo info)
			: base(init, info)
		{
			// The base body starts on the unit's own image; the same sequence carries on in the other image.
			DefaultAnimation.ChangeImage(info.Image, info.StandSequences[0]);
		}
	}
}
