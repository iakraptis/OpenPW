# Game rules

The mechanics shared by all races, recovered from `Pw.exe` and the data files. Per-race numbers are in
[primitive.md](primitive.md) and [tyrano.md](tyrano.md). Confidence: **C** confirmed in code, **D** deduced from the
data.

## Time

The game advances in ticks of `ms = speedTable[speed]`, a table of 16 speeds at `0x563ac4`: 45, 47, 50, 52, 55,
58, 62, 66, **71**, 76, 83, 90, 100, 111, 125, 142 ms. The default index is 8 (**71 ms, about 14 ticks a second**);
the options slider shows `15 − index` (C, `0x456a1d`, `0x42a5e2`).

All durations in the data are ticks: training and construction (`.chr` 0x18c), upgrades (`Upgrade.upi` 0x204),
items. Animations advance one sequence step per tick. A walking unit moves each tick by the walk step for its
facing and current frame, plus the speed upgrade's bonus on each moving axis (C, `0x45d1a0`). So the "speed"
in the race tables is pixels per tick: Soldier 5.8 px per tick, about 82 px per second at the default speed. For OpenRA, the mod's `GameSpeeds` can use the same table (`Timestep: 71` as default), so durations
and speeds keep their original values without rescaling.

## Resources

- **Berries** are the resource. Every player starts with 400 (`P_PLAYE` 0x144).
- Berry bushes hold 4,000 and change art at 1,500, 1,000 and 500 left (C, `FUN_0045bb14`).
  **Mod change (balance):** an empty bush stays on the map and slowly grows back (1 berry every 30 ticks,
  `BerryBush.RegrowAmount/RegrowInterval`), instead of being used up for good.
- A worker plays its gather animation once (`Buildman`: 48 steps), then carries **12 berries**, plus the
  gathering upgrade (+4) (C, `FUN_00476e88`: `12 + bonus[player][type]`). It delivers to the main building.
- A **second resource** exists (`P_PLAYE` 0x194, 1,000 at start; type cost 0x194; upgrade cost +0x22c), but every
  unit, building and upgrade costs 0 of it in the shipped data. The mod can leave it out.
- Elf Wizards don't carry: they stay at the bush, and each trip's berries fly to the Heaven on their own (Berry fly,
  `JW2_11` record 16: 16 flight frames, 4 px per tick).
- Repair costs 2 berries per +5 HP (C, `0x459379`). Cancelling refunds the full cost (C, `0x4572dc`).

## Combat

Damage per hit (C, `0x457cc4`):

    damage = max(1, attacker attack + attack upgrades − (target armour + armour upgrades))

- The attacker's side is ×1.5 under a status effect (unit flags 0x1f000, likely Morale).
- The target's armour is raised by a percentage under a shield effect (flags 0xc00).
- Weapons with a splash radius (weapon record 0x1f8) hit everything inside it
  ([effect.md](../formats/effect.md)).
- Whether a weapon can hit air is bit 3 of its target mask.
- Attack range is `.chr` 0x1b0 (0x1b4 against air), plus the level bonus.
- The time between attacks is the attack delay at 0x13cc, reduced by the level bonus.

