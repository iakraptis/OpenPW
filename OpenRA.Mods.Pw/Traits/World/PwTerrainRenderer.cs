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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Pw.Traits
{
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	[Desc("Draws Primitive Wars terrain: each cell blends up to six ground materials through a mask tile and adds up to ",
		"four hill and cliff overlays, the way Pw.exe does (docs/formats/map.md). Replaces TerrainRenderer; maps without ",
		"the terrain file (and the editor's tile previews) use the tileset's template images instead.")]
	public class PwTerrainRendererInfo : TraitInfo, ITiledTerrainRendererInfo
	{
		[FieldLoader.Require]
		[Desc("Original tileset (.trt, 3600 RGB565 tiles of 32x32) per tileset id.")]
		public readonly Dictionary<string, string> Tilesets = new();

		[Desc("Map package file with the original cell data.")]
		public readonly string TerrainFile = "terrain.bin";

		[Desc("Size of the sheets the composed cells are packed into.")]
		public readonly int SheetSize = 4096;

		// The template images are flat placeholders (the movement types); nothing to validate.
		bool ITiledTerrainRendererInfo.ValidateTileSprites(ITemplatedTerrainInfo terrainInfo, Action<string> onError) { return false; }

		public override object Create(ActorInitializer init) { return new PwTerrainRenderer(init.World, this); }
	}

	public sealed class PwTerrainRenderer : IRenderTerrain, IWorldLoaded, ITick, INotifyActorDisposing, ITiledTerrainRenderer
	{
		const int Tile = 32;
		const int TileBytes = Tile * Tile;
		const ushort Green = 0x07E0;
		const ushort Magenta = 0xF81F;

		// Tile ranges in the tileset (docs/formats/trt.md).
		const int LiquidBase = 1200;
		const int OverlayBase = 2400;
		const int RampBase = 2800;
		const int MaskBase = 3000;
		const int AlphaBase = 3400;
		const int LiquidMaterial = 6;
		static readonly int[] MaterialBase = { 0, 200, 400, 600, 800, 1000, LiquidBase };
		static readonly int[] LiquidFrames = { 0, 1, 2, 3, 4, 5, 4, 3, 2, 1 };

		readonly World world;
		readonly PwTerrainRendererInfo info;
		readonly DefaultTerrain terrainInfo;
		readonly DefaultTileCache tileCache;
		readonly Dictionary<(uint Ground, uint Overlay, int Variant, int Odd), Sprite> cache = new();
		readonly Dictionary<(int Frame, int Variant), Sprite> liquidSprites = new();
		readonly List<(CPos Cell, int Variant)> liquidCells = new();
		ushort[] tiles;
		SheetBuilder sheetBuilder;
		TerrainSpriteLayer spriteLayer;
		TerrainSpriteLayer liquidLayer;
		int liquidFrame;
		WorldRenderer worldRenderer;
		bool templateCells;
		bool disposed;

		public PwTerrainRenderer(World world, PwTerrainRendererInfo info)
		{
			this.world = world;
			this.info = info;
			terrainInfo = world.Map.Rules.TerrainInfo as DefaultTerrain;
			if (terrainInfo == null)
				throw new InvalidDataException("PwTerrainRenderer can only be used with the DefaultTerrain parser");

			tileCache = new DefaultTileCache(terrainInfo);
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			worldRenderer = wr;
			var map = world.Map;
			if (!info.Tilesets.TryGetValue(map.Tileset, out var tilesetFile) || !map.Package.Contains(info.TerrainFile))
			{
				// No original terrain: draw the tileset's template images, as TerrainRenderer does.
				templateCells = true;
				spriteLayer = new TerrainSpriteLayer(world, wr, tileCache.MissingTile, BlendMode.Alpha, world.Type != WorldType.Editor);
				foreach (var cell in map.AllCells)
					UpdateTemplateCell(cell);

				map.Tiles.CellEntryChanged += UpdateTemplateCell;
				return;
			}

			sheetBuilder = new SheetBuilder(SheetType.BGRA, info.SheetSize);
			var empty = sheetBuilder.Add(new byte[4 * TileBytes], SpriteFrameType.Bgra32, new Size(Tile, Tile));
			spriteLayer = new TerrainSpriteLayer(world, wr, empty, BlendMode.Alpha, world.Type != WorldType.Editor);

			liquidLayer = new TerrainSpriteLayer(world, wr, empty, BlendMode.Alpha, world.Type != WorldType.Editor);
			using (var s = Game.ModData.DefaultFileSystem.Open(tilesetFile))
				tiles = ReadUShorts(s, (int)(s.Length / 2));

			using (var s = map.Package.GetStream(info.TerrainFile))
			{
				if (s.ReadASCII(4) != "PWT1")
					throw new InvalidDataException($"{info.TerrainFile}: not a Primitive Wars terrain file");

				var width = s.ReadUInt16();
				var height = s.ReadUInt16();
				var originX = s.ReadUInt16();
				var originY = s.ReadUInt16();
				for (var y = 0; y < height; y++)
				{
					for (var x = 0; x < width; x++)
					{
						var ground = s.ReadUInt32();
						var overlay = s.ReadUInt32();
						var cell = new MPos(originX + x, originY + y).ToCPos(map);
						spriteLayer.Update(cell, CellSprite(ground, overlay, x, y, out var hasLiquid), null);
						if (hasLiquid)
						{
							var variant = y % 10 * 20 + x % 20;
							liquidCells.Add((cell, variant));
							liquidLayer.Update(cell, LiquidSprite(0, variant), null);
						}
					}
				}
			}
		}

		void UpdateTemplateCell(CPos cell)
		{
			var tile = world.Map.Tiles[cell];
			var palette = terrainInfo.Palette;
			if (terrainInfo.Templates.TryGetValue(tile.Type, out var template))
				palette = ((DefaultTerrainTemplateInfo)template).Palette ?? palette;

			spriteLayer.Update(cell, tileCache.TileSprite(tile), worldRenderer.Palette(palette));
		}

		static ushort[] ReadUShorts(Stream s, int count)
		{
			var bytes = s.ReadBytes(2 * count);
			var result = new ushort[count];
			Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
			return result;
		}

		// One cell, composed like Pw.exe draws it (FUN_00408700). Every pixel keeps its colour premultiplied by the share
		// that is not liquid, and that liquid share: the liquid itself is drawn underneath by liquidLayer, so it can animate
		// without a sprite per frame. Returns null for a cell that is all liquid.
		Sprite CellSprite(uint ground, uint overlay, int x, int y, out bool hasLiquid)
		{
			var variant = y % 10 * 20 + x % 20;
			var materials = new int[6];
			for (var i = 0; i < 6; i++)
				materials[i] = (int)(ground >> (3 * i)) & 7;

			hasLiquid = Array.IndexOf(materials, LiquidMaterial) >= 0;
			var plain = materials[0] == materials[1] && materials[2] == materials[3] && materials[4] == materials[5];
			var key = (ground & 0x03FFFFFF, overlay, variant, plain ? 0 : x & 1);
			if (cache.TryGetValue(key, out var sprite))
				return sprite;

			var canvas = new Canvas();
			int Material(int m) => (MaterialBase[m] + variant) * TileBytes;
			if (plain)
			{
				for (var i = 0; i < TileBytes; i++)
					canvas.SetGround(i, tiles, Material(materials[0]), materials[0], 0, 0, 1f);
			}
			else
			{
				// FUN_004089d0: the mask's green, blue or red channel blends one material pair.
				var shape = (int)(ground >> 18) & 0x3F;
				var shapeVariant = (int)(ground >> 24) & 3;
				var maskIndex = shape * 2 + shapeVariant;
				var mask = (MaskBase + ((x & 1) != 0 ? 0 : 1) + (maskIndex < MaskTiles.Length ? MaskTiles[maskIndex] : 0)) * TileBytes;
				for (var i = 0; i < TileBytes; i++)
				{
					var m = tiles[mask + i];
					int r = m >> 11, g = (m >> 5) & 63, b = m & 31;
					int alpha, first, second;
					if (g != 0)
					{
						alpha = g >> 1;
						first = materials[3];
						second = materials[2];
					}
					else if (b != 0)
					{
						alpha = b;
						first = materials[5];
						second = materials[4];
					}
					else
					{
						alpha = r;
						first = materials[1];
						second = materials[0];
					}

					canvas.SetGround(i, tiles, Material(first), first, Material(second), second, Weight(alpha));
				}
			}

			if ((overlay & 0x3F) == 0x3F)
			{
				// Large ramp piece.
				var index = (int)((overlay >> 16) & 0x3F) * 41 + (int)((overlay >> 24) & 0x3F);
				if (index < RampTiles.Length)
				{
					var piece = (RampBase + RampTiles[index]) * TileBytes;
					if (RampAlphaTiles[index] < 0)
						canvas.Keyed(tiles, piece, Material(5));
					else
						canvas.AlphaOver(tiles, piece, (AlphaBase + RampAlphaTiles[index]) * TileBytes);
				}
			}
			else
			{
				for (var layer = 3; layer >= 0; layer--)
				{
					var p = (int)(overlay >> (8 * layer)) & 0x3F;
					if (p == 0 || p >= AlphaTiles.Length)
						continue;

					var v = (int)(overlay >> (8 * layer + 6)) & 3;
					var piece = (OverlayBase + OverlayTiles[p * 4 + v]) * TileBytes;
					if (AlphaTiles[p] >= 0)
						canvas.AlphaOver(tiles, piece, (AlphaBase + AlphaTiles[p]) * TileBytes);
					else if (MagentaPieces[p] == 1)
					{
						// Pw.exe redraws layer 0's piece here, whatever the layer (FUN_004092b0).
						var p0 = (int)overlay & 0x3F;
						var v0 = (int)(overlay >> 6) & 3;
						canvas.Keyed(tiles, (OverlayBase + OverlayTiles[p0 * 4 + v0]) * TileBytes, Material(5));
					}
					else
						canvas.Keyed(tiles, piece, -1);
				}
			}

			sprite = canvas.AllLiquid ? null : sheetBuilder.Add(canvas.ToBgra(), SpriteFrameType.Bgra32, new Size(Tile, Tile), true);
			cache[key] = sprite;
			return sprite;
		}

		// Pw.exe blends a pair as first * (alpha + 1) / 32 + second * (31 - alpha) / 32; alpha 31 is the first alone.
		static float Weight(int alpha) { return alpha == 31 ? 1f : (alpha + 1) / 32f; }

		Sprite LiquidSprite(int frame, int variant)
		{
			if (!liquidSprites.TryGetValue((frame, variant), out var sprite))
			{
				var canvas = new Canvas();
				var tile = (LiquidBase + frame * 200 + variant) * TileBytes;
				for (var i = 0; i < TileBytes; i++)
					canvas.SetGround(i, tiles, tile, 0, 0, 0, 1f);

				sprite = sheetBuilder.Add(canvas.ToBgra(), SpriteFrameType.Bgra32, new Size(Tile, Tile), true);
				liquidSprites[(frame, variant)] = sprite;
			}

			return sprite;
		}

		void ITick.Tick(Actor self)
		{
			if (liquidCells.Count == 0)
				return;

			// Pw.exe 0x468bc9: the liquid steps through frames 0-5 and back, 4 ticks each.
			var frame = LiquidFrames[world.WorldTick / 4 % LiquidFrames.Length];
			if (frame == liquidFrame)
				return;

			liquidFrame = frame;
			foreach (var (cell, variant) in liquidCells)
				liquidLayer.Update(cell, LiquidSprite(frame, variant), null);
		}

		// A cell's pixels at 8 bits per channel: colour premultiplied by the part that is not liquid, plus the liquid part.
		sealed class Canvas
		{
			readonly float[] r = new float[TileBytes];
			readonly float[] g = new float[TileBytes];
			readonly float[] b = new float[TileBytes];
			readonly float[] liquid = new float[TileBytes];

			public bool AllLiquid
			{
				get
				{
					foreach (var l in liquid)
						if (l < 1f)
							return false;

					return true;
				}
			}

			static void Rgb(ushort c, out float red, out float green, out float blue)
			{
				red = (c >> 11) * 255f / 31;
				green = ((c >> 5) & 63) * 255f / 63;
				blue = (c & 31) * 255f / 31;
			}

			// first * k + second * (1 - k); a liquid material adds its share to the liquid part instead.
			public void SetGround(int i, ushort[] tiles, int first, int firstMaterial, int second, int secondMaterial, float k)
			{
				r[i] = g[i] = b[i] = liquid[i] = 0;
				Add(i, tiles[first + i], firstMaterial, k);
				if (k < 1f)
					Add(i, tiles[second + i], secondMaterial, 1f - k);
			}

			void Add(int i, ushort c, int material, float k)
			{
				if (material == LiquidMaterial)
				{
					liquid[i] += k;
					return;
				}

				Rgb(c, out var red, out var green, out var blue);
				r[i] += k * red;
				g[i] += k * green;
				b[i] += k * blue;
			}

			// Green is transparent; magenta shows ground material 5 (fill), when one is given.
			public void Keyed(ushort[] tiles, int piece, int fill)
			{
				for (var i = 0; i < TileBytes; i++)
				{
					var c = tiles[piece + i];
					if (c == Green)
						continue;

					Rgb(c == Magenta && fill >= 0 ? tiles[fill + i] : c, out r[i], out g[i], out b[i]);
					liquid[i] = 0;
				}
			}

			// The alpha tile's blue channel blends the piece over what is there.
			public void AlphaOver(ushort[] tiles, int piece, int alphaTile)
			{
				for (var i = 0; i < TileBytes; i++)
				{
					var c = tiles[piece + i];
					var a = tiles[alphaTile + i] & 31;
					if (c == Green || a == 0)
						continue;

					var k = Weight(a);
					Rgb(c, out var red, out var green, out var blue);
					r[i] = k * red + (1 - k) * r[i];
					g[i] = k * green + (1 - k) * g[i];
					b[i] = k * blue + (1 - k) * b[i];
					liquid[i] *= 1 - k;
				}
			}

			public byte[] ToBgra()
			{
				var data = new byte[4 * TileBytes];
				for (var i = 0; i < TileBytes; i++)
				{
					data[4 * i] = (byte)Math.Round(b[i]);
					data[4 * i + 1] = (byte)Math.Round(g[i]);
					data[4 * i + 2] = (byte)Math.Round(r[i]);
					data[4 * i + 3] = (byte)Math.Round(255 * (1 - liquid[i]));
				}

				return data;
			}
		}

		void IRenderTerrain.RenderTerrain(WorldRenderer wr, Viewport viewport)
		{
			liquidLayer?.Draw(wr.Viewport);
			spriteLayer.Draw(wr.Viewport);

			foreach (var r in wr.World.WorldActor.TraitsImplementing<IRenderOverlay>())
				r.Render(wr);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			if (templateCells)
				world.Map.Tiles.CellEntryChanged -= UpdateTemplateCell;

			spriteLayer?.Dispose();
			liquidLayer?.Dispose();
			sheetBuilder?.Dispose();
			tileCache.Dispose();
			disposed = true;
		}

		// The editor's terrain tools preview templates through these (same as TerrainRenderer).
		Sprite ITiledTerrainRenderer.MissingTile => tileCache.MissingTile;

		Sprite ITiledTerrainRenderer.TileSprite(TerrainTile r, int? variant) { return tileCache.TileSprite(r, variant); }

		Rectangle ITiledTerrainRenderer.TemplateBounds(TerrainTemplateInfo template)
		{
			var sprite = tileCache.TileSprite(new TerrainTile(template.Id, 0));
			var size = world.Map.Grid.TileSize;
			return new Rectangle(-size.Width / 2, -size.Height / 2,
				Math.Max(size.Width, (int)sprite.Size.X) * template.Size.X, Math.Max(size.Height, (int)sprite.Size.Y) * template.Size.Y);
		}

		IEnumerable<IRenderable> ITiledTerrainRenderer.RenderUIPreview(WorldRenderer wr, TerrainTemplateInfo t, int2 origin, float scale)
		{
			var sprite = tileCache.TileSprite(new TerrainTile(t.Id, 0), 0);
			var palette = wr.Palette((t as DefaultTerrainTemplateInfo)?.Palette ?? terrainInfo.Palette);
			yield return new UISpriteRenderable(sprite, WPos.Zero, origin - (0.5f * scale * sprite.Size.XY).ToInt2(), 0, palette, scale);
		}

		IEnumerable<IRenderable> ITiledTerrainRenderer.RenderPreview(WorldRenderer wr, TerrainTemplateInfo t, WPos origin)
		{
			var sprite = tileCache.TileSprite(new TerrainTile(t.Id, 0), 0);
			var palette = wr.Palette((t as DefaultTerrainTemplateInfo)?.Palette ?? terrainInfo.Palette);
			yield return new SpriteRenderable(sprite, origin, WVec.Zero, 0, palette, 1f, 1f, float3.Ones, TintModifiers.None, false);
		}

		// Lookup tables from Pw.exe (docs/formats/map.md): mask tile per shape and variant (0x5356c8), overlay tile per
		// piece and variant (0x53471c), hilltop pieces filled with ground material 5 (0x534a4c), alpha tile per piece
		// (0x534a80), large ramp tiles and their alpha tiles (0x53448c, 0x5345d4).
		static readonly int[] MaskTiles =
		{
			133, 333, 21, 221, 24, 224, 27, 227, 30, 230, 33, 233, 53, 253, 73, 273, 93, 293, 36, 236,
			56, 256, 76, 276, 61, 261, 64, 264, 67, 267, 70, 270, 101, 301, 121, 321, 104, 304, 124, 324,
			107, 307, 127, 327, 110, 310, 130, 330, 181, 381, 164, 364, 184, 384, 167, 367, 170, 370, 173, 373,
			193, 393, 196, 396, 187, 387, 190, 390, 116, 316, 156, 356, 158, 358, 118, 318, 78, 278, 58, 258,
			198, 398, 38, 238, 0, 200, 3, 203, 6, 206, 9, 209, 12, 212, 15, 215, 18, 218, 40, 240,
			43, 243, 161, 361, 46, 246, 49, 249, 80, 280
		};

		static readonly int[] OverlayTiles =
		{
			0, 0, 0, 0, 23, 33, 223, 233, 24, 34, 224, 234, 25, 35, 225, 235, 26, 36, 226, 236,
			42, 52, 242, 252, 43, 53, 243, 253, 44, 54, 244, 254, 45, 55, 245, 255, 46, 56, 246, 256,
			47, 57, 247, 257, 61, 71, 261, 271, 62, 72, 262, 272, 63, 73, 263, 273, 64, 74, 264, 274,
			65, 75, 265, 275, 66, 76, 266, 276, 67, 77, 267, 277, 68, 78, 268, 278, 81, 91, 281, 291,
			82, 92, 282, 292, 83, 93, 283, 293, 84, 94, 284, 294, 85, 95, 285, 295, 86, 96, 286, 296,
			87, 97, 287, 297, 88, 98, 288, 298, 101, 111, 301, 311, 102, 112, 302, 312, 103, 113, 303, 313,
			104, 114, 304, 314, 105, 115, 305, 315, 106, 116, 306, 316, 107, 117, 307, 317, 108, 118, 308, 318,
			121, 131, 321, 331, 122, 132, 322, 332, 123, 133, 323, 333, 124, 134, 324, 334, 125, 135, 325, 335,
			126, 136, 326, 336, 127, 137, 327, 337, 128, 138, 328, 338, 143, 153, 343, 353, 144, 154, 344, 354,
			145, 155, 345, 355, 146, 156, 346, 356, 161, 171, 361, 371, 162, 172, 362, 372, 181, 191, 381, 391,
			182, 192, 382, 392
		};

		static readonly int[] MagentaPieces =
		{
			0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1
		};

		static readonly int[] AlphaTiles =
		{
			-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
			-1, -1, -1, -1, -1, -1, -1, 23, 24, -1, -1, -1, -1, 29, 30, 43, 44, 45, 46, 47,
			48, 49, 50, 65, 66, 67, 68, -1, -1, -1, -1
		};

		static readonly int[] RampTiles =
		{
			0, 21, 22, 41, 42, 43, 44, 61, 62, 63, 64, 65, 66, 81, 82, 83, 84, 85, 86, 101,
			102, 103, 104, 105, 106, 121, 122, 123, 124, 125, 126, 141, 142, 143, 144, 145, 146, 163, 164, 165,
			166, 0, 31, 32, 49, 50, 51, 52, 67, 68, 69, 70, 71, 72, 87, 88, 89, 90, 91, 92,
			107, 108, 109, 110, 111, 112, 127, 128, 129, 130, 131, 132, 147, 148, 149, 150, 151, 152, 167, 168,
			169, 170
		};

		static readonly int[] RampAlphaTiles =
		{
			-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 45, 46, -1, -1, -1, -1, 65,
			21, -1, -1, -1, -1, 45, 46, -1, -1, -1, -1, 65, 66, 45, 46, 47, 48, 65, 66, 67,
			68, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 47, 48,
			-1, -1, -1, -1, 22, 68, -1, -1, -1, -1, 47, 48, 45, 46, 47, 48, 67, 68, 65, 66,
			67, 68
		};
	}
}
