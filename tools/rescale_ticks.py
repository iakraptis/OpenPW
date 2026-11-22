"""Rescale the rules from original ticks to world ticks (tickrate-plan.md): durations x FACTOR, speeds / FACTOR.

Usage (from the repository root):
  python tools/rescale_ticks.py            # print the changes
  python tools/rescale_ticks.py --write    # and write them

It rewrites mods/pw/rules, mods/pw/weapons, the Rules and Weapons sections of the maps' map.yaml, and the Tick of
every sequence. Which fields are durations and which are speeds is FIELDS below, keyed by the trait (its Info class
without "Info", or the weapon part); --check compares two dumps of `utility --dump-numbers` against the same table.
Run it once: it doesn't know whether a file was already rescaled.
"""
import argparse
import re
import sys
from pathlib import Path

FACTOR = 2
ROOT = Path(__file__).resolve().parent.parent

# x: a duration in ticks (multiplied); /: a distance or angle per tick (divided). Keyed "Trait.Field"; "*.Field"
# for every trait. Weapon fields are "Weapon.Field"; projectiles and warheads use their type (PwBullet, ...Warhead).
FIELDS = {
    # Weapons.
    'Weapon.ReloadDelay': 'x', 'Weapon.BurstDelays': 'x',
    'PwBullet.Speed': '/', 'PwBullet.TrailDelay': 'x', 'PwBullet.TrailInterval': 'x', 'PwBullet.ContrailDelay': 'x',
    'PwLightningBolt.Duration': 'x',
    '*Warhead.Delay': 'x', 'GrantExternalConditionWarhead.Duration': 'x',
    # Movement.
    'Mobile.Speed': '/', 'Mobile.TurnSpeed': '/',
    'Aircraft.Speed': '/', 'Aircraft.IdleSpeed': '/', 'Aircraft.TurnSpeed': '/', 'Aircraft.AltitudeVelocity': '/',
    'Aircraft.RepulsionSpeed': '/', 'Aircraft.NumberOfTicksToVerifyAvailableAirport': 'x',
    'Locomotor.WaitAverage': 'x', 'Locomotor.WaitSpread': 'x',
    'Wanders.MinMoveDelay': 'x', 'Wanders.MaxMoveDelay': 'x', 'Wanders.ReduceMoveRadiusDelay': 'x',
    'BerryGatherer.FlightSpeed': '/',
    # Combat and production.
    'Armament.FireDelay': 'x', 'AutoTarget.MinimumScanTimeInterval': 'x', 'AutoTarget.MaximumScanTimeInterval': 'x',
    'Buildable.BuildDuration': 'x', 'UnderConstruction.Duration': 'x',
    'Cargo.AfterLoadDelay': 'x', 'Cargo.AfterUnloadDelay': 'x', 'Cargo.BeforeUnloadDelay': 'x',
    'Cargo.BetweenUnloadDelay': 'x', 'Cloak.CloakDelay': 'x', 'Cloak.InitialDelay': 'x', 'KillsSelf.Delay': 'x',
    'PwLevels.AttackDelay': 'x', 'PwMeleeAttack.CheckInterval': 'x', 'PwHunter.ScanInterval': 'x',
    'PwHunter.GiveUpTicks': 'x',
    # Economy and upkeep.
    'BerryBush.RegrowInterval': 'x', 'BerryGatherer.GatherTicks': 'x', 'Builder.RepairInterval': 'x',
    'PwMana.RegenInterval': 'x', 'PwManaAura.Interval': 'x', 'PwRegenerates.Interval': 'x',
    'PwFallOutVictim.Interval': 'x', 'PwMeat.RotInterval': 'x', 'PwMeatEater.SearchInterval': 'x',
    'PwRespawns.Delay': 'x', 'PwExposure.Delay': 'x', 'PwBody.Ticks': 'x', 'PwConjured.FadeOutTicks': 'x',
    # Spells.
    'PwSpell.AutoHealInterval': 'x', 'PwSpell.Delay': 'x', 'PwSpell.RandomDelay': 'x', 'PwSpell.StepInterval': 'x',
    'PwSpell.EffectTicks': 'x', 'PwSpell.ShieldTicks': 'x', 'PwSpell.PetrifyTicks': 'x', 'PwSpell.PortalTicks': 'x',
    # Looks.
    'WithInfantryBody.MinIdleDelay': 'x', 'WithInfantryBody.MaxIdleDelay': 'x',
    'PwWithDisguiseBody.MinIdleDelay': 'x', 'PwWithDisguiseBody.MaxIdleDelay': 'x',
    'WithAttackAnimation.Delay': 'x', 'WithAttackOverlay.Delay': 'x', 'WithDeathAnimation.Delay': 'x',
    'PwWithTextDecoration.BlinkInterval': 'x',
    # AI.
    '*BotModule.ScanInterval': 'x', 'PwBaseBuilderBotModule.DecisionInterval': 'x',
    'PwBaseBuilderBotModule.ExpansionDelay': 'x', 'PwDefenseBotModule.ResponseInterval': 'x',
    'PwDetectorBotModule.EscortDelay': 'x', 'PwTransportBotModule.Cooldown': 'x', 'PwTransportBotModule.StepTimeout': 'x',
    'SquadManagerBotModule.AssignRolesInterval': 'x', 'SquadManagerBotModule.AttackForceInterval': 'x',
    'SquadManagerBotModule.MinimumAttackForceDelay': 'x', 'SquadManagerBotModule.RushInterval': 'x',
}


