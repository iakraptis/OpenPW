# Primitive Wars on OpenRA: plan

Goal: remake **Primitive Wars (Legend of the Land)** (Wizard Soft, 2001) on the OpenRA engine. The approach is the
one that worked for Seven Years War: an OpenRA Mod SDK repository with our own C# traits, and an installer that
converts the player's own copy of the game on first launch. No original asset is ever committed.

Reference projects (same parent folder):
- `../OpenSYW`: the finished SYW remake. **Copy its structure**: Mod SDK layout, `mod.config`, `make.cmd`,
  C# `AssetInstaller` with stages, the `*-content` first-launch mod, `run_tests.py` with Lua test maps, Windows
  packaging and the GitHub release workflow.
- `../OpenSY`: the SYW research workspace. Reuse its **Ghidra 12.1.3 + JDK 21** install
  (`../OpenSY/.local-tools/`), its headless scripts (`SYWtoORA/tools/ghidra/*.ps1`, `SurveySyw.java`) and its
  lessons learned (`Plan.md` §8.2: traits that crash at runtime although `--check-yaml` passes).

Original game: `../Primitive Wars` (read-only, never modified or committed).

---

## 1. What we know about the original (survey of 2026-10-31)

### 1.1 Install

| File | Notes |
|---|---|
| `Pw.exe` | 8 MB, PE32 i386, MSVC + statically linked MFC. Good Ghidra target (RTTI present) |
| `MapEditor.exe` | Original editor. Useful to confirm map fields by editing and diffing |
| `Jw2_*.trc` | 14 data archives (project codename **JW2**, "Jwar2") |
| `Maps/*.jw2`, `Maps/Rank/*.jw2` | 10 + 7 multiplayer maps. Same TRC container, zlib-compressed sections |
| `Save/*.sav` | Same layout as maps. Out of scope |
| `Binkw32.dll`, `Mss32.dll`, `Mp3Dec.asi` | Bink video, Miles audio. Music is MP3 |
| `PwHelp.chm` | Help file: likely unit/building descriptions and stats. Decompile with `hh -decompile` |

### 1.2 TRC container (decoded)

- Header 32 bytes: `"TRC\x1a"`, `u32 count`, `u32 count`, `u32 dataOffset (= 0x20 + 32*count)`, zero padding.
- Directory: `count` × 32 bytes = `char name[12]` (truncated, not always NUL-terminated), `u32 offset` (relative to
  data start), `u32 unpackedSize`, `u32 packedSize`, `u32 checksum?`, `u32 0`.
- `packedSize != unpackedSize` means zlib (`78 9c`).
- Checksum: for most uncompressed entries it is the 16-bit sum of the bytes (400/400 in the small archives,
  159/170 in Jw2_09). Compressed entries store `0x20000 | x`, where the low byte of `x` matches the unpacked sum.
  Not needed for reading; the extractor ignores it.
- Some names are blanked with `0xCC` (masked). Their contents are still valid (WAV / MP3). The real names are
  probably in `Pw.exe` or derivable from directory order.
- Duplicate names exist (e.g. `Char_small.p` four times in Jw2_02: one per race).

Probe scripts: `research/tools/trc_sum.py`, `trc_peek.py`, `names.py`.

**Extraction:** `python research/tools/trc_extract.py "../Primitive Wars" research/extracted` unpacks all 33
containers (14 archives, 17 maps, 2 saves): 3,847 files, 678 MB, every zlib entry matching its declared size.
One folder per container with an `index.json`; masked or duplicate names are written as `NNNN_<name>`.

**Map sections** (every `.jw2` has the same 20, unpacked): `JW2` 94 KB, `P_MAP` 368 B, `P_SCENA` 10 KB, `P_PLAYE`
884 B, `P_UB` 1.3 KB, `P_SCRIP` 1.8 MB (mostly zeros), `OBB` 86 KB, `OBC` 950 KB, `OBI` 30 KB, `RST` 48 KB,
`MAP1/2/3`, `BGI`, `FOGBGI`, `FOGMAP3`, `VSBGI` 256 KB each (a 256×256 or 512×512 grid), `BRUSH` 1.1 MB,
`HILL_BRUSH` 34 KB, `Custom` 41 KB.

**Masked entries identified by content:** Jw2_05 = sound effects (WAV), Jw2_15 = music (MP3), Jw2_17 = AI
scripts (5 files) and the race names, which in the original are *Primitive tribe, Elf tribe, Tyrano tribe, …*
(our Tyrano race; the name follows the original).

