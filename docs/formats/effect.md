# Weapon and spell effects (`JW2_12.TRC`, `JW2_11.TRC`)

`JW2_12` holds the weapons (`.wpn`, names cut to 12 characters: `Soldier arro`, `Fire-arrow.w`); `JW2_11` holds
the spells (`.maz`, `.ma`, `.m`). Both use one layout, read by `FUN_00413ae0` (weapons, table `0xe79bd8`) and
`FUN_00413de0` (spells, table `0xe9c798`), with 0xadc-byte records.

| Offset | Size | Content |
|---|---|---|
| 0x000 | 0xadc | Record: `u32` 100, artist path, name at 0x108, fields below |
| 0xadc | 0x400 | Palette (256 × RGB0), present only when the frame count is non-zero |
| | | Frames: one flat list of [`.chr` frames](chr.md#frame-header-32-bytes) |
| | | Sounds: `RIFF/WAVE` files, back to back, up to the end of the file |

Known record fields:

| Offset | Content |
|---|---|
| 0x148 | Own index: the number units use for their weapon ([chr.md](chr.md) 0x1a0; 37 = `Stone`) |
| 0x14c | 1 = fires a projectile, 0 = instant hit (melee) |
| 0x150 | Draw style: picks the effect's draw routine (C, `FUN_0047a8e0`); 2 for most, 0 for the fire and poison arrows and the trap |
| 0x154 | Target mask, tested bit by bit (C, `0x457c6d`); bit 3 (8) = air: melee and the egg thrower have 23 (no air), arrows 31, `PhonixFire` 8 (air only) |
| 0x190 | Projectile behavior mode: 2 tracks the target (`FUN_0047aee5`, `FUN_0047acd6`); 4 advances in 2-pixel coordinate increments (both coordinates change on diagonal steps), tests each point against eligible actors' axis-aligned collision rectangles from their `.chr` fields 0x360–0x36c, keeps a 16-entry hit list, and continues along the path (`FUN_0047afc0`, `0x4772f1`, `0x477377`, vector at `0x66b05c`); 0 has neither behavior and flies to its fixed impact point (`FUN_0047acd6`), where any splash is applied. Confirmed in code (C). A zero here does not mean there is no projectile: see 0x14c |
| 0x194 | Projectile speed, pixels per tick (arrows 18, `Stone` 20, `Wizard firebolt` 11) (D) |
| 0x1f8 | **Impact splash radius** in pixels; 0 = no impact splash. Loaded at `0x47713d` and applied by the area-damage routine `FUN_00457d4b` (C), which limits damage by distance and reduces it farther from the impact point. Buildings are tested by whether the impact point lies inside their bounds. Path-piercing mode-4 projectiles may still hit multiple actors with zero splash. Examples: fire arrow 40, egg and bomb 48, dragon fire 64 |
| 0x1fc | 1 = the flight frames are directional (arrows, bolts): 8 blocks of flight / 8 frames, clockwise. Most start at north (block k points at facing k: Skeleton arrows, Alien, AngelRing, Bow machine, fire and poison arrows, Pteras axe, Shoot sword sprit, Squadron, Wizard firebolt); `Soldier arrows` and `Rhamphos arrows` start one step clockwise (block k at facing k + 1). The installer tells them apart by the shape of blocks 0 and 2 (C, previews). 0 = tumbling (stones, eggs) (D) |
| 0x200 | Frame count |
| 0x204 | First flight frame (D; Redbolt 10) |
| 0x208 | Flight frame count (Redbolt 10; `Soldier arrows` 8) |
| 0x20c | Impact frame count, after the flight frames (Redbolt 11; `Fire-arrow` 8 + 10, `Stone` 8 + 8) |
| 0x218, 0x21c | Frame delays (D) |
| 0x220 | Lifetime in ticks for lingering effects (C, compared with a per-tick counter, `0x476eff`) |
| 0x830 | Sound count |
| 0x834 | Sound offset, −1 = none |

Melee hits (`Velocis hit`, `Chief hit`) have sounds but no frames. `R-zombie` and `R-skeleton` are records only.

## Spells

The same record in `JW2_11`, with different fields in use (D):

| Offset | Content |
|---|---|
| 0x14c | 1 = targets a place or unit and hits (Drop Stone, Meteo, Thunder bolt); 0 = self or buff |
| 0x150 | Target: 3 a unit, 1 a place, 2 an area, 0 the caster, 4 a fallen unit (D) |
| 0x158 | Hotkey (ASCII) |
| 0x15c | Research needed (`Upgrade.upi` index, −1 none: Fake 15, Meteor 18) (C, `FUN_0046c3a5`) |
| 0x160 | Level needed, counted from 0 (Thunder bolt 1, Mass temper 3) (C, same) |
| 0x1e0 | Health cost: the Demon spells' kill points (Stone curse 85, Quake 150) (C, `FUN_00478cd5`) |
| 0x1e4 | Mana cost (Fall out 80, Thunder bolt 40) (C, same) |
| 0x1e8 | HP effect: negative = damage (Drop Stone −200, Thunder bolt −80, Meteo −30 per hit), positive = heal (Cure +2 per tick) |
| 0x1ec | A count: Rise death's zombies (3) (D) |
| 0x1f4 | Duration (Mass temper 300, Meteo 160, Noxious gas 144) |
| 0x1f8 | Radius in pixels (Quake 144, Suicide 160, Noxious gas 112) |
| 0x208 | Flight frames (Berry fly 16: the berries; Resurrect 16) |
| 0x20c | Effect frames, at the target |
| 0x210, 0x22c | 0x22c = 1: the effect frames are three variants of 0x210 frames each (Shield 3 × 13, Meteo 3 × 21, Cure 3 × 15) |
| 0x214, 0x218 | Ticks per caster frame, per flight frame (Berry fly's flight 9) |
| 0x21c | Ticks per effect frame (4) |
| 0x220 | Lifetime in ticks |
| 0x8b8, 0x8bc | Number and list of the effect frames that hit (Noxious gas and Quake: every frame) (C, `0x478c1a`) |

A spell's frames are, in order: the caster's (the frame count less the flight and effect frames: the Wizard's glow
in its hands, 8 directions × 5 frames, in Berry fly and Elf Build; the ManaSpread's swirl in Meteo), the flight
frames, then the effect frames (D, from the previews: each part is one animation). Most of the art is light painted on
black, drawn with Screen blending in the mod; the berries, the dropped stone and the gas cloud are solid.

The records are reached through a run-time offset table (`0xe9c678`, record *n* at `0xe9c798 + n × 0xadc`).

## Confirmed by

- All 69 files in `JW2_11` and `JW2_12` end exactly after their last sound (`research/tools/effect_parse.py`).
- `research/tools/effect_preview.py`: arrows, stones, eggs and spells render with their own palettes.
