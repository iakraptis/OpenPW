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
using System.IO.Compression;
using System.Text;

namespace OpenRA.Mods.Pw.AssetInstaller
{
	// A TRC container (Jw2_*.trc archives, *.jw2 maps; docs/formats/trc.md): a directory of named entries, zlib-packed
	// when the packed and unpacked sizes differ. The game addresses archive entries by index, and names repeat.
	public sealed class TrcArchive
	{
		public readonly struct Entry
		{
			public readonly int Index;
			public readonly string Name;
			public readonly int Offset;
			public readonly int Unpacked;
			public readonly int Packed;

			public Entry(int index, string name, int offset, int unpacked, int packed)
			{
				Index = index;
				Name = name;
				Offset = offset;
				Unpacked = unpacked;
				Packed = packed;
			}
		}

		public readonly string Path;
		public readonly IReadOnlyList<Entry> Entries;
		readonly byte[] data;
		readonly int dataOffset;

		static TrcArchive()
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		}

		public TrcArchive(string path)
		{
			Path = path;
			data = File.ReadAllBytes(path);
			if (data.Length < 0x20 || Encoding.ASCII.GetString(data, 0, 4) != "TRC\x1a")
				throw new InvalidDataException($"{path}: not a TRC file");

			// +4 the slots in the directory, +8 those in use: the same in the 2002 release; the 2003 Korean one's JW2_11
			// has 44 slots for its 27 spell effects (docs/game/versions.md), the rest empty.
			var count = BitConverter.ToInt32(data, 4);
			var used = BitConverter.ToInt32(data, 8);
			dataOffset = BitConverter.ToInt32(data, 12);
			if (used < 0 || used > count || dataOffset != 0x20 + 32 * count)
				throw new InvalidDataException($"{path}: unexpected TRC header");

			var cp949 = Encoding.GetEncoding(949);
			var entries = new List<Entry>(count);
			for (var i = 0; i < count; i++)
			{
				var at = 0x20 + 32 * i;
				var name = "";
				if (data[at] != 0xCC)
				{
					var length = Array.IndexOf(data, (byte)0, at, 12) is var end and >= 0 ? end - at : 12;
					name = cp949.GetString(data, at, length);
				}

				entries.Add(new Entry(i, name, BitConverter.ToInt32(data, at + 12), BitConverter.ToInt32(data, at + 16),
					BitConverter.ToInt32(data, at + 20)));
			}

			Entries = entries;
		}

		public byte[] Read(int index)
		{
			var e = Entries[index];
			var start = dataOffset + e.Offset;
			if (e.Packed == e.Unpacked)
				return data.AsSpan(start, e.Packed).ToArray();

			using var input = new ZLibStream(new MemoryStream(data, start, e.Packed), CompressionMode.Decompress);
			var result = new byte[e.Unpacked];
			var read = 0;
			while (read < result.Length)
			{
				var n = input.Read(result, read, result.Length - read);
				if (n == 0)
					throw new InvalidDataException($"{Path}: entry {index} ({e.Name}) is truncated");

				read += n;
			}

			return result;
		}

		// The first entry with this name (names are compared without case).
		public byte[] Read(string name)
		{
			foreach (var e in Entries)
				if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
					return Read(e.Index);

			throw new FileNotFoundException($"{Path}: no entry named {name}");
		}

		public bool Contains(string name)
		{
			foreach (var e in Entries)
				if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
					return true;

			return false;
		}
	}
}
