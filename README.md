# OpenPW

A restoration of **Primitive Wars** (Arxel Tribe, Wizard Soft, 2001) on the [OpenRA](https://www.openra.net)
engine, built with the OpenRA Mod SDK. The engine is downloaded automatically by the first build: a commit of
upstream OpenRA `bleed`, pinned in `mod.config` (to become a tag on our fork
[iakraptis/OpenRA](https://github.com/iakraptis/OpenRA) with our engine fixes).

This repository holds only code, rules and documentation. No original game files are included: the asset installer
converts the art, sounds, terrain and maps from your own copy of the game.

## Original Game Description

Primitive Wars (also known as Jurassic War II) is a 2001 real-time strategy (RTS). Set on the fictional Primitive Island, it blends prehistoric dinosaur combat with high-fantasy elements. Playable Factions: Players choose from four distinct races. Primitives (Humans), Elves, Tyranos (mutant dinosaurs), and Demons.

## Status

Skirmish is playable with all four races, against other players or the AI:

- **Primitive**, **Tyrano**, **Demon** and **Elf**, each with its units, buildings, research and the race's own
  mechanics: the Primitive Blacksmith's items and the Chief, Tyrano merging and disguise, Demon kill points, and
  the Elves' and Demons' spells;
- the 17 original multiplayer maps, with their terrain, berry bushes and wild dinosaurs, plus (6) Dino Hunting
  Fixed, a copy of Dino Hunting without the original's walkable cliffs and stray water;
- levels, invisibility and detection as in the original, plus meat and hunting with a smarter algorithm;
- each race's original interface bar, with a lettered Menu button in the race's style, and the original main menu;
- the original music, by race;
- Modern AI with no resource cheating.

The campaign has begun: the first Primitive mission, Battle at Sellbaros, plays from the Missions menu with its
briefing, narration, dialogue and script. Not yet: the other 47 missions.
The decoded file formats are documented in [docs/formats/](docs/formats/), the game rules recovered from the
original in [docs/game/](docs/game/), and the roadmap in [plan.md](plan.md).

## Installing

Download the latest release from the [Releases](https://github.com/iakraptis/OpenPW/releases) page: the `.exe`
installer, or the portable `.zip` (unzip it anywhere; it keeps its settings and converted files in its own `Support`
folder). On first launch OpenPW asks for your copy of Primitive Wars (the folder holding `Pw.exe`; it looks in the
usual places first) and converts its art, sounds, music and maps, which takes a minute or two. The original game
files are not included.

## Releasing

Push a tag such as `v0.1` (`git tag v0.1 && git push origin v0.1`): the `Windows release` workflow
(`.github/workflows/release-windows.yml`) builds the installer and the portable zip with
`packaging/windows/package-windows.ps1` and publishes them as a GitHub Release. Run the workflow by hand (Actions >
Windows release > Run workflow) to only build them as a downloadable artifact. Locally:
`powershell -ExecutionPolicy Bypass -File packaging\windows\package-windows.ps1 -Tag v0.1 -OutputDir dist` (NSIS
needed for the installer; `-NoInstaller` for the zip alone).

## Building and running

Requires the .NET 10 SDK.

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
keeps its settings and logs in game\Support. The top of the folder holds only OpenPW.exe, a small launcher for
game\OpenPW.exe, and a readme for testers. The zip includes the content this PC converted with
install-assets, so it is for private testing only and must never be published. The script refuses a folder inside
the repository.

`packaging\windows\package-windows.ps1` builds the release package without any game content: a portable zip and,
with NSIS installed, a setup.exe in dist\.

## License

GPLv3, see [COPYING](COPYING). Primitive Wars and its assets belong to their respective owners.

The fonts in `OpenRA.Mods.Pw/AssetInstaller/Fonts/` (Caesar Dressing, Metal Mania, Uncial Antiqua, Pirata One) are
under the SIL Open Font License, whose text is beside each font.