# Differences --check doesn't report: settings no unit uses (no airports, bursts of one shot, bullets without
# trails) and the abstract weapon templates (^...).
IGNORED = {'Aircraft.NumberOfTicksToVerifyAvailableAirport', 'Weapon.BurstDelays', 'PwBullet.TrailDelay',
           'PwBullet.TrailInterval', 'PwBullet.ContrailDelay'}


def kind(trait, field):
    """'x', '/' or None for a field of a trait (a weapon's own fields: trait 'Weapon')."""
    if f'{trait}.{field}' in FIELDS:
        return FIELDS[f'{trait}.{field}']
    for key, value in FIELDS.items():
        t, f = key.split('.')
        if f == field and t.startswith('*') and trait.endswith(t[1:]):
            return value
    return None


def scale_number(text, how):
    """Scale one value: an integer, WDist (1c512) or WAngle; a list scales each item."""
    def one(v):
        v = v.strip()
        m = re.fullmatch(r'(-?)(\d+)c(\d+)', v)
        if m:
            total = (int(m.group(2)) * 1024 + int(m.group(3))) * (-1 if m.group(1) else 1)
            total = total * FACTOR if how == 'x' else round_half_away(total / FACTOR)
            sign = '-' if total < 0 else ''
            total = abs(total)
            return f'{sign}{total // 1024}c{total % 1024}'
        if re.fullmatch(r'-?\d+', v):
            n = int(v)
            if n < 0:
                return v  # -1: "use the default" in the engine's traits
            return str(n * FACTOR if how == 'x' else round_half_away(n / FACTOR))
        raise ValueError(f'not a number: {v!r}')
    return ', '.join(one(p) for p in text.split(','))


def round_half_away(x):
    return int(x + 0.5) if x >= 0 else -int(-x + 0.5)


def rescale_yaml(path, lines, depth0, changes, weapons):
    """Rescale rule (or weapon) definitions whose top-level keys are at indentation depth0."""
    trait = None
    part = None
    for i, line in enumerate(lines):
        m = re.match(r'^(\t*)([^\t#:][^:]*):(.*)$', line)
        if not m:
            continue
        depth = len(m.group(1)) - depth0
        key, value = m.group(2).strip(), m.group(3)
        if depth == 0:
            trait = part = None
            continue
        if depth == 1:
            name = key.lstrip('-').split('@')[0]
            if weapons:
                if name == 'Projectile':
                    trait = value.strip()
                elif name == 'Warhead':
                    trait = value.strip()
                    trait = trait if trait.endswith('Warhead') else trait + 'Warhead'
                else:
                    trait = None
                    how = kind('Weapon', name)
                    if how and value.strip():
                        changes.append(rewrite(path, lines, i, m, how, f'Weapon.{name}'))
            else:
                trait = name
            continue
        if depth == 2 and trait:
            how = kind(trait, key)
            if how and value.strip():
                changes.append(rewrite(path, lines, i, m, how, f'{trait}.{key}'))


