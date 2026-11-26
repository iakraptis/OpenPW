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
for the minimap. Heights differ: Primitive 161, Elf 164, Tyrano 179, Demon 148; each has its hole about 8–10 pixels
above its bottom edge, and blank plaques where the Menu, Allies and Msg buttons go (the buttons are separate
`.spz` frames). The original Elf and Demon Menu buttons carry a picture, not the word; the mod's installer makes its
own lettered Menu buttons on each race's plaque (`MenuButtons.cs`, with open-licence fonts).

## Menus: `.mnt` and the per-race menus

`Main.mnt`, `Single.mnt`, `Select.mnt`, `Mainload.mnt`, `Brief.mnt`, `Continue.mnt`, and per race (`P_`, `E_`,
`T_`, `D_`) `game_menu`, `load_menu`, `save_menu`, `end_menu`, `yesno_menu`, `mission_menu`, `option_menu`,
`allies_menu`, `message_menu`, `wait_menu`.

- Header: `u32` widget count, `u32` record count (the widgets with a record), −1, 4.
- Widget records (in `Main.mnt` 0x2a4 bytes each from 0x10; the size varies between files): `i32` 6–9 the screen
  rectangle x1, y1, x2, y2 (inclusive, on the 800x600 screen), `i32` 10–13 the frames of a button's states
  (normal, hover, pressed, disabled; indexes into the frames of the chain, −1 when none), the name at +0x9c.
  `Main.mnt`: `bg`, the buttons `single`, `multi`, `replay` (the Intro picture), `credit`, `quit`, an unnamed text
  area at the bottom right (675, 578, 789, 598), and `bink`, where its video plays (68, 168, 738, 450).
  The installer exports `Main.mnt` as `chrome/main-menu.png` and `mods/pw/chrome/main-menu.yaml` (Chrome.cs).
- Resource chain, up to the end of the file: blocks of `u32` type, `u32` byte size, data.
  - 1: palette (1,024 bytes);
  - 2: one `.chr` frame (the background and every button state; English text is part of the images);
  - 4: a Bink video (`BIKi`): the animated backgrounds of `Main.mnt` and `Single.mnt`. `Main.mnt`'s is 668x277,
    15 fps, 20 s: a slideshow of the races' artwork in the panel.

`Brief.mnt`, the campaign's briefing screen: three widgets, the header text (99, 25 - 700, 100), the story text
(99, 120 - 700, 510) and `button` (336, 532 - 465, 563), CONTINUE, whose normal and hover images are the file's two
frames; no background (the page's picture fills the screen). The installer exports the button and the areas as the
chrome collection `briefing` (Chrome.cs); PwBriefingWidget draws the screen.

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
