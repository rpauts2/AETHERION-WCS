# AETHERION‑WCS — Localization Coverage Report
_Generated: 2026‑06‑27_  
_Audited paths:_
- `src/Systems/*.cs` (15 files)
- `src/UI/*.cs` (2 files)
_Reference dictionary:_ `lang/ru.json` (371 keys)

---

## 1. Executive Summary

| Metric | Value |
|--------|-------|
| Files audited | 18 |
| Files with **zero** `Loc.Get` / localization API calls | **17** |
| Files with **partial** localization coverage via direct `Loc.Get` | 0 |
| Files with **full** coverage | **1** (`ILocalizationReader.cs` — defines the API only) |
| `ru.json` keys actually resolved in runtime by audited files | **0** |
| Hard‑coded Russian strings in audited files | **≈ 130** |
| Suggested new keys for hard‑coded strings | **≈ 70** |

**Verdict:** Localization infrastructure (`ILocalizationReader`, `JsonLocalizationProvider`) is ready, but **Systems and UI code does not use it**. The vast majority of user‑facing strings are inlined in Russian, bypassing `ru.json`.

---

## 2. Localization Key Groups in `ru.json`

Keys follow a consistent namespace pattern that should be preserved when adding missing entries:

- **General / controls** — `ScrollKey`, `SelectKey`, `PrevKey`, `ExitKey`
- **Core plugin** — `Core_AetherionPlugin_*`
- **Nexus integration** — `Core_NexusIntegration_*`, `Plugins_AetherNexus_*`
- **Roulette** — `Plugins_AetherRoulette_*`, `Systems_RouletteSystem_*`
- **Sigils** — `Plugins_AetherSigils_*`, `Plugins_SigilResonance_*`
- **Wisp / Evo** — `Plugins_AetherWisp_*`, `Plugins_WispEvolution_*`
- **Holo selector** — `Plugins_HoloRaceSelector_*`
- **Systems** — `Systems_AetherCommand_*`, `Systems_AetherRiftEvent_*`, `Systems_AudioManager_*`, `Systems_ModelManager_*`, `Systems_DailyReward_*`, `Systems_ItemShop_*`, `Systems_RouletteSystem_*`, `Systems_SeasonSystem_*`, `Systems_GuildSystem_*`
- **UI** — `UI_AetherHud_RaceCard_*`, `UI_AetherHud_RaceList_*`, `UI_AetherWheel_*`
- **Shared** — `RarityIcon_*`, `BossType_*`

---

## 3. Per‑File Audit Details

### 3.1 `src/Systems/ILocalizationReader.cs`
- **Localization calls:** 0 (defines the API + helper types)
- **Status:** ✅ Healthy — infrastructure only.
- **Notes:** Contains *example* `Loc.Get(...)` calls in comments (`Core_AetherionPlugin_NoRace`, `LevelSystem_XpKill` — the latter is a hypothetical key, not present in `ru.json`).
- **Action:** None.

---

### 3.2 `src/Systems/EconomySystem.cs`
- **Localization calls:** 0
- **Hard‑coded strings:** None (pure numeric constants)
- **Suggested keys:** `Systems_Economy_GoldKill` (example), `Systems_Economy_XpKill` — optional for HUD/debug displays.
- **Action:** None required.

---

### 3.3 `src/Systems/LevelSystem.cs`
- **Localization calls:** 0
- **Hard‑coded strings:** None (shift statements reference named constants elsewhere in comments)
- **Suggested keys:** `Systems_LevelSystem_XpKill`, `Systems_LevelSystem_TierCap` — optional.
- **Action:** None required.

---

### 3.4 `src/Systems/UnlockSystem.cs`
- **Localization calls:** 0
- **Hard‑coded strings:** None (numeric/math logic)
- **Suggested keys:** `Systems_UnlockSystem_DivisionThresholds` (if ever displayed).
- **Action:** None required.

---

