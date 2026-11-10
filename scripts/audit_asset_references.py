#!/usr/bin/env python3
"""Audit AETHERION-owned Source 2 particle and custom-audio references.

Stock CS2 resources are excluded because they belong to the game depot. The
report checks repository presence and path conventions, not in-game playback.
"""
from __future__ import annotations

import argparse
import json
import re
from collections import defaultdict
from pathlib import Path

PARTICLE_REF = re.compile(r"""['"](?P<path>particles/[A-Za-z0-9_./-]+\.vpcf)['"]""", re.IGNORECASE)
CUSTOM_PREFIXES = ("particles/aether_", "particles/aetherion/")
TEXT_SUFFIXES = {".cs", ".json"}
AUDIO_SUFFIXES = {".wav", ".mp3", ".ogg", ".flac"}


def _audio_source_candidates(root: Path, configured_path: str) -> list[Path]:
    normalized = configured_path.replace("\\", "/").lstrip("/")
    parts = normalized.split("/")
    if parts and parts[0].lower() in {"sound", "sounds"}:
        parts = parts[1:]
    relative = Path(*parts)
    source = relative
    compiled = relative.with_suffix(".vsnd_c")
    return [
        root / "content/csgo_addons/aetherion/sounds" / source,
        root / "content/csgo_addons/aetherion" / Path(normalized),
        root / "sounds" / source,
        root / "sound" / source,
        root / "assets/sounds" / source,
        root / "game/csgo_addons/aetherion/sounds" / compiled,
    ]


def scan(root: Path):
    particle_refs = defaultdict(list)
    for base in (root / "src", root / "configs"):
        if not base.exists():
            continue
        for path in base.rglob("*"):
            if not path.is_file() or path.suffix.lower() not in TEXT_SUFFIXES:
                continue
            try:
                content = path.read_text(encoding="utf-8-sig")
            except (UnicodeDecodeError, OSError):
                continue
            for match in PARTICLE_REF.finditer(content):
                reference = match.group("path").replace("\\", "/")
                if reference.lower().startswith(CUSTOM_PREFIXES):
                    particle_refs[reference].append(path.relative_to(root).as_posix())

    missing_particles = []
    for reference in sorted(particle_refs, key=str.lower):
        if not (root / reference).is_file() and not (root / (reference + "_c")).is_file():
            missing_particles.append(reference)

    sound_config = root / "configs/assets/sounds.json"
    planned_audio_entries = []
    active_audio_entries = []
    if sound_config.exists():
        try:
            raw = json.loads(sound_config.read_text(encoding="utf-8-sig"))
            if isinstance(raw, dict):
                for sound_id, value in raw.items():
                    if not isinstance(value, dict):
                        continue
                    custom_path = value.get("customSound", "")
                    if not isinstance(custom_path, str) or not custom_path.strip():
                        continue
                    entry = {"id": str(sound_id), "path": custom_path.strip()}
                    if value.get("customSoundEnabled") is True:
                        active_audio_entries.append(entry)
                    else:
                        planned_audio_entries.append(entry)
        except (OSError, json.JSONDecodeError):
            pass

    planned_audio_paths = sorted({entry["path"] for entry in planned_audio_entries}, key=str.lower)
    active_audio_paths = sorted({entry["path"] for entry in active_audio_entries}, key=str.lower)
    audio_paths = sorted(set(planned_audio_paths) | set(active_audio_paths), key=str.lower)
    missing_audio = [
        path for path in active_audio_paths
        if not any(candidate.is_file() for candidate in _audio_source_candidates(root, path))
    ]
    missing_planned_audio = [
        path for path in planned_audio_paths
        if not any(candidate.is_file() for candidate in _audio_source_candidates(root, path))
    ]
    legacy_audio_paths = [
        path for path in audio_paths
        if path.lower().startswith("sound/") or Path(path).suffix.lower() in AUDIO_SUFFIXES
    ]

    extracted = root / "gb_models" / "extracted"
    counts = {
        "source_vpcf": sum(1 for p in root.rglob("*.vpcf") if p.is_file()),
        "compiled_vpcf": sum(1 for p in root.rglob("*.vpcf_c") if p.is_file()),
        "source_audio": sum(1 for p in root.rglob("*") if p.is_file() and p.suffix.lower() in AUDIO_SUFFIXES),
        "compiled_audio": sum(1 for p in root.rglob("*.vsnd_c") if p.is_file()),
        "quarantined_model_files": sum(1 for p in extracted.rglob("*") if p.is_file()) if extracted.exists() else 0,
    }
    audio = {
        "configured_entries": len(planned_audio_entries) + len(active_audio_entries),
        "planned_entries": planned_audio_entries,
        "active_entries": active_audio_entries,
        "planned_paths": planned_audio_paths,
        "active_paths": active_audio_paths,
        "unique_paths": len(audio_paths),
        "paths": audio_paths,
        "missing_paths": missing_audio,
        "missing_planned_paths": missing_planned_audio,
        "legacy_paths": legacy_audio_paths,
    }
    return particle_refs, missing_particles, counts, audio