def rewrite(path, lines, i, m, how, label):
    value = m.group(3)
    comment = ''
    if '#' in value:
        value, comment = value.split('#', 1)
        comment = ' #' + comment
    new = scale_number(value, how)
    lines[i] = f'{m.group(1)}{m.group(2)}: {new}{comment}'
    return f'{path.relative_to(ROOT)}:{i + 1}: {label} {value.strip()} -> {new}'


def rescale_sequences(path, lines, changes):
    for i, line in enumerate(lines):
        m = re.match(r'^(\t+)Tick: (\d+)(.*)$', line)
        if m:
            new = int(m.group(2)) * FACTOR
            lines[i] = f'{m.group(1)}Tick: {new}{m.group(3)}'
            changes.append(f'{path.relative_to(ROOT)}:{i + 1}: Tick {m.group(2)} -> {new}')


def map_sections(lines):
    """Line ranges of the Rules and Weapons sections of a map.yaml: (start, end, weapons)."""
    out = []
    start = None
    for i, line in enumerate(lines + ['End:']):
        if re.match(r'^\S', line):
            if start is not None:
                out.append((start, i, weapons))
                start = None
            if line.startswith('Rules:') or line.startswith('Weapons:'):
                start, weapons = i + 1, line.startswith('Weapons:')
    return out


def run(write):
    changes = []
    files = []
    for folder, weapons in (('mods/pw/rules', False), ('mods/pw/weapons', True)):
        for path in sorted((ROOT / folder).rglob('*.yaml')):
            files.append((path, lambda p, ls, w=weapons: rescale_yaml(p, ls, 0, changes, w)))
    for path in sorted((ROOT / 'mods/pw/maps').glob('*/map.yaml')):
        def maps(p, ls):
            for start, end, weapons in map_sections(ls):
                part = ls[start:end]
                rescale_yaml(p, part, 1, changes, weapons)
                ls[start:end] = part
        files.append((path, maps))
    for path in sorted((ROOT / 'mods/pw/sequences').glob('*.yaml')):
        files.append((path, lambda p, ls: rescale_sequences(p, ls, changes)))

    for path, fn in files:
        raw = path.read_bytes().decode('utf-8')
        crlf = '\r\n' in raw
        text = raw.replace('\r\n', '\n')
        lines = text.split('\n')
        before = len(changes)
        fn(path, lines)
        if write and len(changes) > before:
            out = '\n'.join(lines)
            path.write_bytes((out.replace('\n', '\r\n') if crlf else out).encode('utf-8'))
    for c in changes:
        print(c)
    print(f'{len(changes)} values', file=sys.stderr)


def check(before, after):
    """Compare two --dump-numbers outputs: every field must change by its FIELDS factor, the others stay."""
    def load(path):
        out = {}
        for line in Path(path).read_text(encoding='utf-8', errors='replace').splitlines():
            m = re.match(r'^(\S+) (\S+) (\S+) = (.*)$', line)
            if m:
                out[(m.group(1), m.group(2), m.group(3))] = m.group(4)
            else:
                m = re.match(r'^(weapon:\S+) (\S+) = (.*)$', line)
                if m:
                    out[(m.group(1), 'Weapon', m.group(2))] = m.group(3)
        return out
    a, b = load(before), load(after)
    problems = 0
    for key in sorted(set(a) | set(b)):
        owner, trait, field = key
        t = trait.split('@')[0].split(':')[-1]
        t = t[:-4] if t.endswith('Info') else t
        how = kind(t, field)
        if f'{t}.{field}' in IGNORED or owner.startswith('weapon:^'):
            continue
        old, new = a.get(key), b.get(key)
        if old is None or new is None:
            print(f'only {"after" if old is None else "before"}: {" ".join(key)} = {new or old}')
            problems += 1
            continue
        try:
            expected = scale_number(old, how) if how else old
        except ValueError:
            expected = old
        if new != expected:
            print(f'{" ".join(key)}: {old} -> {new}, expected {expected}')
            problems += 1
    print(f'{problems} differences from the table', file=sys.stderr)
    return problems


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--check', nargs=2, metavar=('BEFORE', 'AFTER'))
    args = parser.parse_args()
    if args.check:
        sys.exit(1 if check(*args.check) else 0)
    run(args.write)
