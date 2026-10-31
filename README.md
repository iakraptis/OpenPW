# OpenPW

A restoration of **Primitive Wars** (Legend of the Land, Wizard Soft, 2001) on the [OpenRA](https://www.openra.net)
engine, built with the OpenRA Mod SDK (engine `release-20250330`).

This repository holds only code, rules and documentation. No original game files are included: the asset installer
converts them from your own copy of the game (the terrain and maps so far; the art and sounds are next).

## Status

Early work. The original file formats are decoded and documented in [docs/formats/](docs/formats/):

- `.trc` archives;
- `.chr` units and buildings, with their stats, animations and sounds;
- `.trt` terrain;
- `.jw2` maps;
- `.doo` doodads.

The mod itself is still an empty skeleton. See [plan.md](plan.md) for the roadmap.

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

## License

GPLv3, see [COPYING](COPYING). Primitive Wars and its assets belong to their respective owners.
