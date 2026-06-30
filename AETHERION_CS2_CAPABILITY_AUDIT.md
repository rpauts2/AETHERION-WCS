# AETHERION WCS — CounterStrikeSharp / CS2 Capability Audit Cheat-Sheet
_Date: 2026-06-27_  
_Focus: listen vs dedicated constraints, Panorama voting/menu limits, OnTick precision, model/sound/particle API, worldtext, hud/centerhtml, input binding limits, plugin reload constraints._

---

## 1. Server Type Constraints (Listen vs Dedicated)

| Constraint | CS2/CounterStrikeSharp reality | Current project status / usage |
|---|---|---|
| **Tick / sim determinism** | Official CS2 run-at 64 tick. Sub-tick improves input precision, but plugin tick code still runs on the server frame, not per-subtick. Plugin timers are not bind-guaranteed. | AETHERION uses: `OnTick` via `RegisterListener<Listeners.OnTick>(…)`, `AddTimer(0.5f, …, REPEAT)` for Rift, `AddTimer(1.0f, …, REPEAT)` for Ether regen, and `AddTimer(0.1f, …, REPEAT)` for Wisp follow. |
| **Server vars / hibernation** | `sv_hibernate_when_empty` can pause sim on listen/ded when empty, breaking periodic timers. | No explicit support in repo. |
| **sv_pure 1/2** | `sv_pure 1` allows whitelist-time override patches; `sv_pure 2` applies strict enforced checks (affects custom models/sounds). | Models and sounds map is written as config-driven but no `pure_server_whitelist` handling observed. |
| **Listen vs dedicated diff** | Listen servers run the same game binary but with additional frame dependencies. | No listen/dedicated detection in code.

---

## 2. Panorama Voting / Menu Limits

| Constraint | Current project usage / evidence |
|---|---|
| **Queueing** | Custom voting logic (e.g., via `PanoramaVote`) exists as an optional API; PanoramaVoteManager queues one vote at a time and requires cooldown. |
| **Custom vote text** | Custom strings require `platform_english.txt` on both server and clients. |
| **CS2MenuManager / WasdMenu usage** | Extensive use of `CS2MenuManager` (WasdMenu, `Display(…, 0)`) and `PanoramaVote` with fixed `20` seconds display; no adaptive width or pagination implemented. |
| **Voting availability** | Vote display uses `vote.Disp` default; no fallback to native vote vs mod menu when CS2 native vote is disabled. |

---

## 3. OnTick Precision & Timing

| Constraint | Current project references | Includes |
|---|---|---|
| **CS2 tick rate reality** | 64 tick = ~15.6ms/server step. Subtick interpolation means client feels snappier than plugin-side view. | subtick + precision timers docs |
| **Framework overhead** | CounterStrikeSharp 1.0.352 regressions reported for >22–24 players. | CSSharp perf + tick + atomic lock constraints |
| **Codebase evidence** | 1+ `RegisterListener<Listeners.OnTick>` calls, plus multiple `AddTimer` patterns: `0.5f`, `1.0f`. | `AetherionPlugin.cs`, lifecycle, Wisp follow (0.1f), and particle damage tick (1s). |

---

## 4. Model / Sound / Particle API

| API | Evidence in repo | Known CS2 constraints |
|---|---|---|
| **Model set + render state** | `SetModel`, precache via `ResourceManifest`, `SetRenderTint` mods `m_clrRender`. | Needs `sv_pure` consideration; invalidation can occur on reconnect/team switch; `SetStateChanged` required for netvar broadcast. |
| **Particle system** | `info_particle_system` (CParticleSystem) spawned per-effect, `StartActive`, `AcceptInput("Start")`, no auto-removal. | Each spawn creates an entity; cleanup must be timer-based or `Server.NextFrame`-style. |
| **Beam effect** | `env_beam` used for visual beam primitives. | Entity lifetime, outfit rules, team color restrictions. |
| **Sound playback** | `ExecuteClientCommand("play <sound>")` uses in-game soundevent names (no external file loading). | Only in-game soundevents included; limited to one channel/file per `play`; no overlapping mix control. |
| **Ignite / fire FX** | Fire implemented via a VPCF particle; burn DoT ticked in `CombatEffects.TickAll()` per second. | No real `ignite` flame entity (replaced by particle). |
| **Accessory parent** | Auras/accessories parented to `CCSPlayerPawn` using `SetParent`. | Requires delta-frame stability; may detach after animation/skin change or teleport. |

