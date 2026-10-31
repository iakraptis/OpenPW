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
- A worker plays its gather animation once (`Buildman`: 48 steps), then carries **12 berries**, plus the
  gathering upgrade (+4) (C, `FUN_00476e88`: `12 + bonus[player][type]`). It delivers to the main building.
- A **second resource** exists (`P_PLAYE` 0x194, 1,000 at start; type cost 0x194; upgrade cost +0x22c), but every
  unit, building and upgrade costs 0 of it in the shipped data. The mod can leave it out.
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

## Population

Units use population (`.chr` 0x184), and Houses, Nests and the main building provide it (main 9, House or Nest
10) (D).

## Levels

Units with flag bit 1 (0x1f4 & 2) gain levels 0–9 (C, `FUN_00458b2c`).

- A level costs `xp_per_level − reduction` experience. The reduction comes from the "Reduce Level Up Exp."
  upgrade, 2 per level, kept per player at `0x54de20`.
- Each level raises HP, attack, armour and two more stats by an even share of `cap − base`, so at level 9 each
  stat reaches its cap (`.chr` 0x168–0x178): `gain(L) = ⌊d·(L+1)/9⌋ − ⌊d·L/9⌋`, `d = cap − base`. HP has
  cap = base for every unit, so HP does not grow.
- `Level.lvt` adds a per-type bonus row (170 types × 8 rows × levels 0–9) (C, `FUN_004594d2`,
  `FUN_00459527`):
  - row 0: % reduction of the value at `.chr` 0x13cc, which is the attack delay (D);
  - row 1: attack range bonus in pixels, also against air;
  - row 2: % mana cost reduction (casters);
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
| 0x1e4 | Icon |
| 0x204 | Time per level (ticks) |
| 0x218 | Cost per level (berries) (C, `0x476e2e`) |
| 0x22c | Second-resource cost per level |
| 0x240 + 4·k | Level *k* has a required building |
| 0x254 + 0x40·k | That building's type (only when the flag is set; the other entries are leftovers) |

## Items (`Item.itm`)

32 records of 0x174 bytes: name, cost (0x40), time (0x48), the unit type that can equip it (0x50), the type it
becomes (0x90), attack bonus (0xd4) and armour bonus (0xd8), icon (0xf4). The Blacksmith sells the four Primitive
items (`.chr` 0x30f count, 0x310 bytes). Equipping is permanent: the unit becomes the equipped type. The other
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

## Open

None of these affect Primitive or Tyrano:

- the spell mana costs (a run-time table; for Elf and Demon);
- `.chr` 0x13d0 and 0x2410;
- the two unused upgrade stat blocks.
