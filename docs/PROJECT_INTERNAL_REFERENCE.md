# AETHERION-WCS — ВНУТРЕННИЙ ОТЧЁТ ПО ПРОЕКТУ
_Создан: 2026-06-27. Назначение: единый источник истины по коду, конфигам, истории ошибок и статусу._

---

## 1. Общее
- Тип проекта: Server-side RPG-мод (WCS/War3FT-style) для CS2.
- Фреймворк: CounterStrikeSharp (CSSharp), net8.0.
- Хост-ОС: ru-RU Windows, режим dev: listen-server `-insecure`.
- Репозиторий: рабочая папка `C:\Users\Administrator\Desktop\AETHERION-WCS`.
- Сборка: `dotnet build -c Release` → `bin\Release\net8.0\AetherionWcs.dll`.
- Hot-reload: не используется (CS2MenuManager мешает).
- Персистентность: SQLite (`aetherion.db` + `e_sqlite3.dll`).
- Локализация: RU primary (`lang/ru.json`), EN вторично.

---

## 2. Структура ключевых файлов
### Сборка
- `AetherionWcs.csproj` — csproj проекта.

### Документация
- `README.md` — краткий обзор.
- `HANDOVER.md` — инструкция по запуску, деплою, командам, known issues.
- `docs/PROJECT_STATUS.md` — текущие статусы систем.
- `docs/MASTER_PLAN.md` — дорожная карта фаз.
- `docs/GRAND_ROADMAP.md` — расширенный план.
- `docs/RACES_CATALOG.md` — каталог рас и способностей.
- `docs/PLUGINS_DESIGN.md` — дизайн плагинов.
- `docs/LORE.md` — лор мира.

### Конфиги
- `configs/races.json` (11 460 строк) — активный конфиг всех рас.
- `configs/races/races_core.json` (5 608 строк) — базовый сбалансированный набор.
- `configs/assets/models.json`, `sounds.json`.

### Ядро
- `src/Core/AetherionPlugin.cs` — главный плагин: события, команды, меню, тики.
- `src/Core/NexusIntegration.cs` — интеграция интерфейса AetherNexus.

### Модели
- `src/Models/PlayerData.cs` — профиль игрока.

### БД
- `src/Database/SqlitePlayerStore.cs`, `IPlayerStore.cs`.

### Системы
- `src/Systems/LevelSystem.cs` — XP/уровни/ AddXp.
- `src/Systems/EconomySystem.cs` — константы голды.
- `src/Systems/RouletteSystem.cs` — рулетки + пити.
- `src/Systems/ItemShop.cs` — магазин.
- `src/Systems/SeasonSystem.cs` — сезоны/Battle Pass.
- `src/Systems/GuildSystem.cs` — гильдии.
- `src/Systems/AetherRiftEvent.cs` — Эфирный Разлом.
- `src/Systems/AetherCommand.cs` — админ-команды.
- `src/Systems/DailyRewardSystem.cs` — ежедневная награда.
- `src/Systems/UnlockSystem.cs` — открытие рас по общему уровню.
- `src/Systems/CombatEffects.cs` — баффы/дебаффы/тики.
- `src/Systems/Cs2EngineApi.cs` — обёртки движка.
- `src/Systems/AudioManager.cs` — звуки.
- `src/Systems/AuraManager.cs` — ауры/аксессуары.
- `src/Systems/ModelManager.cs` — модели игроков.

### UI
- `src/UI/AetherHud.cs` — HTML HUD.
- `src/UI/AetherWheel.cs` — радиальное меню.

### Плагины
- `src/Plugins/AetherNexus.cs` — 3D-зал AetherNexus.
- `src/Plugins/AetherRoulette.cs` — анимация рулетки.
- `src/Plugins/AetherSigils.cs` — жесты/печати.
- `src/Plugins/SigilResonance.cs` — резонанс.
- `src/Plugins/AetherWisp.cs` — дух-компаньон.
- `src/Plugins/WispEvolution.cs` — эволюция духа.
- `src/Plugins/HoloRaceSelector.cs` — голо-меню рас.

### Локализация
- `lang/ru.json`, `lang/en.json`.

---

## 3. Статус сборки
- Последняя сборка: успешна, 0 ошибок, 0 предупреждений.
- DLL: `C:\Users\Administrator\Desktop\AETHERION-WCS\bin\Release\net8.0\AetherionWcs.dll`.

---

## 4. Что работает
- Спавн → пассивки, HP/скорость/реген.
- Киллы → голда/XP/эфир, мульти-киллы, хедшоты.
- Меню: главное, расы, способности, магазин, рулетка, админ.
- Питомец Wisp + эволюция.
- Боссы, дуэли, Эфирная Буря, рулетка.
- Сохранение в SQLite, 88 рас/408 способностей, 9 моделей.
- 31 боевой эффект в `RaceRuntime`.

---

## 5. Проблемы и ограничения
- Hot-reload отключён.
- Автобинд блокируется движком.
- PanoramaVote не всегда рендерится.
- Д1-расы часто только пассивные.
- Названия рас частично RU/EN микс.
- e_sqlite3.dll требует ручной копии.
- ВНИМАНИЕ: часть исходной истории проекта включает обсуждение читов/VAC. Эта часть не относится к текущему легальному коду и игнорируется.

---

## 6. План дальнейшей разработки (приоритизировано)
- Баланс: кривая голды/XP, минимум 1 активка на D1-расу.
- Локализация: довести ru.json до покрытия всех строк.
- Мета: Battle Pass, ачивки, контракты, лидерборды.
- Духовная: Wisp v2, дуэли v2.
- Социальные: гильдии/GvG, Paragon, титулы.
- Infra: dedicated-сервер, MySQL опция, анти-абуз, CI/CD, Discord/WebUI.
- Полировка: звуки, партиклы, nameplates/HUD, карты, гайды.

---

## 7. Следующий спринт предлагаю начать с:
1. Инвентаризация строк для локализации (вытащить из кода все тексты).
2. Файл `lang/ru.json` расширить до покрытия всех строк.
3. Цифровой аудит баланса экономики: пересчитать кривую голды и XP.
