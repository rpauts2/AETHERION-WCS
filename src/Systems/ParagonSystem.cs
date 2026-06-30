using System;
using System.Collections.Generic;
using System.Linq;
using WcsInfinity.Models;
using WcsInfinity.Races;

namespace WcsInfinity.Systems;

// PARAGON — бесконечная прокачка за пределами макс. уровня расы
public enum ParagonAuraId { None, Ember, Frost, Storm, Void, Aether }

public class ParagonAura
{
    public ParagonAuraId Id { get; set; } = ParagonAuraId.None;
    public string AuraParticle { get; set; } = "";
    public string UltAnimation { get; set; } = "";
    public float StatBonus { get; set; } = 1.0f;
}

public static class ParagonSystem
{
    private const long BaseXp = 15000L;
    private static readonly Dictionary<int, ParagonAura> _auras = new();

    private static void EnsureRecipes()
    {
        if (_auras.Count > 0) return;
        _auras[(int)ParagonAuraId.Ember]  = new() { AuraParticle = "particles/aether_paragon_ember.vpcf",  UltAnimation = "cast_ember_overdrive",  StatBonus = 1.10f };
        _auras[(int)ParagonAuraId.Frost]  = new() { AuraParticle = "particles/aether_paragon_frost.vpcf",  UltAnimation = "cast_frost_overdrive",  StatBonus = 1.12f };
        _auras[(int)ParagonAuraId.Storm]  = new() { AuraParticle = "particles/aether_paragon_storm.vpcf",  UltAnimation = "cast_storm_overdrive",  StatBonus = 1.14f };
        _auras[(int)ParagonAuraId.Void]   = new() { AuraParticle = "particles/aether_paragon_void.vpcf",   UltAnimation = "cast_void_overdrive",   StatBonus = 1.16f };
        _auras[(int)ParagonAuraId.Aether] = new() { AuraParticle = "particles/aether_paragon_aether.vpcf", UltAnimation = "cast_aether_overdrive", StatBonus = 1.20f };
    }

    public static bool AddParagonXp(PlayerData p, long xp)
    {
        EnsureRecipes();
        var progress = p.Races.GetValueOrDefault(p.CurrentRaceId);
        if (progress == null) progress = new RaceProgress { RaceId = p.CurrentRaceId };
        p.Races[p.CurrentRaceId] = progress;
        progress.ParagonsTotalXp += xp;
        long needed = XpFor(progress.ParagonLevel);
        if (progress.ParagonsTotalXp < needed) return false;
        progress.ParagonsTotalXp -= needed;
        progress.ParagonLevel++;
        return true;
    }

    public static long XpFor(long level) => BaseXp * level;

    public static ParagonAura? AuraFor(long level)
    {
        EnsureRecipes();
        if (level <= 0) return _auras[(int)ParagonAuraId.None];
        int idx = Math.Min((int)((level - 1) % (_auras.Count - 1)) + 1, _auras.Count - 1);
        return _auras.Values.Skip(1).Skip(idx - 1).FirstOrDefault();
    }

    public static ParagonAuraId CurrentAura(long level)
    {
        EnsureRecipes();
        if (level <= 0) return ParagonAuraId.None;
        int idx = (int)((level - 1) % (_auras.Count - 1)) + 1;
        return (ParagonAuraId)idx;
    }
}
