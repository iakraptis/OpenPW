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
- `Pw.exe` lists the archive names in a table of 74-byte records at `0x58c523`. It references `JW2_08.TRC`, the
  video archive (below), which the setup leaves on the CD; the installed game runs without it. `JW2_04` is not
  referenced, and `JW2_13` is the setup's own archive (below).

## The CD and the installed game

The retail CD (one data track, ISO 9660; checked 2026-11-21) holds `Setup.exe`, `Autorun.inf`, `Readme.txt`,
`manual/PWManual.pdf`, `programming/PROGRAMMING.pdf`, the 17 skirmish maps (`Maps/`, `Maps/Rank/`) and
`Jw2_01`…`Jw2_17` without `JW2_04`. Compared with an installed copy:

- `Jw2_02`, `03`, `05`, `06`, `07`, `09`…`12`, `14`…`17` and the maps are byte-identical. `Jw2_01` differs only in
  `Mousec.dat` (122 bytes, the cursor table; the installer doesn't use it).
- **`JW2_13.TRC`** is what `Setup.exe` unpacks into the game folder: `Pw.exe` (the retail exe), `Binkw32.dll`,
  `Ijl11.dll`, `Mss32.dll`, `Mp3dec.asi`, `Pwhelp.chm`, and three masked entries, which are `MapEditor.exe`,
  `Auto Patch Client.exe` and `UnInstall.exe` (each byte-identical to the installed file).
- **`JW2_08.TRC`** (the videos) is on the CD only; the setup doesn't copy it.

So the installer accepts either an installed copy or the CD's files copied to a folder: it needs only the archives
it reads (`GameFolder.RequiredArchives`), not `Pw.exe`.

## `JW2_08.TRC`: the videos

Bink videos (`BIKi`), 380 MB in all, none compressed; on the retail CD (checked: every entry starts with a Bink
header). Names as stored (12 bytes, masked ones all `0xCC`); sizes in bytes.

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
from the names; when `Pw.exe` plays them is not confirmed (the game probably reads them from the CD). OpenPW doesn't
use them: OpenRA can't play Bink.

## Confirmed by

- `research/tools/trc_extract.py`: all 33 containers of the installed game unpack, every zlib entry matching its
  declared size.
- Ghidra: `FUN_00403410`, `FUN_004030e0` (reading `JW2_09.TRC` by index).