**Poison.** The poison weapons (`JW2_12` "Poison arrows", record 20: the Dilophos and the wild Dilophosaurus;
"poison-arrow", record 44: the Soldier's Venom arrow) stun the unit they hit for a moment, as in the original. The mod
stuns for 12 ticks: the unit can't move or attack, tinted green (`^Poison` in weapons/defaults.yaml). How the original
does it is not traced in `Pw.exe`; the record's 0x150, once taken for a damage kind, is the effect's draw style (C,
`FUN_0047a8e0` picks the draw routine by it).

## Invisibility and detection

- Units whose type has `.chr` 0x1f8 bit 0x2 are invisible from the start (unit flag 0x40, set when the unit is made
  or comes out of a transport; C, `0x460118`, `0x4573d6`): the Primitive Trap, the Elf Pixie, the Demon MudMan and
  DeathEye. The Vanishing spell (record 7) would do the same for a while, but no unit has it.
- An invisible unit is seen by its owner and allies, and by any other player only where one of that player's
  detectors covers its cell (C, `0x4682e0`). Attacking doesn't reveal it.
- Detectors mark their whole sight for this (C, `0x46832b`): the types with 0x1f8 bit 0x1 (Primitive Revolver and
  Trap, Tyrano TwinRhamphos and Ascomoid, Elf Pixie, Demon DeathEye), and the units whose detection research their
  owner has (Improve StoneTower Detection, Study Bubble Detection Secret, Learn Volcano Detection: the towers detect
  nothing before). Buildings detect once finished.

## Meat and healing

- A wild creature (owner 8, type kind 1) killed leaves meat worth its cost (`.chr` 0x190), with a random part, as a
  ground item of type 1-4 (Food small, middle, large, big: below 500, 1000, 2500 and above) (C, `FUN_00462ea6`).
- A unit on the meat's cell takes all of it into its carried store (unit +0x2c) (C, `FUN_004630b3`); idle units look
  for items within 192 px (C, `FUN_004631f6`).
- A hurt unit that can heal (`.chr` 0x1c8) eats 1 meat per tick for 1 HP. Without meat, units with 0x1cc = 0 (the wild
  creatures) heal 1 HP every 32 ticks; living Primitive units (0x1cc = 1) only by eating; machines don't heal
  (C, `FUN_0046089a`).
- Items on the ground count down every 32 ticks from a per-type period (table `0x5675fc`): Food 1, so meat loses 1
  per 32 ticks until gone; the other items 400 (C, `FUN_00462a30`). The mod keeps the other items until picked up.
- Mod addition: dead wild creatures come back where they were placed after a long delay (`PwRespawns`).

## The Chief

- Morale (help file, Special Abilities: "the Morale of chiefs can add to nearby primitive units"): Primitive units
  within 5 cells of their Chief get +10% attack and +10% armour. The original values are not known; these are the
  mod's.
- Fall out (`JW2_11` `Fall out.maz`, unlocked by the upgrade Improve Fall out for Chief): aimed at an enemy unit for
  80 mana (record 0x1e4); it wears the target's HP down to 1 (1 HP per tick) without killing it. The Chief has 100
  mana (`.chr` stat 4), regained over time (mod: 1 every 8 ticks).

## Population

Units use population (`.chr` 0x184), and Houses, Nests and the main building provide it (main 9, House or Nest
10) (D).

## Levels

Units with flag bit 1 (0x1f4 & 2) gain levels 0–9 (C, `FUN_00458b2c`).

- Experience comes from kills only: the killing blow (`FUN_00457d4b`, when HP reaches 0) calls `FUN_00458adc`,
  which gives the killer the victim's population (`.chr` 0x184), unless the victim is allied or a building (move
  class 1). Machines and wild creatures count like any unit. The remainder carries over to the next level.

- A level costs `xp_per_level − reduction` experience. The reduction comes from the "Reduce Level Up Exp."
  upgrade, 2 per level, kept per player at `0x54de20`.
- Each level raises HP, attack, armour and two more stats by an even share of `cap − base`, so at level 9 each
  stat reaches its cap (`.chr` 0x168–0x178): `gain(L) = ⌊d·(L+1)/9⌋ − ⌊d·L/9⌋`, `d = cap − base`. HP has
  cap = base for every unit, so HP does not grow.
- `Level.lvt` adds a per-type bonus row (170 types × 8 rows × levels 0–9) (C, `FUN_004594d2`,
  `FUN_00459527`):
  - row 0: % reduction of the value at `.chr` 0x13cc, which is the attack delay (D);
  - row 1: attack range bonus in pixels, also against air;
  - row 2: spell power: the spell's effect is raised by this % (C, `0x478db8` reads the table at `0xe5e0b8` + 0x50);
  - rows 4 and 5 (Dilophos; MudMan and Gargoyle): no code reads them, so they have no effect.

