# AETHERION asset shortlist: next practical intake

Updated: 2026-11-10  
Branch: improve/production-roadmap

This shortlist is ordered by usefulness and practical Source 2 conversion risk, not just visual appeal. All linked models are source assets until compiled and tested in CS2.

## Priority A: first real import batch

| Candidate | Contents | License | Why useful | CS2 work still required |
|---|---|---|---|---|
| [Gobkit Free Minions](https://gobkit.itch.io/gobkit-free-minions) | 8 rigged enemies, idle/attack/dead, 24 FPS | CC0 1.0 | Small reusable PvE enemies | Inspect one GLB, split timeline if necessary, retarget/import and compile |
| [Gobkit Free Animal Pack Vol.5](https://gobkit.itch.io/gobkit-free-animal-pack-vol-5) | 10 creatures, idle/attack/dead/walk, 24 FPS, 525 KB ZIP | CC0 1.0 | Best low-risk intake and animation test | Validate skeleton, materials and exact clip names; create an addon model |
| [Gobkit Dinosaur Pack](https://gobkit.itch.io/gobkit-free-dinosaur-pack) | 10 rigged dinosaurs, 8.9 MB ZIP | CC0 1.0 | Strong WCS boss/add/companion pool | Inspect per-model topology, animations, scale and collision before server use |
| [KayKit Skeletons](https://kaylousberg.itch.io/kaykit-skeletons) | 4 rigged low-poly skeletons, 90+ animations, 10+ accessories | CC0 1.0 | Readymade undead race and weapon/armor family | Confirm animation skeleton split and port to CS2 player-agent rig |
| [Gobkit Character Pack Vol.3](https://gobkit.itch.io/gobkit-free-character-pack-vol-3) | 8 fantasy human NPCs, 7 named clips at 30 FPS | CC0 1.0 | Guards, archers, footmen and priestess | Rig compatibility and player-agent port are not guaranteed |
| [Polyy.AI Fantasy Weapons](https://polyyai.itch.io/3d-fantasy-weapons-pack) | 25 PBR weapons; 4k-12k tris in low-poly ZIP, 57 MB | CC0 1.0 | Swords, axes, bows, scythes, staffs and spears | Set pivots/grips, UV/material validation and Source 2 compile |
| [Polyy.AI Low-Poly Guns](https://polyyai.itch.io/3d-low-poly-guns-pack) | 25 PBR weapons; 6k-10k tris, 43 MB low-poly ZIP | CC0 1.0 | Sci-fi and fantasy firearms for custom weapon prototypes | Weapon-specific grip, attachment points, animation compatibility and model compile |

### Asset notes

Gobkit character packs include a shared retarget-friendly humanoid skeleton and named clips. The creature/animal GLBs use skinned rigs and should be cloned/retargeted correctly; loading in a generic GLB viewer does not prove it can animate as a CS2 agent.

KayKit's free tier is four characters, while two more skeletons exist only in the paid extra tier. Its CC0 page says no attribution is required but asks users not to resell unmodified copies.

Polyy.AI labels its artwork AI-assisted. The source page declares CC0 1.0 and permits modification and commercial use. It also says not to resell raw unmodified copies. Review distribution-platform AI-content disclosure rules before shipping.

## Priority B: audio and VFX

| Candidate | Contents | License | Best use | Status |
|---|---|---|---|---|
| [SFXMint Dragon Roar 03](https://sfxmint.com/sounds/dragon-roar-03) | 3.8-second boss roar, MP3 | CC0 1.0 | Dragonborn/boss intro and roar | One-command Windows installer prepared; not run here |
| [Kenney Particle Pack](https://kenney.nl/assets/particle-pack) | 80 sprite/light/shader textures | CC0 | Textures for fire, smoke, impact and magic VPCFs | Requires Particle Editor authoring |
| [Kenney Smoke Particles](https://kenney.nl/assets/smoke-particles) | Smoke/explosion sprite sheets | CC0 | Teleport, explosion and impact dust | Requires Particle Editor authoring |
| [PVFX Foundry](https://nerijs.itch.io/pvfx-foundry) | 101 pixel-art effect sprites | CC0 claimed on creator page | Icon/UI previews or sprite-texture source | 2D only; not a VPCF resource |

## Import gate

1. Preserve the original archive and license evidence.
2. Calculate SHA-256; keep the source file and a provenance receipt.
3. Inspect actual topology, UVs, materials, skeleton and every animation.
4. Normalize and retarget in Blender only where required; never edit the source in place.
5. Build model/animation and VPCF resources in CS2 Workshop Tools.
6. Verify server precache, client mounting, visual scale, attachment points and performance on a private server.
7. Only then enable the asset in configs/assets/models.json, configs/assets/sounds.json or ability definitions.

A 3D model with a valid CC0 license is not automatically a working CS2 player agent. A PNG effect sheet is not automatically a VPCF. A source WAV/MP3 is not runtime-ready until Workshop Tools compiles it.


---

## New 2026-11-10 additions: animation-first intake

| Candidate | Contents | License | Recommended use |
|---|---|---|---|
| [Kenney Protagonists](https://kenney.nl/assets/animated-characters-protagonists) | 3D animated pack, 8 files | CC0 | First retarget test for human NPCs |
| [Kenney Retro](https://kenney.nl/assets/animated-characters-retro) | 3D animated pack, 8 files | CC0 | Zombies/survivors and undead waves |
| [Kenney Survivors](https://kenney.nl/assets/animated-characters-survivors) | 3D animated pack, 8 files | CC0 | Survival NPCs and enemy variants |
| [Quaternius Universal Animation Library](https://quaternius.com/packs/universalanimationlibrary.html) | 120+ clips on one humanoid rig; only 60–70% in the free version | CC0 | Locomotion/combat/emotes/death retarget library |
| [Quaternius Ultimate Animated Character Pack](https://quaternius.com/packs/ultimatedanimatedcharacter) | 52 animated and textured models | CC0 | Broader humanoid race/NPC pool |
| [Quaternius RPG Character Pack](https://quaternius.com/packs/rpgcharacters.html) | 6 rigged and animated fantasy characters | CC0 | First fantasy agent prototypes |
| [Quaternius Modular Weapons Pack](https://quaternius.com/packs/medievalweapons.html) | 24 weapon models | CC0 | Race equipment, shop props, cosmetic weapons |

All seven are source assets only. Retargeting, material conversion, Source 2 model compilation, precache and private-server verification remain required. The first model should be chosen by exact rig and animation evidence, not by the number of assets in the pack.


---

## Dungeon, VFX and sound additions

| Candidate | Contents | License | AETHERION use | Import risk |
|---|---|---|---|---|
| [KayKit Dungeon Pack 1.1 Free](https://kaylousberg.itch.io/kaykit-dungeon-pack) | 200 modular props, walls, floors, doors, stairs and furniture, one atlas | CC0 | Boss arenas, dungeon map, lobby | Low-medium; collision, atlas and scale |
| [SigilsVault Modular Dungeon Kit](https://sigilsvault.itch.io/modular-dungeon-kit-v10) | 90+ pieces, GLB/FBX, shared trim sheet, 1 m grid | CC0 | Dark fantasy arenas and corridors | Medium; map assembly and collision |
| [Quaternius Modular Dungeon](https://quaternius.com/packs/medievaldungeon.html) | 41 dungeon models | CC0 | Torches, potions, barrels and walls | Low-medium |
| [Quaternius Modular Dungeons](https://quaternius.com/packs/modulardungeon.html) | 48 modular dungeon models | CC0 | Alternate set for map variety | Low-medium; overlap with 41-model pack |
| [SpriteCanvas effects](https://spritecanvas.com/free-sprite-sheets/effects/) | 6 transparent sprite sheets, 6 frames each, JSON atlas | CC0 | Slash, hit, fire, lightning, explosion, ice VPCF texture source | Medium; must author VPCF |
| [SpritePilot SFX](https://spritepilot.com/free-game-assets/sound-effects) | 78 sounds, 8 seamless ambient loops, WAV/OGG/MP3 | CC0 | Dragon, combat, abilities, UI, ambient | Low source intake; VSND compile + playback gate |

The environment kits are suitable for map/arena construction, not player-agent models. The SpriteCanvas sheets do not create particle definitions by themselves, and SpritePilot sounds do not become usable runtime resources until the Source 2 sound pipeline is run.