**Campaign (Jw2_06):** 48 scenarios confirmed (48 `JW2` sections, 96 MP3s). 384 of its `.jpg` entries are 1-byte
placeholders.

### 1.3 Archive contents

| Archive | Contents | Format status |
|---|---|---|
| Jw2_01 | Fonts (`.fnt`), mouse cursors (`.mcs`, `Mousec.dat`), loading screen, `Tribe.pnt` | Unknown |
| Jw2_02 | UI: per-race panels (`Action/Magic/Tech/Upgrade/Item` `.pnt` palette + `.trt` image), menus (`.mnt`), results screens, `.spz` sprites | `.pnt` = 256×RGBX palette (1024 bytes). Rest unknown |
| Jw2_03 | Terrain: 3 tilesets **Jungle, Dungeon, Ruin** (`.trt`, 7.3 MB each) + 14 doodad sets (`.doo`) each | `.trt` possibly 16-bit; to confirm |
| Jw2_05 | 33 sound effects (WAV, 21 masked names) | Standard WAV |
| Jw2_06 | Campaign: ~48 scenarios `P01..` (embedded `.jw2` sections, briefing JPGs, start/end MP3s) | JPG/MP3 standard; some `.jpg` entries are not JPEG (obfuscated?) |
| Jw2_07 | Effects, selection/start-location markers, debris (`.spz`, `.sp`), `Dir08/Dir16.scr` direction tables | Unknown |
| Jw2_09 | **All characters and buildings** (`.chr`, 170 entries, 79 MB) | Header holds the artist's path (`D:\Jwar2\0 Character\...`) and the name. To decode |
| Jw2_10 | `Upgrade.upi`, `Level.lvt`, `Item.itm`: upgrade/level/item tables | Fixed-size records with English names. Easy |
| Jw2_11 | 24 spell effects (`.maz`) | Same family as `.chr` |
| Jw2_12 | 51 weapons/projectiles (`.wpn`) | Same family as `.chr` |
| Jw2_14 | `Ai_00.txt`, AI script (Korean comments, CP949) | Text |
| Jw2_15 | 19 music tracks (MP3) | Standard |
| Jw2_16 | 384 campaign dialogue texts (`P01_00.txt`…) | Text (encoding to check) |
| Jw2_17 | Localised strings in 6 languages (`Mm_eng.txt` …) + 6 masked entries | Text |

### 1.4 Factions (from `Jw2_09` names)

| Race | Units | Buildings |
|---|---|---|
| **Primitive** (humans) | Buildman (worker), Powerman, Giant, Soldier, Knight, Revolver, Bowmachine, Squadron, Flattop, Chief, Kalma + variants (shield, fire/venom arrows, spear) | Sanctuary, House, Stonetower, Powerhouse, Soldierhouse, Skyhouse, Blacksmith, Landincrease, Skyincreaser, Trap |
| **Elf** | Wizard, Redelf, Whiteelf, Ranger, Blueelf, Unicorn, Pixie, Manaspread, Elfring, Dragon, Phoenix, Angelelf | Heaven, Elftree, Bubble, Red/Blue/Mana/Skyhall, Elf/Sky/Manalibrary |
| **Tyrano** (race; "Tyrano tribe" in game) | Dinos, Masos, Velocis, Twinvelocis, Dilophos, Rhamphos, Twinrhamphos, Pteras, Trices, Dsunguri, Eggthrower, Mutant, Tyranos | Tyranonest, Nest, Ascomoid, Eggnest, Land/Sky/Thrownest, Upgradenest, Land/Skynisdos |
| **Demon** | Insect, Skeleton, Mudman, Deatheye, Phantom, Gargoyle, Kelpa, Warlock, Kingdemon, Darkbat, Devil, Risezombie1/2, Imp | Demonden, Tomb, Volcano, Deathden, Flyden, Magicden, Hell, Dirty/Highportal |
| Neutral | 19 real dinosaurs (Tyrannosaurus … Plesiosaurs), Alien1-4, plants (Noxiousweed, Poisonous, Treant, Demonflower) | |

Spells (Jw2_11): Fall out, Thunder bolt, Shield, Mass temper, Teleport, Jump portal, Drop stone, Vanishing, Fake,
Meteo, Resurrect, Stone curse, Suicide, Noxious gas, Quake, Rise death, Berry fly, Elf build, Cure, Egg mine,
Mana fill, Morale, Bline, Level up. Resources look like **berries** and **water** (`Bery.spz`, `Water.spz`), with
**mana** for spells. Units gain **levels** (`Level.lvt`) and carry **items** (`Item.itm`).

