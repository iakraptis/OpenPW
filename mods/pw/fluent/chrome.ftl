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
label-berries = { $amount }
label-ability-fall-out = Fall out ({ $cost } mana)
tooltip-ability-fall-out = Click, then click an enemy: wears its life down to 1.
label-hunt-off = Hunt (off)
label-hunt-on = Hunt (on)
tooltip-hunt = While on, idle units hunt the wild creatures around them, the dangerous ones first, and pick up their meat.
label-share-meat = Share meat
tooltip-share-meat = The selected units meet and split their meat evenly: { $total } among { $units }, { $each } each.
label-ability-fall-out-locked = Fall out: research Improve Fall out for Chief at the Sanctuary
label-merge = Combine into { $name }
tooltip-merge = { $parts } meet and become one { $name }.
label-split = Split
tooltip-split = Becomes { $parts } again.
label-disguise = Disguise
tooltip-disguise = Takes the form of a wild creature of its kind: enemies take it for one and leave it alone, but it cannot attack.
tooltip-disguise-locked = Research Disguise at the TyranoNest.
label-undisguise = Turn back
label-spell-locked = Needs level { $level }.
spell-stone-curse = Stone curse ({ $cost } KP)
spell-stone-curse-desc = Click, then click an enemy unit: it turns to stone for a while and can't move or attack.
spell-noxious-gas = Noxious gas ({ $cost } KP)
spell-noxious-gas-desc = Click, then click the ground: a poisonous cloud hurts every living thing in it, friends too.
spell-suicide = Suicide
spell-suicide-desc = The Warlock blows itself up, hurting the enemies around it.
spell-quake = Quake ({ $cost } KP)
spell-quake-desc = Click, then click the ground: the earth shakes, hurting enemy units and buildings there.
spell-rise-death = Rise death ({ $cost } KP)
spell-rise-death-desc = Three zombies rise around the Devil to fight for it.
spell-thunder-bolt = Thunder bolt ({ $cost } mana)
spell-thunder-bolt-desc = Click, then click an enemy unit: a streak of lightning strikes it.
spell-shield = Shield ({ $cost } mana)
spell-shield-desc = Click, then click a friendly unit: a shield soaks up the damage it takes, until it breaks.
spell-mass-temper = Mass temper ({ $cost } mana)
spell-mass-temper-desc = Click, then click the ground: the friendly units there get a shield for a while.
spell-teleport = Teleport ({ $cost } mana)
spell-teleport-desc = Click, then click any explored place: the Ranger is there at once.
spell-jump-portal = Jump portal ({ $cost } mana)
spell-jump-portal-desc = Click, then click the ground: a portal opens there for a while and throws away the enemies that walk into it.
spell-drop-stone = Drop stone ({ $cost } mana)
spell-drop-stone-desc = Click, then click the ground: a stone falls there, crushing the enemies under it.
spell-cure = Cure ({ $cost } mana a tick)
spell-cure-desc = Click, then click a friendly unit: it heals while the Unicorn's mana lasts.
spell-fake = Fake ({ $cost } mana)
spell-fake-desc = Click, then click any unit: a harmless copy of it appears, yours, for a while.
spell-fake-locked = Research Study Fake (Unicorn) at the ManaLibrary.
spell-meteor = Meteor
spell-meteor-desc = Click, then click the ground: the ManaSpread gives itself up and meteors rain down there.
spell-meteor-locked = Research Study Meteor (ManaSpread) at the ManaLibrary.
spell-resurrect = Resurrect ({ $cost } mana)
spell-resurrect-desc = Click, then click where units fell: the dead there rise again as yours, friend or foe.
tooltip-undisguise = Back to its own form.
label-population = { $used } / { $limit }
notification-population-limit = Population limit reached: build more Houses or Nests.

## The interface bar
tooltip-berries = Berries: gathered by workers from bushes, spent on buildings, units and research.
tooltip-population = Population in use / limit. Units take population; the main building, Houses or Nests and production buildings provide it.
tooltip-slot = { $name } ({ $cost } berries)
tooltip-build = Takes { $time } s. Click, then click where to build.
tooltip-build-needs = Needs { $needs }.
tooltip-not-enough-berries = Not enough berries. Takes { $time } s to build.
tooltip-train = Takes { $time } s. Click to queue one, Shift+click to cancel one.
button-bar-menu = Menu
button-bar-allies = Players and objectives
button-bar-msg = Messages
label-stat-hp = HP { $hp }/{ $max }
tooltip-stat-hp = Hit points. Living units heal by eating the meat they carry.
label-stat-kp = KP { $hp }/{ $max }
tooltip-stat-kp = Kill points: a Demon's life, spent on its spells and won back by killing.
label-stat-level = Level { $level } ({ $xp }/{ $next } XP)
tooltip-stat-level = Level and experience: kills give experience; each level raises attack and armour.
label-stat-attack = Attack { $attack }
tooltip-stat-attack = Damage per hit before the target's armour (with level, upgrade and morale bonuses).
label-stat-armor = Armour { $armor }
tooltip-stat-armor = Taken off every hit it receives (each hit does at least 1).
label-stat-meat = Meat { $meat }
label-stat-meat-amount = { $meat }
tooltip-stat-meat = Meat carried: eaten while hurt, 1 HP per meat.
label-stat-mana = Mana { $mana }/{ $max }
tooltip-stat-mana = Mana for abilities; it refills over time.
label-stat-construction = Building { $percent }%
tooltip-stat-construction = Under construction: a Buildman inside is building it.

## The main menu
button-main-menu-intro = Intro
tooltip-main-menu-intro-unavailable = The intro is not available yet.
