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
  is not part of the international release (probably removed content, e.g. licensed audio or Korea-only data);
  the game must run without it. `JW2_04` and `JW2_13` are not referenced.

## Confirmed by

- `research/tools/trc_extract.py`: all 33 containers of the installed game unpack, every zlib entry matching its
  declared size.
- Ghidra: `FUN_00403410`, `FUN_004030e0` (reading `JW2_09.TRC` by index).