---

## 5. WorldText (CPointWorldText)

| Constraint | Evidence |
|---|---|
| **Entity type** | Entity type: `CPointWorldText` via `point_worldtext` (VMF usage validated). Project creates one per Wisp instance and one per Wisp evolution. | Used by AETHER Wisp (`AetherWisp.cs`). |

---

## 6. HUD / CenterHtml Limits

| Constraint | Project evidence | CS2 constraints |
|---|---|---|
| **CenterHtml API** | Heavy usage: `PrintToCenterHtml` re-issued every tick via `HudTick` because CS2 fades it after ~1 tick; competitive mode and hud_scaling limit visible area. | Magic number thresholds: hud_scaling 0.85 + font size classes |
| **HUD saturation risk** | Project reacts by supply: overlay + HUD cycle + 12s wheel timer. | HTML subset limited to `<font color/class, br>`, no tables/canvas. |
| **Font size classes** | Usage of `fontSize-l` / `fontSize-s` indicates known Panorama class subset. | No custom CSS. |

---

## 7. Input Binding Limits

| Constraint | Evidence |
|---|---|
| **Server cannot force bind** | Comment in code: “CS2 не даёт серверу гарантированно ставить бинды (защита Valve) — пробуем best-effort + подсказка.” | Implementation: `ExecuteClientCommand(bind "z" "css_z" ...)` with chat fallback for manual paste. |
| **Bind commands shipped** | `css_z`, `css_x`, `css_c`, `css_wheel`, `css_back`. | No detection of conflicting defaults (e.g., `+use`). |

---

## 8. Plugin Reload Constraints

| Constraint | Evidence |
|---|---|
| **Cs2EngineApi instantiation** | `new Cs2EngineApi()` created inline on round start (rift event); no per-player state cache. |
| **State invalidation** | No warm-start logic; reload can drop Wisps, auras, overlays, or bound menus. |
| **Auto hot reload flow** | CounterStrikeSharp DLL replace triggers `Unload()` → `Load(hotReload = true)`. |
| **Singleton use** | `GuildManager.Instance` is a static singleton and will not be recreated on reload. |

---

## 9. Current Capability Usage Mapping (by system)

| System | utilized ticks | utilized entities | utilized menus/voting | utilized hud |
|---|---|---|---|---|
| `AetherionPlugin` | 3 tick sources / OnTick + 4 fixed Repeat timers | - | PanoramaVote + WasdMenu + MenuManager.GetActiveMenu | CenterHtml HudTick (every server tick) + PrintToCenter on every action. |
| `AetherWisp` | FollowLoop: AddTimer(0.1) | CPointWorldText + model precache | - | Evolution popup: CenterHtml. |
| `AuraManager` | 0 | CDynamicProp parented to pawn | - | - |
| `CombatEffects` | TickAll on main OnTick | Particles + beams | - | - |
| `ModelManager` | - | precache manifest, SetModel, tints | - | - |
| `AudioManager` | - | - | - | ExecuteClientCommand soundevents. |

---

## 10. Known Risks / Unknowns

1. **Subtick plugin precision**: HUD + Wisp loop likely out of phase with subtick physics; no validation sampled.
2. **CenterHtml throughput**: Printing to centerhtml every tick for every player can saturate client bandwidth. No empirically tested ceiling in repo.
3. **Entity lifetime for particles**: Multiple per-tick particles (burn/poison) spawned with no explicit `Remove()`; long-run entity leak risk.
4. **Menu stack concurrency**: Overlay + WasdMenu + CenterHtml on same hot paths without explicit acq. Could deadlock menus on race/skin change.
5. **Platform_file requirement for custom votes**: No distribution of `platform_english.txt`.

---

## 11. Three Breakthrough Ideas Not Implemented

### Idea 1 — “Subtick-Safe Event Loop” for CombatEffects
**Concept**: Split tick-heavy effects into a CS2-subtick-aware sequence by bounding `TickAll` damage to once per server tick using `Server.GameEventManager` time-step OR by migrating reload-time burns/poisons to C# `Channel` data that preserves state through hot reload and lets `OnTick` only *issue* the authoritative server-time update.

