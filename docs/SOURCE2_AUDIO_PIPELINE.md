# AETHERION Source 2 audio pipeline

Updated: 2026-11-10

## Correct Source 2 layout and runtime path

Source 2 addon source content belongs in content/csgo_addons/aetherion. Compiled game resources belong in game/csgo_addons/aetherion.

For audio:
- Source WAV/MP3 files belong under content/csgo_addons/aetherion/sounds/.
- Workshop Tools compiles them into game/csgo_addons/aetherion/sounds/... .vsnd_c resources.
- The virtual runtime path is sounds/... .vsnd, not sound/... .wav.
- Sound event definitions, when needed, are authored under soundevents/ as .vsndevts and compiled to .vsndevts_c.

The CounterStrikeSharp example uses ExecuteClientCommand("play sounds/ui/counter_beep.vsnd") for direct addon sound playback. Stock game sound events are a different mechanism and should be emitted through the player's EmitSound(soundEventName) API.

Sources:
- https://www.source2.wiki/Basics/working-on-content/directory-layout
- https://s2v.app/ValveResourceFormat/guides/exporting-sounds.html
- https://docs.cssharp.dev/api/CounterStrikeSharp.API.Core.CCSPlayerController.html
- https://docs.cssharp.dev/api/CounterStrikeSharp.API.Core.CBaseEntity.html

## AETHERION changes

- All 71 non-empty customSound fields in configs/assets/sounds.json now use Source 2 virtual runtime references in sounds/*.vsnd form (68 unique paths).
- AudioManager now emits stock sound event names with EmitSound and sends direct addon VSND paths through the client play command.
- The customSound override takes priority over the built-in default, rather than silently being ignored.
- scripts/audit_audio_assets.py checks both source audio in content/csgo_addons/aetherion/sounds and compiled VSND_C output in game/csgo_addons/aetherion/sounds.
- CI now keeps audio source/compiled resource presence separate from the C# build result.

## First real sound sample

scripts/install_cc0_dragon_roar.ps1 is a one-shot installer for SFXMint's Dragon Roar 03:
1. Downloads the source MP3 from the creator's public library and verifies it looks like an MP3.
2. Captures a SHA-256 and a CC0 provenance receipt.
3. Copies the source into the AETHERION addon content/sounds/race directory.
4. Runs Workshop Tools resourcecompiler and requires the expected .vsnd_c output.
5. Adds the sounds/race/dragon_roar.vsnd customSound mapping for race 2033 (Dragonborn) and backs up sounds.json before the edit.

The source publisher says the public SFXMint library is AI-generated or procedurally synthesised. If the game is distributed, review platform AI-content disclosure rules.

The installer has not been run in this environment because it has no local CS2 checkout or Workshop Tools. After running it locally, verify actual playback on a private server and confirm clients mount the AETHERION addon.

## Current CI-verified baseline

- 18 Python tests passed.
- 30 .NET tests passed.
- Release build succeeded with zero build errors.
- The audit currently reports 68 unique customSound runtime paths missing both source and compiled files; this is expected until actual audio files are staged and compiled.
- The audit does not prove soundevent behaviour, client download, or audible playback.