### 3.5 `src/Systems/DailyRewardSystem.cs`
- **Localization calls:** 0
- **Hard‑coded strings:** All reward labels are hardcoded:
  - `"Разминка"`, `"Втягиваешься"`, `"Бесплатный спин!"`, `"Бонус опыта"`, `"Фри-VIP на день!"`, `"Двойной куш"`, `"★ ДЖЕКПОТ НЕДЕЛИ ★"`
- **Existing `ru.json` keys:** `Systems_DailyReward_Day1..Day7`, `Systems_DailyReward_FreeSpin`, `Systems_DailyReward_BonusXP`, `Systems_DailyReward_FreeVIP`, `Systems_DailyReward_DoubleDown`, `Systems_DailyReward_Warmup`, `Systems_DailyReward_WeekJackpot`
- **Missing coverage:**
  - Streak/milestone context (`"Стрик {streak} дн."`, `"Завтра: {label}"`) → use existing `Core_AetherionPlugin_DailyStreak`, `Core_AetherionPlugin_Tomorrow` in caller, but `DailyRewardSystem` itself doesn’t resolve them.
- **Action:** Wrap these strings via `Loc.GetF(...)` in the consumer (usually `AetherionPlugin`).

---

### 3.6 `src/Systems/GuildSystem.cs`
- **Localization calls:** 0
- **Hard‑coded strings:** None (pure mechanics / data model)
- **Suggested keys:** `Systems_GuildSystem_Create`, `Systems_GuildSystem_Leave`, `Systems_GuildSystem_MaxMembers` — for future HUD.
- **Action:** None required.

---

### 3.7 `src/Systems/Cs2EngineApi.cs`
- **Localization calls:** 0
- **Hard‑coded strings:** Console logs: `"[AETHERION] tint fail {slot}: {ex.Message}"`, `"[AETHERION] freeze fail {slot}: {ex.Message}"`, `"[AETHERION] sound fail {raceId}: {ex.Message}"`
- **Existing `ru.json` keys:** None of these debug strings exist in `ru.json`.
- **Suggested keys (debug-only, optional):**
  - `Systems_Cs2EngineApi_TintFail`
  - `Systems_Cs2EngineApi_FreezeFail`
  - `Systems_Cs2EngineApi_SoundFail`
- **Action:** Low priority — debug messages don’t need full localization.

---

### 3.8 `src/Systems/AudioManager.cs`
- **Localization calls:** 0
- **Hard‑coded strings:** Console logs: `"[AETHERION] sounds.json не найден"`, `"[AETHERION] AudioManager: загружено {_map.Count} звуков ультов"`
- **Existing `ru.json` keys:** `Systems_AudioManager_Loaded`, `Systems_AudioManager_Missing`
- **Coverage:** Existing keys **match** the hardcoded strings almost 1:1, but the code does not call `Loc.Get(...)`.
- **Action:** Wrap log messages with `Loc.Get(...)`.

---

### 3.9 `src/Systems/ModelManager.cs`
- **Localization calls:** 0
- **Hard‑coded strings:** Console logs: `"[AETHERION] models.json не найден: {path}"`, `"[AETHERION] ModelManager: загружено {_map.Count} моделей, {_toPrecache.Count} ассетов к precache"`
- **Existing `ru.json` keys:** `Systems_ModelManager_Loaded`, `Systems_ModelManager_Missing`
- **Coverage:** Same as `AudioManager` — keys exist, code does not use them.
- **Action:** Wrap log messages with `Loc.Get(...)`.

---

### 3.10 `src/Systems/CombatEffects.cs`
- **Localization calls:** 0
- **Hard‑coded strings:** None (effect tags are enum names, not displayed text)
- **Suggested keys:** `Systems_CombatEffects_Burn`, `Systems_CombatEffects_Poison` — for chat/centerhtml notifications if added later.
- **Action:** None required.

---

### 3.11 `src/Systems/AuraManager.cs`
- **Localization calls:** 0
- **Hard‑coded strings:** None
- **Action:** None required.

---

