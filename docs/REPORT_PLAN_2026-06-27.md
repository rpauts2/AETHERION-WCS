# AETHERION-WCS: Итоговый отчёт и план развития
Дата создания: 2026-06-27  
Статус: Актуальная сборка + локализация + конкурентный анализ + стратегический план

---

## 1. Текущее состояние проекта (шорт-версия)

| Компонент | Статус |
|-----------|--------|
| Сборка | ✅ `AetherionWcs.dll` собран (`bin/Release/net8.0/`) |
| База данных | ✅ SQLite через `e_sqlite3.dll` (native в `runtimes/`) |
| Расы | ✅ 198 рас, 8 дивизионов, 1,077 способностей в `races.json` |
| Локализация | ✅ `lang/ru.json` — 346 ключей, ILocalizationReader готов |
| Меню | ✅ CS2MenuManager + WASD-меню |
| Экономика | ✅ Голда: XP, киллы, хедшоты, нож, боссы, дуэли, ежедневки |
| Питомцы | ✅ Wisp (стадии, эволюция, бонусы) |
| Гайд по запуску | ✅ `HANDOVER.md` (полный рестарт, деплой DLL+config) |

### Важная техническая пометка
- **Hot-reload не работает** — требуется полный рестарт CS2.
- Английская локаль (`lang/en.json`) — пока не заполнена; основной фолбэк — хардкод в C#.

---

## 2. Конкурентный разбор (из `docs/COMPETITIVE_ANALYSIS.md`)

