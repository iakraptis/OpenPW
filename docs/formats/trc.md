# TRC container (`Jw2_*.trc`, `*.jw2`, `*.sav`)

All game data, maps and saves use the same container. Little-endian throughout.

## Layout

| Offset | Size | Field |
|---|---|---|
| 0x00 | 4 | Magic `"TRC\x1a"` |
| 0x04 | 4 | Entry count |
| 0x08 | 4 | Entry count again |
| 0x0c | 4 | Data offset = `0x20 + 32 * count` |
| 0x10 | 16 | Zero |
| 0x20 | 32 × count | Directory |
| data offset | | Entry data |

Directory entry (32 bytes):

| Offset | Size | Field |
|---|---|---|
| 0x00 | 12 | Name, CP949. Truncated to 12 bytes, not always NUL-terminated. All `0xCC` = masked (no name) |
| 0x0c | 4 | Offset, relative to the data offset |
| 0x10 | 4 | Unpacked size |
| 0x14 | 4 | Packed size. Differs from the unpacked size when the entry is zlib-compressed (`78 9c`) |
| 0x18 | 4 | Checksum, see below |
| 0x1c | 4 | Zero |

## Notes

- Names repeat inside one archive (e.g. `Char_small.p` once per race in `Jw2_02`). The game addresses entries by
  **index**: `FUN_00403410` loads `JW2_09.TRC` entries 0..169 by number, so directory order matters.
- Masked entries are found by content: WAV in `Jw2_05`, MP3 in `Jw2_15`, AI scripts and race names in `Jw2_17`.
- Checksum: for most uncompressed entries, the low 16 bits of the sum of the bytes (400/400 entries in the small
  archives, 159/170 in `Jw2_09`). Compressed entries store `0x20000 | x`, where the low byte of `x` matches the
  unpacked sum. Not needed for reading.
- `Pw.exe` lists the archive names in a table of 74-byte records at `0x58c523`. It references `JW2_08.TRC`, which
  is not part of the release we have; the game runs without it. `JW2_04` and `JW2_13` are not referenced.
- `JW2_08.TRC` is the **video archive** (see below). Only its directory has been seen: a 544-byte copy (16 entries,
  no entry data) that came with a second, stripped copy of the game (2026-11-21).

## `JW2_08.TRC`: the videos

Bink videos, about 380 MB in all, none compressed. Known from the directory alone; their data has never been seen.
Names as stored (12 bytes, masked ones all `0xCC`); sizes in bytes.

| # | Name | Size | Likely content |
|---:|---|---:|---|
| 0 | (masked) | 328,888 | |
| 1 | `1_arxel.bik` | 2,392,348 | Arxel Tribe logo |
| 2 | `2_open.bik` | 75,750,444 | opening cinematic |
| 3 | (masked) | 15,943,876 | |
| 4 | `P_01.bik` | 22,953,288 | campaign video: Primitive, mission 1 |
| 5 | `E_01.bik` | 7,371,476 | Elf, mission 1 |
| 6 | `T_01.bik` | 9,766,608 | Tyrano, mission 1 |
| 7 | `D_01.bik` | 10,112,704 | Demon, mission 1 |
| 8 | `P_06.bik` | 6,894,720 | Primitive, mission 6 |
| 9 | `E_06.bik` | 5,617,808 | Elf, mission 6 |
| 10 | `T_06.bik` | 6,378,092 | Tyrano, mission 6 |
| 11 | `D_06.bik` | 8,303,256 | Demon, mission 6 |
| 12 | `P_12.bik` | 35,362,356 | Primitive, mission 12 |
| 13 | `E_12.bik` | 36,957,768 | Elf, mission 12 |
| 14 | `T_12.bik` | 56,562,260 | Tyrano, mission 12 |
| 15 | `D_12.bik` | 79,359,468 | Demon, mission 12 |

The names use the campaign scenarios' race letters and numbers (`P01`…`P12`, `E`, `T`, `D`: 12 missions a race,
[campaign.md](campaign.md)), so each race's campaign has a video at its first, middle and last mission. This is read
from the names; when `Pw.exe` plays them is not confirmed. Our copies have no video, and the missions play without
them.

## Confirmed by

- `research/tools/trc_extract.py`: all 33 containers of the installed game unpack, every zlib entry matching its
  declared size.
- Ghidra: `FUN_00403410`, `FUN_004030e0` (reading `JW2_09.TRC` by index).
