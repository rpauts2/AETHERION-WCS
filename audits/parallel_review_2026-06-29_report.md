# Параллельный ревью AETHERION WCS — 2026-06-29

## 1) Race cleanup / concept pass (2000-2004)
Проверены:
- соответствие effect mapping в configs/races.json читаемым тегам;
- tier/division/unlock consistency внутри пула D1;
- не трогал дизайн способностей.

Изменений не потребовалось; очевидных несоответствий в этом срезе не найдено:
- все 4 расы лежат в tier=1, division=1, unlock_level=1;
- ability effect ключи присутствуют в EffectLibrary (pull, bonus_hp, slow_aura, aoe_damage, backstab, teleport_backstab, blinded_bonus, movement_speed, penetration, wallhack, blink, sniper_ult, heal_aura, summon_wisp, cleanse, team_shield, freeze_chance, ice_bolt, self_freeze_aoe).

TODO:
- при расширении проверки на весь races.json желательно автоматизировать сверку effect-ключей races -> RaceRuntime.

## 2) Localization review (lang/ru.json)
Проверено:
- синтаксис JSON валиден и читается;
- дублирующиеся ключи отсутствуют;
- корректно отображены кириллические строки и escape-последовательности;
- найдены явные несуразности в отдельных переводах:
  - "Теір" (опечатка, должно быть "Тир");
  - "Плазма-Шеврон", "Мега-Башмак" и подобные гибридные записи оставлены без изменения как намеренные.

Изменения: нет.

TODO:
- вычистить явные опечатки типа "Теір" и унифицировать вложенные "; " если это влияет на читаемость.

## 3) UX review:Race selection + ability display
Проверены:
- src/UI/AetherHud.cs (race card + список рас);
- src/Plugins/AetherNexus.cs (Nexus-сектор рас);
- src/Races/RaceManager.cs.

Найденные проблемы:
- AetherHud.RaceList отрисовывает 9 позиций, но список не сортируется по доступности/треку прогресса, только по Id -> потенциально неинтуитивный порядок при большом числе рас.
- В RaceCard упрощённый расчёт NextLevelXp выглядит как формула "кругов", но отдельный порог nextThr не привязан к этому XP — визуально может разойтись с реальным порогом следующего круга.

Изменений не вносились (требует дискуссии по структуре UI).

TODO:
- добавить явную сортировку RaceList (unlock/текущий division);
- синхронизировать формулу XP и прогресс-бар с UnlockSystem.NextLevelXp.

## 4) Guild/boss/event review
Проверены:
- src/Systems/GuildSystem.cs;
- src/Systems/BossSystem.cs;
- src/Systems/AetherRiftEvent.cs.

GuildSystem:
- базовая функциональность CREATE/JOIN/LEAVE работает;
- критическая проблема: лидер гильдии при выходе выбирает наследника из Officers, но сам элемент не удаляется из Officers() — возможна ситуация, когда новый лидер остаётся в списке Officers вместе с собой, а ушедший лидер тоже может туда попасть (при Leave не очищается Officers у текущего лидера если он не лидер).

BossSystem:
- явно заглушка (Vote disabled, пустой TickBoss/OnPlayerDeath/OnPlayerHurt). Базовый вызов не упадёт, но функционала нет.

AetherRiftEvent:
- структура в целом целая;
- минус: зависимость от IEngineApi и жёсткая привязка к particles/aether_rift.vpcf, particles/aether_capture.vpcf; если файлы отсутствуют — строка упадёт по exception при SpawnParticle.

TODO:
- доработать передачу наследника и очистку Officers в GuildSystem.Leave;
- реализовать хоть минимальный цикл босса;
- обернуть вызовы SpawnParticle в try/catch или graceful fallback.

## 5) Bug review
Проверены:
- src/Races/RaceRuntime.cs;
- src/Systems/CombatEffects.cs;
- src/Systems/AudioManager.cs.

Наиболее важные находки:
- В RaceRuntime.Effects есть множество дублирующих ключей (invisibility, blink, execute, execute_bonus, execute_ult, chase_speed, armor_reduce, parry, accuracy_debuff, clone_ult, radar, turret, bullet_wall, orbiting_blades, illuison, silent_steps, silent_projectile, summon_wisp, revive, disarm, drone_storm). В C# словарь перезаписывает последнее значение; часть заявленных эффектов работает не как задумано в конфигах, а как упавший alias.
- CombatEffects.AoeApply/AoeDamage используют _engine.SetHealth из CombatEffects, не из AbilityContext; если словарь эффкетов добавляет два эффекта за тик, они могут конфликтовать по таймеру.
- AudioManager.Load требует sounds.json в configs/assets/, но при отсутствии файла просто выводит message и молчит дальше — при этом PlayUlt/PlayCastUltimate могут тихо не проиграть звук без понятной причины для игрока.

Изменений в код не вносилось.

TODO:
- вынести дубликаты ключей, привести alias к единому источнику (configs/races.json -> уникальный маппинг);
- добавить fallback-логи при отсутствии звуков;
- предусмотреть null-safe обёртки вокруг EngineAPI.
