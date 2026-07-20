# AETHERION WCS

RCS2 server-side RPG mod for Counter-Strike 2 (CounterStrikeSharp/.NET 8). Port of WarCraft Source to Source 2.

## Requirements

- Counter-Strike 2 dedicated server
- Metamod:Source 2
- CounterStrikeSharp v1.0.362+
- .NET 8.0 runtime

## Installation

1. Copy `bin/Release/net8.0/AetherionWcs.dll` to `addons/counterstrikesharp/plugins/Aetherion/`
2. Copy `configs/` folder to `cfg/AETHERION/configs/` (relative to server root)
3. Restart server or `css_plugins load AetherionWcs`

## Features

### Race System
- **198+ races** across 8 divisions (D1-D8)
- 114+ unique effect tags in the EffectLibrary
- Abilities: Passive, Active, Ultimate
- Race mutations, guild races, mythic races

### Core Systems
- XP + level system with division-based scaling
- Economy (gold) + roulette (fair chances + pity)
- SQLite player storage (thousands of races per player)
- Battle Pass (free + premium tracks)
- Daily login streak rewards

### PvP/PvE
- Boss system (vote-to-summon, 5 boss tiers)
- Storm Wave PvE mode
- Duel arena with ELO
- Ether Rift events
- Guild tournaments

### Social
- Guild system (create, upgrade, donate, craft)
- Leaderboards (kills, levels, gold)
- Achievement system (30+ achievements)
- Contract system (daily objectives)
- Wisp companion (5 evolution stages)

### Admin Tools
- `!admin` panel (gold, VIP, levels, boss, storm)
- `!admin race` workshop (list, info, setdiv, settier, setcd, setval, setmax, setname, reload, save)

### Player Commands
| Command | Description |
|---------|-------------|
| `!wcs` | Main menu |
| `!races` | Race selection |
| `!rank` | Player stats |
| `!spin gold/eth/race` | Roulette |
| `!ult` | Ultimate ability |
| `!cast` | Active ability |
| `!sigil` | Draw sigil |
| `!shop` | Item shop |
| `!daily` | Daily reward |
| `!bp` | Battle Pass |
| `!guild` | Guild menu |
| `!contracts` | Daily contracts |
| `!ach` | Achievements |
| `!invest` | Level bank |
| `!unlock <id>` | Unlock race |

## Build

```bash
dotnet build -c Release
```

Output: `bin/Release/net8.0/AetherionWcs.dll`

## Project Structure

```
src/
  Core/           Main plugin (event hooks, commands, shop)
  Models/         PlayerData, RaceProgress
  Races/          RaceManager, RaceDefinition, RaceRuntime (EffectLibrary)
  Systems/        XP, Economy, Boss, Guild, Season, Combat, etc.
  Database/       SQLite storage (SqlitePlayerStore)
  Plugins/        AetherNexus, AetherRoulette, AetherSigils, SigilResonance, WispEvolution
  UI/             AetherHud (panorama-style HUD)
configs/
  races.json      Extended races (198+)
  races/races_core.json  Core races (120)
  seasons.json    Season + Battle Pass config
```

## Race Authoring

See [docs/RACE_AUTHORING.md](docs/RACE_AUTHORING.md) for how to create custom races.

## License

Internal project. Not for redistribution.
