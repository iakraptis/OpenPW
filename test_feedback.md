# Tester feedback

Bugs and problems reported by testers. We gather them here first; fixing comes later, before the release.
Each entry: what was reported, the build it was seen on, what we found, and its status.

Status: **open** (not looked at), **investigated** (cause known, not fixed), **fixed** (with the commit), **resolved** (no change needed).

## 1. Elf economy too strong

**Reported:** Elves out-gather the other races. Their Wizards don't walk back to the main building, so many of them
can work the same berry bush at once.

**Build:** portable test-4ee8654 (latest main). The tester probably played Demon, then Elf.

**Status:** resolved, no action needed. At the real maps' distance (bushes 5 cells from the main building) Elf
gathers slightly less than the others; it leads only where bushes are far from the main building.

**How Elf gathering works:** a Wizard stands next to a bush like any worker. It gathers for 48 ticks, then the 12
berries fly to the main building by magic while the Wizard stays at the bush, casting until they arrive. The other
races' workers carry the berries home and walk back, so they spend most of each trip away from the bush. Berries fly
about as fast (158) as workers walk (160, Wizard 144), but only one way.

**Gather test** (mods/pw/maps/gather-test): one base per race, 25 workers each, four bushes in a row below the main
building. These are berries earned in the second half, ticks 2500 to 5000:

| Race | Bushes 6 cells from the HQ | 18 cells |
|---|---:|---:|
| Primitive | 6432 | 2724 |
| Tyrano | 6432 | 2724 |
| Demon | 6432 | 2724 |
| Elf | 6768 (+5%) | 4032 (+48%) |

- **Distance** (empty cells between the main building's edge and the bush): the Elf lead grows with it. Close by it
  is small; at 18 cells Elf earns about half again as much as everyone else.
- **One worker per base:** every race earns the same (252 in the second half, bushes 6 cells away). The gap appears
  only with many workers per bush (not yet checked: one worker with far bushes).
- **Repeats:** three runs gave the same numbers to the berry; the game is deterministic, so there is no luck in it.
- **Demon:** an earlier version of the test had Demon 20% behind. That came from the test's layout (its base was
  against the map edge), not the game. Away from the edge, Demon matches Primitive and Tyrano exactly.

**Real maps:** on all 18 multiplayer maps, every start has its nearest bushes 4 or 5 cells from the main building
(the 4th nearest: median 5), and 7 or 8 bushes within 10 cells. That is closer than the test's 6, where Elf leads by
only 5%. So at the start Elf should barely lead; the gap should open later, as those bushes empty (4000 berries each)
and workers go farther. A Wizard can also work far bushes without a new main building nearby.

**Still to check:** what the original game does (Ghidra); a test with the real layout (bushes 4 to 5 cells away,
around the building) run long enough for them to empty.

**Considered, not done:** Wizards gather 1% slower. Gather time is whole ticks (48), so the nearest step is 49
(+2%). Gather test with the bushes 5 cells from the HQ, as on the real maps (berries earned, ticks 2500 to 5000):

| Race | Wizard 48 ticks (now) | Wizard 49 ticks |
|---|---:|---:|
| Primitive, Tyrano, Demon | 7188 | 7188 |
| Elf | 7092 (-1%) | 6960 (-3%) |

At the real distance Elf already gathers slightly less than the others; 49 ticks would widen that. It only pays off
where bushes are far (18 cells: Elf +48% now).

## 2. Bubble tower never kills flying units

**Reported:** the Elf Bubble tower's damage is so low that passive healing outpaces it. One kept attacking a Demon
Death Eye forever.

**Build:** portable test-4ee8654 (latest main).

**Status:** resolved: fixed (not committed yet). `BubbleShot`'s damage warhead: `Damage: 2`, `ValidTargets: Ground, Air`.
combat-test (Defence): a Death Eye seen through a Pixie takes 2 from the first bubble; a wild Velociraptor loses 17 HP
net in 400 ticks despite its healing. elf-test checks the 2 damage on a ground unit.

