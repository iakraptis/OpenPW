# Text (`JW2_14.TRC`, `JW2_16.TRC`, `JW2_17.TRC`)

Plain text, CRLF line endings, Korean in CP949.

## Strings: `JW2_17`

`Mm_eng.txt`, `Mm_kor.txt`, `Mm_frc.txt`, `Mm_ger.txt`, `Mm_ity.txt` and `Mm_spn.txt` hold one message per line:
the network, lobby, patcher and serial-number messages, with `printf` placeholders (`Connecting to %s`). The six
masked entries are copies of these files plus a Korean-commented master ("다국어 번역 데이터", multilingual
translation data); they hide nothing else. Unit, building and spell names come from the `.chr`, `.wpn` and
`.maz` records instead.

## AI scripts: `JW2_14` and `JW2_16`

`Ai_00.txt` is the skirmish AI. `JW2_16` holds 384 campaign scripts named `<race><scenario>_<player>.txt` (48
scenarios × 8 players; `UNUSED_SCRIPT` for empty slots). Script language:

- `;` starts a comment; `:LABEL` marks a block; `END` ends it.
- `GO_IF_UNDER_RELCNT <label> <n>` and `GO_IF_OVER_ATTCNT <label> <n>` jump on counters.
- Settings, each with numeric arguments (percentages, counts, intervals):
  - economy: `SET_NO_BUILDER_BERY` (build workers while berries are below a percentage),
    `SET_NEW_BASE_BERY` (expand when berries run out), `SET_NO_SUPPLY_BY_UNIT` (houses per unit, %);
  - production: `SET_UNIT_MAKE`, `PRIORITY_UNITMAKE`, `SET_NO_BASE_UNIT`, `SET_NO_MIDDLE_UNIT`,
    `SET_NO_HIGH_UNIT`, `ADD_UNIT_BY_ENEMY`, `SET_UNIT_BY_ENEMY`;
  - attacks: `SET_ATTACK_COUNTER`, `SET_ATTACK_INTERVAL`, `SET_ATTACK_MOVE`, `SET_ATTACK_UNIT`,
    `SET_ATTACK_UNIT_PS`, `SET_ATTACK_WINABLE`, `ADD_ATTACK_WINABLE`, `SET_ATTACK_BY_ENEMY`,
    `ADD_ATTACK_BY_ENEMY`, `SET_ATTACK_BACK`;
  - defence and other: `SET_DEFENSE_TOWER`, `SET_DEFENSE_POS`, `SET_UPGRADE`, `SET_UPGRADE_BERY`,
    `SET_UPGRADE_EXP`, `SET_HUNT`, `ON_EXCEPT_TRIGGER_TEAM`.

### How `Pw.exe` runs them (D)

`FUN_0041bac0` parses a script line against the command table at `0x5477f8` (102 entries of name and argument
kind; `SET_x`, `ADD_x <n> <max>` and `SUB_x <n> <min>` for most settings) into commands that `FUN_00419840` runs,
one `case` per command number. Each setting is a per-player variable; `FUN_00419410` gives the defaults.
`ADD_`/`SUB_` change a setting once per attack launched (they check the attack count changed since).

