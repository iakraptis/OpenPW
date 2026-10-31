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

The exact meaning of each argument is for Phase 7 (AI), from the interpreter in `Pw.exe`.

## Not text

`JW2_16` has no campaign dialogue; briefings are out of scope with the campaign. `Version.dat` and `Mousec.dat`
in `JW2_01` look scrambled.
