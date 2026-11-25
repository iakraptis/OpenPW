# Campaign (`JW2_06.TRC`, `JW2_16.TRC`)

48 scenarios, 12 per race, named by race letter and number: `P01`…`P12` (Primitive), `E` (Elf), `T` (Tyrano), `D`
(Demon). `OpenRA.Mods.Pw/AssetInstaller/Campaign.cs` converts them; `mods/pw/scripts/campaign.lua` runs their scripts.
Evidence: D = decompiled from `Pw.exe` (Ghidra), C = checked against the data.

## `JW2_06.TRC`: one scenario after another

Each scenario's entries, in order (C):

| Entry | Content |
|---|---|
| `<prefix>_S.MP3`, `<prefix>_E.MP3` | Narration of the intro and of the ending story (plain MP3) |
| `JW2` | 94,320 bytes (94,316 in T01): hero names in Korean and English; otherwise unknown |
| `P_MAP`, `P_SCENA`, `P_PLAYE`, `P_UB`, `P_SCRIP`, `OBB`, `OBC`, `OBI`, `RST`, `MAP1`…`MAP3`, `BGI`, `FOGBGI`, `BRUSH`, `HILL_BRUSH`, `FOGMAP3`, `VSBGI` | The map sections, as in a skirmish `.jw2` ([map.md](map.md)); in the campaign `P_SCENA`, `P_PLAYE`, `P_UB`, `P_SCRIP` and `OBC` carry the scenario |
| `AI` | 42,944 bytes, zero in every scenario (missing in T01, which has `RES19` instead) |
| `<prefix>_0.JPG` … `_9.JPG` | Briefing pictures, 800 × 600: 0 a map of the island, 1 a scene; 2-9 are 1-byte placeholders |

## Players

- `P_PLAYE` +0: the local (human) player, 0 in every scenario. +0x144: starting berries per player (P01: 1000 for
  the human, 500 for the others). +0x234: an alliance bit mask per player.
- `OBC`: the placed units, bases and armies included (P01: 181; all 48: about 15,000), with their owner (0-7, 8 wild).
  The slots are **0x1d0** bytes (D: the code indexes `0x7fef08 + 4 · 0x74 · i`); teams refer to units by slot.
- `P_UB`: unit availability, a byte per player and type (170 types). P01 lets the human build only the Buildman,
  Powerman, House and Powerhouse.

## Text: `P_SCENA`

CP949, English in every scenario (C).

| Offset | Content |
|---|---|
| 0x0008 | Header: "The Primitives Stage 1", `Title : …`, `Objective : …` (continued on the next lines) |
| 0x0808 | Intro story (hand-wrapped lines, blank lines between paragraphs, `#…` and `@` marker lines) |
| 0x1808 | Ending story, the same way |

## The scenario script: `P_SCRIP`

Loaded to `0x8ec420`; the code addresses its parts absolutely (D: `FUN_00404490` runs the triggers every tick,
`FUN_00404650` tests a condition, `FUN_00405170` runs an action).

| Address | Offset | Content |
|---|---|---|
| `0x8ec424` | 0x4 | Triggers 0-31, 0x1c4a bytes each |
| `0x924d68` | 0x38948 | 32 teams, 0x9c bytes: +0 name (CP949), +0x20 the "every N ticks" timer, +0x28 member count, +0x2c up to 20 unit slots, +0x7c in use |
| `0x928a58` | 0x3c638 | 100 locations, 0x50 bytes: +0 name, +0x24 rectangle (x1, y1, x2, y2) in pixels |
| `0x92a99c` | 0x3e57c | Triggers 32-255. T01's script (255,356 bytes) ends before them |

A trigger: +0 done, +1 keep running (set while a transmission shows), +2 the condition (type, then arguments at +8,
+0x11c, +0x230, +0x344), +0x8a6 the action (type, arguments at +8, +0x25c, +0x4b0, +0x704, +0x958, text at +0x58),
+0x1b4a name, +0x1bca the tick it was done, +0x1bce in use. Every tick, in order: a trigger in use, not done, whose
condition holds runs its action and is done (with the tick), unless the action keeps it running.

### Conditions (`FUN_00404650`)

Arguments a1…a4 in order. Used by the campaign: 1, 14, 16, 17, 18, 19, 20, 21, 0.

| # | Holds when | Used |
|---|---|---:|
| 0 | always | 29 |
| 1 | the game tick ≥ a1 | 37 |
| 2-12 | resource, score and unit-count comparisons between players | 0 |
| 13 | a unit of player a3 has level ≥ a4 | 0 |
| 14 | a living unit of team a1 is in location a2 | 30 |
| 15 | a1's team timer is a2 ticks old (and restarts) | 0 |
| 16 | at least a1 units of team a2 are in location a3 | 2 |
| 17 | team a1 is all dead | 65 |
| 18 | team a2 has at most a1 living units | 1 |
| 19 | the game was won: the enemies are beaten, and the win waits for this trigger's chain | 32 |
| 20 | trigger a1 is done, and was done at least a2 ticks ago | 496 |
| 21 | the counter (action 26) equals a1 | 4 |

### Actions (`FUN_00405170`)

| # | Does | Used |
|---|---|---:|
| 0 | camera to the centre of location a1 | 48 |
| 1 | **transmission**: shows the text ("Speaker - message", names in brackets) for length / 2 + 80 ticks; the trigger is done when it has shown | 381 |
| 2 | puts a ground object (the `OBI` pool, `FUN_00462b10`) of `Item.itm` type a2 − 1000 at location a3's centre: the story items, 18 the Sword of Jurrasic (P11, D12), 22 the Sorcerous of the Neuully (E05) | 3 |
| 3 | victory; if a trigger waits for the win (condition 19), the first victory only lets it run | 65 |
| 4, 5 | defeat, draw | 32 |
| 6, 7, 9 | give the units in location a4 to player a5; destroy them; move them to location a5 | 0 |
| 10, 16, 29 | order team a2 to location a3; move team a1 to location a2; order the units in location a2 to a3 | 29 |
| 11, 13 | copy the mission title strings; set the objectives text | 0 |
| 14, 15 | set a player's score, resources | 0 |
| 17, 18 | trigger a1 not done (runs again), done (never runs) | 1 |
| 19 | change the units of type a1 in team a3 owned by a4 into type a2 owned by a5, with the new type's full stats (a1 = a2: only the owner changes; E02 turns Elves into Demons, P04 and P09 hand units over) | 9 |
| 20, 25 | camera to team a1 (follow), centre of team a1 | 54 |
| 21 | kill team a1 | 0 |
| 22, 23 | set, clear unit flags of team a1 (+0xa0 bits 0 and 7, +0x58 bit 5) around scripted scenes; meaning unknown | 62 |
| 24 | move team a1 to location a2 | 3 |
| 26 | counter + 1 | 9 |
| 27, 28 | clear, set a global flag (`0x92a950`) | 0 |

The stale text in the other actions is leftover editor text, not shown.

## The computer players: `JW2_16.TRC`

`<prefix>_<player>.txt`, 8 per scenario, 115 of the 384 `UNUSED_SCRIPT`: the AI script language
([text.md](text.md)), mostly settings. P01's player 2 (the Demons): `SET_UNIT_MAKE 49 10` and `54 3` (Skeletons,
Kelpas), `SET_ATTACK_COUNTER 10`, defence and upgrade settings. The mod's campaign AI (`PwCampaignBotModule`) plays
`SET_UNIT_MAKE` and `SET_ATTACK_COUNTER`; the branches (`GO_IF_…`) and the other settings are not followed yet.