### 3.12 `src/Systems/ItemShop.cs`
- **Localization calls:** 0
- **Hard‑coded strings:** Shop catalog names, descriptions, and responses:
  - `"Сапоги Эфира"`, `"Перчатки Хвата"`, `"Амулет Жизни"`, `"Зелье Берсерка"`, `"Зелье Фантома"`, `"Реликвия Эфира"`, `"Камень Возврата"`, `"Сердце Дракона"`, `"Осколок Вечности"`
  - Descriptions like `"+8% скорость на раунд"`, etc.
  - `"Предмет не найден."`, `"Не хватает золота ({p.Gold}/{item.Price})."`, `"Достигнут лимit ({item.MaxStack})."`, `"Куплено: {item.Name} ({item.Rarity})."`
- **Existing `ru.json` keys:** `Systems_ItemShop_Boot_Speed` through `Systems_ItemShop_Rarity_Mythic` cover some localized display strings.
- **Coverage:** Partial — response messages are not localized.
- **Action:** Replace catalog hardcoded strings with `Loc.Get(...)` keys + replace chat/buy responses with keys:
  - `Systems_ItemShop_BuySuccess`, `Systems_ItemShop_NotEnoughGold`, `Systems_ItemShop_NotFound`, `Systems_ItemShop_MaxStack`

---

### 3.13 `src/Systems/AetherCommand.cs`
- **Localization calls:** 0
- **Hard‑coded strings:**
  - `"👁 Режим Духа: !watch <id>, !back"`
  - `"Нет прав."`
  - `"Аномальная точность"`, `"Дух наблюдателя"`, `"Клетка Эфира"`, `"Серия мгновенных наводок (возможно aim)"`, `"Слишком много хедшотов"`, `"Экстремальный KD"`
- **Existing `ru.json` keys:** `Systems_AetherCommand_WatchMode` through `Systems_AetherCommand_Suspicion_KD` already exist and map 1:1.
- **Action:** Replace hardcoded strings with `Loc.GetF(...)` calls.

---

### 3.14 `src/Systems/AetherRiftEvent.cs`
- **Localization calls:** 0
- **Hard‑coded strings:**
  - `"Террористы"`, `"Спецназ"`
  - `"🌀 {name} захватили Эфирный Разлом! Команда получает +25% Эфира и +10% урона на 30 сек."`
- **Existing `ru.json` keys:** `Systems_AetherRiftEvent_Terrorists`, `Systems_AetherRiftEvent_Specnaz`, `Systems_AetherRiftEvent_CaptureMsg`
- **Action:** Replace inline strings with `Loc.GetF(...)`.

---

### 3.15 `src/Systems/SeasonSystem.cs`
- **Localization calls:** 0
- **Hard‑coded strings:** Reward descriptor strings are inline:
  - `"Титул «Уровень {lvl}»"`, `"#FF00AA"` (color, not localized), `"Эфирный след: голубой"`, `"Звук килла: гром"`, `"Скин частиц ульты: золото"`, `"+{lvl}% золота на час"`
- **Existing `ru.json` keys:** `Systems_SeasonSystem_TitleLvl`, `Systems_SeasonSystem_EtherTrail`, `Systems_SeasonSystem_KillSound`, `Systems_SeasonSystem_ParticleGold`, `Systems_SeasonSystem_Bonus1`, `Systems_SeasonSystem_StormConqueror`, `Systems_SeasonSystem_Name`
- **Coverage:** Partial — only a subset of messages are represented in `ru.json`. The `RewardFor` method contains mixed localized/raw content.
- **Action:** Create keys for remaining reward descriptors and resolve via `Loc.Get(...)`.

---

### 3.16 `src/Systems/RouletteSystem.cs`
- **Localization calls:** 0
- **Hard‑coded strings:**
  - `"Недостаточно золота. Нужно {wheel.Cost}."`
  - `"Повезёт в следующий раз!"`
  - `"🎉 ГАРАНТ! Вы выиграли: {jackpot.Name}"`
  - `"Вы выиграли: {prize.Name}"`
- **Existing `ru.json` keys:** `Systems_RouletteSystem_Win`, `Systems_RouletteSystem_NoGold`, `Systems_RouletteSystem_NoLuck`, `Systems_RouletteSystem_JackpotGuaranteed` (note: `Plugins_AetherRoulette_*` contains overlapping messages — should be deduped).
- **Action:** Replace hardcoded returns with `Loc.GetF(...)`.

