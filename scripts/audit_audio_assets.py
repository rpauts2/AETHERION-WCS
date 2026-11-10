#!/usr/bin/env python3
"""Audit active Source 2 customSound references and separate disabled plans."""
from __future__ import annotations

import argparse
import json
from collections import defaultdict
from pathlib import Path, PurePosixPath

SOURCE_EXTENSIONS = (".wav", ".mp3", ".ogg", ".vsnd")


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


def normalize_runtime_path(value: str) -> str:
    normalized = value.strip().replace("\\", "/")
    path = PurePosixPath(normalized)
    if path.is_absolute() or not path.parts or any(part in ("", ".", "..") for part in path.parts):
        raise ValueError(f"unsafe customSound path: {value!r}")
    if ":" in path.parts[0]:
        raise ValueError(f"drive-qualified customSound path is not allowed: {value!r}")
    if path.parts[0].lower() != "sounds":
        raise ValueError(f"Source 2 runtime sound path must start with sounds/: {value!r}")
    if path.suffix.lower() != ".vsnd":
        raise ValueError(f"Source 2 runtime sound reference must end in .vsnd: {value!r}")
    return path.as_posix()


def locate_source(root: Path, runtime_path: str):
    runtime = PurePosixPath(runtime_path)
    stem = runtime.with_suffix("")
    for extension in SOURCE_EXTENSIONS:
        relative = str(stem) + extension
        for candidate in (
            root / "content" / "csgo_addons" / "aetherion" / relative,
            root / relative,
        ):
            if candidate.is_file():
                return candidate
    return None


def locate_compiled(root: Path, runtime_path: str):
    relative = runtime_path + "_c"
    for candidate in (
        root / "game" / "csgo_addons" / "aetherion" / relative,
        root / relative,
    ):
        if candidate.is_file():
            return candidate
    return None


def audit(config_path: Path, root: Path):
    config = load_config(config_path)
    planned: dict[str, list[str]] = defaultdict(list)
    active: dict[str, list[str]] = defaultdict(list)
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
            runtime_path = normalize_runtime_path(str(value))
        except ValueError as exc:
            malformed.append(f"sound {sound_id}: {exc}")
            continue
        if entry.get("customSoundEnabled") is True:
            active[runtime_path].append(str(sound_id))
        else:
            planned[runtime_path].append(str(sound_id))

    found_source = {}
    found_compiled = {}
    missing = []
    for runtime_path in sorted(active, key=str.lower):
        source = locate_source(root, runtime_path)
        compiled = locate_compiled(root, runtime_path)
        if source is not None:
            found_source[runtime_path] = source
        if compiled is not None:
            found_compiled[runtime_path] = compiled
        if source is None and compiled is None:
            missing.append(runtime_path)

    return {
        "entries": len(config),
        "empty": empty_count,
        "planned": planned,
        "active": active,
        "found_source": found_source,
        "found_compiled": found_compiled,
        "missing": missing,
        "malformed": malformed,
    }


def render(result):
    lines = [
        "# AETHERION custom audio asset audit", "",
        "This audit checks active Source 2 source and compiled file presence. Planned-but-disabled paths do not override stock sounds. It does not prove soundevent registration, precache, client delivery, or audible playback.", "",
        "## Summary", "",
        f"- Config entries: **{result['entries']}**",
        f"- Empty customSound slots: **{result['empty']}**",
        f"- Planned disabled paths: **{len(result['planned'])}**",
        f"- Active runtime custom paths: **{len(result['active'])}**",
        f"- Present active source audio files: **{len(result['found_source'])}**",
        f"- Present active compiled VSND_C files: **{len(result['found_compiled'])}**",
        f"- Active paths missing source and compiled files: **{len(result['missing'])}**",
        f"- Invalid runtime paths: **{len(result['malformed'])}**", "",
        "## Planned disabled paths", ""
    ]
    if result["planned"]:
        for runtime_path in sorted(result["planned"], key=str.lower):
            ids = ", ".join(sorted(result["planned"][runtime_path], key=int))
            lines.append(f"- PLANNED {runtime_path} (sound IDs: {ids})")
    else:
        lines.append("No pending custom sound paths.")
    lines.extend(("", "## Active missing resources", ""))
    if result["missing"]:
        for runtime_path in result["missing"]:
            ids = ", ".join(sorted(result["active"][runtime_path], key=int))
            lines.append(f"- MISSING {runtime_path} (sound IDs: {ids})")
    else:
        lines.append("No active custom audio resources are missing.")
    lines.extend(("", "## Present active source files", ""))
    for runtime_path, path in sorted(result["found_source"].items()):
        lines.append(f"- {runtime_path} <- {path.as_posix()}")
    if not result["found_source"]:
        lines.append("No active source audio files found.")
    lines.extend(("", "## Present active compiled files", ""))
    for runtime_path, path in sorted(result["found_compiled"].items()):
        lines.append(f"- {runtime_path} -> {path.as_posix()}")
    if not result["found_compiled"]:
        lines.append("No active compiled VSND_C files found.")
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
        f"{len(result['planned'])} planned disabled, "
        f"{len(result['active'])} active, {len(result['missing'])} active missing, "
        f"{len(result['malformed'])} invalid"
    )
    for runtime_path in result["missing"]:
        print(f"MISSING ACTIVE AUDIO: {runtime_path}")
    for message in result["malformed"]:
        print(f"INVALID AUDIO: {message}")
    return 1 if args.fail_on_missing and (result["missing"] or result["malformed"]) else 0


if __name__ == "__main__":
    raise SystemExit(main())
