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

using System.IO;
using OpenRA.Mods.Common.AudioLoaders;

namespace OpenRA.Mods.Pw.AudioLoaders
{
	// The engine's MP3 loader (mod.yaml SoundFormats: PwMp3 in its place) with the channel count fixed: MP3Sharp's stream
	// always gives 16-bit stereo, but Mp3Format reports the file's own channels, so a mono MP3 (the campaign's
	// narrations, <prefix>_S.MP3 and _E.MP3: MPEG-1 layer III, 44.1 kHz mono) played at half speed, an octave low.
	public class PwMp3Loader : ISoundLoader
	{
		readonly ISoundLoader engine = new Mp3Loader();

		bool ISoundLoader.TryParseSound(Stream stream, out ISoundFormat sound)
		{
			if (!engine.TryParseSound(stream, out var mp3))
			{
				sound = null;
				return false;
			}

			sound = new StereoMp3(mp3);
			return true;
		}

		sealed class StereoMp3 : ISoundFormat
		{
			readonly ISoundFormat mp3;

			public StereoMp3(ISoundFormat mp3)
			{
				this.mp3 = mp3;
			}

			public int Channels => 2;
			public int SampleBits => mp3.SampleBits;
			public int SampleRate => mp3.SampleRate;
			public float LengthInSeconds => mp3.LengthInSeconds;
			public Stream GetPCMInputStream() => mp3.GetPCMInputStream();
			public void Dispose() => mp3.Dispose();
		}
	}
}