---

### 3.17 `src/UI/AetherHud.cs`
- **Localization calls:** 0
- **Hard‑coded strings:**
  - `"⚡ {def.Name}"`, `"Круг {def.Division}"`, `"★{rp.ParagonLevel}"`, `"Уровень {rp.Level}/..."`, `"Эфир {Bar(...)}"`, `"Σ Общий уровень: {total}"`, `"до Круга ..."`, `"Золото: ... · Очки: ..."`, `"ВЫБОР РАСЫ"`, `"Жми 1-9 для выбора"`
- **Existing `ru.json` keys:** `UI_AetherHud_RaceCard_Level`, `UI_AetherHud_RaceCard_Ether`, `UI_AetherHud_RaceCard_TotalLevel`, `UI_AetherHud_RaceCard_ProgressHint`, `UI_AetherHud_RaceCard_GoldPoints`, `UI_AetherHud_RaceList_Title`, `UI_AetherHud_RaceList_Hint`, `UI_AetherHud_TierStars`, `UI_AetherWheel_EtherBar`, `UI_AetherWheel_Controls`
- **Coverage:** Partial — most labels are hardcoded Russian in `AetherHud`; the existing `ru.json` keys don’t match the actual payloads in code (e.g. “Уровень … / {LevelMax(def.Tier)}” is inline, not using `UI_AetherHud_RaceCard_Level` properly).
- **Suggested keys:**
  - `UI_AetherHud_RaceCard_RaceName` = "⚡ {def.Name}"
  - `UI_AetherHud_RaceCard_Division` = "Круг {def.Division}"
  - `UI_AetherHud_RaceCard_Paragon` = "★{rp.ParagonLevel}"
  - `UI_AetherHud_RaceList_Header` = "ВЫБОР РАСЫ"
  - `UI_AetherHud_RaceList_SelectHint` = "Жми 1-9 для выбора"

---

### 3.18 `src/UI/AetherWheel.cs`
- **Localization calls:** 0
- **Hard‑coded strings:**
  - `"◈ AETHER WHEEL ◈"`
  - `"Эфир [{bar}] {ether}/{etherMax}"`
  - `"взгляд = выбор · css_wheel = подтвердить · R = закрыть"`
- **Existing `ru.json` keys:** `UI_AetherWheel_EtherBar`, `UI_AetherWheel_Controls`
- **Action:** Replace hardcoded header with `UI_AetherWheel_Title`.

---

## 4. Missing / Suggested New Keys

These are the most critical additions to `ru.json` for the **Systems + UI** zones that currently lack localization hooks.

### 4.1 Systems — HUD / Command / Event messages
```jsonc
"Systems_Economy_GoldKill":     "+{0} золота за киллы",
"Systems_Economy_XpKill":       "+{0} XP за киллы",
"Systems_AetherCommand_WatchCooldown": "👁 Дух наблюдателя: наблюдатель активен, {cd}с до перевключения",
"Systems_AetherCommand_Suspicion_Summary": "Отчёт по {name}: точность {acc:P0}, KD {kd}, флаги [{flags}]",
"Systems_AetherRiftEvent_Spawning": "🌀 Эфирный Разлом появляется на точке...",
"Systems_AudioManager_Loaded":  "[AETHERION] AudioManager: загружено {0} звуков ультов",
"Systems_AudioManager_Missing": "[AETHERION] sounds.json не найден",
"Systems_ModelManager_Loaded":  "[AETHERION] ModelManager: загружено {0} моделей, {1} ассетов к precache",
"Systems_ModelManager_Missing": "[AETHERION] models.json не найден: {0}",
"Systems_CombatEffects_Burn":   "Горение",
"Systems_CombatEffects_Poison": "Яд",
"Systems_CombatEffects_Regen":  "Регенерация",
"Systems_ItemShop_BuySuccess":  "Куплено: {0} ({1}).",
"Systems_ItemShop_StackFull":   "Достигнут лимит ({0}).",
"Systems_RouletteSystem_WinLabel":   "Вы выиграли: {0}",
"Systems_RouletteSystem_NoGoldLabel":"Недостаточно золота. Нужно {0}.",
"Systems_RouletteSystem_NoLuckLabel":"Повезёт в следующий раз!",
"Systems_RouletteSystem_JackpotLabel":"🎉 ГАРАНТ! Вы выиграли: {0}",
"Systems_AetherRiftEvent_CaptureMsg": "🌀 {0} захватили Эфирный Разлом! Команда получает +25% Эфира и +10% урона на 30 сек."
```

