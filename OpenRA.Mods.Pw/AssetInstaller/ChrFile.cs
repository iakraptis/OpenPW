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
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OpenRA.Mods.Pw.AssetInstaller
{
	// A unit or building type from JW2_09.TRC (docs/formats/chr.md): the type record, an 8-bit palette, 14 animation
	// groups of frames and 9 groups of WAV sounds.
	public sealed class ChrFile
	{
		public const int RecordSize = 0x24bc;
		public const int Groups = 14;

		public sealed class Frame
		{
			public int Width, Height, OffsetX, OffsetY, MirroredOffsetX;

			// Palette indices, row by row; 0 = transparent (the skip runs).
			public byte[] Pixels;
		}

		public readonly byte[] Record;
		public readonly byte[] Palette;
		public readonly string Name;
		public readonly List<Frame>[] Frames = new List<Frame>[Groups];
		public readonly List<byte[]>[] Sounds = new List<byte[]>[9];

		public ChrFile(byte[] data)
		{
			if (data.Length < RecordSize + 0x400)
				throw new InvalidDataException("not a .chr type (too short)");

			Record = data.AsSpan(0, RecordSize).ToArray();
			Palette = data.AsSpan(RecordSize, 0x400).ToArray();
			var nameLength = Array.IndexOf(Record, (byte)0, 0x10c, 0x40) is var end and >= 0 ? end - 0x10c : 0x40;
			Name = Encoding.GetEncoding(949).GetString(Record, 0x10c, nameLength);

			var at = RecordSize + 0x400;
			for (var g = 0; g < Groups; g++)
			{
				Frames[g] = [];
				var count = Int(0x2214 + 4 * g);
				for (var i = 0; i < count; i++)
				{
					Frames[g].Add(ReadFrame(data, ref at));
				}
			}

			for (var g = 0; g < Sounds.Length; g++)
			{
				Sounds[g] = [];
				var count = Int(0x2424 + 4 * g);
				for (var i = 0; i < count; i++)
				{
					if (at + 8 > data.Length || Encoding.ASCII.GetString(data, at, 4) != "RIFF")
						throw new InvalidDataException($"{Name}: sound {g}.{i} is not a WAV");

					var size = BitConverter.ToInt32(data, at + 4) + 8;
					Sounds[g].Add(data.AsSpan(at, size).ToArray());
					at += size;
				}
			}
		}

		public int Int(int offset) { return BitConverter.ToInt32(Record, offset); }

		// Rows of a u16 byte length, then codes: 00 n skips n transparent pixels, any other byte is one pixel.
		// One frame (32-byte header, then the rows), as in .chr, .doo and .spz files; at moves past it.
		public static Frame ReadFrame(byte[] data, ref int at)
		{
			var frame = new Frame
			{
				Width = BitConverter.ToInt32(data, at),
				Height = BitConverter.ToInt32(data, at + 4),
				OffsetX = BitConverter.ToInt32(data, at + 8),
				OffsetY = BitConverter.ToInt32(data, at + 12),
				MirroredOffsetX = BitConverter.ToInt32(data, at + 16),
			};

			var size = BitConverter.ToInt32(data, at + 24);
			frame.Pixels = Decode(data, at + 32, frame.Width, frame.Height);
			at += 32 + size;
			return frame;
		}

		static byte[] Decode(byte[] data, int at, int width, int height)
		{
			var pixels = new byte[width * height];
			for (var y = 0; y < height; y++)
			{
				var end = at + 2 + BitConverter.ToUInt16(data, at);
				at += 2;
				var x = 0;
				while (at < end)
				{
					if (data[at] == 0)
					{
						x += data[at + 1];
						at += 2;
					}
					else
					{
						if (x < width)
							pixels[y * width + x] = data[at];

						x++;
						at++;
					}
				}

				at = end;
			}

			return pixels;
		}

		// First frame of direction block b (1 = N … 5 = S) in group g; entries 6-8 repeat the last.
		public int BlockStart(int g, int b) { return Int(0x2248 + 0x20 * g + 4 * b); }

		// The frame (within a direction block) shown at each animation step of group g, one step per game tick.
		public int[] Sequence(int g)
		{
			var length = Int(0x13d4 + 4 * g);
			if (length <= 0 || length > 64)
				return Array.Empty<int>();

			var steps = new int[length];
			for (var s = 0; s < length; s++)
				steps[s] = Int(0x140c + 0x100 * g + 4 * s);

			return steps;
		}
	}
}
