using System;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WcsInfinity.Systems;

// Диегетическая аура Эфира над игроком — модель по дивизиону расы.
// Корона парится над головой и помечает силу игрока (цвет = дивизион).
public class AuraManager
{
    public static readonly Dictionary<int, string> ByDivision = new()
    {
        [1] = "models/races/aura_d1.vmdl",
        [2] = "models/races/aura_d2.vmdl",
        [3] = "models/races/aura_d3.vmdl",
        [4] = "models/races/aura_dinf.vmdl",
    };
    private readonly Dictionary<int, CBaseEntity?> _auras = new();

    public static void Precache(ResourceManifest m)
    {
        foreach (var v in ByDivision.Values) m.AddResource(v);
    }

    public void Attach(CCSPlayerController player, int division)
    {
        if (player?.PlayerPawn?.Value?.AbsOrigin == null) return;
        if (!ByDivision.TryGetValue(division, out var model)) return;
        Remove(player.Slot);
        var aura = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
        if (aura == null) return;
        aura.SetModel(model);
        var o = player.PlayerPawn.Value.AbsOrigin;
        aura.Teleport(new Vector(o.X, o.Y, o.Z + 75), new QAngle(0,0,0), new Vector(0,0,0));
        aura.DispatchSpawn();
        // Прикрепляем к игроку, чтобы парила над головой
        aura.AcceptInput("SetParent", player.PlayerPawn.Value, null, "!activator");
        _auras[player.Slot] = aura;
    }

    private readonly Dictionary<int, CBaseEntity?> _acc = new();

    // Прикрепить кастом-аксессуар (крылья/коса/посох/нимб) к спине игрока
    public void AttachAccessory(CCSPlayerController player, string model)
    {
        if (player?.PlayerPawn?.Value?.AbsOrigin == null || string.IsNullOrEmpty(model)) return;
        RemoveAcc(player.Slot);
        var ent = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
        if (ent == null) return;
        ent.SetModel(model);
        var o = player.PlayerPawn.Value.AbsOrigin;
        ent.Teleport(new Vector(o.X, o.Y, o.Z + 20), new QAngle(0,0,0), new Vector(0,0,0));
        ent.DispatchSpawn();
        ent.AcceptInput("SetParent", player.PlayerPawn.Value, null, "!activator");
        _acc[player.Slot] = ent;
    }
    public void RemoveAcc(int slot)
    {
        if (_acc.TryGetValue(slot, out var e) && e != null && e.IsValid) try { e.Remove(); } catch {}
        _acc.Remove(slot);
    }

    public void Remove(int slot)
    {
        if (_auras.TryGetValue(slot, out var e) && e != null && e.IsValid)
            try { e.Remove(); } catch {}
        _auras.Remove(slot);
    }
}
