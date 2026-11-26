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
using System.IO;

namespace OpenRA.Mods.Pw.AssetInstaller
{
	// A baseline JPEG decoder (ITU T.81), enough for the campaign's briefing pictures (JW2_06 <prefix>_<n>.JPG: 800x600,
	// one scan, Huffman tables, any sampling, restart markers). The engine reads only PNG, so the installer converts.
	public static class Jpeg
	{
		static readonly int[] ZigZag =
		{
			0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21,
			28, 35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51, 58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61,
			54, 47, 55, 62, 63
		};

		sealed class Huffman
		{
			// Code lengths 1-16: the smallest code, the largest (-1: none) and the index of the first value.
			public readonly int[] MinCode = new int[17], MaxCode = new int[18], ValPtr = new int[17];
			public byte[] Values;
		}

		sealed class Component
		{
			public int Id, H, V, Quant, Dc, Ac, Pred;
			public int BlocksPerLine, BlocksPerColumn;
			public byte[] Pixels;
		}

		// RGBA, width x height.
		public static (byte[] Rgba, int Width, int Height) Decode(byte[] data)
		{
			if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
				throw new InvalidDataException("not a JPEG");

			var quant = new int[4][];
			var dc = new Huffman[4];
			var ac = new Huffman[4];
			Component[] components = null;
			int width = 0, height = 0, restart = 0;
			var at = 2;
			while (at + 4 <= data.Length)
			{
				if (data[at] != 0xFF)
					throw new InvalidDataException("JPEG: marker expected");

				var marker = data[at + 1];
				at += 2;
				if (marker == 0xD9)
					break;

				var length = (data[at] << 8) | data[at + 1];
				var segment = at + 2;
				var end = at + length;
				switch (marker)
				{
					case 0xDB:
						for (var p = segment; p < end;)
						{
							var precision = data[p] >> 4;
							var id = data[p] & 15;
							p++;
							var table = new int[64];
							for (var k = 0; k < 64; k++)
							{
								table[ZigZag[k]] = precision == 0 ? data[p] : (data[p] << 8) | data[p + 1];
								p += precision == 0 ? 1 : 2;
							}

							quant[id] = table;
						}

						break;

					case 0xC4:
						for (var p = segment; p < end;)
						{
							var tableClass = data[p] >> 4;
							var id = data[p] & 15;
							var counts = new int[17];
							var total = 0;
							for (var l = 1; l <= 16; l++)
								total += counts[l] = data[p + l];

							var h = new Huffman { Values = new byte[total] };
							Array.Copy(data, p + 17, h.Values, 0, total);
							int code = 0, k = 0;
							for (var l = 1; l <= 16; l++)
							{
								h.ValPtr[l] = k;
								h.MinCode[l] = code;
								code += counts[l];
								k += counts[l];
								h.MaxCode[l] = counts[l] > 0 ? code - 1 : -1;
								code <<= 1;
							}

							h.MaxCode[17] = int.MaxValue;
							(tableClass == 0 ? dc : ac)[id] = h;
							p += 17 + total;
						}

						break;

					case 0xC0:
					case 0xC1:
						height = (data[segment + 1] << 8) | data[segment + 2];
						width = (data[segment + 3] << 8) | data[segment + 4];
						components = new Component[data[segment + 5]];
						for (var i = 0; i < components.Length; i++)
						{
							var p = segment + 6 + 3 * i;
							components[i] = new Component { Id = data[p], H = data[p + 1] >> 4, V = data[p + 1] & 15, Quant = data[p + 2] };
						}

						break;

					case 0xC2:
						throw new InvalidDataException("JPEG: progressive JPEGs are not supported");

					case 0xDD:
						restart = (data[segment] << 8) | data[segment + 1];
						break;

					case 0xDA:
						if (components == null)
							throw new InvalidDataException("JPEG: scan before frame");

						var count = data[segment];
						for (var i = 0; i < count; i++)
						{
							var id = data[segment + 1 + 2 * i];
							var c = Array.Find(components, x => x.Id == id);
							c.Dc = data[segment + 2 + 2 * i] >> 4;
							c.Ac = data[segment + 2 + 2 * i] & 15;
						}

						at = DecodeScan(data, end, width, height, components, quant, dc, ac, restart);
						continue;
				}

				at = end;
			}

			if (components == null)
				throw new InvalidDataException("JPEG: no frame");

			return (ToRgba(width, height, components), width, height);
		}

		// The entropy-coded data from at; returns where it ends (the next marker).
		static int DecodeScan(byte[] data, int at, int width, int height, Component[] components, int[][] quant, Huffman[] dc,
			Huffman[] ac, int restart)
		{
			var hMax = 1;
			var vMax = 1;
			foreach (var c in components)
			{
				hMax = Math.Max(hMax, c.H);
				vMax = Math.Max(vMax, c.V);
			}

			var mcusX = (width + 8 * hMax - 1) / (8 * hMax);
			var mcusY = (height + 8 * vMax - 1) / (8 * vMax);
			foreach (var c in components)
			{
				c.BlocksPerLine = mcusX * c.H;
				c.BlocksPerColumn = mcusY * c.V;
				c.Pixels = new byte[c.BlocksPerLine * 8 * c.BlocksPerColumn * 8];
			}

			var reader = new BitReader(data, at);
			var block = new int[64];
			var mcus = mcusX * mcusY;
			for (var mcu = 0; mcu < mcus; mcu++)
			{
				if (restart > 0 && mcu > 0 && mcu % restart == 0)
				{
					reader.Restart();
					foreach (var c in components)
						c.Pred = 0;
				}

				var mx = mcu % mcusX;
				var my = mcu / mcusX;
				foreach (var c in components)
				{
					for (var v = 0; v < c.V; v++)
					{
						for (var h = 0; h < c.H; h++)
						{
							DecodeBlock(reader, c, dc[c.Dc], ac[c.Ac], quant[c.Quant], block);
							Idct(block, c.Pixels, c.BlocksPerLine * 8, (my * c.V + v) * 8, (mx * c.H + h) * 8);
						}
					}
				}
			}

			return reader.End();
		}

