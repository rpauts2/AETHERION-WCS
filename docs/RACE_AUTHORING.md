# Race Authoring Guide

How to create and edit races in AETHERION WCS.

## File Format

Races are stored in JSON files:
- `configs/races.json` — extended races (IDs 2000+)
- `configs/races/races_core.json` — core races (IDs 1000+)

Both use the same schema.

## Race Schema

```json
{
  "id": 2000,
  "name": "Pudge",
  "tier": 1,
  "division": 1,
  "unlock_level": 1,
  "archetype": "tank",
  "lore": "Description text",
  "guild": false,
  "abilities": [...]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `id` | int | Unique ID (2000-2999 for extended, 1000-1999 for core) |
| `name` | string | Display name |
| `tier` | int | 1=Spark, 2=Combo, 3=Power, 4=Guild, 5=Mythic |
| `division` | int | 1-8 (gates availability by player division) |
| `unlock_level` | int | Player level required to unlock |
| `archetype` | string | tank, assassin, mage, support, etc. |
| `lore` | string | Flavor text |
| `guild` | bool | If true, only available to guild members |

## Ability Schema

```json
{
  "n": "Ability Name",
  "t": "Active",
  "max": 5,
  "d": "Description",
  "effect": "aoe_explosion",
  "value": 35,
  "cd": 14,
  "ether": 20,
  "radius": 250,
  "duration": 3,
  "angle": 60
}
```

| Field | Type | Description |
|-------|------|-------------|
| `n` | string | Ability name |
| `t` | string | `Passive`, `Active`, or `Ultimate` |
| `max` | int | Max skill level (typically 5-8) |
| `d` | string | Description |
| `effect` | string | Effect tag from EffectLibrary |
| `value` | float | Effect parameter (damage, heal amount, etc.) |
| `cd` | float | Cooldown in seconds (Active/Ultimate only) |
| `ether` | int | Ether cost (Active/Ultimate only) |
| `radius` | float | AoE radius (optional) |
| `duration` | float | Duration in seconds (optional) |
| `angle` | float | Angle for directional abilities (optional) |

## Effect Tags

### Damage
`aoe_damage`, `aoe_explosion`, `projectile`, `pierce_projectile`, `multishot`, `chain_lightning`, `lightning_strike`, `fireball`, `fire_breath`, `smite`, `counter`, `combo_slash`, `orbiting_blades`, `dash_damage`, `hp_cost_aoe`, `double_slash`, `silent_projectile`, `disarm_damage`

### CC (Crowd Control)
`stun_aoe`, `freeze_aoe`, `fear_aoe`, `root_aoe`, `slow_aura`, `slow`, `slow_on_hit`, `silence_shot`, `silence_aura`, `disarm`, `knockback`, `nova_knockback`, `aoe_knockback`

### Defensive
`shield`, `self_shield`, `ally_shield`, `mana_shield`, `damage_reduction`, `bullet_wall`, `death_save`, `revive`, `parry`, `evasion_chance`, `armor_buff`

### Healing
`heal_self`, `heal_target`, `heal_aura`, `team_heal`, `totem_heal`, `kill_heal`, `kill_heal_aura`, `lifesteal`, `lifesteal_aoe`, `soul_collect`, `summon_wisp`

### Movement
`dash_forward`, `blink`, `leap`, `speed_buff`, `speed_boost`, `haste`, `movement_speed`, `chase_speed`, `team_speed`, `low_gravity`

### Damage Modifiers
`crit_chance`, `berserk`, `berserk_self`, `lifesteal`, `Execute_low_hp`, `execute`, `execute_ult`, `execute_bonus`

### Utility
`invisibility`, `invisibility_combo`, `radar`, `trap`, `trap_remote`, `aoe_trap`, `mark`, `armor_break`, `armor_reduce`, `accuracy_debuff`, `blind_chance`, `death_save`

### DoT (Damage over Time)
`burn`, `fire_dot`, `poison_dot`, `ignite_aoe`

### Ultimate
`aoe_damage_ult`, `flash_ult`, `horror_ult`, `stun_ult`, `deafen_ult`, `clone_ult`, `drone_storm`

## SmartFallback

If an effect tag is not found in the EffectLibrary, the system tries to match keywords:
- `damage` → AoE damage + explosion particle
- `fire` → Burn DoT + fire particle
- `freeze` → Freeze debuff + frost particle
- `poison` → Poison DoT + smoke particle
- `stun` → Stun debuff + zap particle
- `slow` → Slow debuff
- `blink`/`dash` → Forward teleport + dash particle
- `heal` → Self-heal
- `shield` → Shield buff
- `lightning` → Beam + thunder particle
- `speed` → Speed buff
- `pull`/`knockback` → Knockback
- `fear` → Fear debuff

## Division Scaling

| Division | Ult Cooldown Range | Ult Value Range | Passive Max |
|----------|-------------------|-----------------|-------------|
| D1 | 35-50s | 20-40 | 5-8 |
| D2 | 40-55s | 25-50 | 5-8 |
| D3 | 45-60s | 30-60 | 5-8 |
| D4+ | 50-70s | 35-75 | 5-8 |

## Examples

### Simple Passive Race (D1)
```json
{
  "id": 2100,
  "name": "Stone Guardian",
  "tier": 1,
  "division": 1,
  "unlock_level": 1,
  "archetype": "tank",
  "lore": "Living stone that never breaks.",
  "abilities": [
    {
      "n": "Stone Skin",
      "t": "Passive",
      "max": 8,
      "d": "+10 HP per level",
      "effect": "bonus_hp",
      "value": 10
    },
    {
      "n": "Earthquake",
      "t": "Ultimate",
      "max": 1,
      "d": "Stuns all enemies nearby",
      "effect": "stun_aoe",
      "value": 35,
      "cd": 45,
      "ether": 60
    }
  ]
}
```

### Active Race with AoE (D2)
```json
{
  "id": 2200,
  "name": "Storm Caller",
  "tier": 2,
  "division": 2,
  "unlock_level": 5,
  "archetype": "mage",
  "lore": "Commands lightning from the aether.",
  "abilities": [
    {
      "n": "Static Charge",
      "t": "Passive",
      "max": 5,
      "d": "+15 damage to abilities",
      "effect": "bonus_hp",
      "value": 15
    },
    {
      "n": "Chain Lightning",
      "t": "Active",
      "max": 5,
      "d": "Lightning chains to 3 enemies",
      "effect": "chain_lightning",
      "value": 30,
      "cd": 12,
      "ether": 25
    },
    {
      "n": "Thunder Storm",
      "t": "Ultimate",
      "max": 1,
      "d": "Massive AoE lightning damage",
      "effect": "lightning_strike",
      "value": 50,
      "cd": 55,
      "ether": 70
    }
  ]
}
```

## Testing

After adding a race:
1. Run `dotnet build -c Release`
2. In-game: `!admin race info <id>` to verify
3. `!admin race reload` to hot-reload without restart
4. `!admin race save` to persist changes to JSON
