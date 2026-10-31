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
| 0x150 | 2 for most; 0 for fire and poison arrows and the trap (damage kind, D) |
| 0x154 | Target mask, tested bit by bit (C, `0x457c6d`); bit 3 (8) = air: melee and the egg thrower have 23 (no air), arrows 31, `PhonixFire` 8 (air only) |
| 0x190 | Projectile kind: 2 most missiles, 4 some fire, 0 none (D) |
| 0x194 | Projectile speed, pixels per tick (arrows 18, `Stone` 20, `Wizard firebolt` 11) (D) |
| 0x1f8 | **Splash radius** in pixels, 0 = single target (C: passed to the area damage routine at impact, `0x47713d`): fire arrow 40, egg and bomb 48, dragon fire 64 |
| 0x1fc | 1 = the flight frames are directional (arrows, bolts); 0 = tumbling (stones, eggs) (D) |
| 0x200 | Frame count |
| 0x208 | Flight frames: 8 for projectiles (repeated at 0x224) |
| 0x20c | Impact frames, after the flight frames (`Fire-arrow` 8 + 10, `Stone` 8 + 8) (repeated at 0x228) |
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
| 0x1e8 | HP effect: negative = damage (Drop Stone −200, Thunder bolt −80, Meteo −30 per hit), positive = heal (Cure +2 per tick) |
| 0x1f4 | Duration (Mass temper 300, Meteo 160, Noxious gas 144) |
| 0x1f8 | Radius in pixels (Quake 144, Suicide 160, Noxious gas 112) |
| 0x220 | Lifetime in ticks |

The mana cost is read through a run-time offset table (`0xe9c678`), so it is not located statically; Elf and
Demon spells come later.

## Confirmed by

- All 69 files in `JW2_11` and `JW2_12` end exactly after their last sound (`research/tools/effect_parse.py`).
- `research/tools/effect_preview.py`: arrows, stones, eggs and spells render with their own palettes.
