# Doodad (`.doo`, `JW2_03.TRC`) and single sprite (`.spz` + `.pnt`)

## `.doo`

Map decorations: 15 entries per tileset in `JW2_03.TRC`, in this order from entry `tileset × 15`: the `.trt`, 12
doodads (`J00`–`J11`, `D00`–`D11`, `R00`–`R11`), `Dong.doo` (동굴, cave) and `Jae.doo` (제단, altar).

| Offset | Size | Content |
|---|---|---|
| 0x000 | 0x39c | Record: `u32` 100, the artist's source path (`D:\Jwar2\03_tiles_\Jungle\Tree\jun00`), name at 0x108, a few small integers from 0x14c (not decoded) |
| 0x39c | 0x400 | Palette: 256 × (R, G, B, 0); index 1 is the shadow, as in [`.chr`](chr.md) |
| 0x79c | | 4 blocks, one per map variant: `u32` frame count (always 1), `u32` byte size of the frames, then [`.chr` frames](chr.md#frame-header-32-bytes) |
| end | 8 | `u32` 10, `u32` 0 (in every file) |

Jungle: J00–J06 trees and palms, J07 rock/idol, J08 skeleton, J09 hut frame, J10 totem, J11 huts; the cave and
the altar. Ruin and Dungeon follow the same numbering with their own art.

Placed by `MAP3` kind 3 (see [map.md](map.md#map3-objects)). The cave is code 48–51; no shipped map places the
altar.

## `.spz` + `.pnt`

A `.spz` is one `.chr` frame (32-byte header + row data) without a palette; the matching `.pnt` is a 256 × RGB0
palette (1,024 bytes). Used for UI and effects (`JW2_02`, `JW2_07`).

Berry bushes: `JW2_07` `Bery<v><s>.spz` with palette `Bery<v>.pnt`, *v* = variant 0–3, *s* = depletion stage 0–3
(full to almost empty).

## Confirmed by

- `research/tools/doo_preview.py`: every `.doo` of the three tilesets parses to exactly 4 blocks and the trailer.
- `research/tools/map_objects.py` draws doodads, bushes and creatures on the rendered maps in the right places
  (`research/previews/maps/`).