**Why it’s feasible now**:
- Existing `ActiveEffect` already emits proper netvar state changes.
- Adds nothing new externally — purely internal refactor using `Server.CurrentTime` deltas and `NextTickUtc`.
- Makes the plugin stable across player counts and survives reload better.
- Allows higher-frequency particle+damage combos without entity spam: `TickAll` applies damage, but `CombatEffects` decides when to spawn particles based on accumulated charge, not every call.

**Rough contract**:
- `Apply(tag, slot, value, duration, tickInterval, source)` remains stable.
- `TickAll` keeps validated existing API.

---

### Idea 2 — “Panorama Vote Bridge” with Custom String Set
**Concept**: Introduce `csgo/resource/platform_english.txt` plus a private/semantic vote engine that bypasses PanoramaVote raw string body limit and enables typed structured votes: `vote_BossSummon`, `vote_Raid`, `vote_Duel`, with `option_0..3`, item metadata (who initiated, map target), and their own result callback. Uses PanoramaVoteManager as queue but exposes a typed wrapper.

**Why it’s feasible now**:
- The plugin already uses PanoramaVote in boss call (`StartBossVote`) and proxying logic with `YesNoVoteInfo`.
- PanoramaVoteManager supports queued global votes; its config supports server-side enabling and custom vote suppression.
- Adding an English vote text manifest plus a human-readable vote runner allows more natural WCS events such as guild wars or raid world bosses.

**Rough contract**:
- `VoteId` enum, `voteText`, `optionTexts[4]`, `durationSeconds`, `onPass`, `onFail`.
- `VoteEngine.Ask(string title, string body, …)` targeting all alive players with team filter.

---

### Idea 3 — “Hot-Reload Resilient Runtime”
**Concept**: Persist the dynamic runtime state (bound menus, active effects, Wisp bond, auras, HUD timers) into a small temp snapshot written to disk on unload and restored on `Load(hotReload = true)`. Use a typed `RuntimeSnapshot` aggregate that snapshots `_wheel`, `_hudUntil`, `_overlay`, `AuraManager` attachments, `CombatEffects` by slot, and `GuildManager` session. The reload validator then compares stored hash vs DLL build time to auto-rollback if DLL is stale.

**Why it’s feasible now**:
- CounterStrikeSharp docs state `Unload()` is called before DLL replacement and state can be preserved.
- AETHERION already persists `PlayerData` in SQLite; snapshot size is small (only ephemeral runtime shape).
- Reduces perceived downtime for devs/players after CSS update.

**Rough contract**:
- `SaveSnapshot()` happens on `Unload` event. `Load(hotReload)` rehydrates and auto-recreates entities that are `null`/invalid.
- Snapshot TTL = map change OR server restart; file-based so minimal RAM overhead.

---

## 12. Sources consulted

- AETHERION repo paths: `src/Core/AetherionPlugin.cs`, `src/Systems/Cs2EngineApi.cs`, `src/Systems/CombatEffects.cs`, `src/Systems/ModelManager.cs`, `src/Systems/AudioManager.cs`, `src/UI/AetherHud.cs`, `src/UI/AetherWheel.cs`, `src/Plugins/AetherWisp.cs`, `src/Systems/AuraManager.cs`, `src/Systems/AetherCommand.cs`.
- CounterStrikeSharp `Load(bool hotReload)` behavior — official docs/README.
- `PanoramaVoteManager` README — queued vote constraints and `platform_english.txt` requirement.
- Public CS2 documentation: 64 tick + subtick design, `sv_pure` restrictions, `point_worldtext` VMF usage.

---

## 13. Recommended small quick wins
1. Add a listen/dedicated server detection + disable `Server.NextFrame` particle flood when count >24.
2. Add explicit particle & beam removal timers for `_combat.TickAll` particles to prevent entity growth.
3. Switch to `CSS.NET`-style `MappingSnapshot` for HUD/Wheel/Overlay and use a `snapshotHash` to auto-rollback on DLL reload when build-time changes or tick precision limits exceeded.