def render(particle_refs, missing_particles, counts, audio):
    lines = [
        "# AETHERION visual/audio asset reference audit", "",
        "Generated by scripts/audit_asset_references.py.", "",
        "## Inventory", "",
        f"- Source VPCF files in repository: **{counts['source_vpcf']}**",
        f"- Compiled VPCF_C files in repository: **{counts['compiled_vpcf']}**",
        f"- Source audio files in repository: **{counts['source_audio']}**",
        f"- Compiled VSND_C files in repository: **{counts['compiled_audio']}**",
        f"- Files under gb_models/extracted (not license-cleared by this audit): **{counts['quarantined_model_files']}**",
        f"- Unique AETHERION-owned particle references: **{len(particle_refs)}**",
        f"- Missing AETHERION-owned particle resources: **{len(missing_particles)}**",
        f"- Non-empty customSound config fields: **{audio['configured_entries']}**",
        f"- Planned disabled customSound entries: **{len(audio['planned_entries'])}**",
        f"- Active customSound entries: **{len(audio['active_entries'])}**",
        f"- Unique customSound paths: **{audio['unique_paths']}**",
        f"- Active paths missing a source/compiled file: **{len(audio['missing_paths'])}**",
        f"- Planned paths not yet installed: **{len(audio['missing_planned_paths'])}**", "",
        "Disabled planned paths are reported separately and do not override working stock sounds. Only customSoundEnabled=true entries are treated as active/missing.",
        "Stock CS2 particle and sound resources are excluded from repository missing-file checks because they belong to the game depot.",
        "This report checks repository file presence and path shape only; it does not prove compilation, precache, client delivery or playback.", "",
        "## AETHERION-owned particle references", ""
    ]
    if not particle_refs:
        lines.append("No AETHERION-owned particle references found.")
    for reference in sorted(particle_refs, key=str.lower):
        status = "MISSING" if reference in missing_particles else "present"
        callsites = ", ".join(sorted(set(particle_refs[reference])))
        lines.extend((f"- **{status}** {reference}", f"  Referenced from: {callsites}"))

    lines.extend(("", "## Active custom audio references", ""))
    if not audio["active_paths"]:
        lines.append("No custom audio paths are enabled. Stock game sounds remain in use.")
    for path in audio["active_paths"]:
        status = "MISSING ACTIVE" if path in audio["missing_paths"] else "active source/compiled file present"
        lines.append(f"- **{status}** {path}")

    lines.extend(("", "## Planned disabled custom audio paths", ""))
    if not audio["planned_paths"]:
        lines.append("No planned disabled custom audio paths.")
    for path in audio["planned_paths"]:
        status = "not installed yet" if path in audio["missing_planned_paths"] else "source/compiled file already present"
        lines.append(f"- **PLANNED** {path} ({status})")
    if audio["legacy_paths"]:
        lines.append(f"- **{len(audio['legacy_paths'])}** paths use a source-style .wav or singular sound/ path. Review their runtime form before enabling.")
    lines.extend(("", "## Interpretation", "",
        "- Missing custom particle references need a real Source 2 particle resource or a code/config change to a verified stock effect.",
        "- A custom WAV is source material, not the compiled CS2 audio resource. Source 2 uses compiled .vsnd_c assets and sound-event scripts where applicable.",
        "- Review customSound values before shipping: AudioManager passes them straight into the client play command; the CounterStrikeSharp upstream test plugin demonstrates a Source 2 resource path such as sounds/ui/counter_beep.vsnd.",
        "- Assets in gb_models/extracted remain quarantined until provenance and license are recorded per package.",
        "- A successful static audit is not a Workshop Tools compile or an in-game visual/audio test.", ""))
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--output", type=Path, default=None)
    parser.add_argument("--fail-on-missing", action="store_true")
    args = parser.parse_args()
    root = args.root.resolve()
    particle_refs, missing_particles, counts, audio = scan(root)
    report = render(particle_refs, missing_particles, counts, audio)
    if args.output:
        output = args.output if args.output.is_absolute() else root / args.output
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(report, encoding="utf-8")
    print(
        f"Asset audit: {len(particle_refs)} custom particle ref(s), {len(missing_particles)} missing; "
        f"{audio['configured_entries']} custom audio field(s), {len(audio['planned_paths'])} planned, "
        f"{len(audio['active_paths'])} active, {len(audio['missing_paths'])} active missing; {counts['source_vpcf']} VPCF, "
        f"{counts['compiled_vpcf']} VPCF_C, {counts['source_audio']} source audio, "
        f"{counts['compiled_audio']} VSND_C"
    )
    for reference in missing_particles:
        print(f"MISSING PARTICLE: {reference}")
    for path in audio["missing_paths"]:
        print(f"MISSING AUDIO: {path}")
    return 1 if args.fail_on_missing and (missing_particles or audio["missing_paths"]) else 0


if __name__ == "__main__":
    raise SystemExit(main())
