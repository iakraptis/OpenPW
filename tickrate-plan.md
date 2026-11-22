# Tick rate plan: two world ticks per original tick

Goal: run the game logic on short ticks, close to OpenRA's default of 40 ms, while every timing in the game (walking,
attacks, training, spells, regrowth, respawns, ...) stays the same in real time.

Decision (2026-11-22): **factor 2**. One original tick (71 ms at normal speed) becomes two world ticks of **36 ms**.
Normal speed then runs 1.4% slower than the original (72 ms per original tick instead of 71).

## Where we are now

- One world tick is one original tick: 71 ms at normal speed (OpenRA's default is 40 ms).
- The rules use the original values directly: build times, attack delays, the 320-tick body, speeds in pixels per
  tick. Nothing is converted.
- What depends on the tick:

| What | How many |
|---|---|
| Timing values in the rule files (build times, reload and attack delays, durations, speeds) | about 520 |
| Timing defaults in the C# code (scan intervals, regeneration, body time, spell timings) | 47, plus a few values written directly in the code |
| Unit animation sequences (written by the installer, 1 frame per tick) | 228 |
| Lua test maps that wait a number of ticks | 11 |

- Engine settings the mod never sets (turn speeds, auto-target scans, ...) run 1.775 times slower than OpenRA intends,
  because they were made for 40 ms ticks.

## Why factor 2 and not exactly 40 ms

| | Exactly 40 ms | 2 ticks per original tick (chosen) |
|---|---|---|
| Factor applied to every value | 71/40 = 1.775 | 2 |
| Normal speed tick | 40 ms | 36 ms |
| Rounding | Every value is rounded; errors spread over hundreds of numbers | Exact: durations double, rates halve |
| Animations | Frames switch every 2 then every 1 tick, unevenly; hit frames drift | One frame every 2 ticks; hits stay on the same frame |
| Per-tick amounts (2 HP a tick, 1 mana a tick) | Need fractions | Double the interval, keep the amount |
| Ticks per second | 25 | 28 |

A 40 ms tick with a factor of 2 would make everything 12.7% slower than the original.

Game speeds (ms per world tick):

| Speed | Now | New |
|---|---|---|
| Fastest | 45 | 23 |
| Faster | 55 | 28 |
| Fast | 62 | 31 |
| Normal | 71 | 36 |
| Slower | 100 | 50 |
| Slowest | 142 | 71 |
| Test (5x normal) | 14 | 7 |

## Steps

### 0. Measure first

- A timing test map records real-time durations (world ticks x tick length):
  - a Soldier walking 10 cells, a full attack cycle and its hit moment, a flyer's turn;
  - training a Soldier, constructing a House;
  - mana refill, a Cure cast's total heal and its rhythm;
  - a Meteor, Noxious gas;
  - a body's lifetime, a Velociraptor's respawn, berry regrowth, gathering and repair rates;
  - the AI's first building.
- Keep the numbers. Every later step must match them within about 2%.
- Dump the fully resolved rules (the mod's values and the engine defaults) to compare before and after.

### 1. One constant, one conversion script

- `PwTime.TicksPerOriginal = 2` in C#.
- The rules hold world ticks; the docs keep original ticks; rule comments give both.
- `tools/rescale_ticks.py`: from a list of which settings are durations, rates and per-tick amounts, it rewrites the
  YAML and prints every change for review.

### 2. Rules (YAML), by kind

- **Durations and intervals, x2:** `BuildDuration`, `ReloadDelay`, `FireDelay`, `BurstDelays`, `Duration`, `Delay`,
  `*Ticks`, `*Interval`, `AttackDelay`, the respawn `Delay`, the bot modules' intervals.
- **Rates per tick, /2:** `Mobile` and `Aircraft` `Speed`, projectile `Speed`, `TurnSpeed`,
  `BerryGatherer.FlightSpeed`, the upgrades' `Speed`. Speeds are in WDist (32 per pixel), so halving is exact or off by
  half a WDist.
- **Amounts per tick: keep the amount, double the interval**, so every blow, heal and total stays the same:
  - Cure: 2 HP every 2 ticks, for 30 ticks;
  - Noxious gas: a blow every 2 ticks;
  - meat: 1 HP every 2 ticks;
  - mana: 1 every 32 ticks.
- **Engine defaults the mod never sets:** find them in the rules dump. For each, either set the value that keeps
  today's timing, or decide to accept OpenRA's intended speed.

### 3. Animations

- The installer writes `Tick: 80` instead of 40 (one frame every 2 ticks): `AssetInstaller/Units.cs` and `Effects.cs`.
  Regenerate the tracked sequence files; players who already converted their game don't need to.
- Check the code that turns an animation's length into ticks (`sequence.Tick / 40` in `PwBody`, `PwBodyEffect` and
  `PwSpell`).

### 4. C# code

- Convert the 47 default timings, in place (x2 or /2) or read through `PwTime`.
- Find timing numbers written directly in the code (for example Cure's stagger, `(tick & 15)`) and route them through
  `PwTime`.
- Check code that runs every tick with an amount: spell steps, healing, the portal's throw.

### 5. Network settings

- Option 1: `NetFrameInterval: 2` (72 ms network frames, as today), OrderLatency 6 (about 0.43 s).
- Option 2: the engine's default `NetFrameInterval` of 3 (108 ms), OrderLatency 4 (about 0.43 s). The engine fork
  would no longer need its network frame changes (the `netframe` branch), only the ramp fix (`jmods`).
- Try option 2 first and judge how responsive it feels.

### 6. Tests and scripts

- Double the tick counts in the 11 Lua tests.
- The test speed goes from 14 ms to 7 ms, so the suite runs as fast as before.
- `DateTime.Seconds` already converts using the tick length.

### 7. Docs

- `docs/game/rules.md` and the comment in `mod.yaml`: one original tick is 2 world ticks.
- The tables in `docs/game` stay in original ticks.

### 8. Checks

- The timing map again: every number within about 2% of step 0.
- The Lua tests and the AI stability games (not the battle-* balance maps).
- In the game: attack hits land on their frame, animations play evenly.
- A multiplayer game on one PC, judged for smoothness.
- Performance: twice the ticks per second costs pathfinding and the AI about twice the CPU per second. Check a big
  8-player map with bots.
- Old replays and saves won't play on the new version: say so in the release notes.

## Risks

- **A missed value** makes something run twice as fast or twice as slow. The timing map and the rules dump are there
  to catch these.
- **Engine defaults** may change the feel (snappier turns, for example). Each needs a decision.
- **Performance:** twice the ticks per second costs more CPU.

## Status (2026-11-22, branch tickrate)

Done:

- Step 0: `timing-test` (14 timings in original ticks) and `utility --dump-numbers` (15,170 rule numbers, defaults
  included).
- Steps 1-4: `PwTime.TicksPerOriginal = 2`; `tools/rescale_ticks.py` rescaled 700 values in the rules, weapons, map
  rules and sequences, and its `--check` finds no number that changed differently from its table. Engine defaults the
  rules relied on are set explicitly, doubled. The C# defaults read `PwTime.Ticks`/`PwTime.PerTick`; fixed tick
  constants are scaled; the speed upgrade stays in original pixels per tick; Cure's stagger counts original ticks; the
  installer writes the doubled frame times.
- Step 5: option 2, the engine's `NetFrameInterval` of 3 with OrderLatency for about 0.4 s at every speed.
- Step 6: instead of doubling the tests' numbers, `testlib.lua` takes original ticks (`Later`, `WaitFor`,
  `FinishWhenDone`, `Now`) through the `PwTime` Lua global; the battle template and the campaign script count original
  ticks (the campaign's triggers run once an original tick).
- Step 7: rules.md, the `mod.yaml` and `defaults.yaml` comments. Rule comments quote the original's ticks.
- Step 8 so far:
  - `timing-test`: every timing within 2% of before (repair 2% faster, the rest within 1%).
  - All 13 test maps: 12 pass; `bot-medium-test` fails the same check as before the change ("MedElf's attack has a
    Pixie with it").
  - bot-test (4 bots, 39,750 original ticks): the economy keeps its pace (berries earned by 5k, 10k and 20k ticks
    as in the last runs); it ran 18% longer at the test speed, so two world ticks cost about 1.2 times one.
    At normal speed that is about 20 ms of work in each 72 ms original tick.

Left:

- Watch a game: animations and attack hits on their frames.
- A multiplayer game on one PC, judged for smoothness.
- The engine fork's network frame change (`netframe`) is no longer used (NetFrameInterval is the engine's 3): the mod
  could go back to `jmods` once `NetFrameInterval` is taken out of `mod.yaml`.
- Release notes: replays and saves from earlier versions won't play.
