# AETHERION asset import audit

Audit date: 2026-11-10  
Working branch: improve/production-roadmap

## What is in the repository now

The branch contains a large gb_models/extracted collection of precompiled Source 2 models and materials. These files are not automatically approved for reuse: a README that tells users where to unzip a model does not establish redistribution or derivative-work rights. Keep this collection quarantined until each package has a creator, source page, license terms and redistribution permission recorded.

The tracked tree currently contains:

- 41 compiled .vmdl_c model files;
- 52 compiled .vmat_c material files;
- 180 compiled .vtex_c texture files;
- no .vpcf source particle files and no .vpcf_c compiled particle files;
- a rigged FBX under assets/skins and another FBX in rigwork, plus a small GLB rig-work artifact.

The code and configuration refer to project-owned particle paths including particles/aether_dash.vpcf, particles/aether_thunder.vpcf, particles/aether_explosion.vpcf, particles/aether_recall.vpcf, particles/aether_fire.vpcf and particles/aether_cast.vpcf. They are not backed by particle resources in this repository. Some abilities therefore call particles that are not present as owned resources here.

Stock CS2 paths such as particles/burning_fx/... are game-depot resources and are deliberately not treated as missing repository files.

## What can be staged safely

The source registry configs/assets/asset_sources.json records CC0 candidates. The Windows fetcher scripts/fetch_cc0_assets.ps1 downloads five source archives into assets/incoming/cc0. It retains original ZIP files, checks that downloads are non-trivial, calculates SHA-256, writes per-package provenance receipts and rejects ZIP path traversal and existing destinations.

The fetcher deliberately does not copy anything into content/csgo_addons/aetherion, change models.json, or pretend that a GLB/PNG is already a Source 2 model or particle. This is safe source intake, not final game installation.

### Initial intake set

| Source | Inventory | Declared license | Best next use |
|---|---:|---|---|
| Cethiel's Dragon 3D | Rigged/animated dragon source; inspect exact archive contents | CC0 | Dragon character or summoned creature prototype |
| Gobkit Animal Pack | 10 rigged GLB creatures, idle/attack/dead/walk timeline | CC0 1.0 | Summoned creatures, neutral wildlife, boss adds |
| Gobkit Minion Pack | 8 rigged GLB enemies, idle/attack/dead timeline | CC0 1.0 | PvE minions and enemy waves |
| Kenney Particle Pack | 80 particle/light/shader sprites | CC0 | Texture source for custom VPCF effects |
| Kenney Smoke Particles | 77 sprites across five styles | CC0 | Smoke, blast, teleport and impact VPCF textures |

Gobkit's official manifest describes GLB units in metres, forward +Z and up +Y. Each rigged model has one master animation timeline at 24 FPS; individual actions must be split using the frame ranges in the manifest. A GLB is still not a CS2 player-agent resource.

## New static guardrail

scripts/audit_asset_references.py scans only project-owned AETHERION particle namespaces, excluding stock game resources. CI can generate a report without failing the build; maintainers can opt into --fail-on-missing once all custom particle resources have been authored and compiled.

This audit checks repository file presence only. It does not prove that a VPCF compiles, precaches, renders or is networked to clients.

## Required import gate

1. Inspect source topology, materials, skeleton and every animation clip.
2. Normalize/retarget in Blender where required.
3. Author VPCF files in the CS2 Particle Editor for VFX.
4. Compile source assets using Counter-Strike 2 Workshop Tools.
5. Verify server precache and client delivery on a private CS2 server.
6. Visually review spawn timing, attachment, scale, performance and cleanup.

## Current execution blocker

This work environment has GitHub text-file access but no local AETHERION checkout, no Counter-Strike 2 Workshop Tools/resourcecompiler, no Blender, and no installed game-dev asset CLI. Binary source archives cannot be downloaded into this session or compiled here. The fetch script is committed for execution on the Windows machine that has the repository and game tools.


## Additional audio gap found

The sounds.json configuration contains 175 records. Of these, 71 records set customSound and they resolve to 68 unique paths. Every unique path currently uses the source-looking form sound/...wav. The tracked repository contains no WAV/MP3/OGG/FLAC sources and no compiled VSND_C audio assets.

AudioManager.Execute passes the selected string directly to the client's play command. The CounterStrikeSharp upstream TestPlugin sample uses a Source 2 resource path such as sounds/ui/counter_beep.vsnd. Source 2 Viewer documents compiled audio resources as VSND_C under sounds/ or sound/ directories. That means the existing customSound entries need a deliberate source-to-runtime path review and actual audio packages; simply downloading WAVs will not complete the feature.

Reference implementation and format notes:
- CounterStrikeSharp sample: https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/TestPlugin/TestPlugin.cs (css_sound command)
- Source 2 audio resources: https://s2v.app/ValveResourceFormat/guides/exporting-sounds.html
- Source 2 sound-event editor: https://www.source2.wiki/ExternalTools/Hammer5Tools/editors/sound-editor

No sound paths were bulk-rewritten because there are no compiled custom sounds to test against the running client. The expanded CI audit now reports both missing custom audio and missing custom particle resources.


## Gobkit intake has been made dynamic

The Windows source-intake script now reads the official no-auth JSON manifest instead of hard-coding only two Gobkit ZIP addresses. It:
- snapshots the manifest used for that download run;
- accepts only packs whose manifest license string explicitly contains CC0 1.0;
- accepts ZIP URLs only over HTTPS from the gobkit.com host;
- records pack metadata (kind, GLB format, count, rigged flag, FPS and animation names);
- preserves the raw ZIP and a SHA-256 receipt;
- uses a path-traversal-safe ZIP extractor and never overwrites an existing extracted package.

Important freshness caveat: the API snapshot currently advertises version 2026-07-23 and enumerates Minion, Animal, Animal Vol. 2 and Nature Kit packages. The current freebies landing page describes a newer 79-model count and the creator devlog mentions additional later packs. The intake script downloads only the packs present in the live API response at execution time; it does not claim the API fully mirrors every newer page/devlog item.

The script is still source intake only. It does not place GLB files in the CS2 game directory, retarget skeletons, compile models or run a server test.
