#!/usr/bin/env python3
"""Audit configured AETHERION customSound paths and report missing files.

The script checks source files in the repository/addon content tree; it does not
claim that audio is compiled, precached, delivered to clients, or audible in-game.
"""
from __future__ import annotations

import argparse
import json
from collections import defaultdict
from pathlib import Path, PurePosixPath

ALLOWED_EXTENSIONS = {".wav", ".mp3", ".ogg", ".vsnd"}


def load_config(path: Path):
    try:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
    except FileNotFoundError as exc:
        raise ValueError(f"sound config not found: {path}") from exc
    except json.JSONDecodeError as exc:
        raise ValueError(f"{path}:{exc.lineno}:{exc.colno}: invalid JSON: {exc.msg}") from exc
    if not isinstance(data, dict):
        raise ValueError(f"{path}: expected an object keyed by sound ID")
    return data


def normalize_asset_path(value: str) -> str:
    normalized = value.strip().replace("\\", "/")
    while normalized.startswith("./"):
        normalized = normalized[2:]
    path = PurePosixPath(normalized)
    if path.is_absolute() or not path.parts or any(part in ("", ".", "..") for part in path.parts):
        raise ValueError(f"unsafe customSound path: {value!r}")
    if path.suffix.lower() not in ALLOWED_EXTENSIONS:
        raise ValueError(f"unsupported customSound extension: {value!r}")
    return path.as_posix()


def locate_asset(root: Path, asset_path: str):
    candidates = [
        root / asset_path,
        root / "content" / "csgo_addons" / "aetherion" / asset_path,
        root / "game" / "csgo_addons" / "aetherion" / asset_path,
    ]
    for candidate in candidates:
        if candidate.is_file():
            return candidate
    return None


def audit(config_path: Path, root: Path):
    config = load_config(config_path)
    references: dict[str, list[str]] = defaultdict(list)
    malformed: list[str] = []
    empty_count = 0

    for sound_id, entry in config.items():
        if not isinstance(entry, dict):
            malformed.append(f"sound {sound_id}: expected an object")
            continue
        value = entry.get("customSound")
        if value is None or not str(value).strip():
            empty_count += 1
            continue
        try:
            asset_path = normalize_asset_path(str(value))
        except ValueError as exc:
            malformed.append(f"sound {sound_id}: {exc}")
            continue
        references[asset_path].append(str(sound_id))

    found = {}
    missing = []
    for asset_path in sorted(references, key=str.lower):
        located = locate_asset(root, asset_path)
        if located is None:
            missing.append(asset_path)
        else:
            found[asset_path] = located

    return {
        "entries": len(config),
        "empty": empty_count,
        "references": references,
        "found": found,
        "missing": missing,
        "malformed": malformed,
    }


def render(result):
    lines = [
        "# AETHERION custom audio asset audit", "",
        "This report checks configured source-file presence only. It does not prove soundevent registration, compilation, client delivery, precache, or playback.", "",
        "## Summary", "",
        f"- Config entries: **{result['entries']}**",
        f"- Empty customSound slots: **{result['empty']}**",
        f"- Unique configured custom audio paths: **{len(result['references'])}**",
        f"- Present source files: **{len(result['found'])}**",
        f"- Missing source files: **{len(result['missing'])}**",
        f"- Invalid paths or unsupported formats: **{len(result['malformed'])}**", "",
        "## Missing files", ""
    ]
    if result["missing"]:
        for asset_path in result["missing"]:
            ids = ", ".join(sorted(result["references"][asset_path], key=int))
            lines.append(f"- MISSING {asset_path} (sound IDs: {ids})")
    else:
        lines.append("No configured custom audio files are missing.")
    lines.extend(("", "## Present source files", ""))
    if result["found"]:
        for asset_path, full_path in sorted(result["found"].items()):
            lines.append(f"- {asset_path} -> {full_path.as_posix()}")
    else:
        lines.append("No configured custom audio source files were found.")
    lines.extend(("", "## Invalid configuration entries", ""))
    lines.extend(f"- {message}" for message in result["malformed"])
    if not result["malformed"]:
        lines.append("None.")
    lines.append("")
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--config", type=Path, default=None)
    parser.add_argument("--output", type=Path, default=None)
    parser.add_argument("--fail-on-missing", action="store_true")
    args = parser.parse_args()

    root = args.root.resolve()
    config = (args.config or root / "configs" / "assets" / "sounds.json").resolve()
    try:
        result = audit(config, root)
    except ValueError as exc:
        parser.error(str(exc))

    report = render(result)
    if args.output:
        output = args.output if args.output.is_absolute() else root / args.output
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(report, encoding="utf-8")
    print(
        f"Audio asset audit: {result['entries']} config entries, "
        f"{len(result['references'])} unique custom paths, "
        f"{len(result['missing'])} missing, {len(result['malformed'])} invalid"
    )
    for asset_path in result["missing"]:
        print(f"MISSING: {asset_path}")
    for message in result["malformed"]:
        print(f"INVALID: {message}")
    return 1 if args.fail_on_missing and (result["missing"] or result["malformed"]) else 0


if __name__ == "__main__":
    raise SystemExit(main())
