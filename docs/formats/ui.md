# Interface art (`JW2_01.TRC`, `JW2_02.TRC`, `JW2_07.TRC`)

All 8-bit images use a separate 1,024-byte palette (256 × R, G, B, 0): `.pnt`, `.pn` or `.p`, usually the entry
just before the image. Pure green (`0x00ff00` in palettes, `0x07E0` in 16-bit art) is the transparent key in
interface panels.

## Sprites: `.spz`, `.sp`, `.s`

One or more [`.chr` frames](chr.md#frame-header-32-bytes) back to back, with no palette. Interface sprites store 0
in the mirrored-offset fields. This covers all 683 `.spz` and 63 `.sp`/`.s` files: buttons, icons, result titles
(`Win`, `Defeat`), resource icons (`Food`, `Water`, `Stone`, `House`), the minimap dot (`Icondot`), effects in
`JW2_07` (`Debs` debris, `Elfspark`, `Undatt`, berry bushes `Bery<v><s>`).

## Raw 8-bit sheets

No header; tiles stored one after the other, each `w × h` bytes.

| File | Tile | Count | Content |
|---|---|---|---|
| `Char_small.t` (+ `.p`) | 38 × 38 | 170 | Small portraits, by unit type index (see [chr.md](chr.md)) |
| `Char_middle` | 50 × 50 | 170 | Larger portraits, same order, palette of `Char_small.p` |
| `Action.trt` | 38 × 38 | 74 | Command buttons (move, attack, stop, gather, build, …) |
| `Upgrade.trt` | 38 × 38 | 48 | Upgrade buttons |
| `Item.trt` | 38 × 38 | 32 | Item buttons (Blacksmith items) |
| `Magic.trt` | 38 × 38 | 16 | Spell buttons |
| `Tech.trt` | 38 × 38 | 10 | Tech buttons |
| `*.scr` | 800 × 600 | 1 | Full screens: `Loading`, victory and defeat per race (`P/E/T/D_win`, `_def`) |
| `Fogmask1/2.trt` | 512 × 512 | 1 | Fog-of-war edge masks (D) |

`JW2_02` has these portraits and button sheets four times, in 13-entry blocks starting at entries 0, 13, 26 and
39: one block per race (Primitive, Elf, Tyrano, Demon). The images are the same; each race's block has the
button frames in its own style.

## `Intfbg.spr`: interface bar

`u16` width (800), `u16` height, 8 zero bytes, then `width × height` bytes. One per race, each after its palette
(`Primitive.pn`, `Elf.pnt`, `Tyrano.pnt`, `Demon.pnt`): the bottom bar, with a green surround and a black hole
for the minimap.

## Menus: `.mnt` and the per-race menus

`Main.mnt`, `Single.mnt`, `Select.mnt`, `Mainload.mnt`, `Brief.mnt`, `Continue.mnt`, and per race (`P_`, `E_`,
`T_`, `D_`) `game_menu`, `load_menu`, `save_menu`, `end_menu`, `yesno_menu`, `mission_menu`, `option_menu`,
`allies_menu`, `message_menu`, `wait_menu`.

- Header: `u32` widget count, `u32` (unknown), −1, 4.
- Widget records: rectangles (`0, 0, 799, 599` for the background), a name at +0x9c (`bg`, `Continue`), and
  more. The records vary in size and are not decoded; the mod uses OpenRA layouts instead.
- Resource chain, up to the end of the file: blocks of `u32` type, `u32` byte size, data.
  - 1: palette (1,024 bytes);
  - 2: one `.chr` frame (the background and every button state; English text is part of the images);
  - 4: a Bink video (`BIKi`): the animated backgrounds of `Main.mnt` and `Single.mnt`.

`Credit.mnt` is a stub ("this is null data").

## Cursors: `Mouse100.mcs`

Header `u32` 16, `u32` 100 (count), `u32` 32, `u32` 32. Then 100 cursors, each a 16-byte header (`i32` hotspot
x, `i32` hotspot y, 0, 0) and 32 × 32 RGB565 pixels; black (0) is transparent. Cursors: pointer, eight scroll
arrows, wait, forbidden, harvest (sickle), build (shovel), the coloured action spears (several animation frames
each), hands, and the animated move marker. `Mousec.dat` (122 bytes) looks scrambled and is not decoded.

## Fonts: `.fnt`

256 glyphs of `1 + w × h` bytes, in ASCII order, with grey levels 0–11 and palette `Font.pnt`:
`Fe0809rd` (8 × 9), `Fe1011rd` (10 × 11), `Fe1218rd` (12 × 18). `Fh1818ad` holds 2,400 Korean 18 × 18 glyphs.
The glyphs render but drift slightly, so the byte per glyph is not fully understood. The mod uses TrueType fonts,
so this is not needed.

## Confirmed by

`research/tools/sniff.py` (classification), `raw_preview.py`, `mnt_parse.py` (every menu file ends exactly with
its chain), and the previews in `research/previews/ui/` and `research/previews/menus/`.