		static void DecodeBlock(BitReader reader, Component c, Huffman dc, Huffman ac, int[] quant, int[] block)
		{
			Array.Clear(block);
			var t = reader.Decode(dc);
			c.Pred += t == 0 ? 0 : Extend(reader.Bits(t), t);
			block[0] = c.Pred * quant[0];
			for (var k = 1; k < 64;)
			{
				var rs = reader.Decode(ac);
				var r = rs >> 4;
				var s = rs & 15;
				if (s == 0)
				{
					if (r != 15)
						break;

					k += 16;
					continue;
				}

				k += r;
				if (k > 63)
					break;

				block[ZigZag[k]] = Extend(reader.Bits(s), s) * quant[ZigZag[k]];
				k++;
			}
		}

		static int Extend(int v, int t) => v < 1 << (t - 1) ? v - (1 << t) + 1 : v;

		// A straightforward separable inverse DCT, rounded and level-shifted into the component's pixels.
		static readonly double[,] Cosines = BuildCosines();

		static double[,] BuildCosines()
		{
			var t = new double[8, 8];
			for (var x = 0; x < 8; x++)
				for (var u = 0; u < 8; u++)
					t[x, u] = (u == 0 ? Math.Sqrt(0.5) : 1) * Math.Cos((2 * x + 1) * u * Math.PI / 16);

			return t;
		}

		static void Idct(int[] block, byte[] pixels, int stride, int top, int left)
		{
			var rows = new double[64];
			for (var y = 0; y < 8; y++)
			{
				for (var x = 0; x < 8; x++)
				{
					double s = 0;
					for (var u = 0; u < 8; u++)
						s += Cosines[x, u] * block[y * 8 + u];

					rows[y * 8 + x] = s / 2;
				}
			}

			for (var x = 0; x < 8; x++)
			{
				for (var y = 0; y < 8; y++)
				{
					double s = 0;
					for (var v = 0; v < 8; v++)
						s += Cosines[y, v] * rows[v * 8 + x];

					var value = (int)Math.Round(s / 2 + 128);
					pixels[(top + y) * stride + left + x] = (byte)Math.Clamp(value, 0, 255);
				}
			}
		}

		static byte[] ToRgba(int width, int height, Component[] components)
		{
			var hMax = 1;
			var vMax = 1;
			foreach (var c in components)
			{
				hMax = Math.Max(hMax, c.H);
				vMax = Math.Max(vMax, c.V);
			}

			int Sample(Component c, int x, int y) =>
				c.Pixels[y * c.V / vMax * c.BlocksPerLine * 8 + x * c.H / hMax];

			var rgba = new byte[width * height * 4];
			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					var o = (y * width + x) * 4;
					if (components.Length == 1)
					{
						rgba[o] = rgba[o + 1] = rgba[o + 2] = (byte)Sample(components[0], x, y);
					}
					else
					{
						var lum = Sample(components[0], x, y);
						var cb = Sample(components[1], x, y) - 128;
						var cr = Sample(components[2], x, y) - 128;
						rgba[o] = (byte)Math.Clamp((int)Math.Round(lum + 1.402 * cr), 0, 255);
						rgba[o + 1] = (byte)Math.Clamp((int)Math.Round(lum - 0.344136 * cb - 0.714136 * cr), 0, 255);
						rgba[o + 2] = (byte)Math.Clamp((int)Math.Round(lum + 1.772 * cb), 0, 255);
					}

					rgba[o + 3] = 255;
				}
			}

			return rgba;
		}

		sealed class BitReader
		{
			readonly byte[] data;
			int at, bits, count;

			public BitReader(byte[] data, int at)
			{
				this.data = data;
				this.at = at;
			}

			int NextByte()
			{
				if (at >= data.Length)
					return 0;

				var b = data[at];
				if (b == 0xFF)
				{
					var next = at + 1 < data.Length ? data[at + 1] : 0;
					if (next == 0)
					{
						at += 2;
						return 0xFF;
					}

					// A marker: feed zeros, don't step over it.
					return 0;
				}

				at++;
				return b;
			}

			int Bit()
			{
				if (count == 0)
				{
					bits = NextByte();
					count = 8;
				}

				count--;
				return (bits >> count) & 1;
			}

			public int Bits(int n)
			{
				var v = 0;
				for (var i = 0; i < n; i++)
					v = (v << 1) | Bit();

				return v;
			}

			public int Decode(Huffman h)
			{
				var code = 0;
				for (var l = 1; l <= 16; l++)
				{
					code = (code << 1) | Bit();
					if (h.MaxCode[l] >= 0 && code <= h.MaxCode[l] && code >= h.MinCode[l])
						return h.Values[h.ValPtr[l] + code - h.MinCode[l]];
				}

				throw new InvalidDataException("JPEG: bad Huffman code");
			}

			// Skip to the restart marker and past it.
			public void Restart()
			{
				count = 0;
				while (at + 1 < data.Length && !(data[at] == 0xFF && data[at + 1] >= 0xD0 && data[at + 1] <= 0xD7))
					at++;

				at += 2;
			}

			// Where the scan ends: the next marker that isn't a restart.
			public int End()
			{
				while (at + 1 < data.Length && !(data[at] == 0xFF && data[at + 1] != 0 && (data[at + 1] < 0xD0 || data[at + 1] > 0xD7)))
					at++;

				return at;
			}
		}
	}
}
