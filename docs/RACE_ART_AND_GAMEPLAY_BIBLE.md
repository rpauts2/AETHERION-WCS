# AETHERION WCS: Race Art & Gameplay Bible

This document is the quality bar for hand-authored race work. A race is not complete just because its JSON parses.

## Content rule

Every race gets its own:
- fantasy and gameplay promise;
- ability loop, including a reason to use each ability;
- visual palette and readable combat language;
- model silhouette / accessory brief, plus a separate technical asset status;
- balance envelope and automated checks.

Never call a recolored stock model a custom model. A custom CS2 player model is only complete after its Source 2 model, materials, required animations, packaging/distribution and in-game import have been checked.

## First bespoke pass: Хрономант (ID 2031)

**Fantasy:** a duelist-mage who steals seconds, controls the tempo of a skirmish and survives one bad trade. The theme is violet chronal glass, thin concentric rings, fractured clock-hand shapes and delayed afterimages. Avoid generic blue lightning and generic fire explosions.

### Ability kit

1. **Искажение такта** (Active): local 420-unit pulse, slows nearby enemies for 3 seconds. Intended opener to set up a reposition or team focus.
2. **Петля времени** (Active): recovers a bounded amount of health and grants a short shield. This is deliberately a survival reset, not a fake resurrection or position rewind.
3. **Ускорение** (Active): short movement-speed window for disengage or chase. It does not claim to reduce other cooldowns until cooldown reduction is actually implemented.
4. **Разрыв времени** (Ultimate): bounded 900-unit crowd-control burst with damage. It is not a map-wide hard-freeze.

### Visual brief

- Primary: amethyst/violet; secondary: pale silver; accent: cold white.
- Silhouette: long split coat, asymmetrical shoulder mantle, a floating broken-hourglass / ring motif behind the upper back, thin luminous seams. Keep the silhouette legible at CS2 match distance.
- Skill VFX: concentric rings and thin violet beams for the slow field; a rewind spiral and brief glass-like pulse for the self reset; narrow afterimage streaks for haste; a radial clock-shatter pulse for the ultimate.
- Avoid screen-filling opaque particles that obscure opponents or crosshair readability.

### Implementation status

- Config and explicit effect handlers are implemented on the development branch.
- The current engine API does not expose historical player-position snapshots or a verified death-rewind hook here. Therefore the first pass does **not** claim resurrection or teleport-to-past-position.
- The current model entry still uses a stock CS2 model as a safe fallback. The visual brief is not a compiled Source 2 model. Custom model production and live-server review remain separate tasks.
- Static build/test success does not prove that each particle path or status effect looks correct in a live CS2 match.
