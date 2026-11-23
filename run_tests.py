"""OpenPW test suite runner: build, YAML check, then the test maps through the engine's shared runner.

Usage (from the repository root):
  python run_tests.py                 # build + check-yaml + every *-test map, headless and in parallel
  python run_tests.py --only start-test,combat-test
  python run_tests.py --no-build --no-check
  python run_tests.py --windowed --only combat-test   # watch one test in a window, at normal speed
  python run_tests.py --battles      # the AI battles (bot-test and battle-* maps, about 90 minutes), then tools/battle_stats.py

The test maps run through engine/tools/run_tests.py: each as its own game with no window (Game.Platform=Null) and
no waiting for the game speed (Launch.Uncapped=true), several at once. testlib.lua reports the result with
Test.Pass/Test.Fail and the game exits with its code; logs, replay and summary.md go to test-results/<map>/ and
test-results/. Per-map settings (tick limits, timeouts, manual maps) are in tests.json. Other options (--jobs, ...)
are passed to the engine runner. The maps need the game assets converted from an original copy of Primitive Wars
(install-assets); the installer also builds bot-test and pathing-test in the player content folder.

--battles still runs the old way, windowed and one at a time (the shared runner doesn't run battles yet): each map is
launched with Launch.Map=<map> and passes when its "... TEST PASS" line appears in %APPDATA%/OpenRA/Logs/lua.log or
stdout. Its logs go to test-results/<map>-*.txt, which tools/battle_stats.py reads.
"""
import argparse
import os
import re
import subprocess
import sys
import time
from pathlib import Path

SDK = Path(__file__).resolve().parent
ENGINE = SDK / 'engine'
MAPS = SDK / 'mods' / 'pw' / 'maps'
# Test maps the asset installer may build on converted original maps, in the player content folder.
CONTENT_MAPS = Path(os.environ.get('APPDATA', '')) / 'OpenRA' / 'Content' / 'pw' / 'maps'
RESULTS = SDK / 'test-results'
LOGS = Path(os.environ.get('APPDATA', '')) / 'OpenRA' / 'Logs'

# Seconds of wall time per battle, including game start-up: up to 40000 original ticks (their script's MaxTicks).
BATTLE_TIMEOUT = 2400

FAIL_PATTERN = re.compile(r'TEST FAIL|FAIL:|Fatal Lua Error')


def read(path: Path) -> str:
    try:
        return path.read_text(encoding='utf-8', errors='replace')
    except OSError:
        return ''


def kill_openra():
    subprocess.run(['taskkill', '/F', '/IM', 'OpenRA.exe'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


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


def run_map(name: str):
    RESULTS.mkdir(exist_ok=True)
    out_path, err_path = RESULTS / f'{name}-stdout.txt', RESULTS / f'{name}-stderr.txt'
    lua_log = LOGS / 'lua.log'
    lua_log.unlink(missing_ok=True)
    started = time.time()

    with open(out_path, 'w') as out, open(err_path, 'w') as err:
        proc = subprocess.Popen([str(ENGINE / 'bin' / 'OpenRA.exe'), 'Game.Mod=pw', 'Engine.EngineDir=..',
                                 'Engine.ModSearchPaths=../mods,./mods', f'Launch.Map={name}'],
                                cwd=ENGINE, stdout=out, stderr=err)
        verdict, detail = None, ''
        while verdict is None:
            time.sleep(1)
            text = read(out_path) + '\n' + read(lua_log)
            stderr = read(err_path)
            crash_logs = [p for p in LOGS.glob('exception-*.log') if p.stat().st_mtime >= started]
            fail = FAIL_PATTERN.search(text)
            if 'Exception' in stderr:
                verdict, detail = 'FAIL', next((l for l in stderr.splitlines() if 'Exception' in l), '')
            elif fail:
                verdict, detail = 'FAIL', next(l for l in text.splitlines() if FAIL_PATTERN.search(l))
            elif crash_logs:
                verdict, detail = 'FAIL', f'crash log {crash_logs[0].name}: ' + next(
                    (l for l in read(crash_logs[0]).splitlines() if 'Exception' in l), '')
            elif 'TEST PASS' in text:
                verdict = 'PASS'
            elif proc.poll() is not None:
                verdict, detail = 'FAIL', f'game exited (code {proc.returncode}) before passing'
            elif time.time() - started > BATTLE_TIMEOUT:
                verdict, detail = 'FAIL', f'timeout after {BATTLE_TIMEOUT}s; last output: ' + (text.strip().splitlines() or [''])[-1]
        proc.kill()
        proc.wait()
        # lua.log is only flushed when the game closes; re-check it for errors the live read missed.
        late = read(lua_log)
        if verdict == 'PASS' and FAIL_PATTERN.search(late):
            verdict, detail = 'FAIL', next(l for l in late.splitlines() if FAIL_PATTERN.search(l))
        elif verdict == 'FAIL' and 'Fatal Lua Error' in late and 'Lua' not in detail:
            detail += ' | ' + next(l for l in late.splitlines() if 'Fatal Lua Error' in l)

    (RESULTS / f'{name}-lua.txt').write_text(read(lua_log), encoding='utf-8')
    return verdict, detail.strip()[:300], time.time() - started


def run_battles(folders):
    maps = sorted(p.name for folder in folders for p in folder.iterdir() if p.name.startswith('battle-') or p.name == 'bot-test')
    results = []
    for name in maps:
        print(f'{name:22} ...', end=' ', flush=True)
        verdict, detail, seconds = run_map(name)
        print(f'{verdict} ({seconds:.0f}s) {detail}', flush=True)
        results.append((name, verdict, seconds, detail))

    passed = sum(v == 'PASS' for _, v, _, _ in results)
    lines = ['# Battle results', '', f'{passed}/{len(results)} maps passed', '',
             '| Map | Result | Time | Detail |', '|---|---|---:|---|']
    lines += [f'| {n} | {v} | {s:.0f}s | {d.replace("|", "/")} |' for n, v, s, d in results]
    (RESULTS / 'battles-summary.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(f'\n{passed}/{len(results)} passed; see {RESULTS / "battles-summary.md"}')
    sys.path.insert(0, str(SDK / 'tools'))
    import battle_stats
    print()
    battle_stats.main([n for n, _, _, _ in results])
    return passed == len(results)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--no-build', action='store_true')
    parser.add_argument('--no-check', action='store_true')
    parser.add_argument('--battles', action='store_true', help='run the AI battles (battle-* maps) and collect their stats')
    args, engine_args = parser.parse_known_args()

    if args.battles:
        kill_openra()
    if not args.no_build and not build():
        sys.exit('Build failed')
    yaml_ok = True if args.no_check else check_yaml()
    if not yaml_ok:
        print('YAML check FAIL')

    if args.battles:
        ok = run_battles([MAPS] + ([CONTENT_MAPS] if CONTENT_MAPS.is_dir() else []))
    else:
        ok = subprocess.run([sys.executable, str(ENGINE / 'tools' / 'run_tests.py'), '--mod-dir', str(SDK),
                             *engine_args]).returncode == 0
    print(f'YAML check {"PASS" if yaml_ok else "FAIL"}')
    sys.exit(0 if ok and yaml_ok else 1)


if __name__ == '__main__':
    main()