### Что есть у других
- **ThaPwned/WCS** (Python, Source.Python): актуальный до август 2022, legacy инфраструктура.
- **War3CS2** (C#, CSSharp): 326 каталогизированных рас, `(lvl+1)*300` XP-формула, 50 предметов ($1k–$20k), diminishing-buffs, weapon-fire-rate scaling.
- **CrazyStar / DivineWCS2 / AzerotCS2**: 500+ рас, гибридные моды (ZM|WCS|ZE|VIP).
- Общие паттерны: Level-cap 1000, 8 дивизионов, рулетка, VIP-монетизация, knife rounds, skins/shop/gloves.

### Критические пробелы конкурентов
| Фича | У конкурентов | У AETHERION |
|------|--------------|-------------|
| Гильдии / GvG | ❌ отсутствует | 🟡 каркас, не финализировано |
| Сезоны / Battle Pass | ❌ отсутствует | 🟡 каркас, не финализировано |
| Paragon / престиж | ❌ отсутствует | 🟡 есть ParagonLevel в PlayerData |
| Питомцы-компаньоны | ❌ отсутствует | ✅ Wisp реализован |
| Discord/WebUI лидерборды | ❌ отсутствует | ❌ не начато |
| Source 2 нативные фичи | ❌ не используются | 🟡 частично (CPointWorldText, PanoramaVote) |

### Уникальные возможности CS2 для AETHERION
1. **Subtick-safe event loop** — ивент-луп на subtick (меньше джиттера, выше стабильность).
2. **Panorama vote bridge с typed IDs** — типизированные голосования (boss/raid/duel/guild_war) через `platform_english.txt`.
3. **Hot-reload resilient runtime** — снапшот состояния на `Unload()` + ресторейт на `Load(hotReload)`.
4. **Source-level skin/agent integration** — нативные анимации/агенты CS2.
5. **Steam Workshop дистрибуция** — пакеты рас/items через Workshop.
6. **Server browser competitive tag** — интеграция в сервер браузер как “competitive WCS”.

---

## 3. Аудит CS2-ограничений (из `docs/AETHERION_CS2_CAPABILITY_AUDIT.md`)

### Что уже используется
- `OnTick` + `AddTimer(0.1f)` для Wisp follow.
- `PrintToCenterHtml` saturation (HUD каждый тик).
- `CS2MenuManager` (WASD-меню).
- `PanoramaVote` (boss call).
- `CPointWorldText` + `info_particle_system` + `env_beam`.
- `ExecuteClientCommand("play ...")` для звуков.

### Жёсткие ограничения CS2
- **Тик**: 64 tick, плагин-таймеры не привязаны к subtick.
- **Серверные бинды**: Valve блокирует `bind` через `ExecuteClientCommand` (рабочий обход — ручная консоль).
- **sv_pure**: кастомные модели/звуки требуют whitelist.
- **CenterHtml**: только HTML-подмножество `<font color/class, br>`; сервер не гарантирует доставку каждый тик.
- **PanoramaVote**: одна голосование в очередь, кастомный текст через `platform_english.txt`.
- **Дедicated vs listen**: listen не рендерит некоторые Panorama-фичи.

### Риски в текущем коде
1. **CenterHtml throughput** — перегрузка при >16 игроков.
2. **Entity leak** — партиклы/лучи без явного `Remove()`.
3. **Wisp follow-loop deletion risk** — WorldText может удалиться без cleanup.
4. **sv_pure не обработан** — кастомные ассеты не работают на чистом сервере.
5. **Menu stack concurrency** — overlay + WasdMenu + CenterHtml могут дедлокиться.

### 3 прорывные идеи (не реализованы)
1. **Subtick-safe event loop** — внутренний рефактор CombatEffects на `Server.CurrentTime` + charge-аккумулятор.
2. **Panorama vote bridge** — typed wrapper: `VoteId` enum + `optionTexts[4]` + `onPass/onFail` callback.
3. **Hot-reload runtime snapshot** — файловый снапшот `_wheel`, `_hudUntil`, `_overlay`, `CombatEffects`, `GuildManager` на `Unload`.

---

## 4. ПЛАН РАЗВИТИЯ (обновлённый)

### PHASE 0 — Локализация и стабильность (1-2 недели)
| # | Задача | Артефакт |
|---|--------|---------|
| 0.1 | Интегрировать `ILocalizationReader` во все системы (`AetherionPlugin`, `ItemShop`, `Roulette`, `Wisp`, `Sigils`, `Guild`). | Код: `Loc.Get("...")` вместо хардкода |
| 0.2 | Заполнить `lang/en.json` fallback на все 346 ключей (min: copy RU values). | `lang/en.json` |
| 0.3 | Убрать смесь RU/EN из названий рас (`races.json` → `lang/ru.json`). | Очищенные `name` в races.json |
| 0.4 | Исправить ID namespace gap: добавить `soundId`/`modelId` в `races.json` или перереномерить assets под 2000+. | `docs/CONFIG_FIX_PLAN.md` |
| 0.5 | Добавить explicit particle/beam removal timer в `CombatEffects.TickAll`. | Код: cleanup timer |
| 0.6 | Подготовить `csgo/resource/platform_english.txt` для Panorama vote strings. | Файл в деплой-пакете |

### PHASE 1 — Уникальный геймплей на Source 2 (3-4 недели)
**Цель: фичи, которых нет ни у CrazyStar, ни у War3CS2, ни у DivineWCS2.**

| # | Фича | Почему уникально | Ссылка на аудит |
|---|------|-----------------|-----------------|
| 1.1 | **Subtick-safe Combat Loop** | Стабильность при 64+ игроках, меньше джиттера | Audit §3, Idea 1 |
| 1.2 | **Typed Panorama Vote Bridge** | Голосования за босса/рейд/гильд-войну с callbacks | Audit §3, Idea 2 |
| 1.3 | **Hot-reload Runtime Snapshot** | 0-downtime development, игроки не теряют прогресс раунда | Audit §3, Idea 3 |
| 1.4 | **Source 2 Skin Tint System** | Кастомные тины моделей под расы (а не статик .mdl) | Audit §4, model API |
| 1.5 | **SV_Pure Whitelist Manager** | Гарантированный запуск кастом-ассетов на dedicated | Audit §10,Risk #4 |
| 1.6 | **AETHER WHEEL v2 (client-side VPK)** | Радиальное меню без серверных таймеров (на dedicated) | HANDOVER §4,214 |

### PHASE 2 — Прогрессия и мета (3-4 недели)
| # | Фича | Артефакт |
|---|------|---------|
| 2.1 | **Battle Pass + Сезон I**: трек 50 уровней, free+premium, закрытие по времени. | `src/Systems/SeasonSystem.cs` финализация |
| 2.2 | **Ачивки + Контракты**: 100 киллов расой, дуэли-серии, босс-ивенты. | `src/Systems/AchievementSystem.cs` |
| 2.3 | **Лидерборды**: общий уровень, сезон, PvP-рейтинг. | `src/Systems/LeaderboardSystem.cs` |
| 2.4 | **Paragon-престиж**: после капа 1000 — перерождение с визуальными татуировками. | Расширение `PlayerData.ParagonLevel` |
| 2.5 | **D1-расам минимум 1 активка** | Датасет `docs/D1_BALANCE_PATCH.md` |

### PHASE 3 — Социальные (3-4 недели)
| # | Фича | Артефакт |
|---|------|---------|
| 3.1 | **Гильдии**: создание, гильд-банк, бонусы, GvG-матчи. | `src/Systems/GuildSystem.cs` финализация |
| 3.2 | **Sigil Resonance v2**: комбо-бонусы за наборы печатей, визуальные комбо. | `src/Plugins/SigilResonance.cs` |
| 3.3 | **Wisp v2**: активные способности духа, скины, кормление. | `src/Plugins/WispEvolution.cs` |

### PHASE 4 — Инфраструктура (2-3 недели)
| # | Фича | Артефакт |
|---|------|---------|
| 4.1 | **Dedicated сервер**: деплой на VPS, `sv_pure 0` + кастом-сборка. | `DEPLOY.md` |
| 4.2 | **MySQL + бэкапы**: резервный драйвер, автоматический бэкап SQLite. | `src/Database/` |
| 4.3 | **Анти-абуз**: валидация голды/уровней, rate-limit, слив-детектор. | `src/Systems/AntiAbuse.cs` |
| 4.4 | **Discord Webhook + WebUI лидерборд** | Flask/FastAPI sidecar |
| 4.5 | **CI/CD**: GitHub Actions → build + deploy artifact. | `.github/workflows/ci.yml` |

### PHASE 5 — Полировка (2 недели)
| # | Фича | Артефакт |
|---|------|---------|
| 5.1 | **Звуки + партиклы Source 2**: звуки способностей, ульт-эффекты, Death/Respawn FX. | `configs/assets/*.json` заполнение |
| 5.2 | **Nameplates over players**: раса + уровень + КД ульты (через CPointWorldText или Overlay). | `src/UI/NameplateSystem.cs` |
| 5.3 | **Карта-тестирование**: de_dust2, mirage, inferno, ancient, anubis. | Тест-чеклист |
| 5.4 | **Гайды**: админский гайд + гайд игрока (команды, бинды, старт). | `docs/GUIDES.md` |

---

## 5. Эксплуатационные риски и mitigation

| Риск | Вероятность | Митигация |
|------|------------|-----------|
| sv_pure блокирует кастом-модели | Высокая (dedicated) | Whitelist + tutorial по запуску |
| Performance degradation >24 игроков | Средняя | Subtick-safe loop + particle cleanup |
| Entity leak в CombatEffects | Средняя | Timer-based removal, лимит на entities |
| Миграция на dedicated сложная | Средняя | Phased: local → listen → dedi |
| Баланс 198 рас — непросто | Высокая | Разделить на tiers, playtest пачками |
| Локализация не покрывает 100% | Средняя | Auto-extract ключей из .cs при каждом build |

---

## 6. Следующие шаги (сразу)

1. **Интегрировать `ILocalizationReader`** в `AetherionPlugin` + 3-4 ключевых системы.
2. **Заполнить `lang/en.json`** (фолбэк, 346 ключей).
3. **Исправить entity leak** в `CombatEffects` (add removal timer).
4. **Подготовить `platform_english.txt`** для Panorama vote.
5. **Начать PHASE 1.1** (Subtick-safe event loop) — внутренний рефактор, без внешних изменений.

---

_Отчёт обновлён 2026-06-27. Основа: HANDOVER.md, COMPETITIVE_ANALYSIS.md, AETHERION_CS2_CAPABILITY_AUDIT.md, CONFIG_AUDIT.md, аудит исходников._