This is about 4× the content of SYW (4 races of ~12 units + ~10 buildings, vs 2 nearly symmetrical factions).

---

## 2. Repository layout

```
primwars/                      (git)
  plan.md
  docs/formats/*.md            file format specs: no original data, tracked
  docs/game/*.md               recovered rules: stats, tech trees, spells
  OpenRA.Mods.Pw/              C# traits, widgets, AssetInstaller
  mods/pw/                     rules, weapons, sequences, chrome YAML, Lua, fluent
  mods/pw-content/             first-launch "point me at Primitive Wars" screen
  engine/                      fetched automatically (gitignored)
  research/                    gitignored: probes, extracted dumps, Ghidra project, previews
```

- Mod id **`pw`**, engine **`release-20250330`** (same as OpenSYW, so its C# compiles as a starting point).
- The C# installer is the shipping converter. Python in `research/tools` is for exploration only; once a format is
  understood, port the reader to C# (`OpenRA.Mods.Pw/AssetInstaller/`) and document it in `docs/formats/`.
- Ghidra: point a copy of `Run-SywAnalysis.ps1` at the OpenSY install (no second 1 GB download); keep the project
  in `research/ghidra/`, the Java/PowerShell scripts in `research/tools/ghidra/` (move them to `docs/` if they
  become worth keeping).

---

## 3. Data files: inheritance

Every actor, weapon and sequence inherits from shared templates (`^abstract` names; OpenRA merges `Inherits`,
`Inherits@tag` for several parents, and `-Trait:` to remove an inherited trait). A concrete actor should hold only
what makes it different: its image, cost, stats, weapons and prerequisites.

OpenSYW does this partly: `^syw-infantry`, `^syw-building`, `^syw-tower`, `^syw-vehicle`, `^syw-ship` family bases,
then a `^peasant`-style template per unit shared by both factions (`rules/common/`), then the faction actor
(`rules/korea/`, `rules/japan/`). Its weak point: the five family bases are separate copies of the flattened C&C
defaults, so traits such as `AppearsOnRadar` or the handicap multipliers are repeated in each one. We build one tree
instead.

### 3.1 Actor tree (`mods/pw/rules/defaults/`)

A unit is described on two independent axes, so they are separate templates:
- **body** (the tree below): what it is made of. Armour class, healed or repaired, crushable, death effects.
- **movement** (a mixin, §3.1.1): how it moves. Ground and air are different engine traits (`Mobile` vs
  `Aircraft`) that cannot share an actor, so no body template contains either.

A flying vehicle is `^Machine` + `^AirMovement`; a pterosaur is `^Beast` + `^AirMovement`; a cart is `^Machine` +
`^WheeledMovement`.

```
^ExistsInWorld          radar, statistics, script triggers, debug, handicap multipliers, owner colour
└─ ^SpriteActor         BodyOrientation, RenderSprites, facings from sequence
   ├─ ^Unit             Selectable + decorations, Health, Armor, AttackMove, Voiced, DeathSounds,
   │  │                 RevealsShroud, Guard, levels/experience, Tooltip   (no movement trait)
   │  ├─ ^Infantry      infantry armour and hit shapes, crushable, healed by healers
   │  │  ├─ ^Worker     gathering, building (Builder), repair
   │  │  └─ ^Caster     mana, spell traits (Phase 5)
   │  ├─ ^Beast         living creature: dinosaurs, Giant, Unicorn, Dragon; healed, not crushable
   │  └─ ^Machine       constructed: Bowmachine, Revolver, siege, flying machines; repaired, not healed
   ├─ ^Building         footprint, BuildingInfluence needs, UnderConstruction, sell/repair, capture, damage states
   │  ├─ ^ProductionBuilding   own queue, exits, rally point
   │  ├─ ^Defense       turret / attack (Stonetower)
   │  └─ ^Wall / ^Trap  minor structures
   ├─ ^Plant            static neutral hazards (Noxiousweed, Demonflower, …)
   └─ ^Doodad           trees, rocks, ruins from `.doo` (no owner, blocks or not)
```

#### 3.1.1 Movement mixins

| Mixin | Engine trait | Used by |
|---|---|---|
| `^FootMovement` | `Mobile`, foot locomotor | infantry, small beasts |
| `^HeavyMovement` | `Mobile`, heavy locomotor (crushes infantry) | large dinosaurs, Giant |
| `^WheeledMovement` | `Mobile`, wheeled locomotor | ground machines |
| `^AirMovement` | `Aircraft`, air target type, shadow, no crush | flying infantry, beasts and machines alike |
| `^WaterMovement` | `Mobile`, water locomotor | sea creatures (Plesiosaurs), if the maps need them |

Each mixin also lists the world trait it needs (`Mobile` → `PathFinder` + `Locomotor`).

#### 3.1.2 Owner mixins

| Mixin | Adds |
|---|---|
| `^Primitive`, `^Tyrano` (later `^Elf`, `^Demon`) | the race layer, §3.2 |
| `^Wild` | neutral wildlife owned by Creeps: wanders, attacks when provoked, not buildable. The 19 wild dinosaurs, aliens, Treant |

**Naming:** the playable race is **Tyrano** everywhere (`^Tyrano`, `rules/tyrano/`, actors `tyrano-velocis`, queue
`TyranoUnit`). "Dinosaur" only ever means the neutral wildlife, whose actors are `wild-tyrannosaurus`,
`wild-triceratops`, …, built from `^Beast` + a movement mixin + `^Wild`.

### 3.2 Race layer (`mods/pw/rules/<race>/`)

Each race adds a small mixin, combined with the kind template through a second parent:

```yaml
^Primitive:                 # rules/primitive/defaults.yaml
	Buildable:
		Queue: PrimitiveUnit
	Voiced:
		VoiceSet: PrimitiveVoice
	# race-wide upgrades (Blacksmith), colours, death effects

primitive-soldier:          # rules/primitive/infantry.yaml
	Inherits: ^Infantry
	Inherits@move: ^FootMovement
	Inherits@race: ^Primitive
	Inherits@weapon: ^Ranged         # optional behaviour mixins, see below
	Tooltip:
		Name: actor-primitive-soldier.name
	Health:
		HP: 6000
	Armament:
		Weapon: SoldierArrow
```

Equipped forms (Soldier fire/venom arrows, Powerman with shield, Knight with spear) come from the Primitive
**item** mechanic: the Blacksmith makes items, and some Primitive units equip one permanently for better stats.
Each equipped form has its own `.chr` (own art and stats), so it is a separate actor that inherits from its base
unit and overrides weapon, armour and art; equipping turns the unit into it (a `Transforms`-style trait that keeps
level and experience).

Tyrano has a **merge** mechanic: two units combine into a stronger one (`TwinVelocis`, `TwinRhamphos`, `Mutant`,
all cost 0 because they are never built). This needs a custom trait (two actors in, one out).

Behaviour mixins for things shared across kinds and races: `^Ranged`, `^Melee`, `^Gatherer`, `^Transport`,
`^Detector`, `^Healer`, `^Levels`. Keep them small and named after what they do.

### 3.3 Weapons, sequences, voices

- Weapons (`mods/pw/weapons/`): `^Melee`, `^Arrow`, `^Thrown` (bombs, eggs, stones), `^Magic` (bolts, fireballs),
  `^Breath` (dragon/phoenix fire), `^Siege`; each defines projectile, warheads, versus-armour table and impact
  effects. Concrete weapons set damage, range, reload and their art only.
- Sequences (`mods/pw/sequences/`): `^infantry-sequences` (stand/walk/attack/die with the common facings and
  lengths), `^beast-sequences`, `^building-sequences` (make, idle, damaged, dead), `^flyer-sequences`; the
  installer writes frame counts per unit where they differ. The original timing comes from per-group frame
  sequences in the `.chr` (frames held or repeated), and the direction blocks can differ in length, so the
  installer generates `Frames:` lists per unit and sequence instead of `Length` × `Facings`.
- Voice and notification sets per race with a common base.

### 3.4 Rules for the tree

- No trait is defined twice where a template could hold it. If two concrete actors share three or more identical
  traits, those go into a template.
- Templates hold behaviour, not balance: stats (HP, damage, cost, speed) live on the concrete actor, so the
  numbers recovered in Phase 2 can be checked against one place.
- The runtime-only dependencies listed in OpenSY `Plan.md` §8.2 (`Mobile` needs `PathFinder`, `Building` needs
  `BuildingInfluence`, …) are documented next to the template that introduces them.
- A test (`--check-yaml` plus a small script in `run_tests.py`) fails if a non-abstract actor has no `Inherits`.

---

## 4. Phases

Each phase ends with something runnable or a written spec. Order follows risk: the formats are the unknown, the
OpenRA side is well understood from OpenSYW.

### Phase 0: Bootstrap (small)
- [x] Survey the install and the TRC container (§1).
- [x] `research/` folder, gitignored.
- [x] Mod SDK skeleton (2026-10-31). Built from the upstream OpenRA Mod SDK, which targets the same engine as
      OpenSYW (`release-20250330`), rather than OpenSYW itself: its chrome depends on SYW widgets, so stripping
      it would have left more to undo. `example` was renamed to `pw` (`OpenRA.Mods.Pw`, `OpenPW.sln`), with
      factions `primitive` and `tyrano`, a `placeholder` actor, and a `TEST` tileset with a test map and a
      shellmap. `make.cmd all`, `test` and `check` pass, and `launch-game.cmd` starts the mod. The style check
      runs with `NuGetAudit=false` because of an advisory in the engine's own tooling.
      OpenSYW parts to bring over as they are needed: `run_tests.py`, CI tweaks, the content installer mod.
- [x] Ghidra: `Pw.exe` imported and analysed (2026-10-31) into `research/ghidra/PrimitiveWars.gpr` with
      `research/tools/ghidra/Run-PwAnalysis.ps1` (uses OpenSY's Ghidra and JDK) and `SurveyPw.java` (strings and
      format landmarks; `-Addresses` decompiles more). `Pw.exe` is a **debug build** (`0xCC` stack fill,
      `__chkesp`), so it decompiles cleanly.
- [x] `trc_extract.py`: unpack every archive and map into `research/extracted/<archive>/` (zlib, duplicates,
      masked names). Done 2026-10-31, see §1.2.

### Phase 1: File formats (the critical path)
Find the decoders in `Pw.exe` with Ghidra (search for the extensions as strings: `.chr`, `.spz`, `.trt`, `.doo`,
`.pnt`), then confirm with a Python preview that renders PNGs into `research/previews/`.

Priority order:
1. **`.chr`**: units and buildings. **Decoded 2026-10-31**, see [docs/formats/chr.md](docs/formats/chr.md):
   9404-byte type record (likely the stats), per-file 8-bit palette, 14 animation groups of skip-run frames,
   5 directions, shadow index 1, player colour 245–254. **Units finished 2026-10-31**: direction order (N to S,
   mirrored for the west side), per-direction start frames, frame sequences (timing), walk displacement per
   facing, group meanings (workers: gather, carry two loads, build), mirrored offsets, and the sounds (plain WAVs,
   triggers traced in code). Remaining: the per-unit groups of the other races as they come up.
2. **`.trt` + map sections**: terrain tiles and the `.jw2` sections (`P_MAP`, `P_SCENA`, `P_PLAYE`, `P_UB`,
   `P_SCRIP`, `OBB/OBC/OBI`, `RST`, `MAP1..`). Use `MapEditor.exe` to make tiny maps and diff them.
   **Terrain decoded 2026-10-31**, see [docs/formats/trt.md](docs/formats/trt.md) and
   [docs/formats/map.md](docs/formats/map.md): 3,600 RGB565 tiles of 32 × 32 per tileset (six ground materials,
   animated liquid, hill pieces, blend masks, alpha tiles); `MAP1` six materials blended by mask tiles, height,
   walkable/water flags; `MAP2` up to four hill/cliff overlays per cell; tileset in `P_MAP`. All 17 maps render
   with `research/tools/map_render.py`. **Rest of the map decoded 2026-10-31**: `MAP3` objects (doodads, and
   berry bushes with 4,000 berries and 4 depletion stages), `OBC` placed units (only wild creatures in the
   shipped maps), `P_PLAYE` start positions and starting resources (400 berries), `P_SCENA` player count,
   `P_UB` unit availability. `OBB`, `OBI`, `RST`, `P_SCRIP` and `Custom` are run-time pools or campaign data,
   empty in every skirmish map. `research/tools/map_objects.py` draws everything over the terrain for all 17
   maps.

   **Decision needed for Phase 3:** the terrain is blended per pixel, not tiled. All maps together have 91,013
   distinct blended cells, so a classic OpenRA tileset is not possible. The mod gets its own terrain renderer
   (`OpenRA.Mods.Pw`, replacing `TerrainRenderer`). It is fed per cell with the materials, mask and overlays
   (stored next to `map.bin`), and blends on the GPU from the tileset texture. Pathing uses ordinary OpenRA
   terrain types derived from the flags (clear, water, cliff, ramp), so the standard pathfinder keeps working.
3. **`.doo`** doodads (trees, rocks), **`.spz/.sp`** effects and UI sprites, **`.wpn`/`.maz`** projectiles and
   spell effects (probably the same container as `.chr`). `.doo` **decoded 2026-10-31**
   ([docs/formats/doo.md](docs/formats/doo.md)): palette + 4 variants of `.chr` frames. **Effects decoded
   2026-10-31** ([docs/formats/effect.md](docs/formats/effect.md)): weapons and spells share one layout (0xadc
   record, palette, flat frame list, WAVs); projectiles are 8 flight directions + impact frames. All 69 files
   parse exactly.
4. **`.pnt/.trt/.mnt/.scr`** UI panels and menus, **`.fnt`** fonts, **`.mcs`** cursors. **Decoded 2026-10-31**
   ([docs/formats/ui.md](docs/formats/ui.md)):
   - every `.spz`/`.sp` is `.chr` frames;
   - portraits (170 small and middle, by unit type) and the command, upgrade, item, spell and tech button sheets,
     one block per race (the art is the same, the frame styles differ);
   - full screens, and the per-race interface bar;
   - menus: a resource chain of palette, frames and Bink videos (the animated main-menu background);
   - 100 cursors with hotspots.

   Fonts render but are only partly understood, which is fine because OpenRA uses TrueType.
5. Text encodings. **Decoded 2026-10-31** ([docs/formats/text.md](docs/formats/text.md)):
   - `Mm_*.txt` network and lobby messages in six languages;
   - the masked `JW2_17` entries are copies of them;
   - the AI script language, used by the skirmish AI and by 384 campaign scripts (48 scenarios × 8 players);
   - there is no campaign dialogue in the archives.

Deliverable: one `docs/formats/<ext>.md` per format, plus a preview for each. **Phase 1 is complete
(2026-10-31).** Open details, none of them blocking:
- the `.fnt` glyph layout;
- the menu widget records;
- `Mousec.dat` and `Version.dat`;
- a few record fields: the `.chr` type record (0x13cc, 0x13d0, 0x2410), `P_PLAYE` 0xa8, `P_SCENA` 0x2808.

The weapon and spell record fields, the `Upgrade.upi`/`Level.lvt`/`Item.itm` tables, and the AI command
arguments belong to Phases 2 and 7.

**Player colours:** characters are 8-bit with a reserved ramp (245–254), so OpenRA's palette remap works for
them. Each `.chr` has its own palette, so expect one palette per actor (or convert to RGBA PNG plus a remap mask,
as OpenSYW's `PlayerColorShift` does). Terrain (`.trt`) is 16-bit RGB565 and needs no palette.

### Phase 2: Game rules
Started 2026-10-31: the `.chr` type record holds the stats. HP, attack, armour, level caps, cost, build time,
experience and movement class are confirmed in code; sight, range, weapon, population and hotkey are deduced
(see [docs/formats/chr.md](docs/formats/chr.md)). Full table: `research/extracted/chr_stats.csv`.
Units level up to 9, raising five stats from base to cap.

**Done 2026-10-31** for Primitive and Tyrano: [docs/game/rules.md](docs/game/rules.md) (tick length and the
original speed table, gathering 12 berries per trip, repair, levels, `Level.lvt` bonus rows, the upgrade and
item formats, tech tree fields, merge recipes) and [docs/game/primitive.md](docs/game/primitive.md) /
[docs/game/tyrano.md](docs/game/tyrano.md) (units, buildings, upgrades, items), generated by
`research/tools/game_tables.py`. Decisions this enables: the mod uses the original speed table as `GameSpeeds`
(default 71 ms) so all durations stay in original ticks; one resource (berries), since the second resource costs
nothing anywhere. Resolved since:
- the damage formula (`attack − armour`, at least 1);
- weapon fields: projectile speed, splash radius, target mask with an air bit, lifetime;
- walk speed is pixels per tick;
- `Level.lvt` rows 4 and 5 are unused;
- the spell effect fields.

Left for Elf and Demon: the spell mana costs.

- Parse `Upgrade.upi`, `Level.lvt`, `Item.itm` (fixed records, English names).
- Ghidra: unit constructors / stat tables for HP, damage, armour, range, speed, sight, cost, build time; tech
  trees; spell costs and effects; resource gathering rates. Follow OpenSY's method: extract into JSON, then verify
  each value against the decompiled code.
- Read `PwHelp.chm` and the `Mm_eng.txt` strings for descriptions and names.
- Deliverable: `docs/game/<race>.md` with stats tables and `docs/game/TechTree.md`.

### Phase 3: First playable (one race, one map)
- [x] **Terrain renderer** (2026-10-31): `PwTerrainRenderer` (`OpenRA.Mods.Pw/Traits/World`) replaces
      `TerrainRenderer`. At map load it composes each cell from the original tileset exactly as `Pw.exe` does
      (material blending through mask tiles, hill overlays, alpha edges, hilltop fill), caching identical cells.
      The largest map needs about 25,000 cells, which fit in two 4096 sheets.
  - Maps carry the original cell words in `terrain.bin`. `map.bin` holds the movement type (tilesets `JUNGLE`,
    `DUNGEON`, `RUIN`: Clear, Ramp, Water, Cliff, with flat placeholder images).
  - Maps without `terrain.bin` and the editor previews fall back to the template images.
  - The liquid animates (frames 0-5 and back, 4 ticks each): it has its own layer under the composed cells,
    whose liquid share is transparent, so shores blend exactly without a sprite per frame.
  - The asset installer (Phase 4, started early) writes the tilesets and all 17 maps to `^SupportDir/Content/pw/`.
  - Checked in game (One VS One) with `research/tools/shot.ps1`.
- [x] **Art and the first actors** (2026-11-01):
  - Installer stages `units` (sprite sheets for all 119 types, generated `sequences/<race>.yaml`) and `chrome`
    (the 100 original cursors).
  - The template tree of §3 is in `rules/defaults.yaml`.
  - The Primitive units and buildings, with their weapons, are in `rules/primitive/` and `weapons/`; damage uses
    the original rule (`SubtractArmorDamage` warhead, `PwArmor`).
  - Game speeds are the original ones (71 ms default).
  - The team colour goes through the engine's `PlayerColorShift`, with the band set in `palettes.yaml`. The SDK's
    duplicate copies of the traits are removed.
  - Checked in game: a Sanctuary and four Buildmen in the player's colour.
  - Tyrano is hidden from the lobby until its actors exist.
- [x] **Production, first version** (2026-11-01):
  - A command panel (`CommandPanelLogic`, `chrome/layouts/ingame-player.yaml`) shows the berries and either the
    selected worker's build list or what the selected building trains. Its icons are the original portraits
    (each image's `icon` sequence; the installer's `chrome` stage writes the sheet).
  - The Buildman builds as in the original: `Builder`, the `BuildAtCellOrderGenerator` placement mode, and the
    `ConstructBuilding` activity, ported from OpenSYW. The worker stays beside the site playing its build
    animation. `UnderConstruction` takes the original build time and `WithConstructionStages` shows the stage
    frames.
  - Buildings train from their own queue, with the original training times and prerequisites.
  - Next: the race interface bar (Intfbg) instead of the plain panel, rally points, population.
- [x] **Map contents** (2026-11-01): doodads and berry bushes (workers gather beside a bush, 12 per trip; bushes
      regrow slowly instead of disappearing, a balance change) and the wild creatures of `OBC` (Creeps actors,
      rules generated by `research/tools/wild_rules.py`, [docs/game/wild.md](docs/game/wild.md)). Creatures wander
      near where they were placed; the ones with an attack fight back when attacked. Units don't target them on
      their own (target type `Wild`); flyers are aircraft that only arrows reach, swimmers stay on water.
- [x] **Combat fix**: hits used to do no damage (a position impact with no spread); weapons now strike their target
      (`TargetActorCenter`) and projectiles damage what they land on. `combat-test` checks the damage rule.
- [x] **Primitive rules** (2026-11-01): population (units use it, finished buildings provide it, training stops at the
      limit); StoneTower and Trap; levels (experience per kill: the victim's population, levels 0-9 with the
      original stat growth and Level.lvt bonuses); the 27 upgrade levels (generated by `research/tools/upgrade_rules.py`,
      researched by their buildings, attack/armour/speed/gathering/level cost applied; ability, transport and
      detection kept for later). Each has a test map.
- [x] **Items** (2026-11-01): the Blacksmith makes them (`research/tools/item_rules.py`), they lie beside it with their
      own ground sprite, and a unit of the right type picks one up and becomes its item variant, keeping level,
      experience and health. `items-test`.
- [x] **Air units** (2026-11-01, branch `prim_air`): Revolver (transport for 10: passengers weigh their `.chr` 0x188
      size, deduced; lands to load), Squadron and Flattop (splash). They hover at a token altitude because the art
      draws the height and shadow; ground weapons can't reach them, arrows and stones can. `air-test`. Left: the
      Revolver's +8 transport upgrade (Cargo's capacity is fixed; needs a custom cargo trait).
- [x] **Effects and sounds** (2026-11-01, branch `prim_air`): the installer's `effects` stage writes every weapon's
      flight and impact art (`sequences/weapons.yaml`) and sounds; the `units` stage writes each unit's sounds and a
      voice set per image (`voices/units.yaml`: Select, Action, Attack, Die, Work, Built). Weapons reference them
      (`research/tools/wire_weapons.py`); `PwVoiced` gives each unit its image's voice set. Still to do: shroud art,
      the idle roar of wild creatures, interface sounds.
- Test map: the converted original maps work directly.
- Automated tests: Lua test maps (`mods/pw/maps/*-test`, shared `testlib.lua`, `PwTest` helpers in
  `OpenRA.Mods.Pw/Tests`), run by `python run_tests.py`. Each fix gets a check that fails without it.
- Port the OpenSYW traits as needed: `Builder` / `UnderConstruction` (if the original uses worker construction),
  per-building production queues, sidebar logic.
- Lua test maps + `run_tests.py`, as in OpenSYW.

### Phase 4: Asset installer
Started 2026-10-31: `install-assets.cmd` / `.sh` run `utility pw --install-assets [GAME_FOLDER] [--out] [--only]
[--write-yaml] [--list]` (`OpenRA.Mods.Pw/AssetInstaller`, modelled on OpenSYW's). Stages so far: `tilesets`, `maps`;
every stage that converts art is added there directly (units next). Its map output matched the Python prototype
byte for byte.
- `OpenRA.Mods.Pw/AssetInstaller`: `TrcArchive`, then one stage per format (units, buildings, terrain, effects,
  chrome, audio, maps). Output to `^SupportDir/Content/pw/`, mounted with the same relative paths as `mods/pw/`.
- `mods/pw-content` first-launch screen: locate the folder holding `Pw.exe` and convert.
- Generated YAML (tilesets, sequences, cursors) stays in git; regenerate with `--write-yaml` and check it is
  unchanged.

### Phase 5: More races
One race at a time, the same way: art → rules (on the shared templates) → test map → playtest.
Order: **Primitive → Tyrano**, then Elf and Demon later. Adding a race should mean a new mixin and concrete
actors only; a needed change to a shared template is a sign the tree is wrong and gets fixed there.
Mechanics that need new traits as they come up: mana and spells (24), unit levels, Primitive items (Blacksmith
makes them, units equip them permanently), Tyrano merging, flying units, traps and egg mines, resurrection/raise
dead.

### Phase 6: Maps
- Converter for all 17 multiplayer maps (`Maps/`, `Maps/Rank/`), all 3 tilesets, doodads, neutral creatures,
  start locations. Everything it needs is decoded ([docs/formats/map.md](docs/formats/map.md)):
  - terrain: `MAP1` and `MAP2` per cell, for the custom terrain renderer, plus terrain types from the flags;
  - `MAP3` doodads become map actors (`doodad-<tileset>-<n>`, the variant chosen by sequence); berry bushes become
    resource actors with 4,000 berries and art that changes at 1,500 / 1,000 / 500 remaining;
  - `OBC` creatures become `wild-*` actors owned by the Creeps player, with their facing;
  - `P_PLAYE` start positions become `mpspawn` actors, and the player count goes into the map title.
- The Python tools (`map_render.py`, `map_objects.py`) are the reference for the C# converter: the same map must
  look the same in both.

### Phase 7: AI, UI and audio
- Bots per race (start from OpenSYW's `PeasantBaseBuilderBotModule`; read `Ai_00.txt` for the original priorities).
- Original chrome per race (the four panel sets in Jw2_02), cursors, fonts, menus.
- Sound effects, voices, music.

### Phase 8: Release
- Windows installer and portable zip (OpenSYW `packaging/`, GitHub release workflow). Only files that git tracks
  under `mods/` are packaged.

### Out of scope for now
- Campaign: ~48 scenarios in Jw2_06 (maps, briefings, dialogue, scripts in `P_SCRIP`). The archive reader should
  still list them, so they can be converted later.
- Elf and Demon races until Primitive and Tyrano are playable.

---

## 5. Rules for working

- Never modify or run anything inside `../Primitive Wars`; read only.
- No original asset in git: art, audio, text and map binaries go to `research/` or the player content folder.
- Every format claim in `docs/formats` cites how it was confirmed (Ghidra address, preview, editor diff).
- Before trusting `--check-yaml`, read OpenSY `Plan.md` §8.2: several missing world/player traits crash only at
  runtime.

## 6. Decisions (2026-10-31)

1. Project **OpenPW**, mod id `pw`.
2. **Modernised**, like OpenSYW: the original is the reference for content and balance, not for its interface
   limits.
3. Campaign **out of scope** for now; skirmish and multiplayer first.
4. Races: **Primitive, then Tyrano**; Elf and Demon later. "Tyrano" is the race, "Dinosaur"/`wild-*` the neutral
   wildlife.
5. Data files use **inheritance** throughout (§3).
