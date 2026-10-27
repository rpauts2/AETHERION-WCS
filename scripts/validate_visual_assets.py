#!/usr/bin/env python3
"""Validate AETHERION race/model configuration without launching a CS2 server.

Usage:
  python scripts/validate_visual_assets.py
  python scripts/validate_visual_assets.py --models configs/assets/models.json --races configs/races.json
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any


def load_json(path: Path) -> Any:
    try:
        with path.open("r", encoding="utf-8-sig") as stream:
            return json.load(stream)
    except FileNotFoundError:
        raise ValueError(f"file not found: {path}")
    except json.JSONDecodeError as exc:
        raise ValueError(f"{path}:{exc.lineno}:{exc.colno}: invalid JSON: {exc.msg}")


def validate_models(path: Path) -> tuple[list[str], list[str], set[int]]:
    errors: list[str] = []
    warnings: list[str] = []
    raw = load_json(path)
    if not isinstance(raw, dict):
        return [f"{path}: expected a JSON object keyed by numeric race ID"], warnings, set()

    ids: set[int] = set()
    for key, entry in raw.items():
        if key == "_meta":
            continue
        if not key.isdigit():
            errors.append(f"{path}: key {key!r} must be a numeric race ID")
            continue
        race_id = int(key)
        ids.add(race_id)
        if not isinstance(entry, dict):
            errors.append(f"{path}: race {race_id}: expected an object")
            continue

        for field in ("race", "baseModel", "customModel", "fx", "aura", "accessory"):
            value = entry.get(field, "")
            if value is not None and not isinstance(value, str):
                errors.append(f"{path}: race {race_id}: {field} must be a string")

        if not str(entry.get("baseModel", "")).strip():
            warnings.append(f"race {race_id}: baseModel is empty; no guaranteed fallback model")

        tint = entry.get("tint", [])
        if tint not in (None, []):
            if not isinstance(tint, list) or len(tint) != 3:
                errors.append(f"{path}: race {race_id}: tint must contain exactly 3 integers")
            elif any(not isinstance(v, int) or isinstance(v, bool) or not 0 <= v <= 255 for v in tint):
                errors.append(f"{path}: race {race_id}: tint values must be integers from 0 to 255")

        glow = entry.get("glow", 1)
        if not isinstance(glow, (int, float)) or isinstance(glow, bool) or not 0 <= glow <= 10:
            errors.append(f"{path}: race {race_id}: glow must be a number from 0 to 10")

        for field in ("baseModel", "customModel", "aura", "accessory"):
            value = entry.get(field, "")
            if value and not str(value).lower().endswith(".vmdl"):
                warnings.append(f"race {race_id}: {field} path does not end in .vmdl: {value}")

        fx = entry.get("fx", "")
        if fx and not str(fx).lower().endswith(".vpcf"):
            warnings.append(f"race {race_id}: fx path does not end in .vpcf: {fx}")

    return errors, warnings, ids


def validate_races(path: Path, model_ids: set[int]) -> tuple[list[str], list[str]]:
    errors: list[str] = []
    warnings: list[str] = []
    raw = load_json(path)
    if isinstance(raw, dict):
        races = raw.get("races", raw.get("items", []))
    else:
        races = raw
    if not isinstance(races, list):
        return [f"{path}: expected a list of races (or an object containing 'races')"], warnings

    seen: set[int] = set()
    for index, race in enumerate(races):
        if not isinstance(race, dict):
            errors.append(f"{path}: entry {index}: expected an object")
            continue
        race_id = race.get("id", race.get("Id"))
        if not isinstance(race_id, int):
            warnings.append(f"{path}: entry {index}: no integer 'id' field found")
            continue
        if race_id in seen:
            errors.append(f"{path}: duplicate race ID {race_id}")
        seen.add(race_id)
        if race_id not in model_ids:
            warnings.append(f"race {race_id}: no entry in models.json; default model/tint/fx will be used")

    return errors, warnings


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--models", type=Path, default=Path("configs/assets/models.json"))
    parser.add_argument("--races", type=Path, default=Path("configs/races.json"))
    args = parser.parse_args()

    errors: list[str] = []
    warnings: list[str] = []
    try:
        model_errors, model_warnings, model_ids = validate_models(args.models)
        errors.extend(model_errors)
        warnings.extend(model_warnings)
        if args.races.exists():
            race_errors, race_warnings = validate_races(args.races, model_ids)
            errors.extend(race_errors)
            warnings.extend(race_warnings)
        else:
            warnings.append(f"race file not found, skipped: {args.races}")
    except ValueError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2

    for warning in warnings:
        print(f"WARNING: {warning}")
    for error in errors:
        print(f"ERROR: {error}", file=sys.stderr)

    print(f"Visual config validation: {len(errors)} error(s), {len(warnings)} warning(s), {len(model_ids)} model entries")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
