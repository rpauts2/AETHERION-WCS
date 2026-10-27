# AETHERION WCS: Engineering & Gameplay Roadmap

> Status: proposal based on the public README and declared feature surface. This is a prioritization guide, not a claim that the listed safeguards are currently missing. Verify each item against the implementation before changing code.

## Implementation status (2026-10-27)

- **M0a complete:** GitHub Actions now validates the main, core, and boss race catalogs, runs eight Python validator/report unit tests, restores .NET 8 dependencies, and builds the plugin in Release mode on pushes and pull requests.
- **Verified:** the first workflow run passed all validation, test, restore, and build steps. The workflow now publishes `AetherionWcs.dll` as a 14-day artifact for private-server smoke testing.
- **Implemented in this branch:** daily contract selections, progress, and claimed state are now stored in the player profile. Claim state and its gold/XP reward are saved together, preventing a server restart from rerolling the same day's contracts and re-awarding a claimed contract. Progress sync reuses snapshots to avoid per-hit allocations.
- **M0b implemented:** SQLite now uses WAL mode and a bounded 5-second default command timeout. The `crafted_ids` upgrade checks the actual schema instead of swallowing every SQL exception. Added `docs/DATABASE_OPERATIONS.md` with backup/restore and safe upgrade guidance.
- **M0c implemented:** added a .NET test project with eleven deterministic economy/progression tests covering gold caps and overflow, spending validation, XP thresholds, level-up points, and invalid XP grants/multipliers. CI now runs these tests before the Release build.
- **Additional guard:** XP grants are capped per event and Paragon processing is bounded per call, preventing corrupt or extreme saved XP values from monopolizing the game loop. Dedicated tests cover both cases.
- **Audit fix:** guild leadership transfer now removes the promoted member from the officer roster, avoiding duplicated leadership roles. The older June audit reported duplicate effect keys; the current `RaceRuntime.cs` was rechecked and has 114 explicit keys with no duplicate dictionary keys, so that item is not reproduced in the current branch.
- **M1a implemented:** `scripts/race_balance_report.py` summarizes race counts by tier/division, ability types, frequent effect tags, unusually large configured values, missing abilities, and duplicate IDs across catalogs. CI publishes the report as an artifact; this is a static config audit, not live win-rate telemetry. Added two Python tests for report behavior.
- **Still open:** core gameplay calculation tests, live CS2 plugin load/unload testing, broader database migration/idempotency coverage, and runtime balance telemetry. CI success is not a substitute for an in-game smoke test.
- **Next implementation target:** audit other economy/reward paths for duplicate claims and transaction boundaries, then add database busy-timeout/backup guidance before expanding progression systems.

## Product direction

Make AETHERION WCS feel like a persistent, fair, season-based RPG layered onto CS2: fast to understand in a live match, deep enough to retain long-term players, configurable for server owners, and safe to update without losing player progress.

The highest-value next step is not adding another 50 races. It is making the existing breadth reliable, understandable, balanced, and easy to operate.

## Priority 0 — Reliability before expansion

### 1. Establish a reproducible build and test baseline
- Pin/document supported CounterStrikeSharp, Metamod:Source 2, and .NET versions.
- Add CI that restores and builds Release on every PR.
- Add unit tests for XP/level thresholds, division scaling, roulette probabilities and pity, cooldowns, ELO updates, contract progress, and Battle Pass rewards.
- Add JSON configuration validation tests using representative valid and invalid fixtures.
- Add a release checklist with a clean-server smoke test and plugin load/unload test.
- Treat warnings and unhandled exceptions as visible CI failures where practical.

**Done when:** a clean checkout builds in CI and core calculations have deterministic tests.

### 2. Protect player data
- Add versioned SQLite schema migrations instead of relying on implicit schema shape.
- Use transactions for multi-step rewards, purchases, crafting, guild donations, and season claims.
- Enforce uniqueness/idempotency for daily rewards, contract claims, Battle Pass claims, and roulette requests so retries cannot duplicate rewards.
- Add database busy-timeout, bounded retry for transient lock errors, and clear error logging.
- Provide an operator backup/restore guide and a startup integrity/compatibility check.
- Test interrupted writes and upgrades from a copy of an older database.

**Done when:** updates preserve existing player data and repeat requests cannot mint duplicate rewards.

### 3. Make gameplay actions server-authoritative
- Validate ownership, currency, level, cooldown, alive/dead state, team, and match phase on the server for every ability and economy action.
- Centralize cooldown and resource consumption so command, menu, and ability paths cannot disagree.
- Add per-player action throttling for spam-prone commands.
- Ensure delayed callbacks stop safely when a player disconnects, changes team, or the plugin unloads.
- Never trust client-provided race IDs, prices, rewards, or target state without server-side validation.

**Done when:** all important actions pass through one validation path and are covered by negative tests.

## Priority 1 — Balance and content quality

### 4. Build a Race Balance Lab
Create a repeatable report from configuration and match telemetry:
- Pick rate, win rate, kills/deaths, damage contribution, ability usage, and cooldown uptime by race and division.
- Separate results by player skill band, map, mode, team, and sample size.
- Flag extreme outliers, but do not auto-nerf races from tiny samples.
- Add a race sandbox/simulation for effect combinations and configuration validation.
- Track balance changes per season and keep a short changelog explaining the intended effect.

Use confidence/sample-size thresholds and avoid collecting unnecessary personal data. Make telemetry opt-in/configurable where appropriate.

