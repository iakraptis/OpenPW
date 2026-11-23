"""OpenPW test suite runner: build, YAML check, then the test maps through the engine's shared runner.

Usage (from the repository root):
  python run_tests.py                 # build + check-yaml + every *-test map, headless and in parallel
  python run_tests.py --only start-test,combat-test
  python run_tests.py --no-build --no-check
  python run_tests.py --windowed --only combat-test   # watch one test in a window, at normal speed
  python run_tests.py --battles      # the AI battles (bot-test and the battle-* maps), then tools/battle_stats.py

The maps run through engine/tools/run_tests.py: each as its own game with no window (Game.Platform=Null) and no
waiting for the game speed (Launch.Uncapped=true), several at once. testlib.lua reports the result with
Test.Pass/Test.Fail and the game exits with its code; each map's output, logs and replay go to test-results/<map>/,
the summary to test-results/summary.md. Per-map settings (tick limits, timeouts, manual maps) are in tests.json. Other
options (--jobs, --windowed, ...) are passed to the engine runner. The maps need the game assets converted from an
original copy of Primitive Wars (install-assets); the installer also builds bot-test, the battles and pathing-test in
the player content folder.
"""
import argparse
import os
import re
import subprocess
import sys
from pathlib import Path

SDK = Path(__file__).resolve().parent
ENGINE = SDK / 'engine'
MAPS = SDK / 'mods' / 'pw' / 'maps'
# Maps the asset installer builds on converted original maps, in the player content folder.
CONTENT_MAPS = Path(os.environ.get('APPDATA', '')) / 'OpenRA' / 'Content' / 'pw' / 'maps'


def build():
    print('Building OpenRA.Mods.Pw ...', flush=True)
    r = subprocess.run(['dotnet', 'build', str(SDK / 'OpenRA.Mods.Pw'), '-c', 'Debug', '--nologo', '-v', 'q'],
                       capture_output=True, text=True)
    errors = sorted({line.strip() for line in r.stdout.splitlines() if ' error ' in line})
    if r.returncode != 0 or errors:
        print('\n'.join(errors) or r.stdout[-2000:])
        return False
    return True


def check_yaml():
    print('Checking YAML ...', flush=True)
    r = subprocess.run(['cmd', '/c', str(SDK / 'make.cmd'), 'test'], cwd=SDK, capture_output=True, text=True,
                       stdin=subprocess.DEVNULL)
    errors = [line for line in r.stdout.splitlines() if re.search(r'error|exception', line, re.I)]
    for line in errors:
        print('  ' + line)
    return not errors


def battle_maps():
    folders = [f for f in (MAPS, CONTENT_MAPS) if f.is_dir()]
    return sorted({p.name for f in folders for p in f.iterdir() if p.name.startswith('battle-') or p.name == 'bot-test'})


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--no-build', action='store_true')
    parser.add_argument('--no-check', action='store_true')
    parser.add_argument('--battles', action='store_true', help='run the AI battles and collect their stats')
    args, engine_args = parser.parse_known_args()

    if not args.no_build and not build():
        sys.exit('Build failed')
    yaml_ok = True if args.no_check else check_yaml()

    battles = battle_maps() if args.battles else []
    if args.battles:
        if not battles:
            sys.exit(f'No battle maps in {CONTENT_MAPS}: run install-assets first')
        engine_args = ['--only', ','.join(battles), *engine_args]
    ok = subprocess.run([sys.executable, str(ENGINE / 'tools' / 'run_tests.py'), '--mod-dir', str(SDK),
                         *engine_args]).returncode == 0
    if battles:
        sys.path.insert(0, str(SDK / 'tools'))
        import battle_stats
        print()
        battle_stats.main(battles)
    print(f'YAML check {"PASS" if yaml_ok else "FAIL"}')
    sys.exit(0 if ok and yaml_ok else 1)


if __name__ == '__main__':
    main()