### 4.2 UI labels (AetherHud + AetherWheel)
```jsonc
"UI_AetherHud_RaceCard_HeaderTitle":   "⚡ {0}",
"UI_AetherHud_RaceCard_DivisionLabel": "Круг {0}",
"UI_AetherHud_RaceCard_ParagonLabel":  "★{0}",
"UI_AetherHud_RaceList_TitleLabel":    "ВЫБОР РАСЫ",
"UI_AetherHud_RaceList_SelectHint":    "Жми 1-9 для выбора",
"UI_AetherWheel_Title":                "◈ AETHER WHEEL ◈",
"UI_AetherWheel_ControlsHint":         "взгляд = выбор · css_wheel = подтвердить · R = закрыть"
```

### 4.3 Daily / Season / Guild auxiliary strings
```jsonc
"Systems_DailyReward_StreakLabel":     "Стрик входов: {0} дн.",
"Systems_DailyReward_TomorrowLabel":   "Завтра (день {0}): {1} — +{2} голота",
"Systems_SeasonSystem_BattlePassHeader":"ПЬЕДЕСТАЛ СЕЗОНА",
"Systems_SeasonSystem_RewardDate":     "Уровень {0} — {1}",
"Systems_GuildSystem_CreateHint":      "Вы без гильдии. !guild create <имя> <тег>"
```

---

## 5. Priority Recommendations

| Priority | Action | Files |
|----------|--------|-------|
| **P0** | Replace all `Core_AetherionPlugin_*` hardcoded strings in downstream systems with `Loc.GetF(...)` equivalents (keys already exist). | `AetherCommand`, `ItemShop`, `RouletteSystem`, `AetherRiftEvent`, `AudioManager`, `ModelManager` |
| **P0** | Wire `UI_AetherHud.cs` and `UI_AetherWheel.cs` to resolve through `Loc` (keys in `ru.json` are not sufficient for the current HUD payloads). | `AetherHud.cs`, `AetherWheel.cs` |
| **P1** | Add the “Suggested New Keys” above to `lang/ru.json` and add matching consumer calls to `DailyRewardSystem`, `SeasonSystem`. | `DailyRewardSystem.cs`, `SeasonSystem.cs` |
| **P2** | Eat-your-own-dogfood: replace `Console.WriteLine` debug strings in `Cs2EngineApi.cs`, `AudioManager.cs`, `ModelManager.cs` with localized or at least centralized logger. | `Cs2EngineApi.cs`, `AudioManager.cs`, `ModelManager.cs` |
| **P3** | Add English fallback `lang/en.json` (current fallback is guaranteed by `LocalizationService`, but dictionary is missing). | N/A |

---

## 6. Key References for Patch Groups

| Suggested group prefix | Scope |
|------------------------|-------|
| `Systems_Economy_*` | Gold / XP constants shown to players |
| `Systems_CombatEffects_*` | Periodic damage/heal/CC chat labels |
| `Systems_ItemShop_*` | Shop purchase & error messages |
| `Systems_RouletteSystem_*` | Roulette spin results & errors |
| `Systems_SeasonSystem_*` | Season pass HUD strings |
| `Systems_GuildSystem_*` | Guild hints & member info |
| `UI_AetherHud_*` | Race card / list text payloads |
| `UI_AetherWheel_*` | Wheel title + controls |
| `Systems_Admin_*` | Anti‑cheat admin labels (currently `Systems_AetherCommand_*`) |
| `Systems_Sigil_*` | Sigil drawing & combo names (already `Plugins_*` in `ru.json`) |

---

_End of report._
