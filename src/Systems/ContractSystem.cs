using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace WcsInfinity.Systems;

public enum ContractType { KillEnemies, WinRounds, UseUlt, DealDamage, GetHeadshots }

public class ContractDef
{
    public ContractType Type;
    public string Name;
    public string Desc;
    public int Target;
    public int GoldReward;
    public int XpReward;
}

public class PlayerContract
{
    public ContractDef Def;
    public int Progress;
    public bool Claimed;
}

public class ContractSystem
{
    private readonly Func<ulong, Models.PlayerData> _dataFn;
    private readonly Action<Models.PlayerData> _saveFn;
    private readonly Dictionary<ulong, List<PlayerContract>> _contracts = new();
    private readonly Random _rng = new();
    private DateTime _lastReset = DateTime.MinValue;

    private static readonly ContractDef[] _templates =
    {
        new() { Type = ContractType.KillEnemies, Name = "Убить 10 врагов", Desc = "Убей 10 врагов любым способом", Target = 10, GoldReward = 200, XpReward = 100 },
        new() { Type = ContractType.WinRounds, Name = "Выиграть 3 раунда", Desc = "Твоя команда должна выиграть 3 раунда", Target = 3, GoldReward = 300, XpReward = 150 },
        new() { Type = ContractType.UseUlt, Name = "Использовать ульт 5 раз", Desc = "Примени ультимейт-способность 5 раз", Target = 5, GoldReward = 250, XpReward = 120 },
        new() { Type = ContractType.DealDamage, Name = "Нанести 500 урона", Desc = "Нанеси суммарно 500 урона врагам", Target = 500, GoldReward = 350, XpReward = 200 },
        new() { Type = ContractType.GetHeadshots, Name = "Сделать 5 хедшотов", Desc = "Убей 5 врагов выстрелом в голову", Target = 5, GoldReward = 250, XpReward = 130 },
    };

    public ContractSystem(Func<ulong, Models.PlayerData> dataFn, Action<Models.PlayerData> saveFn)
    {
        _dataFn = dataFn;
        _saveFn = saveFn;
    }

    public void DailyReset()
    {
        if (DateTime.UtcNow.Date <= _lastReset.Date) return;
        _lastReset = DateTime.UtcNow;
        _contracts.Clear();
    }

    private List<PlayerContract> GetContracts(ulong steamId)
    {
        if (_contracts.TryGetValue(steamId, out var list)) return list;

        list = new List<PlayerContract>();
        var available = _templates.OrderBy(_ => _rng.Next()).Take(3).ToList();
        foreach (var def in available)
            list.Add(new PlayerContract { Def = def, Progress = 0, Claimed = false });
        _contracts[steamId] = list;
        return list;
    }

    public void OnKill(ulong steamId)
    {
        foreach (var c in GetContracts(steamId))
            if (c.Def.Type == ContractType.KillEnemies && !c.Claimed)
                c.Progress = Math.Min(c.Def.Target, c.Progress + 1);
    }

    public void OnRoundWin(ulong steamId)
    {
        foreach (var c in GetContracts(steamId))
            if (c.Def.Type == ContractType.WinRounds && !c.Claimed)
                c.Progress = Math.Min(c.Def.Target, c.Progress + 1);
    }

    public void OnUltUsed(ulong steamId)
    {
        foreach (var c in GetContracts(steamId))
            if (c.Def.Type == ContractType.UseUlt && !c.Claimed)
                c.Progress = Math.Min(c.Def.Target, c.Progress + 1);
    }

    public void OnDamage(ulong steamId, int amount)
    {
        foreach (var c in GetContracts(steamId))
            if (c.Def.Type == ContractType.DealDamage && !c.Claimed)
                c.Progress = Math.Min(c.Def.Target, c.Progress + amount);
    }

    public void OnHeadshot(ulong steamId)
    {
        foreach (var c in GetContracts(steamId))
            if (c.Def.Type == ContractType.GetHeadshots && !c.Claimed)
                c.Progress = Math.Min(c.Def.Target, c.Progress + 1);
    }

    public bool Claim(CCSPlayerController p, int index)
    {
        var contracts = GetContracts(p.SteamID);
        if (index < 0 || index >= contracts.Count) return false;
        var c = contracts[index];
        if (c.Claimed || c.Progress < c.Def.Target) return false;

        c.Claimed = true;
        var d = _dataFn(p.SteamID);
        EconomySystem.AddGold(d, c.Def.GoldReward);
        LevelSystem.AddXp(d, d.GetRace(d.CurrentRaceId), Races.RaceTier.T1_Spark, c.Def.XpReward);
        _saveFn(d);
        p.PrintToChat($" \x06[AETHERION] 📋 Контракт выполнен: {c.Def.Name}! +{c.Def.GoldReward}з +{c.Def.XpReward}XP");
        return true;
    }

    public void ShowContracts(CCSPlayerController p)
    {
        var contracts = GetContracts(p.SteamID);
        p.PrintToChat(" \x0B═══ КОНТРАКТЫ ═══");
        for (int i = 0; i < contracts.Count; i++)
        {
            var c = contracts[i];
            string status = c.Claimed ? "✅" : c.Progress >= c.Def.Target ? "🎁" : "⏳";
            p.PrintToChat($"  {status} {i + 1}. {c.Def.Desc} ({c.Progress}/{c.Def.Target}) — {c.Def.GoldReward}з");
        }
        p.PrintToChat("  \x04!claim <1-3>\x01 — забрать награду");
    }
}