| Command | Variable | Default | What reads it |
|---|---|---|---|
| `SET_UNIT_MAKE <type> <n>` | wanted count of a type (`0xeb9970`) | 0 | the production manager (`FUN_0041e670`) trains up to it |
| `SET_NO_BASE_UNIT`, `_MIDDLE_`, `_HIGH_ <n>` | the wanted count of every unit of that tier of its race (tables `0x5476f4`, `0x547734`, `0x547794`) | 0 | the same |
| `SET_NO_BUILDER_BERY <p>` | workers wanted: p % of a figure from its bushes, at most 15 | 0 | the same |
| `SET_NO_SUPPLY_BY_UNIT <p>` | houses: p % of the population used, capped at 160 | 125 | the same |
| `SET_NEW_BASE_BERY <n>` | when to build a new main building | 10 | |
| `SET_ATTACK_COUNTER <n>` | attack once n army units are home (`0xebc510`) | 0 = off | `FUN_0041c470` |
| `SET_ATTACK_INTERVAL <n>` | also attack every n × 22 ticks (`0xebc4f0`) | 0 = off | `FUN_0041c470` |
| `SET_ATTACK_WINABLE <p>` | also attack when its army's strength is p % of the enemy's whole army (`0xebc530`) | 120 | `FUN_0041c470` |
| `SET_ATTACK_BACK <p>` | also attack when its army is p % of the enemy units near the enemy's base (`0xebc570`) | 120 | `FUN_0041c470` |
| `SET_ATTACK_BY_ENEMY <p>`, `ADD_UNIT_BY_ENEMY` | train against the enemy's units (counters table `0x547b28`) | 0 | `FUN_0041c470`, `FUN_0041e670` |
| `SET_ATTACK_UNIT <n>`, `SET_ATTACK_UNIT_PS <p>` | an attack takes the larger of n units and p % of the army home (`0xebc590`, `0xebc5b0`) | all, 100 % | `FUN_00421b00` |
| `SET_ATTACK_MOVE <p>` | the attack first gathers p % of the way along the path to its target (`0xec2a10`) | 70 | `FUN_0041cde0` |
| `SET_DEFENSE_POS <p>` | where defenders stand: p % of the way towards the enemy (`0xec2bb0`) | 20 | `FUN_0041df80` |
| `SET_DEFENSE_TOWER <n>` | towers it builds (`0xec2e50`) | 20 | |
| `SET_UPGRADE <p>`, `_BERY`, `_EXP` | research (`0xec2eb0`, …) | 20 | |
| `SET_HUNT <p>` | new units go hunting while the hunters are below p % of the army (`0xec2a70`) | 20 | `FUN_00421f30` |
| `ON_EXCEPT_TRIGGER_TEAM` | the units in the mission script's teams stay out of its squads and attacks (`0xebc5d0`) | off | `FUN_0041c7e0`, `FUN_00421f30` |
| `GO_IF_OVER_ATTCNT <label> <n>` | jump once n attacks have been launched (`0xebc470`) | | |
| `GO_IF_UNDER_RELCNT <label> <n>` | jump while the game tick is below n | | |

An attack (`FUN_0041c470`, then `FUN_00421b00`): when the interval has passed, or the army home reaches the counter
(or the population is above 144), or its strength (HP + attack + armour of each unit, `FUN_0041c7e0`) beats the
enemy's by the winable or attack-back share, the AI forms a squad of the units waiting home and picks a target: an
enemy building, else a unit (`FUN_0041cde0`). The squad walks to the gathering point (`SET_ATTACK_MOVE`), waits
there for its members (at most 8 checks), then goes for the target, picks the next when it falls and comes home
when too few are left (`FUN_00420b10`, squad states 10-20).

The AI turn (`FUN_004192a0`): every player flagged as a computer (`P_PLAYE` 0xf4 + 4 × player) runs once every
128 original ticks, in turn (all of them on the first tick): the script again from the top (`FUN_004197a0`, so the
settings are set again every turn and the branches are re-tested), its bases (main buildings, `FUN_0041e230`), idle
workers to the berries (`FUN_0041e0b0`), research (`FUN_00422540`; while a research is due, nothing is trained that
turn), training and building (`FUN_0041e670`), the attack decision (`FUN_0041c470`), defence of the alarm points
(`FUN_0041d490`: units at home go to an enemy near one, twice its strength, allies' too; an alarm point is where
one of its units was hit, up to 4, one within 8 cells of another taking its place: `FUN_0041dde0`,
`FUN_0041de40`) and the squads
(`FUN_00420b10`). A unit with no squad gets one (`FUN_00421f30`): the script's teams (with `ON_EXCEPT_TRIGGER_TEAM`)
1, workers 2, home 3 or 4, hunters 5. An attack takes units of squads 3-5. Squads give orders only to idle units: a
gathering squad goes on when its idle members are within (members / 2 + 8) cells of the point; an attacking one
sends the idle ones further than that from the target there (`FUN_004211e0`). `UNUSE_SCRIPT` stops the player's
AI altogether.

The placed army joins the home squads and attacks like any unit: in the original P01 the Demons' first attack
(at 3:30) is their 3 placed Skeletons and 2 Zombies with 4 trained Skeletons, then 3 and 5 Skeletons, then
Kelpas. (Their "camps" of 50 more Skeletons are units deleted in the editor, left in `OBC`: [map.md](map.md).)

`OBC` is the unit array itself (`0x7fef08`, 0x1d0 bytes a unit), saved by the editor: +0x08 is the selection (0x80)
and control group (low 4 bits), not an AI setting.

The Primitive and Demon scripts name the player with two digits (`P01_02.txt`), the Elf and Tyrano ones with one
(`E01_2.txt`).

## Not text

`JW2_16` has no campaign dialogue; briefings are out of scope with the campaign. `Version.dat` and `Mousec.dat`
in `JW2_01` look scrambled.
