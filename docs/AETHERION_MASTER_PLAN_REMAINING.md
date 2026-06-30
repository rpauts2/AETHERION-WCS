# AETHERION-WCS: Remaining Master Execution Plan
## Goal
Make the server unique and so attractive that players choose it over competitors.
Key pillars: 25 custom races with unique ultimates, active wisp companion, nameplates,
custom weapon skins + restrictions, boss events, battle/season pass, achievements,
VIP/admin systems, deep Source2 SFX/VFX, perfect ru/en localization.

## Stream A — Races, Wisps, Nameplates, Weapons
### A1. Division/Race balance rework and unique ultimates (id=a1)
Deliverables:
- docs/races/DIVISIONS_AND_RACES_DESIGN.md: 25 races across 8 divisions.
- For each race in configs/races.json add/verify:
  - Division 1..8 unlock thresholds.
  - Unique passive, active ability and signature ultimate.
  - Explicit counters, weaknesses and playstyle blurb.
  - Balanced stat curves (HP, speed, armor).
- Implement ultimate exceptions/special rules as AbilityContext flags.
- Validate with dotnet build -c Release.

### A2. Wisp v2 companion (id=a2)
Deliverables:
- src/Systems/WispCompanionV2.cs: spirit model attached to player, follows owner.
- Skin system: store cosmetic skins for wisp tied to owner inventory.
- Active abilities: ambient aura, on-kill burst, ultimate_assist switcher.
- Persistent bond data in PlayerData, UI preview in AETHER NEXUS.

### A3. Nameplates (id=a3)
Deliverables:
- Add nameplate overlay via AuraManager or dedicated NameplateRenderer.
- Show race icon + level near player head.
- Vip glow effect on nameplate for active VIP players.

## Stream B — Events & Progression
### B1. BossSystem full cycle (id=b1)
Deliverables:
- src/Systems/BossSystem.cs (full version): vote -> spawn -> HP scaling -> rewards -> loot -> boss skins.
- events/boss_events.json with boss archetypes, HP curves, rewards.
- Source2 particle and sound hooks for spawn/damage/death.
- Admin command to force-spawn boss.

### B2. Battle / Season Pass (id=b2)
Deliverables:
- Expand SeasonSystem to seasons.json with tiers, rewards, exclusive skins.
- Track season XP per player, auto-claim mechanics.
- Pass-exclusive cosmetics stored in PlayerData inventory.
- Localization keys for pass UI.

### B3. Achievements and dailies (id=b3)
Deliverables:
- src/Systems/AchievementSystem.cs: milestones (100 kills with race, boss kills, round wins).
- Daily challenges with rewards (gold, XP, free spins).
- Celebration VFX/sound on unlock, chat announcement.

## Stream C — Social & Economy
### C1. Vip/VipMenu (id=c1)
Deliverables:
- VipMenu fully operational: status view, time remaining, daily bonus path.
- Integration with EconomySystem: XP/Gold multipliers, queue skip flag.
- Vip-exclusive cosmetics/skins in shop.

### C2. Admin/AdminMenu (id=c2)
Deliverables:
- AetherionPlugin OpenAdminMenu: player selector.
- Grant levels/gold, kick/ban hooks, server stats dashboard.
- Audit log for admin actions in /addons/counterstrikesharp/logs/aetherion_admin.log.

## Stream D — Polish & Release
### D1. Source2 sounds and particles (id=d1)
Deliverables:
- configs/assets/sounds.json: categories for races, abilities, boss, UI.
- SoundManager triggers: levelup, ult cast, boss death, storm start.
- ParticleManager: utf-8 .vpcf paths for race-specific auras, ultimates, boss death.

### D2. Custom weapons + restrictions (id=d2)
Deliverables:
- per-race weapon_whitelist in RaceDefinition.
- WeaponSkinManager applying custom models/tints per race.
- Admin override command css_admin_weapon.

### D3. Localization cleanup (id=d3)
Deliverables:
- Complete lang/ru.json and lang/en.json for all systems.
- JSON validation with python -m json.tool.

### D4. Integration and final build (id=d4)
Deliverables:
- Audit RaceRuntime and CombatEffects for desync and overheating.
- Optimize HudTick/WheelTick.
- dotnet build -c Release must give 0 errors.
- Deploy AetherionWcs.dll to CS2 server plugins path once found.