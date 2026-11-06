# Character file (`.chr`, `JW2_09.TRC`)

One file per unit or building type: game data, palette, animation frames and sounds. `JW2_09.TRC` holds 170
entries (`0xaa`), loaded by index into a table of type records. Entries named `Null.chr` are 1-byte empty slots.

## Layout

| Offset | Size | Content |
|---|---|---|
| 0x0000 | 0x24bc (9404) | Type record, copied as-is into the game's type table (`0x677248 + index * 0x24bc`) |
| 0x24bc | 0x400 | Palette: 256 × (R, G, B, 0) |
| 0x28bc | | Frames: 14 groups, each `count` × (32-byte header + data) |
| | | Sounds: 9 groups, each `count` complete `RIFF/WAVE` files back to back, up to the end of the file |

### Type record (known fields)

The game loads record *n* to `0x677248 + n * 0x24bc`, so a field at offset *o* is the address `0x677248 + o` in
Ghidra. `research/tools/ghidra/TableRefsPw.java` lists every code reference by offset (139 distinct fields are
used); `research/tools/chr_stats.py` dumps the fields below to CSV. Indices below 96 are units, 96 and up are
buildings.

Confidence: **C** confirmed in decompiled code, **D** deduced from the values only.

| Offset | Type | Field | | Evidence |
|---|---|---|---|---|
| 0x0000 | `u32` + text | 100, then the artist's source path (`D:\Jwar2\0 Character\...`) | C | |
| 0x010c | text | Name (`BuildMan`, `Soldier (Fire Arrow)`) | C | `LEA` in `0x46e035`… |
| 0x014c | `i32` | Kind: 0 combat unit, 1 wild, 2 non-combat (buildings, Revolver) | D | `FUN_004613f9` tests `!= 2` |
| 0x0154 | `i32` | Hit points (base) | C | `FUN_00458b2c` level-up, `FUN_004613f9` spawn |
| 0x0158 | `i32` | Attack (base) | C | same |
| 0x015c | `i32` | Armour (base) | C | same |
| 0x0160, 0x0164 | `i32` | Two more levelled stats (zero in the sampled units) | C | same |
| 0x0168–0x0178 | `i32[5]` | Level-9 caps of the five stats above | C | `FUN_00458b2c` |
| 0x017c | `i32` | Movement class: 0 ground, 1 building, 2 Rhamphos only (?), 3 air | C/D | `== 1` starts construction in `FUN_004613f9` |
| 0x0184 | `i32` | Population used (units) / provided (House 10, Nest 10, Sanctuary 9) | D | AI divides its budget by it, `FUN_0041e670` |
| 0x018c | `i32` | Build / training time; construction runs `0x18c / 10` steps | C | `FUN_004613f9` |
| 0x0190 | `i32` | Cost (main resource) | C | subtracted from the player's resource, `FUN_0041e670` |
| 0x0194 | `i32` | Second cost; zero in every record so far | C | added to the statistics with 0x190 |
| 0x0198 | `i32` | Sight, pixels (224–384) | D | |
| 0x01a0, 0x01a4 | `i32` | Weapon, index into `JW2_12.TRC` (19 = `Velocis hit`, 37 = `Stone.wpn`) | D | names match for every unit |
| 0x01b0, 0x01b4 | `i32` | Attack range, pixels (32 melee) | D | |
| 0x0188 | `i32` | Transport size (1 workers, 2 soldiers) | C | subtracted from a transport's space, `0x45f334` |
| 0x01e8 | `i32` | Experience per level | C | `FUN_00458b2c` |
| 0x01f0 | `u32` | Spells: bit *n* = JW2_11 record *n* (RedElf 0x2 Thunder bolt, Warlock 0x7000) | C | copied to unit +0xf0, `0x460108`; tested in `FUN_0046c3a5` |
| 0x01f4 | `u32` | Flags; bit 2 = gains levels; bit 0x10 = living (kills feed Demon kill points) | C | `FUN_00458b2c`, `FUN_004578a9` |
| 0x01f8 | `u32` | Flags; bit 0x1 = detector (sees invisible units over its sight); 0x2 = invisible from the start (unit flag 0x40); 0x20 = gains kill points from kills (Demon); 0x4 ManaSpread, 0x8 Chief, 0x10 Unicorn (D) | C/D | `0x46832b`, `0x460118`, `FUN_004578a9` |
| 0x01fc, 0x0200 | `i32`, `i32[]` | Number and list of required buildings | D | Knight needs Blacksmith; Chief needs all six |
| 0x0240, 0x0244 | `i32`, `i32[]` | Workers: buildings they can build | D | Buildman: the 10 Primitive buildings |
| 0x0284, 0x0288 | `i32`, `i32[]` | Buildings: units they train | D | PowerHouse: Powerman, Giant |
| 0x02c8, 0x02cc | `i32`, `i32[]` | Buildings: upgrades they research (`Upgrade.upi` index) | D | LandIncreaser: 2–6 |
| 0x030c | `u8` | Hotkey letter | D | `B`uildman, `S`oldier, `K`night… |
| 0x030f, 0x0310 | `u8`, `u8[]` | Buildings: items they sell (`Item.itm` index) | D | Blacksmith: 9, 5, 6, 7 |
| 0x0330, 0x0334 | `i32` | Building footprint in 32-pixel cells (width, height) | D | Sanctuary 6 × 4 matches its 199 × 136 frame |
| 0x0350–0x035c | `i32[4]` | Additional bounds; meaning not confirmed | | |
| 0x0360 | `i32` | Actor collision rectangle left offset from its position; used by mode-4 projectiles | C | `FUN_0047afc0` |
| 0x0364 | `i32` | Actor collision rectangle top offset from its position; used by mode-4 projectiles | C | `FUN_0047afc0` |
| 0x0368 | `i32` | Actor collision rectangle width; used by mode-4 projectiles | C | `FUN_0047afc0` |
| 0x036c | `i32` | Actor collision rectangle height; used by mode-4 projectiles | C | `FUN_0047afc0` |
| 0x0370–0x0388 | `i32` | Other bounds; meaning not confirmed | | |
| 0x03cc + 0x200·(f−1) | `(i32, i32)[64]` | Walk step displacement in pixels for facing *f* = 1…8, one pair per walk frame | D | see [Movement](#movement) |
| 0x13cc | `i32` | Attack delay (Soldier 11, Buildman 19); `Level.lvt` row 0 reduces it by a % per level | C/D | `FUN_004594d2` |
| 0x13d0 | `i32` | Unknown (320 for ground units, 0 for Rhamphos) | | |
| 0x13d4 + 4·g | `i32[14]` | Length of the frame sequence of group *g* | D | matches every sequence |
| 0x140c + 0x100·g | `i32[64]` | Frame sequence of group *g*: frame (within the direction block) shown at each step | C | `0x459e79` |
| 0x2210 | `i32` | Total frame count | D | sum of 0x2214 |
| 0x2214 | `u32[14]` | Frame count per animation group | C | `FUN_004030e0` |
| 0x2248 + 0x20·g + 4·b | `i32` | First frame of direction block *b* = 1…5 in group *g* (entries 6–8 repeat the last) | C | `0x459e69` |
| 0x240c | `i32` | Facing offset into the direction tables (9 for most units, 0 for the non-rotating Revolver) | C | `0x459e53` |
| 0x2410, 0x2414 | `i32` | Unknown point (x, y), maybe the sprite centre (Rhamphos 53, 52; Blacksmith 72, 115) | | `FUN_0041cde0` |
| 0x2420 | `i32` | Attack sound chance: played 1 in *n* times | C | `FUN_004626c7` |
| 0x2424 | `u32[9]` | Sound count per sound group | C | `FUN_004030e0` |
| 0x2448, 0x244c, 0x2454, 0x2458 | `i32` | Buildings only: global sounds (`JW2_05` entries). 0x244c is 20 for every building (the select sound; 0x2448 is −1), 0x2454 22–25 by size (select below 75% HP, D), 0x2458 26–28 by size (destroyed) (−1 none) | C | `FUN_004624a3`, `FUN_00462616` |
| 0x245c | `i32` | Frame of the work animation that plays the work sound | C | `FUN_004627e1` |
| 0x247c | `i32[]` | Attack steps that strike (damage or launch the projectile), ended by −1: Soldier 13 of 18, Chief 5 and 12 | C | `FUN_00457a94`, `FUN_00457920` |
| 0x249c | `i32[]` | Attack frames that play the attack sound, ended by −1 | C | `FUN_004626c7` |

Tyrano merged units cost 0 (`TwinVelocis`, `TwinRhamphos`, `Mutant`): they are made by merging two units, never
built. Primitive equipped forms (`Soldier (Fire Arrow)`, …) have their own records: a unit becomes one when it
equips a Blacksmith item.

### Frame header (32 bytes)

| Offset | Field |
|---|---|
| 0x00 | `i32` width |
| 0x04 | `i32` height |
| 0x08 | `i32` x offset |
| 0x0c | `i32` y offset |
| 0x10 | `i32` x offset when drawn mirrored: always `1 − width − x offset` |
| 0x14 | `i32` y offset when drawn mirrored: always equal to the y offset |
| 0x18 | `i32` data size |
| 0x1c | `i32` zero |

### Frame data

Row by row, `height` rows. Each row is a `u16` byte length followed by codes:
- `00 n`: skip `n` transparent pixels;
- any other byte: one pixel with that palette index.

Every row decodes to exactly `width` pixels.

### Palette conventions

| Index | Use |
|---|---|
| 0 | Never drawn (transparency is a skip run) |
| 1 | Shadow (`255, 0, 255`); drawn translucent by the game |
| 245–254 | Player colour ramp (bright green in the files), the same for all four races (seen in game) |

The game draws to a 16-bit screen: each type's palette is converted once, index 1 halves the pixel under it, and
the others are copied (`FUN_0045ac5b`).

### Directions

Units face 8 ways, numbered 1–8 clockwise from north: N, NE, E, SE, S, SW, W, NW (screen y points down). A group
stores **5 direction blocks**, N, NE, E, SE, S; SW, W and NW draw SE, E and NE mirrored. The game looks up
`facing + record[0x240c]` in two tables in `Pw.exe`: the block number (`0x564444`) and a mirror flag (`0x56448c`).

| Facing | 1 N | 2 NE | 3 E | 4 SE | 5 S | 6 SW | 7 W | 8 NW |
|---|---|---|---|---|---|---|---|---|
| Block | 1 | 2 | 3 | 4 | 5 | 4 mirrored | 3 mirrored | 2 mirrored |

Block *b* of group *g* starts at frame `record[0x2248 + 0x20·g + 4·b]`. Blocks are usually equal (Soldier: 0, 12,
24, 36, 48), but not always: Buildman's carrying walk has 8 frames facing north and 16 in the other directions. A
converter must read the table, not divide by 5. Groups that are not directional (flyer deaths, building groups) have
a single block.

### Frame sequences

The frame shown at animation step *s* is

    group start + block start + record[0x140c + 0x100·g + 4·s]

for *s* below the sequence length `record[0x13d4 + 4·g]`. The sequence sets the timing: Soldier's stand repeats
each of its 12 frames twice (24 steps), and its attack holds frame 10 for three steps (18 steps for 16 frames).
OpenRA sequences need `Frames:` lists (or `Length` with repeats) built from these.

### Movement

Each facing has a list of (dx, dy) pixel steps, one per walk frame (`0x03cc + 0x200·(facing − 1)`). The walk speed
is in the data: Soldier moves 70 px per 12-frame cycle north (8, 6, 5, 5, 6, 8, …: faster on the stride), and (44, 44)
diagonally; Buildman moves 5 px per frame; Rhamphos 7.

### Animation groups

The four common groups are 0 **stand**, 1 **walk** (flying units: fly), 2 **attack**, 3 **die**. Units that only fly
have no stand group, and their death (6 frames) has no direction. Workers use more groups (`Buildman`):

| Group | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---|---|---|---|---|---|---|---|---|---|
| Buildman | stand | walk | attack | die | gather | walk carrying load A | walk carrying load B (fruit basket) | stand carrying A | stand carrying B | build |

Buildings: group 0 holds the construction stages and the finished building as the last frame (`Landincrease`: 4
frames). Group 11 is the working animation (`SkyHouse` 20, `Blacksmith` 21), and group 2 the attacking part of a
defence (`StoneTower` 1, `Bubble` 8).

## Sounds

Nine groups of standard `RIFF/WAVE` files, mono 16-bit at 11,025, 22,050 or 44,100 Hz, one after the other;
counts at `0x2424`. The game picks one at random from the group (`FUN_0046950d`).

| Group | Use | Trigger in `Pw.exe` |
|---|---|---|
| 0 | Select. Wild creatures (types 64–90): when hit while idle and not fighting back, they run to a random spot within 64 px and roar 1 in 4 | `FUN_004624a3` (selection), `FUN_00458278` (reaction to a hit); select and acknowledge are each limited to one per second |
| 1 | Acknowledge an order | `FUN_00462561`, called from about 20 order handlers |
| 2 | Attack | `FUN_004626c7`, on the frames listed at `0x249c`, 1 in `record[0x2420]` |
| 3 | Not played (`Wizard` has one) | |
| 4 | Die | `FUN_00462616` |
| 5 | Work (gathering, building) | `FUN_004627e1`, on frame `record[0x245c]` |
| 6, 7 | Not played; most units have one sound in group 7 | |
| 8 | Construction finished (workers only), local player only | `0x45e436` |

Buildings have no sounds of their own and play global sounds (see `0x2448`–`0x2458`).

## Confirmed by

- Ghidra: `FUN_004030e0` (record, palette, 14 frame groups with the size at `+0x18`, 9 sound groups).
- `research/tools/chr_probe.py`: `Landincrease` ends exactly after its frames.
- `research/tools/chr_preview.py`: contact sheets in `research/previews/chr/`. RGB order checked visually (BGR is
  wrong).
- `research/tools/chr_groupsheet.py`: one direction block per group; direction order checked visually on Tyranos.
- `research/tools/chr_sounds.py`: exports the WAVs. All 69 files with sounds (368 WAVs) end exactly after the
  last one.
- Ghidra `DecompileAtPw.java` and `research/tools/disasm.py` (capstone) for the frame selection at `0x459e40`,
  which has no regular function.
