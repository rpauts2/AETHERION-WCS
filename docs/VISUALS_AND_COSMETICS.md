# Visuals, Custom Skins, Effects, and Menus

This guide documents the current AETHERION visual pipeline and the new race-specific visual hooks.

## What is supported by the plugin code

- **Player models:** `configs/assets/models.json` maps a race ID to a base CS2 model or a custom `.vmdl`.
- **Render tint:** optional RGB tint and glow value can be set per race.
- **Division aura:** the default aura is selected from the player's division.
- **Race aura / accessory:** optional model paths can be configured per race. These are attached to the player's pawn and removed on disconnect or replaced on respawn.
- **Spawn particle:** optional `fx` particle path is emitted once at spawn. This is a burst, not a continuous particle trail.
- **Cosmetic loadout menu:** `!cosmetics` opens the WASD menu for titles, name colors, and seasonal trails; the main `!wcs` menu also links to it.

## Configure a race model

Edit the race entry in `configs/assets/models.json` using the numeric race ID as the key:

```json
"2025": {
  "race": "Искровой маг",
  "division": 1,
  "baseModel": "characters/models/tm_phoenix/tm_phoenix.vmdl",
  "customModel": "",
  "fx": "",
  "aura": "",
  "accessory": "",
  "tint": [255, 240, 80],
  "glow": 1
}
```

- Keep `baseModel` set to a known-good CS2 model as a fallback.
- Set `customModel` to the installed custom model's compiled `.vmdl` path. PNG concept art and FBX source files are not directly loadable as player models.
- Set `aura` and `accessory` only to valid compiled model paths that are present on the server and available to clients.
- Set `fx` to a valid particle resource path. The plugin precaches this resource and emits it once when the player spawns.
- After changing resources, restart the server and check the console for model/particle errors.

## Custom skins: asset pipeline

1. Create or obtain a model that is licensed for your server's use.
2. Rig and export it for the CS2 character skeleton, preserving the expected bone names and compatible animation setup.
3. Compile it using the appropriate Source 2 asset tools into a `.vmdl` plus all required materials, textures, and animation resources.
4. Install the complete compiled asset tree under the server's game content path, not just the model file.
5. Add the compiled model path to the race's `customModel` field.
6. Test both CT and T sides, spawn, death, weapon holding, crouch, and common animations on a private server before shipping it.
7. Keep a valid base-model fallback so a missing custom asset does not leave the race without a configured model.

The repository contains concept images and some source assets. Those are references or source files, not proof that a complete, compiled, client-downloadable CS2 skin is ready to use.

## Particle effects

Use only particle resources present in the server's CS2 content. Paths already referenced by the code include:

- `particles/burning_fx/env_fire_small.vpcf`
- `particles/burning_fx/env_fire_medium.vpcf`
- `particles/explosions_fx/explosion_smoke_grenade.vpcf`

These paths are examples from the existing codebase, not a guarantee that every effect fits every race. Test the particle in-game and verify that it cleans up naturally. Spawn FX are deliberately one-shot; do not configure a long-running particle as a spawn burst unless the resource is designed for that use.

## Cosmetic menu

The in-game entry point is `!wcs` → **Внешний вид**, or `!cosmetics`.

- Titles and name colors use the existing level/division/achievement/guild unlock rules.
- Seasonal trails require the relevant season reward to be owned.
- The menu re-renders the player's nameplate after a successful selection.
- Badges are currently catalog/display cosmetics; a separate equip/display path may be needed before they appear on the player.

## Safety and performance rules

- Do not spawn persistent particles on every tick. Use an entity/controller with explicit creation and cleanup if a continuous aura or trail is required.
- Remove attached entities on disconnect and when changing the configured model.
- Keep the number of visible props and particle systems bounded; many simultaneous emitters can hurt server/client performance.
- Never overwrite the only copy of a model or database. Back up configs and player data before deployment.
- Verify custom asset redistribution rights and avoid shipping third-party models without permission.

## Next visual milestones

1. Add persistent, lifecycle-managed particle trails with per-player opt-out.
2. Add an explicit skin unlock/equip catalog backed by player ownership, rather than changing the race model globally.
3. Add preview and ownership status to the cosmetic menu.
4. Add an asset validator that checks configured model paths and referenced particle files before release.
5. Add server-side visual-effect rate limits and a global maximum for attached props.
