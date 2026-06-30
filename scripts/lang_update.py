import json, os

base = r"C:\Users\Administrator\Desktop\AETHERION-WCS"
ru_path = os.path.join(base, "lang", "ru.json")
en_path = os.path.join(base, "lang", "en.json")

changes = {
    ################################################################
    # 2000
    "leap": {
        "ru": "Скачок",
        "en": "Leap"
    },
    "blink": {
        "ru": "Эфирный шаг",
        "en": "Ether Step"
    },
    "phase_shift": {
        "ru": "Фазовая невидимость",
        "en": "Phase Shift"
    },
    ################################################################
    # 2001
    "lifesteal_passive": {
        "ru": "Вампиризм",
        "en": "Lifesteal"
    },
    "evasion_chance": {
        "ru": "Уклонение",
        "en": "Evasion Chance"
    },
    ################################################################
    # 2002
    "damage_reflect": {
        "ru": "Ледяной щит",
        "en": "Reflective Ice"
    },
    ################################################################
    # 2003
    "conditional_buff": {
        "ru": "Усилитель урона",
        "en": "Conditional Damage Amp"
    },
    ################################################################
    # 2004
    "crit_chance": {
        "ru": "Критический укус",
        "en": "Critical Bite"
    },
    "armor_break": {
        "ru": "Взрывной разлом",
        "en": "Armor Break"
    },
    "mana_shield": {
        "ru": "Щит-скорлупа",
        "en": "Mana Shell"
    },
    ################################################################
    # 2005
    "frost_nova": {
        "ru": "Токсическое облако",
        "en": "Frost Nova"
    },
    "pet_swarm": {
        "ru": "Рой плесеней",
        "en": "Spore Swarm"
    },
    ################################################################
    # 2006
    ################################################################
    # 2007
    "Execute_low_hp": {
        "ru": "Кровавый азарт",
        "en": "Execute Low HP"
    },
    ################################################################
    # 2008
    "summon_ward": {
        "ru": "Живой щит",
        "en": "Warder Shield"
    },
    ################################################################
    # 2009
    "terrain interaction": {
        "ru": "Темная аура",
        "en": "Terrain Interaction"
    },
    "chain_lightning": {
        "ru": "Цепной угрь",
        "en": "Chain Lightning"
    },
    ################################################################
    # 2010
    "magic_damage_amp": {
        "ru": "Эгида забвения",
        "en": "Magic Amp Shield"
    },
    ################################################################
    # 2011
    ################################################################
    # 2012
    "conditionalbuff": {
        "ru": "Бальзам",
        "en": "ConditionalBuff"
    },
    ################################################################
    # 2013
    ################################################################
    # 2014
    ################################################################
    # 2015
    "berserk": {
        "ru": "Мгновенная расправа",
        "en": "Instant Execution"
    },
    ################################################################
    # 2016
    "slow_aura": {
        "ru": "Эхо льда",
        "en": "Slow Aura"
    },
    ################################################################
    # 2017
    "nova_knockback": {
        "ru": "Разлом пустоты",
        "en": "Void Nova"
    },
    ################################################################
    # 2018
    ################################################################
    # 2019
    ################################################################
    # 2020
    "cooldown_reduction": {
        "ru": "Аура вечности",
        "en": "Cooldown Reduction"
    },
    ################################################################
    # 2021
    ################################################################
    # 2022
    ################################################################
    # 2023
    ################################################################
    # 2024
}

# Add API-localized effect keys for UI tooltips if needed.
for path in [ru_path, en_path]:
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)

    lang = "ru" if "ru" in path else "en"
    data.setdefault("D1Rebalance", {})
    for key, text in changes.items():
        # Map normalized key to ability root name token used in configs
        norm = key.replace("_", " ")
        data.setdefault("D1Rebalance", {}).setdefault("AbilityEffect", {})[key] = text[lang]

    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)

print("Lang updated.")
