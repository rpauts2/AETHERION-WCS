import json, pathlib

p = pathlib.Path('C:/Users/Administrator/Desktop/AETHERION-WCS/configs/races.json')
obj = json.loads(p.read_text(encoding='utf-8'))
idx_by_id = {r['id']: i for i, r in enumerate(obj)}
assert len(idx_by_id) == len(obj), 'duplicate ids'

updates = {
    2000: [ # Awful's Vidya Gaem Collection - Glitch Mage
        {'n': 'Power Glitch', 't': 'Active', 'max': 5, 'd': 'Прыжок с уроном при приземлении · Актив · макс 5 · КД 18с', 'effect': 'leap', 'value': 320, 'cd': 18},
        {'n': 'Save Scum', 't': 'Active', 'max': 4, 'd': 'Короткий телепорт с перезарядкой способностей · Актив · макс 4 · КД 16с', 'effect': 'blink', 'value': 260, 'cd': 16},
        {'n': 'Lag Switch', 't': 'Active', 'max': 4, 'd': 'Невидимость + замедление врагов рядом · Актив · макс 4 · КД 22с', 'effect': 'phase_shift', 'value': 8, 'cd': 22}
    ],
    2001: [ # Fox McCloud - Winged Ace
        {'n': 'Вампиризм Arwing', 't': 'Passive', 'max': 5, 'd': 'Возвращает % урона в HP · Пассивно · макс 5', 'effect': 'lifesteal_passive', 'value': 20},
        {'n': 'Вихрь Arwing', 't': 'Active', 'max': 4, 'd': 'Рывок с усколением и уроном · Актив · макс 4 · КД 16с', 'effect': 'berserk', 'value': 25, 'cd': 16},
        {'n': 'Уклонение', 't': 'Passive', 'max': 5, 'd': 'Шанс уклониться с контуром · Пассивно · макс 5', 'effect': 'evasion_chance', 'value': 18}
    ],
    2002: [ # Colossus - Thermal Walker
        {'n': 'Термо-щит', 't': 'Passive', 'max': 5, 'd': 'Отражает thermal-урон и замедляет · Пассивно · макс 5', 'effect': 'damage_reflect', 'value': 35},
        {'n': 'Ступень', 't': 'Active', 'max': 4, 'd': 'Телепорт по земле + усиление урона · Актив · макс 4 · КД 14с', 'effect': 'blink', 'value': 260, 'cd': 14},
        {'n': 'Кольцо огня', 't': 'Active', 'max': 4, 'd': 'Окружающий урон + замедление · Актив · макс 4 · КД 18с', 'effect': 'frost_nova', 'value': 24, 'cd': 18}
    ],
    2003: [ # Terran Marine - Shock Trooper
        {'n': 'Тактический прыжок', 't': 'Active', 'max': 4, 'd': 'Скачок с перезарядкой оружия · Актив · макс 4 · КД 14с', 'effect': 'leap', 'value': 300, 'cd': 14},
        {'n': 'Тактический подавитель', 't': 'Active', 'max': 5, 'd': 'Снижает урон врага и поджигает · Актив · макс 5 · КД 18с', 'effect': 'armor_break', 'value': 30, 'cd': 18},
        {'n': 'Shield Battery', 't': 'Active', 'max': 4, 'd': 'Хил союзников рядом · Актив · макс 4 · КД 25с', 'effect': 'summon_ward', 'value': 90, 'cd': 25}
    ],
    2004: [ # Baneling - Juggernaut Roller
        {'n': 'Критический укус', 't': 'Passive', 'max': 5, 'd': 'Шанс на +% урона при атаке · Пассивно · макс 5', 'effect': 'crit_chance', 'value': 22},
        {'n': 'Acid Explode', 't': 'Ultimate', 'max': 4, 'd': 'Взрыв с дроблением брони · Ульта · макс 4 · КД 35с', 'effect': 'armor_break', 'value': 40, 'cd': 35},
        {'n': 'Acid Blood', 't': 'Passive', 'max': 5, 'd': 'Отражает урон при получении удара · Пассивно · макс 5', 'effect': 'damage_reflect', 'value': 15}
    ],
    2005: [ # Infestor - Mind Controller
        {'n': 'Fungal Growth', 't': 'Active', 'max': 4, 'd': 'Зона отравления + замедление · Актив · макс 4 · КД 18с', 'effect': 'slow', 'value': 45, 'cd': 18},
        {'n': 'Swarm', 't': 'Ultimate', 'max': 4, 'd': 'Призыв летающих юнитов · Ульта · макс 4 · КД 35с', 'effect': 'pet_swarm', 'value': 6, 'cd': 35},
        {'n': 'Move', 't': 'Active', 'max': 5, 'd': 'Смена места с иллюзией · Актив · макс 5 · КД 20с', 'effect': 'blink', 'value': 240, 'cd': 20}
    ],
    2006: [ # Roach - Bulky Tank
        {'n': 'Кислотный панцирь', 't': 'Passive', 'max': 5, 'd': 'Отражает часть урона врагу · Пассивно · макс 5', 'effect': 'damage_reflect', 'value': 28},
        {'n': 'Бронебой', 't': 'Active', 'max': 5, 'd': 'Снижает броню цели · Актив · макс 5 · КД 18с', 'effect': 'armor_break', 'value': 35, 'cd': 18},
        {'n': 'Третий шанс', 't': 'Passive', 'max': 5, 'd': 'Каждая 3 атака наносит доп. урон · Пассивно · макс 5', 'effect': 'conditional_buff', 'value': 30}
    ],
    2007: [ # Ultralisk - Heavy Assault
        {'n': 'Шоковый прыжок', 't': 'Active', 'max': 4, 'd': 'Прыжок с оглушением по приземлению · Актив · макс 4 · КД 22с', 'effect': 'leap', 'value': 340, 'cd': 22},
        {'n': 'Берсерк хвостов', 't': 'Passive', 'max': 5, 'd': '+урон при низком HP · Пассивно · макс 5', 'effect': 'Execute_low_hp', 'value': 35},
        {'n': 'Шипы', 't': 'Passive', 'max': 5, 'd': 'Наносит урон при получении удара · Пассивно · макс 5', 'effect': 'damage_reflect', 'value': 40}
    ],
    2008: [ # Juggernaut - Mobile Brawler
        {'n': 'Вихрь', 't': 'Active', 'max': 5, 'd': 'Окружает врагов AoE-уроном · Актив · макс 5 · КД 18с', 'effect': 'frost_nova', 'value': 28, 'cd': 18},
        {'n': 'Живой щит', 't': 'Active', 'max': 4, 'd': 'Создаёт защитный торе́м вокруг союзников · Актив · макс 4 · КД 20с', 'effect': 'summon_ward', 'value': 120, 'cd': 20},
        {'n': 'Страж-Тень', 't': 'Active', 'max': 4, 'd': 'Быстрый рывок в спину врагу · Актив · макс 4 · КД 18с', 'effect': 'blink', 'value': 280, 'cd': 18}
    ],
    2009: [ # Naga Сирена - Tidecaller
        {'n': 'Рев прилива', 't': 'Ultimate', 'max': 5, 'd': 'Сбивает и тянет врагов к цели · Ульта · макс 5 · КД 35с', 'effect': 'nova_knockback', 'value': 35, 'cd': 35},
        {'n': 'Вихрь', 't': 'Passive', 'max': 5, 'd': 'Замедляет атакующих врагов · Пассивно · макс 5', 'effect': 'slow', 'value': 35, 'cd': 0},
        {'n': 'Цепной угрь', 't': 'Active', 'max': 5, 'd': 'Молния между врагами · Актив · макс 5 · КД 18с', 'effect': 'chain_lightning', 'value': 120, 'cd': 18}
    ],
    2010: [ # Pugna Oblivion - Sigil Weaver
        {'n': 'Эгида забвения', 't': 'Passive', 'max': 5, 'd': 'Поглощает урон и конвертирует в эфир · Пассивно · макс 5', 'effect': 'magic_damage_amp', 'value': 30},
        {'n': 'Война теней', 't': 'Ultimate', 'max': 4, 'd': 'Иллюзии клоня урон · Ульта · макс 4 · КД 35с', 'effect': 'summon_ward', 'value': 2, 'cd': 35},
        {'n': 'Удар Бездны', 't': 'Active', 'max': 5, 'd': 'Рывок с уроном по площади · Актив · макс 5 · КД 18с', 'effect': 'leap', 'value': 320, 'cd': 18}
    ],
    2011: [ # Barathrum - Thunder Caster
        {'n': 'Рокот бури', 't': 'Active', 'max': 5, 'd': 'Поражение ближайшего врага молнией · Актив · макс 5 · КД 18с', 'effect': 'chain_lightning', 'value': 140, 'cd': 18},
        {'n': 'Подавление', 't': 'Active', 'max': 4, 'd': 'Снижает урон врагов вокруг · Актив · макс 4 · КД 18с', 'effect': 'magic_damage_amp', 'value': 30, 'cd': 18},
        {'n': 'Зов эфира', 't': 'Ultimate', 'max': 4, 'd': 'Создаёт стража с вспышкой · Ульта · макс 4 · КД 35с', 'effect': 'nova_knockback', 'value': 40, 'cd': 35}
    ],
    2012: [ # Dirge - Soul Medic
        {'n': 'Бальзам', 't': 'Passive', 'max': 5, 'd': 'Хил союзников при ударе · Пассивно · макс 5', 'effect': 'conditionalbuff', 'value': 20},
        {'n': 'Rally Cry', 't': 'Active', 'max': 5, 'd': 'Массовый хил + щит · Актив · макс 5 · КД 22с', 'effect': 'summon_ward', 'value': 80, 'cd': 22},
        {'n': 'Ward', 't': 'Ultimate', 'max': 4, 'd': 'Останавливает смертельный урон на союзника · Ульта · макс 4 · КД 35с', 'effect': 'damage_reflect', 'value': 35, 'cd': 35}
    ],
    2013: [ # Necro'lic Visage - Stalker
        {'n': 'Призрак шага', 't': 'Passive', 'max': 5, 'd': 'Шанс не оставить след при движении · Пассивно · макс 5', 'effect': 'evasion_chance', 'value': 20},
        {'n': 'Щепка Пустоты', 't': 'Active', 'max': 4, 'd': 'Взрыв с разлётом осколков · Актив · макс 4 · КД 18с', 'effect': 'frost_nova', 'value': 30, 'cd': 18},
        {'n': 'Soul Rend', 't': 'Ultimate', 'max': 5, 'd': 'Убийство низкого HP усиливает атаку · Ульта · макс 5 · КД 35с', 'effect': 'Execute_low_hp', 'value': 35, 'cd': 35}
    ],
    2014: [ # Windrunner - Gale Archer
        {'n': 'Ветер-лезвие', 't': 'Active', 'max': 5, 'd': 'Серия быстрых ударов по прямой · Актив · макс 5 · КД 18с', 'effect': 'crit_chance', 'value': 28, 'cd': 18},
        {'n': 'Gust Step', 't': 'Passive', 'max': 5, 'd': 'Уклон при движении + ускорение · Пассивно · макс 5', 'effect': 'evasion_chance', 'value': 15},
        {'n': 'Поджог ветра', 't': 'Ultimate', 'max': 4, 'd': 'Дождь стрел по зоне · Ульта · макс 4 · КД 35с', 'effect': 'chain_lightning', 'value': 130, 'cd': 35}
    ],
    2015: [ # Meet Spy - Backstabber
        {'n': 'Укол в спину', 't': 'Passive', 'max': 5, 'd': 'Крит при атаке со спины/сбоку · Пассивно · макс 5', 'effect': 'crit_chance', 'value': 30},
        {'n': 'Слез. дым', 't': 'Active', 'max': 4, 'd': 'Создаёт дымовую завесу · Актив · макс 4 · КД 18с', 'effect': 'invisibility', 'value': 40, 'cd': 18},
        {'n': 'Мгновенная расправа', 't': 'Ultimate', 'max': 4, 'd': 'Удвоенный урон на след. атаку · Ульта · макс 4 · КД 28с', 'effect': 'berserk', 'value': 45, 'cd': 28}
    ],
    2016: [ # Ancient Frost Lord - Glacier Mage
        {'n': 'Адаптивный холод', 't': 'Active', 'max': 5, 'd': 'Заморозка с доп. уроном по замороженным · Актив · макс 5 · КД 18с', 'effect': 'frost_nova', 'value': 24, 'cd': 18},
        {'n': 'Эхо льда', 't': 'Active', 'max': 4, 'd': 'Отсроченный AoE замедление · Актив · макс 4 · КД 18с', 'effect': 'slow', 'value': 35, 'cd': 18},
        {'n': 'Кристаллическая форма', 't': 'Ultimate', 'max': 5, 'd': 'Иммунитет к управлению, замедление вокруг · Ульта · макс 5 · КД 35с', 'effect': 'phase_shift', 'value': 12, 'cd': 35}
    ],
    2017: [ # Void Lord - Rift Stalker
        {'n': 'Разлом пустоты', 't': 'Active', 'max': 5, 'd': 'Оглушает и тянет ближайших · Актив · макс 5 · КД 18с', 'effect': 'nova_knockback', 'value': 35, 'cd': 18},
        {'n': 'Шаг в бездну', 't': 'Active', 'max': 4, 'd': 'Телепорт через врагов с уроном · Актив · макс 4 · КД 20с', 'effect': 'blink', 'value': 300, 'cd': 20},
        {'n': 'Облик пустоты', 't': 'Ultimate', 'max': 4, 'd': 'Невидимость + ускорение отряда · Ульта · макс 4 · КД 35с', 'effect': 'phase_shift', 'value': 12, 'cd': 35}
    ],
    2018: [ # Dark Templar - Phantom Blade
        {'n': 'Фантомный клинок', 't': 'Active', 'max': 5, 'd': 'Атака из тени с критическим уроном · Актив · макс 5 · КД 18с', 'effect': 'crit_chance', 'value': 35, 'cd': 18},
        {'n': 'Шепот пустоты', 't': 'Passive', 'max': 5, 'd': 'Снижает ауру врагов · Пассивно · макс 5', 'effect': 'magic_damage_amp', 'value': 25},
        {'n': 'Storm', 't': 'Ultimate', 'max': 5, 'd': 'Молнии по зоне + страх · Ульта · макс 5 · КД 35с', 'effect': 'chain_lightning', 'value': 140, 'cd': 35}
    ],
    2019: [ # Morphling - Adaptive Fluid
        {'n': 'Изменение формы', 't': 'Passive', 'max': 5, 'd': 'Адаптирует статы под врага · Пассивно · макс 5', 'effect': 'conditional_buff', 'value': 22},
        {'n': 'Волна уплотнения', 't': 'Active', 'max': 5, 'd': 'Взрыв водой: замедление + хил себе · Актив · макс 5 · КД 18с', 'effect': 'frost_nova', 'value': 20, 'cd': 18},
        {'n': 'Приливная волна', 't': 'Ultimate', 'max': 4, 'd': 'Оглушает и притягивает врагов · Ульта · макс 4 · КД 35с', 'effect': 'leap', 'value': 300, 'cd': 35}
    ],
    2020: [ # Immortal - Eternal Support
        {'n': 'Аура вечности', 't': 'Passive', 'max': 5, 'd': 'Снижает КД союзников рядом · Пассивно · макс 5', 'effect': 'cooldown_reduction', 'value': 12},
        {'n': 'Стена света', 't': 'Active', 'max': 5, 'd': 'Отталкивает врагов · Актив · макс 5 · КД 18с', 'effect': 'nova_knockback', 'value': 40, 'cd': 18},
        {'n': 'Перерождение', 't': 'Ultimate', 'max': 5, 'd': 'Сброс смерти и иммунитет · Ульта · макс 5 · КД 40с', 'effect': 'damage_reflect', 'value': 50, 'cd': 40}
    ],
    2021: [ # Unstable Anomaly - Glitch Horror
        {'n': 'Аномальный скачок', 't': 'Active', 'max': 5, 'd': 'Телепорт с отдачей урона · Актив · макс 5 · КД 18с', 'effect': 'blink', 'value': 280, 'cd': 18},
        {'n': 'Chaos Aura', 't': 'Passive', 'max': 4, 'd': 'Случайный бафф/дебафф на врагов · Пассивно · макс 4', 'effect': 'conditional_buff', 'value': 18},
        {'n': 'Взрыв Fundamental', 't': 'Ultimate', 'max': 4, 'd': 'Массовый урон + телепорт · Ульта · макс 4 · КД 38с', 'effect': 'chain_lightning', 'value': 130, 'cd': 38}
    ],
    2022: [ # Knife Race - Blade Dancer
        {'n': 'Острые клинки', 't': 'Passive', 'max': 5, 'd': 'Шанс пробить броню · Пассивно · макс 5', 'effect': 'armor_break', 'value': 22},
        {'n': 'Тень ножа', 't': 'Passive', 'max': 5, 'd': '+урон при необнаружении · Пассивно · макс 5', 'effect': 'invisibility', 'value': 35},
        {'n': 'Вихрь клинков', 't': 'Active', 'max': 5, 'd': 'Вращение с AoE уроном · Актив · макс 5 · КД 18с', 'effect': 'frost_nova', 'value': 30, 'cd': 18}
    ],
    2023: [ # Orzhov Acolyte - Saint of Gold
        {'n': 'Святой Грааль', 't': 'Ultimate', 'max': 5, 'd': 'Массовый хил и защита · Ульта · макс 5 · КД 35с', 'effect': 'summon_ward', 'value': 180, 'cd': 35},
        {'n': 'Pillage', 't': 'Active', 'max': 5, 'd': 'Снижает защиту врага + кража золота · Актив · макс 5 · КД 18с', 'effect': 'armor_break', 'value': 35, 'cd': 18},
        {'n': 'Tithe Aura', 't': 'Passive', 'max': 5, 'd': 'Хил союзников рядом · Пассивно · макс 5', 'effect': 'conditionalbuff', 'value': 18}
    ],
    2024: [ # Dark Archon - Psionic Tyrant
        {'n': 'Архонт-Разряд', 't': 'Active', 'max': 5, 'd': 'Цепная молния по врагам · Актив · макс 5 · КД 18с', 'effect': 'chain_lightning', 'value': 150, 'cd': 18},
        {'n': 'Архонт-Щит', 't': 'Passive', 'max': 5, 'd': 'Щит при получении магии · Пассивно · макс 5', 'effect': 'mana_shield', 'value': 40},
        {'n': 'Искажение', 't': 'Ultimate', 'max': 5, 'd': 'Фаза + урон отражением · Ульта · макс 5 · КД 35с', 'effect': 'phase_shift', 'value': 10, 'cd': 35}
    ],
}

assert set(updates) <= {r['id'] for r in obj}
for rid, new_abs in updates.items():
    idx = idx_by_id[rid]
    obj[idx]['abilities'] = new_abs

p.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding='utf-8')
print('updated', len(updates), 'races')
