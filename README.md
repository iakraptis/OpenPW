# OpenPW

A restoration of **Primitive Wars** (Legend of the Land, Wizard Soft, 2001) on the [OpenRA](https://www.openra.net)
engine, built with the OpenRA Mod SDK (engine `release-20250330`).

This repository holds only code, rules and documentation. No original game files are included: the asset installer
converts the art, sounds, terrain and maps from your own copy of the game.

## Status

Skirmish is playable with all four races, against other players or the AI:

- **Primitive**, **Tyrano**, **Demon** and **Elf**, each with its units, buildings, research and the race's own
  mechanics: the Primitive Blacksmith's items and the Chief, Tyrano merging and disguise, Demon kill points, and
  the Elves' and Demons' spells;
- the 17 original multiplayer maps, with their terrain, berry bushes and wild dinosaurs;
- levels, invisibility and detection as in the original, plus hunting and meat as new mechanics;
- each race's original interface bar and the original main menu;
- the original music, by race;
- Normal and Hard AI.

Not yet: the campaign, and an installer screen inside the game (the conversion runs from the command line).
The decoded file formats are documented in [docs/formats/](docs/formats/), the game rules recovered from the
original in [docs/game/](docs/game/), and the roadmap in [plan.md](plan.md).

## Building and running

Requires the .NET SDK (6.0 or newer).

```
make.cmd all          # fetch the engine into ./engine and build (Linux/macOS: make)
install-assets.cmd [PATH TO PRIMITIVE WARS]
                      # convert your copy of the game into the mod's content folder (./install-assets.sh)
launch-game.cmd       # start the mod (Linux/macOS: ./launch-game.sh)
make.cmd test         # YAML and lint checks
make.cmd check        # C# style checks
```

## Test builds

```
powershell -ExecutionPolicy Bypass -File packaging\windows\update-portable.ps1 -Pack
```

This builds a self-contained portable game, ..\OpenPW-portable next to the checkout, and zips it to
..\OpenPW-portable-test-<commit>.zip. The game runs on any Windows PC as it is: it has its own .NET runtime, and it
keeps its settings and logs in its Support folder. The zip includes the content this PC converted with
install-assets, so it is for private testing only and must never be published. The script refuses a folder inside
the repository.

`packaging\windows\package-windows.ps1` builds the release package without any game content: a portable zip and,
with NSIS installed, a setup.exe in dist\.

## License

GPLv3, see [COPYING](COPYING). Primitive Wars and its assets belong to their respective owners.
