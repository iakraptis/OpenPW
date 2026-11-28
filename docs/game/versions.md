# Game versions

Two copies have been examined (read only). The mod is built from the first.

| | `Primitive Wars` (the mod's source) | `Primitive Wars EN` |
|---|---|---|
| Executable | `Pw.exe`, 8,183,846 bytes, April 2002 | `Jw2.exe` (and an identical `Jw2_CD.exe`), 4,120,633 bytes, March 2003 |
| Language | English, Korean, French, German, Italian, Spanish (`JW2_17`) | Korean only (`JW2_17`: `kor_Mm.txt`, `kor_Flatfor`): the Korean release, "Jurassic War 2", despite the folder's name |
| Extra archives | | `JW2_08` (the videos), `JW2_18` (replay controls: play, pause, speed, a replay save screen), `JW2_19` (an online lobby: create and join games, rankings, whisper, IPX and peer-to-peer connections) |

Identical in both: `JW2_03`, `05`, `06` (the campaign), `07`, `12`, `14`, `15`, `16` (the campaign AI scripts) and the
multiplayer maps they share; the later one has four more ranked maps (below). The unit archive (`JW2_09`) has the same 170 entries (119 types, 51 empty), so the later version
adds **no units**. It is a balance patch with three new spells.

## New spells (`JW2_11`)

The later `JW2_11` has 27 effects (its header counts 44 slots and 27 entries, which our `trc` reader rejects): ours
24, then three more, named only inside the files (the archive names are the editor's path):

| Record | Spell | Unit (`.chr` 0x1f0 bit) |
|---|---|---|
| 24 | Sky Fallout | Chief (bit 24) |
| 25 | Blessing | Unicorn (bit 25) |
| 26 | Shadow Force | Phantom (bit 26; it had none) |

`JW2_02`'s `Magic.trt` (the spell buttons) and `Single.mnt` also differ.

They are in use in this release, not left over (2026-11-28, read from the files):

- The type records grant them: the Chief's spell mask (`.chr` 0x1f0) is `0x1000001` (Fall out and Sky Fallout), the
  Unicorn's `0x2000100`, the Phantom's `0x4000000`.
- `Magic.trt` holds 27 button icons (38,988 bytes, 27 × 1,444), against 16 in the English one.
- `Jw2.exe` takes them: its spell cost check (`0x49e29f`) and cast (`0x49e382`) refuse records from `0x1b` (27) on,
  where `Pw.exe`'s (`0x478cd5`, `0x478db8`) refuse them from `0x18` (24). The cast singles out the same records in
  both (2, 5, 9, 15); the rest, the new three included, run from the record's own fields.
- The records are complete: Sky Fallout 80 mana, 10 within 100 px, from level 2 (counted from 0); Blessing 150 mana;
  Shadow Force 80 health, effect 240 for 100 ticks. None needs a research (0x15c is −1). Their header names the tool
  "Export" where the first 24 have "JWar": they were made later, with another tool.

Not checked in play (the game folders are read-only here).

## Balance changes (`JW2_09` type records)

| Unit | Change (ours → later) |
|---|---|
| Powerman | HP 160 → 150, armour cap 30 → 21 |
| Powerman (shield) | HP cap 100 → 150 |
| Giant, Soldier | experience per level 16 → 15, 14 → 12 |
| Bowmachine | population 4 → 6, sight 384 → 416 |
| Squadron | armour 0 → 3 |
| Flattop | armour 10 → 12 |
| BlueElf | armour 5 → 3 (cap 23 → 21) |
| Pixie | build time 560 → 400, cost 300 → 250 |
| Dragon | population 6 → 8 |
| Phoenix | attack cap 75 → 57, experience per level 12 → 14 |
| AngelElf | cost 5000 → 4500 |
| Velocis, Pteras, Darkbat | armour cap 27 → 18 |
| TwinVelocis | HP 350 → 380 |
| Rhamphos | attack 27 → 26 |
| Pteras | population 4 → 3, range 160 → 192, experience per level 14 → 16 |
| Trices | population 5 → 4 |
| EggThrower | population 4 → 6 |
| Mutant | HP 550 → 450, population 11 → 12, range 192 → 224, experience per level 30 → 24 |
| DeathEye | build time 560 → 400, cost 300 → 250 |
| Warlock | cost 650 → 600 |
| Triceratops (wild) | armour 15 → 0 |
| SkyIncreaser, HighPortal | cost 1000 → 800 |
| Hell | cost 800 → 700 |
| Volcano | HP 500 → 450 |

Also changed, meaning unknown: `.chr` 0x13fc (-1, 3, 4 or 6) on several units, the Revolver's 0x14c (kind) 2 → 0,
and the frame offsets at 0x3d0-0x438 of the Revolver, Elfring and Dsunguri (art placement).

`JW2_10`: the Blacksmith's arrows cost 150 → 100 and the Knight's spear 300 → 150, each giving 10 attack instead of
15 (`Item.itm`); `Upgrade.upi` differs in 89 values and `Level.lvt` in one row (0x3e84-0x3ea4), not yet read.

## The mod's installer with the later version

The 2003 copy installs the mod as well (checked with `install-assets` into a separate folder): everything both
copies share converts to the same bytes. What differs:

- `JW2_11`'s header has fewer entries in use (+8) than slots (+4); the reader takes the slots, the empty ones
  skipped (`TrcArchive`).
- `Maps\Rank` has four more ranked maps: Altar of Sacrifice, Four Tribes v3.0, Stone Henge and Four Ways v2.0,
  which is titled "(4) Four Ways" like the original; a map whose title is taken gets its file name for a title
  (`Maps.cs`), so it becomes "(4) Four Ways v2.0" beside the original. All four convert and run.
- The three new spells' art converts too (`effects/spell-sky-fallout.png`, …) but nothing uses it, and the
  installer warns that `sequences/spells.yaml` differs.
- The unit stats don't matter: the mod's rules are its own (`mods/pw/rules`), only the art comes from the game, and
  that is the same.

It registers as `HKLM\SOFTWARE\WIZARD SOFT\Jurassic era war 2`, `InstallPath` (its `Jurassic era war 2.reg`), in
a folder named `Pw`; the first-launch screen reads every Wizard Soft `InstallPath` (`PwContent.Candidates`).