## Upgrades (`Upgrade.upi`)

64 records of 0x394 bytes after a `u32` 100. Each building lists the upgrades it researches (`.chr` 0x2c8 count,
0x2cc list).

| Offset | Content |
|---|---|
| 0x00 | Name |
| 0x41 | "Improve at <building>" text |
| 0x84, 0x88 | Number and list of affected unit types |
| 0xc8 | Levels (1, 3 or 5) |
| 0xcc + 0x14·s | Bonus per level for stat *s*: HP, mana, attack (0xf4), armour (0x108), 2 unknown, range (0x144), speed (0x158), sight (0x16c), transport (0x180), gathering (0x194), ability (0x1a8), detection (0x1bc), skill (0x1d0) |
| 0x1e4 | Hotkey (ASCII: `G`athering, `W`eapon, `A`rmour, …). The button icon is the record number: `Upgrade.trt` has one per record (48) |
| 0x204 | Time per level (ticks) |
| 0x218 | Cost per level (berries) (C, `0x476e2e`) |
| 0x22c | Second-resource cost per level |
| 0x240 + 4·k | Level *k* has a required building |
| 0x254 + 0x40·k | That building's type (only when the flag is set; the other entries are leftovers) |

## Items (`Item.itm`)

32 records of 0x174 bytes: name, cost (0x40), time (0x48), the unit type that can equip it (0x50), the type it
becomes (0x90), attack bonus (0xd4) and armour bonus (0xd8), hotkey (0xf4, ASCII; the button icon is the record
number in `Item.trt`). On the ground an item is `JW2_07` `I<nn>.spz` (palette `Item.pnt`). The Blacksmith sells the four Primitive
items (`.chr` 0x30f count, 0x310 bytes). Equipping is permanent: the unit becomes the equipped type.

