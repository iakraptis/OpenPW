# Map (`.jw2`)

A `.jw2` map is a [TRC container](trc.md) of named sections (zlib packed). `FUN_00463976` reads each section into
a fixed buffer, in this order:

| Section | Size | Loaded to | Content |
|---|---|---|---|
| `JW2` | 0x17070 | `0x549784` | Magic `JWAR`, version 150, then run-time globals (save-game state); +0x143c and +0x1440: the heads of the live and free unit lists (`DAT_0054abc0` is +0x143c) |
| `P_MAP` | 0x170 | `0x5607f4` | Map header (below) |
| `P_SCENA` | 0x2898 | `0x560964` | Scenario: +4 number of players; +0x2808 and +0x2850 are 1 or 4 (unknown); rest zero in skirmish maps |
| `P_PLAYE` | 0x374 | `0x5631fc` | **Players**: resources and start positions (below) |
| `P_UB` | 0x550 | `0x563570` | Unit availability: 8 players × 170 types, one byte each; 1 everywhere in the shipped maps |
| `P_SCRIP` | variable | `0x8ec420` | Scenario script; empty in every skirmish map (the campaign's: [campaign.md](campaign.md)) |
| `OBB` | 0x15000 | `0x10b2350` | Run-time pool, 512 × 0xa8 (free-list links at +0xa0/+0xa4); empty in every map |
| `OBC` | 0xe8000 | `0x7fef08` | The **unit table**: placed units (below) |
| `OBI` | 0x7800 | `0xee2ed0` | Run-time pool, 512 × 0x3c (links at +0x34/+0x38); empty in every map |
| `RST` | 0xbd14 | `0x5806dc` | Empty in every map |
| `MAP1` | 0x40000 | `0xbd6fdc` | **Ground** layer (below) |
| `MAP2` | 0x40000 | `0xc16fdc` | **Overlay** layer: hills and cliffs (below) |
| `MAP3` | 0x40000 | `0xc56fdc` | **Objects**: doodads and berry bushes (below) |
| `BGI`, `FOGBGI`, `FOGMAP3`, `VSBGI` | 0x40000 | | Run-time vision and fog state (zero in shipped maps; `FOGMAP3` is a copy of `MAP3`) |
| `BRUSH`, `HILL_BRUSH` | 4 read | `0x567660` | Map editor brush data (the sections are larger; the game reads 4 bytes) |
| `Custom` | | | Read by `FUN_00419250`; empty in every map |

For converting the skirmish maps, `P_MAP`, `P_PLAYE`, `OBC`, `MAP1`, `MAP2` and `MAP3` are everything; the other
sections are run-time state or campaign data.

## `P_PLAYE`

Loaded to `0x5631fc`, so the resource array the cost code uses (`0x563340`) is at +0x144.

| Offset | Content |
|---|---|
| 0x000 | Local player (0 in files) |
| 0x0a8 | `i32[8]` 1, 2, 3, 0, 1, 2, 3, … (unknown, maybe colours or teams) |
| 0x144 | `i32[8]` Starting **berries** per player: 400 |
| 0x194 | `i32[8]` Second resource per player: 1000 (to confirm: water?) |
| 0x1e4 | `i32[20]` 1000 (unknown) |
| 0x234, 0x284 | `i32[20]` 1, 2, 4, … 2^19: per-slot bit masks (alliances) |
| 0x2d4 | **Start positions**: (x, y) pixel pairs, ended by −1 (one per player; matches `P_SCENA` +4 in every map) |

## `OBC`: placed units

The game's unit table itself: 2,048 slots of 0x1d0 bytes (the code indexes `0x7fef08 + 4 · 0x74 · i`; the campaign's
teams refer to units by slot). The units on the map are the **live list**: it starts at the byte offset (into
`OBC`) stored at `JW2` +0x143c (the game's `DAT_0054abc0`), and each slot's +0x1cc is the next one's, +0x1c8 the
previous one's (0 ends it). Every other slot is on the free list (head at `JW2` +0x1440): units deleted in the
editor keep their type, owner and place there, so a slot's own fields can't tell it from a live one (C: the game's
loops follow `[0x73]`, +0x1cc; in the original P01 the Demons start with the 3 Skeletons and 2 Zombies of the live
list, not the 50 Skeletons of all the slots). The skirmish maps place **only wild creatures** (owner 8): no player
units or buildings; the campaign places bases and armies ([campaign.md](campaign.md)). Deleted creatures: Dino
Hunting 119 live of 193, Dino Legend 124 of 144, Clover 118 of 120, Jurassic World 152 of 153; the other maps none.

| Offset | Content |
|---|---|
| 0x00 | Type (index into `JW2_09.TRC`, see [chr.md](chr.md)) |
| 0x04 | Owner: 0–7 players, 8 wildlife |
| 0x08 | Selection as the editor saved it: 0x80 selected, low 4 bits the control group (D: `FUN_00473228`, `FUN_00475e70`) |
| 0x0c | Low byte: the AI squad (0 none; [text.md](text.md)) |
| 0xa0 | Flags: bit 2 dead |
| 0x10, 0x18 | Hit points, maximum |
| 0x1c, 0x20 | Attack, armour (copied from the type; the AI's strength is HP + attack + armour) |
| 0x58 | The type's flags (`.chr` 0x1ec), copied |
| 0x60 | Activity: 1 idle (D: `FUN_004178a0`; the AI orders only idle units). Not a live flag: deleted units keep it |
| 0x84–0x90 | The current order: kind (4 move, 5 attack-move, 6 build), target unit, x, y (D: `FUN_00417ab0`, `FUN_00417b70`) |
| 0xa8 | Facing 1–8 |
| 0xb8, 0xbc | Position, pixels (a building's footprint top-left corner) |
| 0xc0–0xd4 | Copies of the position (movement targets) |
| 0x1c8, 0x1cc | Previous and next slot in its list, as byte offsets into `OBC` |

## `P_MAP`

| Offset | Type | Content |
|---|---|---|
| 0x000 | text | Map name (`(2) One VS One`) |
| 0x020 | text | Author (`Create By Wizard Soft`) |
| 0x160 | `i32` | Tileset: 0 Jungle, 1 Dungeon, 2 Ruin (`FUN_00463548` passes it to the loader `FUN_0040b190`, which reads `JW2_03.TRC` entry `index × 15`). `The X Cross` is Dungeon, `Rank/Four Ways` Ruin, the rest Jungle |
| 0x164 | `i32` | Width in cells (64, 96, 128, 176) |
| 0x168 | `i32` | Height in cells |
| 0x16c | `i32` | 1 for the normal maps, 2 for the `Rank/` maps; meaning unknown |

## Terrain

A cell is one 32 × 32 [tileset](trt.md) tile. `MAP1`, `MAP2` and `MAP3` are 256 × 256 grids of `u32`, row-major
(cell (x, y) at `(y * 256 + x) * 4`); only the top-left width × height is used. Older content can remain outside
it (`One VS One` has 128 × 128 cells of `MAP1` data for a 64 × 64 map).

Movement (`FUN_0045cf94`, one step of a unit; the check by movement class is `FUN_0045cae0`): a water unit needs the
water bit; a flyer goes anywhere; a hovering unit (class 2: Insect, Rhamphos, Warlock) needs the walkable or the water
bit and is otherwise checked as a ground unit; any other unit needs the walkable bit, no `MAP3` object (`& 0x700`) and
no building (`0x58e270` bit `0x20000000`: a building's footprint, cleared over it when the building goes, C,
`FUN_00461276`). No other unit is checked, so units walk through each other (D: no unit sets that bit in the code
surveyed). Height and the cliff art are not looked at, so a walkable cliff cell can be climbed. `(6) Dino Hunting` has
such ways onto two plateaus, a liquid layer under its cliffs and sea creatures placed on plateau grass; the original
game shows the same. The installer converts it as it is and adds `(6) Dino Hunting Fixed` (`Maps.cs`, `FixedCopy`):
walkable plateau cells touching walkable lowland made unwalkable (7 cells), the liquid material replaced where there
is no water bit, and no Plesiosaurs or Peleneustes.

The installer writes each map's preview (`map.png`, one pixel per cell, `Maps.cs`) with the original minimap's
formula (`FUN_00407be0`): the first pixel of the cell's six material tiles summed and shifted right by 3, or, under an
overlay, mixed half with the overlay's pixel (approximated by the hilltop material); plus `2 · height + 2` when the
height is not 0 (twice that for 6-bit green); times a grain of 6-8 eighths (random in the original, a hash here).

### `MAP1`: ground

| Bits | Content |
|---|---|
| 0–17 | Six 3-bit material indices *f0…f5*, used as three pairs (f0, f1), (f2, f3), (f4, f5) |
| 18–23 | Mask shape |
| 24–25 | Mask variant |
| 26–28 | Height level 0–3; vision compares it with the viewer's (`FUN_00467975`) |
| 29 | Walkable (`0x20000000`, tested by movement) |
| 30 | Water (`0x40000000`; all liquid cells) |
| 31 | Probably buildable: set on flat ground, clear on cliffs and ramps (D) |

Drawing (`FUN_00408700`):

- If f0 = f1, f2 = f3 and f4 = f5, the cell is one plain tile of material f0 (`FUN_004088a0`).
- Otherwise (`FUN_004089d0`) a **mask tile** is chosen: `3000 + (x odd ? 0 : 1) + maskTable[shape][variant]`
  (`FUN_00409920`, table at `0x5356c8`, 55 shapes × 2 variants). For every pixel of the mask:
  - green ≠ 0: blend f2 → f3 with alpha = green (6-bit, halved to 0–31);
  - else blue ≠ 0: blend f4 → f5 with alpha = blue;
  - else: blend f0 → f1 with alpha = red.

  `out = second · (a + 1) / 32 + first · (31 − a) / 32` per channel, and a = 31 is the second material alone.
  The materials are the tiles at the cell's texture position, so the blend stays seamless.
- Material 6 (liquid) cycles through 6 frames.

### `MAP2`: overlays

Four 8-bit layers per cell (byte 0 to 3), drawn from layer 3 down to 0. Each byte holds a piece (bits 0–5,
0 = none) and a variant (bits 6–7). The piece's tile is `2400 + overlayTable[piece][variant]` (`FUN_004099a0`,
table at `0x53471c`; variants 2–3 are the second art set at +200).

- Pure green is transparent.
- Pieces with an alpha tile (`0x534a80[piece]` ≥ 0) blend with `3400 + alpha` (blue channel) for soft edges
  (`FUN_00409560`).
- Hilltop pieces (`0x534a4c[piece]` = 1) show ground material 5 where they are magenta (`FUN_004092b0`). That
  routine always redraws layer 0's piece, whatever the layer.
- Byte 0 = `0x3f` marks a large ramp piece: tile `2800 + bigTable[byte2 × 41 + byte3]` (`0x53448c`), with the
  alpha tile from `0x5345d4` or a magenta fill when that entry is −1.

### `MAP3`: objects

| Bits | Content |
|---|---|
| 0–1 | Berry bush: depletion stage 0–3, set from the amount left (≥ 1500, 1000, 500, below) by `FUN_0045bb14` |
| 2–3 | Variant 0–3 (bushes); for doodads bits 0–7 are the code below |
| 8–10 | Kind: 1 berry bush, 3 doodad, 0 none |
| 11 | Second cell of a two-cell object (bushes take the cell to their right, value `0x900`) |
| 12–27 | Berry bush: amount left (4,000 in the shipped maps) |
| 28 | Run time: being harvested |

- **Doodad** (kind 3): code = bits 0–7; draws `JW2_03.TRC` entry `tileset × 15 + 1 + (code >> 2)`, variant
  `code & 3` (see [doo.md](doo.md)). Codes 48–51 are the cave.
- **Berry bush** (kind 1): `Bery<variant><stage>.spz` from `JW2_07` (see [doo.md](doo.md#spz--pnt)). The minimap
  shows bushes in blue; the AI counts the bushes and their amounts around its base (`FUN_0041e3c0`).

Objects are drawn with their frame offset from the cell's top-left corner. Berry groves sit next to the start
positions. `Three Bridge` has no bridge objects: its bridges are sand crossings in the terrain.

## Conversion to OpenRA

The terrain cannot be a classic tileset. Across all 17 maps there are 1,397 distinct plain cells but 91,013
distinct blended or overlaid ones. The mod therefore needs its own terrain renderer that blends the
materials with the mask tiles at run time (a shader over the tileset texture, or chunks rendered on the CPU),
fed from the three per-cell values. See [plan.md](../../plan.md) §4, Phase 1.

## Confirmed by

- Ghidra: `FUN_00463976` (section loader), `FUN_00408700` / `FUN_004088a0` / `FUN_004089d0` (ground),
  `FUN_00409020` and the functions it calls (overlays), `FUN_00407be0` (minimap: averages the six materials and
  brightens by height).
- `research/tools/map_objects.py` draws `MAP3` objects and `OBC` creatures over the terrain render; doodads,
  bushes and creatures land in sensible places (trees on land, flyers over water, groves at the starts).
- `research/tools/map_render.py` re-implements the drawing in Python. `One VS One`, `Clover`, `Three Bridge`
  (Jungle), `The X Cross` (Dungeon) and `Four Ways` (Ruin) render with clean blends, cliffs, ramps and hilltops
  (`research/previews/maps/`).
