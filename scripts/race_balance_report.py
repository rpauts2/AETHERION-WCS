#!/usr/bin/env python3
"""Static race configuration report. This is not a substitute for live match telemetry."""
from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path
from typing import Any

DEFAULT_PATHS = [
    Path('configs/races.json'),
    Path('configs/races/races_core.json'),
    Path('configs/races/boss_races.json'),
]

def read_races(path: Path) -> list[dict[str, Any]]:
    with path.open('r', encoding='utf-8-sig') as stream:
        raw = json.load(stream)
    if isinstance(raw, dict):
        raw = raw.get('races', raw.get('items', []))
    if not isinstance(raw, list):
        raise ValueError(f'{path}: expected a list of race definitions')
    return [race for race in raw if isinstance(race, dict)]

def build_report(paths: list[Path]) -> str:
    races: list[tuple[Path, dict[str, Any]]] = []
    for path in paths:
        if path.exists():
            races.extend((path, race) for race in read_races(path))
    if not races:
        raise ValueError('no race definitions found in the configured catalogs')

    ids: Counter[int] = Counter()
    tiers: Counter[str] = Counter()
    divisions: Counter[str] = Counter()
    ability_types: Counter[str] = Counter()
    effects: Counter[str] = Counter()
    missing_abilities: list[str] = []
    invalid_values: list[str] = []
    values: list[tuple[float, str]] = []
    for path, race in races:
        race_id = race.get('id', race.get('Id'))
        name = str(race.get('name', race.get('Name', 'Unnamed')))
        label = f'{path}:{race_id if race_id is not None else "?"} {name}'
        if isinstance(race_id, int):
            ids[race_id] += 1
        tiers[str(race.get('tier', race.get('Tier', 'unknown')))] += 1
        divisions[str(race.get('division', race.get('Division', 'unknown')))] += 1
        abilities = race.get('abilities', race.get('Abilities', []))
        if not isinstance(abilities, list) or not abilities:
            missing_abilities.append(label)
            continue
        for ability in abilities:
            if not isinstance(ability, dict):
                invalid_values.append(f'{label}: ability entry is not an object')
                continue
            kind = str(ability.get('t', ability.get('type', ability.get('Type', 'unknown'))))
            ability_types[kind] += 1
            effect = str(ability.get('effect', ability.get('Effect', ''))).strip()
            if effect:
                effects[effect] += 1
            raw_value = ability.get('value', ability.get('Value'))
            if isinstance(raw_value, (int, float)) and not isinstance(raw_value, bool):
                values.append((abs(float(raw_value)), f'{label} / {ability.get("n", ability.get("name", "ability"))}: {raw_value} ({effect or "no effect tag"})'))
            for field, minimum in [('max', 1), ('maxLevel', 1), ('cd', 0), ('cooldown', 0), ('ether', 0), ('etherCost', 0)]:
                value = ability.get(field)
                if value is not None and (not isinstance(value, (int, float)) or isinstance(value, bool) or value < minimum):
                    invalid_values.append(f'{label}: {field}={value!r} is below the expected minimum {minimum}')

    duplicates = [(race_id, count) for race_id, count in ids.items() if count > 1]
    lines = [
        '# AETHERION WCS static race balance report',
        '',
        '> Generated from configuration only. Win rate, pick rate, damage contribution, and real match balance require runtime telemetry.',
        '',
        '## Catalog overview',
        '',
        f'- Race definitions scanned: **{len(races)}**',
        f'- Unique numeric race IDs: **{len(ids)}**',
        f'- Duplicate IDs across catalogs: **{len(duplicates)}**',
        f'- Races with no abilities: **{len(missing_abilities)}**',
        f'- Suspicious ability config values: **{len(invalid_values)}**',
        '',
        '## By tier',
        '',
        '| Tier | Race definitions |',
        '|---|---:|',
    ]
    lines.extend(f'| {tier} | {count} |' for tier, count in sorted(tiers.items()))
    lines.extend(['', '## By division', '', '| Division | Race definitions |', '|---|---:|'])
    lines.extend(f'| {division} | {count} |' for division, count in sorted(divisions.items()))
    lines.extend(['', '## Ability types', '', '| Type | Count |', '|---|---:|'])
    lines.extend(f'| {kind} | {count} |' for kind, count in ability_types.most_common())
    lines.extend(['', '## Most common effect tags', '', '| Effect | Uses |', '|---|---:|'])
    lines.extend(f'| `{effect}` | {count} |' for effect, count in effects.most_common(25))
    lines.extend(['', '## Largest configured absolute values', '', '| Entry | Absolute value |', '|---|---:|'])
    lines.extend(f'| {label.replace("|", "\\|")} | {value:g} |' for value, label in sorted(values, reverse=True)[:20])
    if duplicates:
        lines.extend(['', '## Duplicate race IDs', ''])
        lines.extend(f'- Race ID `{race_id}` appears {count} times across catalogs.' for race_id, count in sorted(duplicates))
    if missing_abilities:
        lines.extend(['', '## Races with no abilities', ''])
        lines.extend(f'- {label}' for label in missing_abilities[:100])
    if invalid_values:
        lines.extend(['', '## Suspicious ability config values', ''])
        lines.extend(f'- {item}' for item in invalid_values[:100])
    lines.append('')
    return '\n'.join(lines)

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--races', type=Path, action='append', default=None, help='Race catalog to scan; repeat to include multiple catalogs.')
    parser.add_argument('--output', type=Path, help='Optional Markdown report output path.')
    args = parser.parse_args()
    try:
        report = build_report(args.races or DEFAULT_PATHS)
    except (OSError, json.JSONDecodeError, ValueError) as exc:
        parser.error(str(exc))
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(report, encoding='utf-8')
        print(f'Race balance report written to {args.output}')
    else:
        print(report)
    return 0

if __name__ == '__main__':
    raise SystemExit(main())