In play (from the original game): the Blacksmith makes the item, which appears on the ground beside it, even when
every cell there is taken. The player selects a unit of the item's type and clicks the item; the unit walks over,
picks it up and becomes the equipped type, keeping its level and experience. The other
items (Medical Herb, the Dragon's Pearl, …) have no target type and belong to the campaign.

## Tech tree (`.chr`)

| Offset | Content |
|---|---|
| 0x1fc, 0x200 | Number and list of required buildings |
| 0x240, 0x244 | Workers: buildings they can build |
| 0x284, 0x288 | Buildings: units they train |
| 0x2c8, 0x2cc | Buildings: upgrades they research |
| 0x30f, 0x310 | Buildings: items they sell (bytes) |
| 0x330, 0x334 | Building footprint in 32-pixel cells |
| 0x188 | Transport size (1 for workers, 2 for soldiers) (C, `0x45f334`) |

## Merging (Tyrano)

The command panel offers a merge when the selection holds (C, `0x4716d1`):

| Ingredients | Result |
|---|---|
| 2 × Velocis | TwinVelocis |
| 2 × Rhamphos | TwinRhamphos |
| Dilophos + Pteras + Trices | Mutant |

Each recipe also passes an availability check (`FUN_0047259e`), where the result's own requirements apply:
TwinVelocis needs a LandNest, and Mutant needs the "Mutant Morphosis" upgrade (D). The merged unit keeps the
originals and releases them when it changes back (C, `0x45ff60`). Units carry flag 0x800 when they can merge.

In the mod (`PwMerge` on the parts, `PwMerged` on the result): the parts meet at the one nearest their middle and
become the merged unit there, with their mean health and their lowest level. Split, they come back with the merged
unit's health share and their own levels. If one of them is lost, ordered elsewhere, or doesn't arrive within 400
ticks, nothing happens. The population adds up (TwinVelocis 4 = 2 + 2, Mutant 11 = 2 + 4 + 5); a TwinRhamphos takes 2
of its parts' 4.

The original's merged units are weaker than their parts together: two Velocis have 400 HP and deal 35 every 29
ticks each, a TwinVelocis 350 HP and 55 every 40; the Mutant's 550 HP and 100 every 64 (with splash) are below its
three parts' 730 HP and their three weapons. The mod evens it out: TwinVelocis 400 HP and 60 damage, Mutant 900 HP.
The TwinRhamphos has no attack (`.chr` attack 0 and no weapon; the help file lists it with no ground or sky attack):
it is a scout, and a detector.

The AI (`PwMergeBotModule`) merges the Tyrano units waiting at its base that aren't in an attack or defence squad:
every TwinVelocis and Mutant it can, the closest parts together, and one TwinRhamphos to see invisible units. With a
part of the Mutant and no Mutant Morphosis, it researches it. Its Ground Dino Mutation Armor and Weapon research come
before more units once it has four (`PwUnitBuilderBotModule.PriorityResearch`): Tyrano units have no armour of their
own.

## Disguise (Tyrano)

The help file (Special Abilities): "Tyranos are mutated dinosaurs. They have the ability to revert to their original
forms. They often use this ability to sneak past sentries as hunting game … it has a disadvantage of not being able
to attack." The Disguise research (`Upgrade.upi` 42, at the TyranoNest) lists Velocis, Dilophos, Rhamphos,
TwinRhamphos, Pteras and Trices.

In the mod (`PwDisguise`): the unit takes the look of the wild creature of its kind (Velociraptor, Dilophosaurus,
Rhamphorhynchus, Pteranodon for both pterosaurs, Triceratops) and back, keeping its own health, speed, level and
experience. Disguised, it cannot attack; enemies see the creature's name and the Creeps' colours, and nothing attacks
it on its own (it has the Wild target type), though it can still be attacked by order. The original's stats in
disguise are not known.

## Spells (`JW2_11`)

Which spells a unit has is its type's spell bitmask, `.chr` 0x1f0 (bit *n* = record *n*), copied to the unit when it
is made (C, `0x460108`, `0x4615cf`). A spell button is usable (C, `FUN_0046c3a5`) when the owner has the research
of record 0x15c (−1 none; Fall out 1, Fake 15, Meteor 18), the unit's level (counted from 0) is at least record
0x160 (Thunder bolt 1, Mass temper 3, Jump portal 3, Stone curse 1, Quake 1; the help file counts levels from 1),
and its mana covers the cost.

A spell record's costs (C, `FUN_00478cd5` checks them, `FUN_00478db8` pays them): 0x1e4 mana (Primitive and Elf:
Fall out 80, Thunder bolt 40, …) and 0x1e0 health (Demon: Stone curse 85, Noxious gas 120, Quake 150, Rise death
100, Suicide 1). The caster needs more health than the cost. The effect (0x1e8, negative = damage) is raised by the
caster's spell power. Area effects hit within the radius (0x1f8) on the effect frames listed at 0x8bc (count 0x8b8),
one frame every 0x21c ticks (4), until the effect ends: Noxious gas on every frame for its duration (0x1f4, 144
ticks), Quake on each of its 31 frames, Suicide once. Stone curse sets the target's petrified flag (unit flags 0x20)
on its hit frame; where the flag is cleared was not found.

