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
| 0x200 | Frame count |
| 0x208 | Flight frames: 8 (one per direction) for projectiles |
| 0x20c | Impact frames, after the flight frames (`Fire-arrow` 8 + 10, `Stone` 8 + 8) |
| 0x830 | Sound count |

The other fields (speed, damage area, timing) belong to [Phase 2](../../plan.md). Melee hits (`Velocis hit`,
`Chief hit`) have sounds but no frames. `R-zombie` and `R-skeleton` are records only.

## Confirmed by

- All 69 files in `JW2_11` and `JW2_12` end exactly after their last sound (`research/tools/effect_parse.py`).
- `research/tools/effect_preview.py`: arrows, stones, eggs and spells render with their own palettes.
