# Missile hit behavior

## Goal

Most missile attacks in Primitive Wars hit the unit they are aimed at instead of missing because the target moved
after launch. This branch will revisit OpenPW's current projectile hit resolution and make the covered attacks
reliably hit their chosen target. Keep the projectile travel, artwork, attack timing, damage, splash, and target
restrictions unless a later decision explicitly changes them.

The inventory below is the coverage checklist. A missile attack is any concrete actor weapon that resolves to
`Projectile: Bullet` through weapon inheritance. This includes thrown weapons and magic effects as well as arrows.
Melee and instant-hit weapons are excluded. Missile attacks from defensive buildings and hostile wildlife are
included so they are not missed by a unit-only change.

The original game's projectile mode and impact splash are separate properties. The mode controls whether a projectile
tracks its target, checks for actors along its path, or simply travels to its impact point. Splash controls whether
nearby actors are damaged at impact. A zero splash radius does **not** mean the projectile can only damage one actor:
projectiles in mode 4 can pierce actors along their path.

The original mode (`JW2_12` field `0x190`) and splash radius (`0x1f8`) below are read from the extracted weapon
records. The code uses mode 2 to update the projectile's direction toward its target (`0x47aee5`) and terminate when
it reaches the target (`0x47acd6`); mode 4 checks actors along the path (`0x4772f1`/`0x477377` call `0x47afc0`),
keeps a per-projectile hit list, and continues moving after a hit (`0x47acd6`); mode 0 has neither behavior and
impacts at its fixed endpoint (`0x47acd6`). These findings are confirmed in `Pw.exe` (C; see
[effect format](docs/formats/effect.md)). The splash field is loaded at `0x47713d` and damage is applied by the
area-damage routine at `0x457d4b` (C).

### Impact splash vs. path collision

- **Mode 0:** The projectile does not collide with actors along its flight path. Its impact applies splash around
  the endpoint. The original mode-0 weapons in this inventory have radii of 32 px (BowMachine), 48 px (Flattop
  and EggThrower), and 64 px (Dragon). The radius is read from the weapon record, not inferred from the art.
- **Mode 4:** The projectile has no fixed-radius splash in any of the four listed weapons (all have radius 0).
  Instead, it advances in 2-pixel coordinate increments (diagonal steps change both coordinates) and tests each
  step's point against eligible actors' axis-aligned collision rectangles (`FUN_0047afc0`, called from the mode-4
  update loop; movement vector at `0x66b05c`). A point hits when
  `actorX + left <= projectileX <= actorX + left + width` and likewise for Y. The rectangle comes from the target
  actor's `.chr` fields `0x360`–`0x36c`; for example, the original Soldier box is 24×45 px, Knight 33×65 px,
  KingDemon 47×82 px, and Phoenix 50×25 px. There is no projectile radius or fixed-width corridor: the effective
  hit width depends on the target's box and the projectile's direction. A 16-entry per-projectile list prevents
  repeat hits on the same actor. This is path collision, not impact splash.

The original area-damage routine uses the weapon radius as the distance limit and reduces damage with distance;
it is not a full-damage disk. Buildings are checked against their bounding rectangles. See `FUN_00457d4b` and
`FUN_0047afc0` in the OpenPW Ghidra export (`research/ghidra/out/missiletrace/`).

## Missile inventory

### Primitive

| Actor | Attack | Kind | Original mode | Original splash |
|---|---|---|---|---|
| `primitive-giant` | `ThrowBomb` | thrown bomb | 2: tracks target | 48 px |
| `primitive-soldier` | `SoldierArrow` | arrow | 2: tracks target | 0 (none) |
| `primitive-soldier-fire-arrow` | `FireArrow` | fire arrow item variant | 2: tracks target | 40 px |
| `primitive-soldier-venom-arrow` | `VenomArrow` | venom arrow item variant | 2: tracks target | 32 px |
| `primitive-knight` | `ShootSword` | thrown sword | 4: pierces along path | 0 (none) |
| `primitive-knight-spear-of-justice` | `SpearOfJustice` | spear item variant | 4: pierces along path | 0 (none) |
| `primitive-bowmachine` | `BowMachineArrow` | arrows | 0: fixed endpoint | 32 px |
| `primitive-squadron` | `SquadronShot` | aerial projectile | 2: tracks target | 0 (none) |
| `primitive-flattop` | `PhantomFire` | magic projectile | 0: fixed endpoint | 48 px |
| `primitive-stonetower` (building) | `Stone` | thrown stone | 2: tracks target | 0 (none) |