Elf spells in the mod (`PwSpell`): Thunder bolt 80 damage to a unit; Shield a 480-point damage shield on a friendly
unit (the shield pool of `0x478db8`, drained by damage in `FUN_00457d4b`), Mass temper 160 for 300 ticks to the
friendly units within 56 px; Teleport the Ranger to any explored place; Jump portal open 216 ticks (its 54 frames of
4), throwing enemies that walk in 12 cells away (the landing is the mod's); Drop stone 200 within 64 px; Cure 2 HP a
tick for 1 mana a tick; Fake a copy of a unit, the caster's own, harmless, gone after 960 ticks (Fake's 0x1ec 30 ×
32: the original's timer for summoned units); Meteor 60 meteors of 30 within 64 px, falling within 80 px of the
place, the ManaSpread lost; Resurrect the living units that died within 64 px of the place lately, the caster's own,
at half health. The ManaSpread's passive (.chr 0x1f8 bit 0x4, and its name) gives nearby casters mana (the amounts
are the mod's). Magic.trt holds 16 icons (records 0-15): Cure, record 18, takes Vanishing's.

## Kill points (Demon)

The help file (Special Abilities): "Demons, who were conceived from evil, gain KP (Kill Points) by destroying living
creatures. KP are the source of strength in a Demon, and serve as HP and MP. Being damaged or casting spells will
reduce KP. … Any units killed, even friendly units, will replenish KP."

- KP is the unit's health; spells cost health (above).
- A kill gives the killer a quarter of the victim's full health, up to its own, when the killer's type has
  `.chr` 0x1f8 bit 0x20 (every Demon unit but the DeathEye and the Imp) and the victim's has 0x1f4 bit 0x10 (living:
  not machines, buildings or the raised zombies) (C, `FUN_004578a9`, called on the killing blow).
- Healing follows 0x1c8/0x1cc as for the others (C, `FUN_0046089a`): Demon units with 0x1cc = 1 don't heal over time
  and heal by eating meat.

In the mod (`PwKillPoints`, `PwLiving`, `PwSpell`): as above. Stone curse lasts 264 ticks (its 66 read as frames of 4
ticks, D), raised by spell power; the spell levels of the help file (Kelpa Stone curse at level 2, Warlock Quake at 2)
count from 1, ours from 0. The raised zombies stay until killed: the only timed units in the original are the Fake
spell's illusions (unit flag 0x10 and a lifetime from Fake's 0x1ec, counted down every 32 ticks; C, `0x4792c2`,
`FUN_0046089a`).

## Music (`JW2_15`)

`JW2_15` holds 19 MP3s (128 kbps, 44.1 kHz). Most of the names are masked; the named ones are `01af01`,
`02af02`, `04elf01`, `05elf02`, `07ty01`, `08ty02`. `Pw.exe` plays them by entry number through Miles
(`mss32.dll`, `Mp3dec.asi`), or CD audio track number + 2 when the MP3 decoder is missing (C, `FUN_00403b90`,
`FUN_004621fa`). Races are numbered Primitive 0, Elf 1, Tyrano 2, Demon 3.

| Entry | Length | Played |
|---|---|---|
| 0 | 59 s | the menus and the start of the program (C, `0x403e22`, `0x46883c`, `0x46a158`) |
| 3 × race + 1, + 2 | 3-6 min | the battle: the two tracks of the player's race in turn, each time one ends (C, `FUN_00462150`) |
| 13 + race | 40-106 s | the start of a game and the race choice (C, `FUN_004665f0`) |
| 17 | 57 s | the score screen after a defeat (C, `FUN_00412230`, outcomes 1 and 3) |
| 3 | 53 s | the score screen after a victory: the code plays `4 × race + 3`, which is 3 for Primitive but a battle track for the others, so the mod plays 3 for every race (D) |
| 6, 9, 12, 18 | 21-175 s | not played by the skirmish game; perhaps the campaign |

In the mod (`MusicPlaylist`, `PwRaceMusic`, `music/pw.yaml`): the installer writes them as `music/<name>.mp3`
(`AssetInstaller/Music.cs`). The menu plays track 0; a game plays the race's start tune and then its battle
tracks, and the victory or defeat tune at the end. The battle tracks and track 18 are in the music player; the
others are hidden. Observers hear the plain playlist.

## Open

None of these affect the races' play:

- where petrification (Stone curse) ends, and Jump portal's landing place;
- `.chr` 0x13d0 and 0x2410;
- the two unused upgrade stat blocks.
