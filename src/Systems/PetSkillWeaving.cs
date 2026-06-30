using System;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WcsInfinity.Systems;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════╗
// ║  PET COMPANION SKILL WEAVING — сплетение скиллов питомца.        ║
// ║  Питомец держит до 3 активных «нитей» (ink=чернила), каждая    ║
// ║  даёт локальный бафф/дебафф. Переплетение даёт комбо-эффекты.   ║
// ╚══════════════════════════════════════════════════════════════════╝
public enum ThreadId { Ember, Frost, Storm }

public class PetThread
{
    public ThreadId Id;
    public string Name;
    public float Power;
}

public static class PetSkillWeaving
{
    public static int MaxActiveThreads => 3;

    // Комбо-эффекты: набор нитей -> локальный эффект на владельца
    private static readonly Dictionary<string, EffectTag> _combos = new()
    {
        ["Ember+Frost"] = EffectTag.Berserk,
        ["Frost+Storm"] = EffectTag.Shield,
        ["Ember+Storm"] = EffectTag.Lifesteal,
        ["Ember+Frost+Storm"] = EffectTag.Shield,
    };

    private static readonly Dictionary<string, float> _comboValues = new()
    {
        ["Ember+Frost"] = 0.25f,
        ["Frost+Storm"] = 180f,
        ["Ember+Storm"] = 0.20f,
        ["Ember+Frost+Storm"] = 220f,
    };

    public static string ResolveCombo(HashSet<ThreadId> threads, out EffectTag tag, out float value)
    {
        tag = EffectTag.Berserk; value = 0f;
        if (threads == null || threads.Count == 0) return "none";
        string key = string.Join("+", threads.OrderBy(t => t.ToString()));
        if (_combos.TryGetValue(key, out var t)) tag = t;
        if (_comboValues.TryGetValue(key, out var v)) value = v;

        return key switch
        {
            "Ember+Frost" => "Пламя + Лёд → ярость",
            "Frost+Storm" => "Лёд + Шторм → щит",
            "Ember+Storm" => "Пламя + Шторм → вампиризм",
            "Ember+Frost+Storm" => "Триада → абсолютный щит",
            _ => "Тишина"
        };
    }

    public static int ActiveFor() => 0;
}
