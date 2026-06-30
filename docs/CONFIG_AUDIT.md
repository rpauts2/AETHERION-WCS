# AETHERION-WCS Config Audit

**Date:** 2026-06-27  
**Configs reviewed:** `configs/races.json`, `configs/assets/sounds.json`, `configs/assets/models.json`

---

## 1. `configs/races.json` Structure

| Metric | Value |
|--------|-------|
| Total races | 198 |
| Race ID range | 2000 – 2197 |
| Divisions used | 8 (1–8) |
| Archetypes | legacy (85), warcraft (97), starcraft (6), dota (8), tf2 (2) |

### Abilities

| Type | Count |
|------|-------|
| Passive | 460 |
| Active | 431 |
| Ultimate | 186 |
| **Total** | **1,077** (avg ~5.4 per race) |

### Unique ability effects (31 total)
- `low_gravity` (41), `invisibility` (62), `speed_buff` (81), `bonus_hp` (113),
  `death_save` (43), `regen` (31), `shield` (51), `hook_pull` (19), `aoe_damage` (59),
  `totem_heal` (15), `teleport_spawn` (31), `root_aoe` (28), `stun_aoe` (47),
  `dash_forward` (58), `summon` (10), `team_heal` (27), `freeze_aoe` (26),
  `aoe_explosion` (27), `ignite_aoe` (42), `berserk` (69), `fear_aoe` (31),
  `team_speed` (10), `slow_aura` (28), `heal_self` (16), `lifesteal` (23),
  `lightning_strike` (21), `soul_collect` (15), `poison_dot` (30),
  `nova_knockback` (12), `chain_lightning` (8), `lifesteal_aoe` (3)

### Checks
- All 198 races have `name`, `tier`, `unlock`, `lore`, `division`, and `abilities` fields.
- No structural config issues detected in `races.json`.

---

## 2. `configs/assets/sounds.json` Coverage

| Metric | Value |
|--------|-------|
| Total entries | 88 |
| Missing from `races.json` | All 88 IDs (1000–1087) |
| Extra IDs not in `races.json` | None |
| All entries have base `sound` | ✅ |
| Custom sounds filled | 0 of 88 |

### Assessment
`sounds.json` covers IDs **1000–1087**, which do **not** overlap with `races.json` IDs (**2000–2197**).  
All entries reference standard CS2 sound paths (e.g., `Weapon_HEGrenade.Explode`, `Physics.WaterSplash`), so the sound table appears consistent internally, but it is **not linked to any race in `races.json`**.

There are two possibilities:
1. IDs 1000–1087 belong to a different/wip race roster not included in this audit scope, or
2. `sounds.json` is stale/out-of-sync with `races.json`.

---

## 3. `configs/assets/models.json` Coverage

| Metric | Value |
|--------|-------|
| Total entries | 110 |
| Missing from `races.json` | All 110 IDs (1000–1109) |
| Extra IDs not in `races.json` | None |
| Missing `baseModel` | 0 |
| Missing `fx` | 0 |
| Missing `aura` | 0 |
| Division mismatches vs `races.json` | 0 |
| With `customModel` | 0 |
| With `accessory` | 8 |

### Assessment
`models.json` covers IDs **1000–1109**, also **not overlapping** with `races.json` IDs.  
Coverage is internally complete — every entry has `baseModel`, `fx`, and `aura`.  
Auras scale by division (`aura_d1`–`aura_d3`, `aura_dinf`), which is consistent.  
Eight races have an `accessory` model (all in the 1000s range).

Same conclusion as sounds: `models.json` appears valid on its own but **disconnected from `races.json` keyspace**.

---

## 4. Cross-file Issues Identified

| # | Issue | Severity | Details |
|---|-------|----------|---------|
| 1 | ID namespace gap between `races.json` and assets | **High** | `races.json` uses IDs 2000–2197; `sounds.json` uses 1000–1087; `models.json` uses 1000–1109. There is no shared key to associate models or sounds with races. |
| 2 | `sounds.json` orphaned from `races.json` | **High** | 0 of 198 races in `races.json` have a corresponding sound entry. |
| 3 | `models.json` orphaned from `races.json` | **High** | 0 of 198 races in `races.json` have a corresponding model entry. |
| 4 | `customSound` never populated | **Low** | All `sounds.json` `customSound` fields are empty strings. |
| 5 | `customModel` never populated | **Low** | All `models.json` `customModel` fields are empty strings. |

### Recommended Fixes
1. **Align IDs:** Decide whether `races.json` should renumber to 1000+, or whether `sounds.json`/`models.json` should be extended with 2000+ entries.
2. **Add association:** Introduce an `assets` block or explicit `soundId`/`modelId` fields inside each race entry to point to the correct asset records.
3. **Fill custom assets incrementally** as true custom models and sound banks are produced.

---

_End of audit._