**Cause:** the Bubble's shot (`BubbleShot`, mods/pw/weapons/elf.yaml) does no damage at all to flying units. It
replaces the arrow template's damage warhead (`Warhead@1Dam`, valid against Ground and Air) with the bubble effect;
its own damage warhead (`Warhead@2Dam`, 1 damage) names no valid targets, so it takes the engine's default, Ground
and Water. The bubble itself still lands on flyers, so the tower keeps shooting a Death Eye, Pixie, Rhamphos and so on
for ever without hurting it.

**Healing:** wild creatures, the wild dinosaurs among them, heal 1 HP every 32 ticks on their own (`PwRegenerates`,
the original's rule, rules.md "Meat and healing"), and the Bubble tower shoots wild predators. At 1 damage every 26
ticks the tower barely outpaces that: it takes off about 1 HP net every 140 ticks, so a 100 HP dinosaur takes about
14000 ticks (over 16 minutes). Player units don't heal on their own; a living one carrying meat eats 1 meat every 8
ticks, healing 1 HP.

**Test:** a Bubble tower with a Pixie beside it (to see the invisible Eye), against an enemy Death Eye (100 HP,
armour 0) 6 cells away; one Eye without meat, one carrying 300. The tower fires every 26 ticks (71 ms each at the
default speed):

| Shot | Death Eye | Death Eye with 300 meat |
|---|---|---|
| Now (no damage to flyers) | never dies (100 HP after 3000 ticks) | never dies |
| Damage 1, valid against Air | dies after 2592 ticks (about 3 min) | 100 HP after 3000 ticks |
| Damage 3, valid against Air | dies after 878 ticks (about 62 s) | 55 HP after 3000 ticks; dies once the meat runs out (about 2400 ticks) |

**Proposed fix:**
- **Air units:** add `ValidTargets: Ground, Air` to `BubbleShot`'s damage warhead (`Warhead@2Dam`), so the tower
  hurts flyers. Without it, no damage value helps against them.
- **Damage 2** (from 1): a design change, as the original's bubble does no damage (docs/game/elf.md). Worked out, not
  yet measured:
  - Death Eye, no meat: 50 hits, about 1300 ticks (about 1.5 min); at 1 it was 2592, at 3 it was 878.
  - Wild dinosaur healing 1 HP every 32 ticks: about 1.2 HP net per shot, so 100 HP in about 2200 ticks (2.5 min),
    instead of about 14000.
  - A unit carrying meat still outheals it (1 HP every 8 ticks is more than 2 every 26) until the meat runs out.
  - Ground units take 2 too, minus armour, at least 1.
- **Test:** turn the Bubble test above into checks in combat-test (about 40 s more), with a wild dinosaur case.

## 3. Economy too fast: too many workers on one bush

**Reported:** the economy is too fast. Since workers share cells, many of them gather the same bush at once.

**Build:** portable test-4ee8654 (latest main).

**Status:** fixed (not committed yet), the original's rule. `BerryBush.MaxGatherers: 1`: a worker holds the bush for
its gather animation (a Wizard lets go before its berries fly); finding it held, it goes to the nearest bush with room
within 5 cells, or waits beside it (`GatherBerries`). economy-test: three workers sent to one bush, never two
gathering it at once, and one goes to the free bush 4 cells away. Gather test (25 workers, 4 bushes 5 cells away,
ticks 2500 to 5000): 2,544 berries per base for every race (was 7,188, Elf 7,092), 0.25 a tick per bush, the
original's ceiling. The AI's gatherer module is unchanged (it already spreads its workers).

**The original (C, Pw.exe, hand-written assembly; listings in `research/ghidra/out/gather/`):** **one worker gathers a
bush at a time.**
- Each bush cell has a "taken" bit (`0x10000000` in its MAP3 word). A table of up to 400 entries
  (`0x54cb5c` count, `0x54cb60` entries: in use, bush cell, unit) records who holds which bush.
- A worker that reaches its bush checks the bit (`0x45b832`): free, it starts gathering (state `0x29`) and takes
  the bush (`0x45bb89` sets the bit); taken, it looks for the nearest free bush within 5 cells around it (an 11 × 11
  spiral, `0x45ba99`) and walks there; with none free it waits (state `0x2d`).
- When its gather animation ends (`0x45e60a`) it frees the bush (`0x45bc0b`), takes 12 berries and walks home.
  Workers that stop gathering also free theirs (`0x45bc58`).
- Elf Wizards hold the bush the same way while they cast (42 ticks, `0x45f5e8`) and free it before the berries
  fly, so the next Wizard can start while the berries are on the way.
- So one bush yields at most 12 berries per gather animation (48 ticks for the Buildman), about 0.25 a tick.

**The mod:** no limit. Workers gather from any of the 8 cells around a bush, and since workers share cells (up to 5
in a cell), dozens can work one bush. In the gather test (4 bushes, 25 workers each, bushes 5 cells away) a base earns
about 2.9 berries a tick, about 0.72 a tick per bush: nearly 3 times the original's ceiling.

**Possible fixes:**
1. **The original rule** (recommended): `BerryBush` takes one gatherer at a time; a worker finding its bush taken
   goes to the nearest free bush within 5 cells, or waits beside it. Wizards free the bush when the berries leave.
   Keeps cell sharing, so workers still walk past each other.
2. **A limit per bush, a Mod change:** `BerryBush.MaxGatherers` (2 or 3), same behaviour otherwise; tune with the
   gather test.
3. Not recommended: undo cell sharing. It only made crowding worse; the original lets units walk through each other,
   and without a per-bush limit the throughput would still be unbounded.

To check with the fix: the gather test before and after (berries a tick per base and per bush); the AI's gatherer
module still spreads its workers (a bot sending all workers to one bush would stall); bot battles' economy.

## 4. Melee units can't attack diagonally

**Reported:** Powermen (probably every melee unit) can't attack a target from a diagonal cell, so only 4 can attack
one target. Large groups of melee units are weak.

**Build:** portable test-4ee8654 (latest main).

**Status:** fixed (not committed yet). `^Melee` `Range: 1c512`. combat-test: eight Powermen in the eight cells
around an enemy Giant all hit it; a Powerman holding its ground 2 cells from an enemy never reaches it. The blocked
melee check now blocks all 8 cells. Not run: the bot battles (melee armies are stronger now).

**Cause:** every melee weapon (19, all races and wild creatures) inherits `^Melee` (mods/pw/weapons/defaults.yaml) with
`Range: 1c256` (1,280). A unit in a side cell is 1,024 from the target, in reach; a diagonal cell is 1,448 away, out
of reach. So 4 attackers at most around a one-cell target. `PwMeleeAttack`/`MeleeAttack` pick their cells by the
weapon range (`IsInRange`), not by a fixed list, so they follow whatever range the weapon has.

(The original's melee reach is a rectangle check that includes the diagonals, `.chr` 0x370, docs/formats/chr.md. Not
a reason for the fix: the mod chooses its own.)

**Possible fixes:**
1. **Raise `^Melee` to `Range: 1c512`** (1,536) (recommended): covers the diagonal (1,448) with some margin and still
   doesn't reach two cells away (2,048). 8 attackers around a one-cell target; more around large units and
   buildings. One line; applies to wild predators too.
2. A tighter value (`1c460`, about 1,484): diagonals only just in reach; less margin for workers standing at
   sub-cell positions.
3. Let melee units share cells as workers do: more attackers than 8, but bunching changes all combat. Not
   recommended.

To check with the fix: a combat-test check that 8 melee units hit one target together (and none from 2 cells away);
update the "four cells" comment in `MeleeAttack.cs`; bot battles, since melee armies get stronger (Primitive's AI
already leads).

## 5. Berry bushes grow back too fast

**Reported:** bushes regrow too fast; regrowth should be halved.

**Build:** portable test-4ee8654 (latest main).

**Status:** fixed (not committed yet). `BerryBush.RegrowInterval` default 60 (1 berry every 60 ticks), with the
trait's description and docs/game/rules.md.

**Now:** a Mod change (the original's bushes never grow back, docs/game/rules.md): every bush below 4,000 grows 1
berry every 30 ticks (`BerryBush.RegrowAmount: 1`, `RegrowInterval: 30`, the trait's defaults in
`OpenRA.Mods.Pw/Traits/BerryBush.cs`; no rules file overrides them). It grows whenever it isn't full, not only once
empty. That is about 28 berries a minute per bush at the default speed (71 ms a tick); the 7 to 8 bushes around a
start give about 200 to 225 a minute, about what one worker would take gathering without a break (12 every 48
ticks, about 210 a minute).

**Fix:** 1 berry every 60 ticks (about 14 a minute per bush). Set `RegrowInterval: 60` on `^BerryBush`
(mods/pw/rules/defaults.yaml) or change the trait's default and its description; update the Mod change line in
docs/game/rules.md ("1 berry every 30 ticks"). With entry 3's per-bush limit the two compound: decide the regrowth
after that fix, with the gather test run long enough to empty the bushes.

## 6. No victory or defeat screen

**Reported:** a game ends with no victory or defeat screen, image or message; the top right shows a developer
version text.

**Build:** portable test-4ee8654 (latest main).

**Status:** fixed (not committed yet), as the chosen design below. `PwEndScreenWidget` and `PwEndScreenLogic`; the
sheets `mods/pw/chrome/assets/end-<race>.png`, `end-defeat.png` drawn by `tools/end_screens.py`. Seen in the game
(Elf victory, defeat, 16:9 and 21:9 windows): it fades in after the game-over delay, shows the elapsed time, and a
click opens the OpenRA in-game menu. The version label at the menu's top right is unchanged (`{DEV_VERSION}` in a
development build). The OpenRA end-of-game menu stays (statistics, leave).

**The mod now:** the engine's flow. When the local player wins or loses, after `MissionObjectives.GameOverDelay` the
interface bar is replaced by the stock observer widgets; when the whole game is over, the stock in-game menu opens
(`common|chrome/ingame-menu.yaml`). That menu has the OpenRA logo slot and the version label at the top right
(`VersionLabelLogic`: the build's version, `test-4ee8654`): the "developer message". Victory and defeat only show as
the "Win"/"Lose" notification and the music (`PwRaceMusic` VictoryMusic/DefeatMusic).

**The original** (art in `JW2_02`; previews in `research/previews/ui/end/`):
- Full-screen pictures, 800 × 600 (`.scr` + `.pnt`): `P/E/T/D_win`, `P/E/T/D_def`. Only four distinct images:
  Primitive and Elf share theirs (victory: an Elf sorceress with a staff; defeat: a horned Demon lord), Tyrano and
  Demon share theirs (victory: a Tyrano warrior with a saw-toothed blade; defeat: the Demon lord again, other
  pose). Parchment-toned, dark vignette (C, the files are identical pixel for pixel).
- The score screen (C, `FUN_00412230`): the outcome's picture full screen, four column headers at the top, then a
  row per player (45 px apart) with the race emblem and seven numbers; in multiplayer "Elapsed Time hh : mm : ss".
  Outcomes 0-3: 0 and 2 play the victory tune, 1 and 3 the defeat tune (0 victory, 1 defeat; 2 and 3 probably draw
  and surrender, D, as the End Game menu offers Draw and Surrender).
- Small art: `Win` and `Defeat` titles (green and red brush lettering), a `Resource` header, a stone tablet panel
  (`Result_bg`, 293 × 175) and a `Continue` button (`Result_cont`), palette `Outcome.pnt`.
- The per-race End Game menu (`*_end_menu.m`: Restart, Abort Mission, Exit Program, Previous Menu, Draw,
  Surrender) is the in-game quit menu, not needed: the OpenRA menu covers it.

**Ideas for the end image** (to choose; mock-ups as PNGs before building):
1. **The original pictures, full screen.** At the local player's victory or defeat the view fades to the picture
   (scaled to fit, letterboxed in black) with the original `Win`/`Defeat` title over it; a click or a few seconds
   later the OpenRA menu opens on top (statistics). The installer exports the four images. Faithful, cheapest; but
   a Primitive player sees an Elf on victory and a Demon on defeat, as in the original.
2. **One picture per race and outcome, from the race's own art.** The parchment background of the original
   pictures with the race's figure: its strongest unit or hero drawn large from its `.chr` frames (Chief, AngelElf,
   Tyranos, Devil), standing for victory, its death frames for defeat. Eight distinct screens, all from original
   art; more work, and the unit art is small (upscaled).
3. **A banner over the battlefield.** The world stays visible, darkened and desaturated; a wide banner across the
   middle with "VICTORY" or "DEFEAT" in the race's lettering (the fonts of the lettered Menu buttons,
   `AssetInstaller/MenuButtons.cs`), the race emblem (the lobby flags) and the original title art as an accent.
   Works for every race and outcome; no pictures needed.