### Tyrano

| Actor | Attack | Kind | Original mode | Original splash |
|---|---|---|---|---|
| `tyrano-dilophos` | `PoisonSpit` | poison spit | 2: tracks target | 0 (none) |
| `tyrano-trices` | `CarrnosBoomerang` | boomerang | 2: tracks target | 0 (none) |
| `tyrano-eggthrower` | `EggThrow` | thrown egg | 0: fixed endpoint | 48 px |
| `tyrano-rhamphos` | `RhamphosArrow` | arrows | 2: tracks target | 0 (none) |
| `tyrano-pteras` | `PterasAxe` | thrown axe | 2: tracks target | 0 (none) |
| `tyrano-mutant` | `MutantArrow` | arrows | 2: tracks target | 64 px |
| `tyrano-ascomoid` (building) | `AscomoidShot` | projectile | 2: tracks target | 0 (none) |

### Demon

| Actor | Attack | Kind | Original mode | Original splash |
|---|---|---|---|---|
| `demon-skeleton` | `SkeletonArrow` | arrows | 2: tracks target | 0 (none) |
| `demon-kingdemon` | `KingDemonFire` | fire projectile | 4: pierces along path | 0 (none) |
| `demon-darkbat` | `NoiseWave` | wave projectile | 2: tracks target | 0 (none) |
| `demon-volcano` (building) | `VolcanoShot` | thrown projectile | 2: tracks target | 0 (none) |

### Elf

| Actor | Attack | Kind | Original mode | Original splash |
|---|---|---|---|---|
| `elf-wizard` | `WizardFirebolt` | firebolt | 2: tracks target | 0 (none) |
| `elf-redelf` | `RedBolt` | bolt | 2: tracks target | 0 (none) |
| `elf-whiteelf` | `WhiteBolt` | bolt | 2: tracks target | 0 (none) |
| `elf-dragon` | `DragonFire` | fire projectile | 0: fixed endpoint | 64 px |
| `elf-phoenix` | `PhoenixFire` | fire projectile | 4: pierces along path | 0 (none) |
| `elf-angelelf` | `AngelRing` | ring projectile | 2: tracks target | 0 (none) |
| `elf-bubble` (building) | `BubbleShot` | bubble | 2: tracks target | 0 (none) |

### Hostile wildlife

| Actor | Attack | Kind | Original mode | Original splash |
|---|---|---|---|---|
| `wild-dilophosaurus` | `DilophosaurusSpit` | poison spit | 2: tracks target | 0 (none) |

**Inventory total:** 29 actors with projectile attacks: 25 mobile actors (including item variants and wildlife) and
4 defensive buildings. Of these, 8 have nonzero original splash and 21 have no area splash. The original modes are:
21 target-tracking projectiles, 4 path-piercing projectiles, and 4 fixed-endpoint projectiles.

## Implementation status

Implemented for all inventory entries:

- `PwBullet` preserves OpenRA projectile rendering and uses original travel speeds while implementing
  original modes 0 (fixed endpoint), 2 (tracks the target actor), and 4 (pierces actors along its path).
- Mode-2 impacts resolve at the tracked actor's current position after the flight, so terrain collision cannot
  prematurely end a target-tracking shot.
- Mode-4 collision samples the path every 2 px against enabled OpenRA hit shapes. This intentionally approximates
  the original actor-specific `.chr` rectangles; a projectile hits each actor at most once, up to the original
  16-actor limit. It continues along its aimed direction to the weapon's full effective range, even if the aimed
  target is closer. Endpoint damage is handled by the path check, avoiding a second hit from OpenRA's endpoint
  damage warhead; non-damage impact effects still run.
- Impact splash remains separate from path collision and linearly falls from full damage to zero at each inventoried
  weapon's original splash radius.
- The source-record audit keyed records by their actual `index` and confirmed all 29 inventory entries have the
  documented mode and speed (`original px/tick x 32 WDist/px`). Item variants inherit their base projectile's
  mode and speed.
- The focused `combat-test` covers target tracking, path piercing through the aimed target to full weapon range,
  single-hit behavior, fixed-endpoint path avoidance, splash falloff, and the hostile-wildlife projectile rule.

## Work plan

1. Complete.
2. Complete. The original modes, splash behavior, target masks, and attack timing are preserved; mode-4 hit shapes
   are the approved approximation.
3. Complete for the implemented modes and splash behavior in `combat-test`.
4. Complete for this implementation: the inventory and concrete weapon rules were cross-checked against the
   extracted source records.
