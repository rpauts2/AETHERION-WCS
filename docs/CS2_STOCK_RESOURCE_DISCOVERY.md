# CS2 stock particle and audio resource discovery

Updated: 2026-11-10

## Why this exists

AETHERION currently references project-owned particles such as particles/aether_dash.vpcf, particles/aether_thunder.vpcf, particles/aether_explosion.vpcf, particles/aether_recall.vpcf, particles/aether_fire.vpcf and particles/aether_cast.vpcf, but the repository has no source .vpcf or compiled .vpcf_c for those custom paths.

The next safe fallback is to inventory the resources already shipped in the user's installed CS2 depot. Do not guess paths and do not copy Valve's compiled game assets into AETHERION. Use the game depot only as a read-only reference for valid existing particle and sound-event names.

## Tool

Source 2 Viewer is MIT-licensed, cross-platform for its CLI, and can list/extract/decompile CS2 VPK resources:
- Main tool: https://s2v.app/ValveResourceFormat/
- GitHub/releases: https://github.com/ValveResourceFormat/ValveResourceFormat
- CLI guide: https://s2v.app/ValveResourceFormat/guides/command-line
- VPK/file discovery guide: https://s2v.app/ValveResourceFormat/guides/finding-files.html

The official CLI example lists the CS2 VPK using Source2Viewer-CLI.exe -i "steam:730/game/csgo/pak01_dir.vpk" --vpk_list. The repository script uses the local file path instead.

## Run on Windows

1. Install Counter-Strike 2 and the Source 2 Viewer CLI release.
2. Save the CLI as C:\Tools\Source2Viewer-CLI.exe or pass its actual path.
3. From the AETHERION repo root, run:

    powershell -ExecutionPolicy Bypass -File .\scripts\audit_cs2_stock_resources.ps1

Or specify paths:

    powershell -ExecutionPolicy Bypass -File .\scripts\audit_cs2_stock_resources.ps1 -Cs2Path "D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive" -Source2ViewerCli "C:\Tools\Source2Viewer-CLI.exe"

The script is read-only against the CS2 VPK. It writes reports to the Windows temp directory:
- stock-particles.txt: actual .vpcf_c resources in the base VPK.
- stock-soundevents.txt: actual .vsndevts_c scripts.
- stock-sounds.txt: direct .vsnd_c assets.
- all-matched-resources.txt and vpk-list-raw.txt: full matched list and raw CLI output.
- summary.json: counts and provenance.

## Follow-up

Use the verified paths to map AETHERION effects to existing stock effects when that visual is acceptable. This is a path-validation step only: it does not prove that an effect looks right at AETHERION's scale or that custom addon assets can be redistributed. For original VFX, author source .vpcf with the CS2 Particle Editor, compile it with Workshop Tools, then test on a private server.
