"""Collect the AI battle results: the "BATTLE RESULT" and "BATTLE END" lines the battle maps print (the installer's
AssetInstaller/MapTemplates/bot-test/test.lua), from test-results/<map>-stdout.txt.

Every run is appended to test-results/battles.csv (one row per bot per battle), so results build up over runs; the
summary (wins and economy per race, the duel results, every battle of this run) is printed and written to
test-results/battles.md, over this run and over the whole history.

Usage (from the repository root): python tools/battle_stats.py [map ...]   (default: every battle with results)
run_tests.py --battles calls it after running the battles.
"""
import csv
import datetime
import re
import subprocess
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RESULTS = ROOT / 'test-results'
HISTORY = RESULTS / 'battles.csv'
REPORT = RESULTS / 'battles.md'
RACES = ['primitive', 'tyrano', 'demon', 'elf']
FIELDS = ['run', 'commit', 'battle', 'tick', 'winner', 'leader', 'bot', 'race', 'alive', 'gone', 'buildings', 'army',
          'armyValue', 'assets', 'peakWorkers', 'peakArmy', 'peakArmyValue', 'earned', 'spent', 'earned5k', 'earned10k',
          'earned20k', 'kills', 'losses', 'killsValue', 'lossesValue']


def pairs(line):
    return dict(re.findall(r'(\w+)=(\S+)', line))


def collect(maps):
    """The rows of these battles' latest results."""
    run = datetime.datetime.now().strftime('%Y-%m-%d %H:%M')
    commit = subprocess.run(['git', 'rev-parse', '--short', 'HEAD'], cwd=ROOT, capture_output=True, text=True).stdout.strip()
    rows = []
    for name in maps:
        text = (RESULTS / f'{name}-stdout.txt').read_text(encoding='utf-8', errors='replace') if (RESULTS / f'{name}-stdout.txt').exists() else ''
        results = [pairs(l) for l in text.splitlines() if 'BATTLE RESULT ' in l]
        end = next((pairs(l) for l in text.splitlines() if 'BATTLE END ' in l), None)
        if not results or not end:
            print(f'{name}: no battle results', file=sys.stderr)
            continue
        for r in results:
            row = {'run': run, 'commit': commit, 'tick': end['tick'], 'winner': end['winner'], 'leader': end['leader']}
            row.update(r)
            rows.append({f: row.get(f, '') for f in FIELDS})
    return rows


def history():
    if not HISTORY.exists():
        return []
    with HISTORY.open(encoding='utf-8', newline='') as f:
        return list(csv.DictReader(f))


def append(rows):
    new = not HISTORY.exists()
    with HISTORY.open('a', encoding='utf-8', newline='') as f:
        w = csv.DictWriter(f, fieldnames=FIELDS)
        if new:
            w.writeheader()
        w.writerows(rows)


def num(v):
    try:
        return int(v)
    except (TypeError, ValueError):
        return 0


def summary(rows, title):
    """Markdown: per race wins and economy, the duel table, and each battle."""
    out = [f'## {title}', '']
    battles = defaultdict(list)
    for r in rows:
        battles[(r['run'], r['battle'])].append(r)

    # Per race: games, wins (last standing), leads at the time cap, and averages.
    stats = {race: defaultdict(int) for race in RACES}
    for (_, _), bots in battles.items():
        for r in bots:
            s = stats[r['race']]
            s['games'] += 1
            s['wins'] += r['winner'] == r['bot']
            s['leads'] += r['winner'] == 'none' and r['leader'] == r['bot']
            s['survived'] += num(r['alive'])
            for k in ('earned', 'earned10k', 'spent', 'peakArmy', 'peakArmyValue', 'kills', 'losses', 'killsValue', 'lossesValue'):
                s[k] += max(num(r[k]), 0)
    out += ['| Race | Games | Wins | Leads at cap | Survived | Earned (avg) | Earned by 10k | Peak army | Peak army value | '
            'Kills / losses | Kill value / loss value |', '|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---|']
    for race in RACES:
        s = stats[race]
        g = s['games'] or 1
        if not s['games']:
            continue
        out.append(f"| {race} | {s['games']} | {s['wins']} | {s['leads']} | {s['survived']} | {s['earned'] // g} | "
                   f"{s['earned10k'] // g} | {s['peakArmy'] // g} | {s['peakArmyValue'] // g} | "
                   f"{s['kills'] // g} / {s['losses'] // g} | {s['killsValue'] // g} / {s['lossesValue'] // g} |")

    # Duels: row race against column race, wins-losses-draws.
    duel = defaultdict(lambda: [0, 0, 0])
    for (_, _), bots in battles.items():
        if len(bots) != 2:
            continue
        a, b = bots
        for x, y in ((a, b), (b, a)):
            rec = duel[(x['race'], y['race'])]
            if x['winner'] == x['bot']:
                rec[0] += 1
            elif x['winner'] == y['bot']:
                rec[1] += 1
            else:
                rec[2] += 1
    if duel:
        out += ['', 'Duels (row against column: wins-losses-draws):', '',
                '| | ' + ' | '.join(RACES) + ' |', '|---|' + '---|' * len(RACES)]
        for x in RACES:
            out.append(f'| {x} | ' + ' | '.join('x' if x == y else '-'.join(map(str, duel[(x, y)])) if (x, y) in duel else ''
                                               for y in RACES) + ' |')

    out += ['', '| Run | Battle | Ticks | Winner | Leader | Bots (alive: assets, earned, kills/losses) |', '|---|---|---:|---|---|---|']
    for (run, battle), bots in sorted(battles.items()):
        desc = '; '.join(f"{r['bot']} ({'alive' if num(r['alive']) else 'out @' + r['gone']}: {r['assets']}, "
                         f"{r['earned']}, {r['kills']}/{r['losses']})" for r in bots)
        b = bots[0]
        out.append(f"| {run} | {battle} | {b['tick']} | {b['winner']} | {b['leader']} | {desc} |")
    return '\n'.join(out)


def main(maps):
    if not maps:
        maps = sorted(p.name[:-len('-stdout.txt')] for p in RESULTS.glob('*-stdout.txt')
                      if p.name.startswith(('battle-', 'bot-test')))
    rows = collect(maps)
    if not rows:
        sys.exit('No battle results found.')
    append(rows)
    text = '# AI battles\n\n' + summary(rows, 'This run') + '\n\n' + summary(history(), 'All runs') + '\n'
    REPORT.write_text(text, encoding='utf-8')
    print(summary(rows, 'This run'))
    print(f'\nHistory: {HISTORY} ({len(history())} rows); report: {REPORT}')


if __name__ == '__main__':
    main(sys.argv[1:])
