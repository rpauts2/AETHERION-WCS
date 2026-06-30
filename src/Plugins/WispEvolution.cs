using System;
using System.Collections.Generic;
using System.Linq;

namespace WcsInfinity.Plugins;

// ╔══════════════════════════════════════════════════════════════════════╗
// ║  ЭВОЛЮЦИЯ ДУХА (WISP EVOLUTION) — дух растёт вместе с игроком.         ║
// ║  Bond (привязанность) копится за килы/сессии. На порогах дух          ║
// ║  ЭВОЛЮЦИОНИРУЕТ: новый облик, имя, бонусы и пассивные способности.    ║
// ║  Тамагочи встречает WCS — у игрока появляется «питомец», который      ║
// ║  его помнит между сессиями (Bond сохраняется в SQLite).              ║
// ╚══════════════════════════════════════════════════════════════════════╝
public class WispStage
{
    public int Stage;
    public string Title = "";        // название формы
    public int BondRequired;
    public string Glyph = "◔ ◡ ◔";   // базовый облик (меняется по настроению)
    public string Aura = "#7C5CFF";  // цвет ауры формы
    public string PassivePerk = "";  // описание пассивки этой формы
    // числовые бонусы владельцу (читаются боевым расчётом)
    public Dictionary<string,float> OwnerBonus = new();
    public string UnlockLine = "";   // фраза при эволюции
}

public static class WispEvolution
{
    // Ступени эволюции духа. Каждая — новая личность и сила.
    public static readonly List<WispStage> Stages = new()
    {
        new WispStage{ Stage=1, Title="Искорка", BondRequired=0, Glyph="◔ ◡ ◔", Aura="#7C5CFF",
            PassivePerk="Подсказывает печати.",
            UnlockLine="Я только родился из твоего Эфира…" },

        new WispStage{ Stage=2, Title="Светлячок", BondRequired=50, Glyph="◕‿◕", Aura="#46E0FF",
            PassivePerk="+5% накопление Эфира.",
            OwnerBonus={["ether_gain"]=0.05f},
            UnlockLine="Я расту! Теперь Эфир течёт к тебе быстрее." },

        new WispStage{ Stage=3, Title="Дух-Хранитель", BondRequired=200, Glyph="✦ω✦", Aura="#7CFFA0",
            PassivePerk="+10% Эфир, раз в раунд предупреждает о враге за спиной.",
            OwnerBonus={["ether_gain"]=0.10f,["backstab_warn"]=1f},
            UnlockLine="Я твой Хранитель. Я прикрою твою спину." },

        new WispStage{ Stage=4, Title="Эфирный Феникс", BondRequired=600, Glyph="🔥ω🔥", Aura="#FFD24D",
            PassivePerk="+15% Эфир, +20 стартовых HP, аура подсветки союзников.",
            OwnerBonus={["ether_gain"]=0.15f,["bonus_hp"]=20f},
            UnlockLine="Из искры — в пламя! Мы прошли долгий путь вместе." },

        new WispStage{ Stage=5, Title="Аватар Бури", BondRequired=1500, Glyph="⚡Θ⚡", Aura="#FF3B6B",
            PassivePerk="+20% Эфир, -10% КД печатей, раз/раунд авто-Купол при смертельном уроне.",
            OwnerBonus={["ether_gain"]=0.20f,["sigil_cdr"]=0.10f,["death_save"]=1f},
            UnlockLine="Мы — одно целое. Буря Эфира признала тебя." },
    };

    public static WispStage StageFor(int bond) =>
        Stages.Last(s => bond >= s.BondRequired);

    // Проверка эволюции: вернёт новую ступень, если Bond пересёк порог
    public static WispStage? CheckEvolve(int oldBond, int newBond)
    {
        var oldStage = StageFor(oldBond).Stage;
        var newStage = StageFor(newBond);
        return newStage.Stage > oldStage ? newStage : null;
    }

    // Суммарные бонусы владельцу от текущей формы духа
    public static Dictionary<string,float> OwnerBonuses(int bond) =>
        StageFor(bond).OwnerBonus;

    // Прогресс до следующей ступени (0..1) и сколько Bond осталось
    public static (float progress, int remaining, WispStage? next) Progress(int bond)
    {
        var cur = StageFor(bond);
        var next = Stages.FirstOrDefault(s => s.Stage == cur.Stage + 1);
        if (next == null) return (1f, 0, null);
        int span = next.BondRequired - cur.BondRequired;
        int done = bond - cur.BondRequired;
        return ((float)done / span, next.BondRequired - bond, next);
    }
}
