## Shroud
checkbox-fog-of-war =
    .label = Fog of War
    .description = Line of sight is required to view enemy forces

checkbox-explored-map =
    .label = Explored Map
    .description = Initial map shroud is revealed

## DeveloperMode
checkbox-debug-menu =
    .label = Debug Menu
    .description = Enables cheats and developer commands

## MapStartingLocations
checkbox-separate-team-spawns =
    .label = Separate Team Spawns
    .description = Players without assigned spawn points will start as far as possible from enemy players

## SpawnStartingUnits
dropdown-starting-units =
    .label = Starting Units
    .description = The units that players start the game with

## World
options-starting-units =
    .unlabeled = Unlabeled

## ingame-observer.yaml
options-game-speed-test = Test (5x, test maps)
label-mute-indicator = Audio Muted
button-observer-widgets-options = Options (Esc)
button-replay-player-pause-tooltip = Pause
button-replay-player-play-tooltip = Play

button-replay-player-slow =
    .tooltip = Slow speed
    .label = 50%

button-replay-player-regular =
    .tooltip = Regular speed
    .label = 100%

button-replay-player-fast =
    .tooltip = Fast speed
    .label = 200%

button-replay-player-maximum =
    .tooltip = Maximum speed
    .label = MAX

## Command panel
label-berries = Berries: { $amount }
label-ability-fall-out = Fall out ({ $cost } mana)
tooltip-ability-fall-out = Click, then click an enemy: wears its life down to 1.
label-ability-fall-out-locked = Fall out: research Improve Fall out for Chief at the Sanctuary
label-population = Population: { $used } / { $limit }
notification-population-limit = Population limit reached: build more Houses.

## The interface bar
tooltip-berries = Berries: gathered by Buildmen from bushes, spent on buildings, units and research.
tooltip-population = Population in use / limit. Units take population; the Sanctuary, Houses and production buildings provide it.
tooltip-slot = { $name } ({ $cost } berries)
tooltip-build = Takes { $time } s. Click, then click where to build.
tooltip-build-needs = Needs { $needs }.
tooltip-train = Takes { $time } s. Click to queue one, Shift+click to cancel one.
button-bar-menu = Menu
button-bar-allies = Players and objectives
button-bar-msg = Messages
label-stat-hp = HP { $hp }/{ $max }
tooltip-stat-hp = Hit points. Living units heal by eating the meat they carry.
label-stat-level = Level { $level } ({ $xp }/{ $next } XP)
tooltip-stat-level = Level and experience: kills give experience; each level raises attack and armour.
label-stat-attack = Attack { $attack }
tooltip-stat-attack = Damage per hit before the target's armour (with level, upgrade and morale bonuses).
label-stat-armor = Armour { $armor }
tooltip-stat-armor = Taken off every hit it receives (each hit does at least 1).
label-stat-meat = Meat { $meat }
tooltip-stat-meat = Meat carried: eaten while hurt, 1 HP per meat.
label-stat-mana = Mana { $mana }/{ $max }
tooltip-stat-mana = Mana for abilities; it refills over time.
label-stat-construction = Building { $percent }%
tooltip-stat-construction = Under construction: a Buildman inside is building it.