**Done when:** maintainers can identify outliers from evidence rather than anecdotes alone.

### 5. Turn race configuration into a safe content pipeline
- Define a versioned schema for race definitions, effects, mutations, guild races, and mythic races.
- Validate unknown effect tags, invalid numeric ranges, duplicate IDs, missing localization keys, and impossible combinations before loading.
- Provide an offline command that checks every race file without launching a CS2 server.
- Support stable race IDs independent of display names; renaming a race must not break player progression.
- Add examples and a template for new races, with ability cost/cooldown/limits documented.
- Add a compatibility report when an update changes or removes a race/effect.

**Done when:** invalid content fails early with file, field, and actionable error, not at runtime mid-match.

### 6. Balance the economy as a closed loop
- Document all gold/ETH sources and sinks, including average rewards per hour and per season.
- Add configurable caps and server-side validation for transfers, crafting, spins, and reward claims.
- Track inflation and reward distribution over time.
- Make roulette probabilities explicit in the UI/config, and test the pity system mathematically.
- Keep premium progression cosmetic or convenience-oriented where possible; avoid pay-to-win power gaps.
- Provide a staging mode to test economy changes against synthetic player histories.

**Done when:** operators can estimate how a change affects currency supply before deploying it.

## Priority 2 — Better player experience

### 7. Make onboarding useful in the first five minutes
- A short first-run flow: choose a starter race, explain passive/active/ultimate, show the next achievable goal.
- Add concise contextual hints rather than dumping every system into the main menu.
- Show locked-race requirements and the exact next step to unlock.
- Add searchable/filterable race lists by division, role, playstyle, and owned/unowned state.
- Provide a compact ability preview with cooldown, cost, range, and restrictions.
- Keep messages short and localizable; avoid flooding chat during combat.

### 8. Make progression legible
- A single profile view for level, XP to next level, division, race mastery, season progress, guild, and recent achievements.
- Explain why XP/rewards were granted or denied.
- Add a reward history and an admin-visible audit trail for economy-changing actions.
- Make daily reset and season-end times explicit and configurable.
- Ensure menus degrade gracefully when a player is disconnected or a feature is disabled.

### 9. Make PvE and events replayable
- Give each boss tier clear telegraphs, phases, role contributions, and a post-fight summary.
- Scale boss health/rewards against actual participants with minimum/maximum bounds.
- Add event scheduling, cooldowns, cancellation rules, and cleanup after server restart/unload.
- Make Storm Wave difficulty and rewards configurable with predictable progression.
- Reward participation and team contribution without encouraging AFK farming.

## Priority 3 — Server-owner experience

### 10. Add operator diagnostics
- A read-only diagnostics command showing plugin version, dependency versions, loaded race counts, config validation status, database status, active events, and recent errors.
- Structured logs with event IDs and player identifiers handled according to server policy.
- Admin audit entries for grants, edits, race overrides, and manual reward changes.
- Dry-run mode for risky admin operations and confirmation for destructive actions.
- Document permission requirements for every admin command.

### 11. Configuration profiles and safe updates
- Ship minimal, competitive, and progression example profiles without forcing a single balance philosophy.
- Separate defaults from local overrides so updates do not overwrite server-owner choices.
- Validate configuration on startup and report exactly which defaults/overrides were applied.
- Document backup, rollback, migration, and upgrade steps.
- Keep secrets out of config examples and logs.

### 12. Compatibility contract
- Maintain a documented support matrix for CS2/CounterStrikeSharp/.NET versions.
- Add a startup warning for unsupported versions rather than failing mysteriously.
- Isolate version-sensitive API calls behind a small compatibility layer where justified.
- Test plugin load/unload and map change paths, not only a successful build.

## Suggested implementation sequence

| Milestone | Scope | Acceptance gate |
|---|---|---|
| M0: Baseline | CI build, configuration validation, core unit tests | Clean checkout builds; deterministic tests pass |
| M1: Data safety | Migrations, transactional rewards, idempotency, backup guide | Upgrade and duplicate-claim tests pass |
| M2: Runtime safety | Central action validation, cooldowns, throttling, lifecycle cleanup | Negative-path and disconnect tests pass |
| M3: Content tooling | Race schema, offline validator, stable IDs | All shipped configs validate |
| M4: Balance visibility | Configurable telemetry and race/economy reports | Reports include sample sizes and clear definitions |
| M5: UX and events | Onboarding, profile, event cleanup and summaries | New player can understand first progression loop |
| M6: Release operations | Diagnostics, profiles, compatibility matrix, rollback guide | A server owner can install, diagnose, upgrade, and roll back |

## Working rules

1. Inspect the implementation before assuming a feature or safeguard is absent.
2. Prefer small pull requests with one risk area each.
3. Add a regression test with every bug fix.
4. Never change player data formats without a migration and rollback story.
5. Do not add more races until validation, balance visibility, and race authoring are dependable.
6. Measure performance on a real dedicated server; avoid per-tick allocations, synchronous database work in hot event handlers, and unbounded timers.
7. Keep the mod's declared internal-project status and redistribution restrictions in mind when publishing builds or assets.

## First concrete PRs after this roadmap

1. CI build + configuration validation.
2. Unit tests for XP, roulette/pity, cooldown, ELO, and reward idempotency.
3. SQLite migration and backup/restore test.
4. Race configuration validator.
5. Read-only diagnostics command.

These are proposed next steps. Implementation should begin only after the relevant source files and current behavior have been inspected.