4. **2 + 3 combined:** the banner, with the race's figure from idea 2 beside it.
5. **Extras for any of them:** the camera moves to the last building destroyed (or the winner's main building)
   before the image; the victory or defeat tune already plays; the elapsed time and a short line under the title
   ("The Elf tribe stands victorious"); allies' victory shown as shared; observers see the winner's screen.

**Also:** decide whether the in-game menu keeps the version label at the top right (useful to testers to report
the build) or shows the game's title instead.

**Chosen design** (mock-ups: `research/previews/ui/end/mockup/panel-<race>-victory.png`, `panel-all.png`):
- **Picture:** one new painted picture per race (`research/previews/GenImages/`, made with an AI image generator from
  the prompts below), in the original's style: a single figure on plain aged parchment, sepia, dark vignette. Primitive
  chieftain with a stone club, Tyrano reptilian warrior with a saw-toothed blade, Demon lord with a spiked staff, Elf
  sorceress with a staff. 1248 × 832 (3:2).
- **Fit:** the picture fills the window's height, centred; the margins on wider windows are the same picture blurred
  and darkened, so there are no black bars.
- **Panel:** the same spot for every race, at the right middle (centred about 78% across, halfway down), 460 × 230 px
  at 1600 × 900: the picture behind it blurred and slightly darkened, rounded corners, a thin rule in the race's light
  colour. Where it overlaps the figure (Tyrano's and the Primitive's raised arm), the blur keeps the text clear.
- **Message inside the panel:** "VICTORY" in the race's lettering (the font and colours of its Menu button,
  `MenuButtons.cs`: gradient fill, dark outline, glow in the race's colour); "The <Race> tribe stands victorious" in
  the same font; "Elapsed time mm:ss" and "Click to continue". A click opens the OpenRA menu as now.
- **Watch:** the Elf subtitle nearly fills the panel's width (widest font): a wider panel or smaller subtitle. Demon's
  blackletter capital V reads like a U at a glance.
- **Defeat** (mock-up: `mockup/panel-defeat.png`): simpler, the same for every race. A plain aged parchment with no
  figure (`research/previews/GenImages/defeat-parchment.png`, 1248 × 832, generated in code, so no licence question):
  light sepia, faint mottling and grain, darkening gradually to brown at the edges. The same fit and the same panel in
  the same spot, with a neutral message in OpenRA's own fonts (`common|FreeSansBold.ttf` and `FreeSans.ttf`, already
  shipped): "Defeat" in bold, "Your tribe has fallen", "Elapsed time mm:ss", "Click to continue"; muted cream ink, no
  glow.
