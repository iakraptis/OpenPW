#!/bin/sh
# Converts an original Primitive Wars installation (tilesets, maps, and later art and sounds) into the mod's content.
#   ./install-assets.sh [GAME_FOLDER] [--only STAGE,...] [--write-yaml] [--list]
# GAME_FOLDER defaults to a "Primitive Wars" folder next to this repository. Run `make` first so the engine is built.
set -e

if [ $# -gt 0 ] && [ "${1#-}" = "$1" ]; then
	GAME=$(cd "$1" && pwd)
	shift
	cd "$(dirname "$0")"
	exec ./utility.sh --install-assets "${GAME}" "$@"
fi

cd "$(dirname "$0")"
exec ./utility.sh --install-assets "$@"
