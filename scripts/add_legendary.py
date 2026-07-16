#!/usr/bin/env python3
"""Добавляет 5 легендарных рас (ID 2035-2039) в configs/races.json"""
import json

with open('configs/races.json', encoding='utf-8') as f:
    races = json.load(f)

legendary = [
    {
        "id": 2035,
        "name": "Yidhari - Эфирная Грация",
        "tier": 5,
        "division": 5,
        "archetype": "duelist",
        "guild": True,
        "unlock_level": 80,
        "lore": "Танцовщица из мира Zenless Zone Zero, скользящая между измерениями. Каждый её удар - это грация смерти. Модель: GameBanana #690671.",
        "abilities": [
            {"n": "Эфирный танец", "t": "Passive", "max": 5,
             "d": "+12% к скорости передвижения за каждый уровень; при киле +15 HP",
             "effect": "speed_buff", "value": 12, "stack": "kill"},
            {"n": "Зеркальный сдвиг", "t": "Active", "max": 3,
             "d": "Телепорт вперёд на 350 юнитов + невидимость 1.5 сек",
             "effect": "blink", "value": 350, "cd": 8},
            {"n": "Лезвия Эфира", "t": "Passive", "max": 4,
             "d": "Каждый 4-й удар наносит дополнительно 60 урона (комбо-серия)",
             "effect": "execute", "value": 60, "radius": 100},
            {"n": "Шторм Клинков", "t": "Ultimate", "max": 1,
             "d": "5 секунд: оранжевая аура 300 юнитов, наносит 30 урона/сек всем врагам вокруг + замедление 40%",
             "effect": "storm_ult", "value": 30, "radius": 300, "duration": 5, "cd": 50}
        ]
    },
    {
        "id": 2036,
        "name": "Master Chief - СПАРТАНЕЦ",
        "tier": 5,
        "division": 5,
        "archetype": "tank",
        "guild": True,
        "unlock_level": 85,
        "lore": "Супер-солдат из вселенной Halo. Его броня MJOLNIR поглощает урон, а сам он не знает страха. Модель: GameBanana #464875.",
        "abilities": [
            {"n": "Броня MJOLNIR", "t": "Passive", "max": 5,
             "d": "+30 макс. HP за уровень; поглощает 20% входящего урона",
             "effect": "bonus_hp", "value": 30, "stack": "level"},
            {"n": "Спартанский щит", "t": "Active", "max": 3,
             "d": "4 сек: +150 брони и отражение 50% урона обратно",
             "effect": "shield", "value": 150, "duration": 4, "cd": 15},
            {"n": "Регенерация СПАРТАН", "t": "Passive", "max": 4,
             "d": "Вне боя: +5 HP/сек регенерации",
             "effect": "regen", "value": 5, "tick": 1},
            {"n": "УДАР СПАРТАНЦА", "t": "Ultimate", "max": 1,
             "d": "Прыжок-удар: AoE урон 120 + оглушение 2 сек всем в радиусе 250",
             "effect": "strike", "value": 120, "radius": 250, "duration": 2, "cd": 40}
        ]
    },
    {
        "id": 2037,
        "name": "Deadpool - БЕССМЕРТНЫЙ ШУТНИК",
        "tier": 5,
        "division": 5,
        "archetype": "assassin",
        "guild": True,
        "unlock_level": 90,
        "lore": "Наемник с регенерацией, который ломает четвёртую стену. Невозможно убить - только задержать. Модель: GameBanana #483043.",
        "abilities": [
            {"n": "Фактор исцеления", "t": "Passive", "max": 5,
             "d": "Регенерация 8 HP/сек всегда (даже в бою)",
             "effect": "regen", "value": 8, "tick": 1},
            {"n": "Двойные диглы", "t": "Passive", "max": 3,
             "d": "Deagle стреляет в 2 раза быстрее + +15% шанс хедшота",
             "effect": "haste", "value": 15},
            {"n": "Телепорт-протонка", "t": "Active", "max": 3,
             "d": "Рандомный телепорт в радиусе 500 юнитов + ослепление врагов рядом",
             "effect": "teleport", "value": 500, "cd": 10},
            {"n": "ВЕРНЁТСЯ! (4-я стена)", "t": "Ultimate", "max": 1,
             "d": "При смерти: мгновенно воскрешается с 50% HP и баффом 8 сек (двойной урон)",
             "effect": "revive", "value": 50, "duration": 8, "cd": 120}
        ]
    },
    {
        "id": 2038,
        "name": "Yoru - РАЗРЫВАТЕЛЬ ИЗМЕРЕНИЙ",
        "tier": 5,
        "division": 5,
        "archetype": "assassin",
        "guild": True,
        "unlock_level": 95,
        "lore": "Агент из Valorant, способный открывать порталы между измерениями. Его шаги неслышны, а удар приходит из ниоткуда. Модель: GameBanana #465393.",
        "abilities": [
            {"n": "Тихий шаг", "t": "Passive", "max": 5,
             "d": "Бесшумное передвижение + +20% скорости в приседе",
             "effect": "speed_buff", "value": 20},
            {"n": "Разлом", "t": "Active", "max": 3,
             "d": "Открывает портал: телепорт на 600 юнитов + пометка всех врагов в радиусе 300",
             "effect": "blink", "value": 600, "radius": 300, "cd": 14},
            {"n": "Кража измерения", "t": "Passive", "max": 4,
             "d": "Бэкстаб (удар в спину): x2.5 урон + 25 HP воруется",
             "effect": "execute", "value": 25, "radius": 100},
            {"n": "ПОРТАЛ ИЗМЕРЕНИЙ", "t": "Ultimate", "max": 1,
             "d": "8 сек: телепорт к любому врагу на карте (по прицелу) + 200 урона + замедление 3 сек",
             "effect": "teleport", "value": 200, "duration": 3, "radius": 0, "cd": 55}
        ]
    },
    {
        "id": 2039,
        "name": "Metrocop - КОЛЛАБЕ РЕЙД",
        "tier": 5,
        "division": 5,
        "archetype": "controller",
        "guild": True,
        "unlock_level": 100,
        "lore": "Альянсовский метрокоп из Half-Life: Alyx. Подавитель сопротивления, controlling площадей через страх и электричество. Модель: GameBanana #470173.",
        "abilities": [
            {"n": "Электро-броня", "t": "Passive", "max": 5,
             "d": "Пассивная защита: атакующие получают 15 обратного электро-урона",
             "effect": "reflect", "value": 15},
            {"n": "ЭМИ-удар", "t": "Active", "max": 3,
             "d": "Электромагнитный импульс: 60 урона + disarm 2 сек всем в радиусе 200",
             "effect": "strike", "value": 60, "radius": 200, "duration": 2, "cd": 12},
            {"n": "Страх подавления", "t": "Passive", "max": 4,
             "d": "Враги в радиусе 150 юнитов замедляются на 20% и дрожат",
             "effect": "slow", "value": 20, "radius": 150},
            {"n": "ПОДАВЛЕНИЕ СЕКТОРА", "t": "Ultimate", "max": 1,
             "d": "6 сек: вся область в радиусе 400 - зона подавления: враги получают 40 урона/сек + стан 1 сек каждые 2 сек",
             "effect": "storm_ult", "value": 40, "radius": 400, "duration": 6, "cd": 60}
        ]
    }
]

races.extend(legendary)

with open('configs/races.json', 'w', encoding='utf-8') as f:
    json.dump(races, f, ensure_ascii=False, indent=2)

print(f"OK Добавлено {len(legendary)} легендарных рас. Всего: {len(races)}")
for r in legendary:
    print(f"  #{r['id']} {r['name']} - {len(r['abilities'])} способности")