- **Licence:** check the licence of the generated victory pictures before they go into the repository; they are not
  original game files, so unlike the original art they could ship with the mod.
- **Considered:** the original pictures (only four, shared by pairs of races), a race-coloured banner over the
  darkened battlefield (`mockup/<race>-victory.png`, `-defeat.png`), the pictures with the text placed beside each
  figure (`mockup/picture-*.png`).

**Image prompts** (victory; same style line for all four):
- Primitive: "Painted fantasy illustration of a muscular primitive tribal chieftain in furs and bone ornaments,
  raising a stone-headed club in triumph, single figure centered, plain aged parchment background, sepia and warm
  brown tones, soft dark vignette, dramatic side lighting, no text"
- Elf: "Painted fantasy illustration of a graceful elf sorceress in flowing robes holding a tall carved wooden staff,
  calm and victorious, single figure centered, plain aged parchment background, sepia and warm brown tones, soft dark
  vignette, dramatic side lighting, no text"
- Tyrano: "Painted fantasy illustration of a towering reptilian dinosaur warrior with scaled armor and chains, raising
  a jagged saw-toothed blade and roaring in triumph, single figure centered, plain aged parchment background, sepia and
  warm brown tones, soft dark vignette, dramatic side lighting, no text"
- Demon: "Painted fantasy illustration of a horned demon lord in spiked bone armor and a dark red cape, gripping a
  spiked staff and standing victorious, single figure centered, plain aged parchment background, sepia and warm brown
  tones, soft dark vignette, dramatic side lighting, no text"
