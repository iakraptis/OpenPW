# Terrain tileset (`.trt`, `JW2_03.TRC`)

Three tilesets: `Jungle.trt`, `Ruin.trt`, `Dungeon.trt`. Each is 7,372,800 bytes of raw 16-bit RGB565 pixels with
no header: **3,600 tiles of 32 × 32**, stored tile after tile (one tile = 0x800 bytes). Laid out 20 tiles wide
(640 × 5,760 px), the file is one sheet; all three tilesets share the same layout and only the art differs.

| Tiles | Rows (of 20) | Content |
|---|---|---|
| 0–1199 | 0–59 | Six ground **materials**, 200 tiles each (one 20 × 10 seamless texture, 640 × 320 px) |
| 1200–2399 | 60–119 | **Liquid**: 6 animation frames of 200 tiles (water in Jungle, mud in Ruin, lava in Dungeon) |
| 2400–2799 | 120–139 | Hill / cliff **overlay pieces**, two art sets; pure green (`0x07E0`) is transparent, magenta (`0xF81F`) shows ground material 5 (the plateau top) |
| 2800–2999 | 140–149 | Large ramp pieces (overlay code `0x3f`) |
| 3000–3399 | 150–169 | **Blend masks**: red, green and blue channels, one per material pair |
| 3400–3599 | 170–179 | **Alpha tiles** for soft overlay edges (blue channel = alpha 0–31), plus the artists' numbered layout chart |

Material order (index in the map's 3-bit fields; base tile in `Pw.exe` at `0x547354`):

| Index | Base | Jungle |
|---|---|---|
| 0 | 0 | sand |
| 1 | 200 | dry earth |
| 2 | 400 | grass |
| 3 | 600 | rough earth |
| 4 | 800 | patchy grass |
| 5 | 1000 | dark grass (also the hilltop fill) |
| 6 | 1200 + frame × 200 | liquid |

A cell at (x, y) uses tile `base + (y mod 10) × 20 + (x mod 20)` of its material (`FUN_0040aac0`), so the
textures repeat every 20 × 10 cells without seams.

How the map combines these tiles is in [map.md](map.md#terrain).

## Confirmed by

- Ghidra: `FUN_004088a0` (plain cell copies tile `base + variant` from the tileset at `index * 0x800`),
  `FUN_0040ab00` (material base, liquid frame), `FUN_0040aac0` (texture position).
- `research/tools/map_render.py` renders whole maps from it without seams (`research/previews/maps/`).
