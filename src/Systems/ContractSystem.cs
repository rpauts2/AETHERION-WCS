using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WcsInfinity.Races;

namespace WcsInfinity.Systems;

public enum ContractType { KillEnemies, WinRounds, UseUlt, DealDamage, GetHeadshots, GetKillsNoDeath, PlayMinutes, UseSigils }

public class ContractDef
{
    public ContractType Type;
    public string Name;
    public string Desc;
    public int Target;
    public int GoldReward;
    public int XpReward;
    public int BonusGoldOnStreak;
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
    private readonly Dictionary<ulong, DateTime> _contractDay = new();
    private Func<int, RaceDefinition?>? _raceDefFn;

    private static readonly ContractDef[] _templates =
    {
        new() { Type = ContractType.KillEnemies, Name = "Убить 10 врагов", Desc = "Убей 10 врагов любым способом", Target = 10, GoldReward = 200, XpReward = 100, BonusGoldOnStreak = 50 },
        new() { Type = ContractType.KillEnemies, Name = "Убить 5 врагов", Desc = "Убей 5 врагов любым способом", Target = 5, GoldReward = 120, XpReward = 60, BonusGoldOnStreak = 30 },
        new() { Type = ContractType.WinRounds, Name = "Выиграть 3 раунда", Desc = "Твоя команда должна выиграть 3 раунда", Target = 3, GoldReward = 300, XpReward = 150, BonusGoldOnStreak = 80 },
        new() { Type = ContractType.UseUlt, Name = "Использовать ульт 3 раза", Desc = "Примени ультимейт-способность 3 раза", Target = 3, GoldReward = 180, XpReward = 90, BonusGoldOnStreak = 40 },
        new() { Type = ContractType.UseUlt, Name = "Использовать ульт 5 раз", Desc = "Примени ультимейт-способность 5 раз", Target = 5, GoldReward = 280, XpReward = 140, BonusGoldOnStreak = 60 },
        new() { Type = ContractType.DealDamage, Name = "Нанести 300 урона", Desc = "Нанеси суммарно 300 урона врагам", Target = 300, GoldReward = 250, XpReward = 130, BonusGoldOnStreak = 50 },
        new() { Type = ContractType.DealDamage, Name = "Нанести 750 урона", Desc = "Нанеси суммарно 750 урона врагам", Target = 750, GoldReward = 400, XpReward = 220, BonusGoldOnStreak = 80 },
        new() { Type = ContractType.GetHeadshots, Name = "Сделать 3 хедшота", Desc = "Убей 3 врагов выстрелом в голову", Target = 3, GoldReward = 180, XpReward = 90, BonusGoldOnStreak = 40 },
        new() { Type = ContractType.GetHeadshots, Name = "Сделать 7 хедшотов", Desc = "Убей 7 врагов выстрелом в голову", Target = 7, GoldReward = 350, XpReward = 180, BonusGoldOnStreak = 70 },
        new() { Type = ContractType.GetKillsNoDeath, Name = "Убить 3 без смерти", Desc = "Убей 3 врагов подряд без смерти", Target = 3, GoldReward = 250, XpReward = 130, BonusGoldOnStreak = 60 },
        new() { Type = ContractType.PlayMinutes, Name = "Играть 10 минут", Desc = "Проведи 10 минут в игре", Target = 600, GoldReward = 150, XpReward = 80, BonusGoldOnStreak = 30 },
        new() { Type = ContractType.UseSigils, Name = "Использовать 5 печатей", Desc = "Примени печати Эфира 5 раз", Target = 5, GoldReward = 200, XpReward = 100, BonusGoldOnStreak = 40 },
    };

    private readonly Dictionary<ulong, int> _killStreak = new();

    public ContractSystem(Func<ulong, Models.PlayerData> dataFn, Action<Models.PlayerData> saveFn)
    {
        _dataFn = dataFn;
        _saveFn = saveFn;
    }

    public void SetRaceLookup(Func<int, RaceDefinition?> fn) => _raceDefFn = fn;

    public void DailyReset()
    {
        if (DateTime.UtcNow.Date <= _lastReset.Date) return;
        _lastReset = DateTime.UtcNow;
        _contracts.Clear();
        _contractDay.Clear();
    }

    private static int DayKey(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;

    private void SyncContracts(ulong steamId, List<PlayerContract> contracts)
    {
        var data = _dataFn(steamId);
        data.DailyContractDayKey = DayKey(DateTime.UtcNow.Date);
        data.DailyContracts = contracts.Select(c => new Models.DailyContractState
        {
            Type = (int)c.Def.Type,
            Name = c.Def.Name,
            Desc = c.Def.Desc,
            Target = c.Def.Target,
            GoldReward = c.Def.GoldReward,
            XpReward = c.Def.XpReward,
            BonusGoldOnStreak = c.Def.BonusGoldOnStreak,
            Progress = c.Progress,
            Claimed = c.Claimed
        }).ToList();
        data.IsDirty = true;
    }

    private List<PlayerContract> GetContracts(ulong steamId)
    {
        var today = DateTime.UtcNow.Date;
        var dayKey = DayKey(today);
        if (_contracts.TryGetValue(steamId, out var list) && _contractDay.GetValueOrDefault(steamId) == today)
            return list;

        var data = _dataFn(steamId);
        if (data.DailyContractDayKey == dayKey && data.DailyContracts.Count > 0)
        {
            list = data.DailyContracts.Select(saved => new PlayerContract
            {
                Def = new ContractDef
                {
                    Type = Enum.IsDefined(typeof(ContractType), saved.Type) ? (ContractType)saved.Type : ContractType.KillEnemies,
                    Name = saved.Name ?? "",
                    Desc = saved.Desc ?? "",
                    Target = Math.Max(1, saved.Target),
                    GoldReward = Math.Max(0, saved.GoldReward),
                    XpReward = Math.Max(0, saved.XpReward),
                    BonusGoldOnStreak = Math.Max(0, saved.BonusGoldOnStreak)
                },
                Progress = Math.Clamp(saved.Progress, 0, Math.Max(1, saved.Target)),
                Claimed = saved.Claimed
            }).ToList();
        }
        else
        {
            list = _templates.OrderBy(_ => _rng.Next()).Take(3)
                .Select(def => new PlayerContract { Def = def, Progress = 0, Claimed = false })
                .ToList();
            SyncContracts(steamId, list);
            _saveFn(data);
        }

        _contracts[steamId] = list;
        _contractDay[steamId] = today;
        return list;
    }

    public void OnKill(ulong steamId)
    {
        _killStreak.TryGetValue(steamId, out int streak);
        streak++;
        _killStreak[steamId] = streak;

        var contracts = GetContracts(steamId);
        foreach (var c in contracts)
        {
            if (c.Def.Type == ContractType.KillEnemies && !c.Claimed)
                c.Progress = Math.Min(c.Def.Target, c.Progress + 1);
            if (c.Def.Type == ContractType.GetKillsNoDeath && !c.Claimed)
                c.Progress = Math.Min(c.Def.Target, Math.Max(c.Progress, streak));
        }
        SyncContracts(steamId, contracts);
    }

    public void OnDeath(ulong steamId)
    {
        _killStreak[steamId] = 0;
    }

    public void OnRoundWin(ulong steamId)
    {
        var contracts = GetContracts(steamId);
        foreach (var c in contracts)
            if (c.Def.Type == ContractType.WinRounds && !c.Claimed)
                c.Progress = Math.Min(c.Def.Target, c.Progress + 1);
        SyncContracts(steamId, contracts);
    }

    public void OnUltUsed(ulong steamId)
    {
        var contracts = GetContracts(steamId);
        foreach (var c in contracts)
            if (c.Def.Type == ContractType.UseUlt && !c.Claimed)
                c.Progress = Math.Min(c.Def.Target, c.Progress + 1);
        SyncContracts(steamId, contracts);
    }

    public void OnDamage(ulong steamId, int amount)
    {
        if (amount <= 0) return;
        var contracts = GetContracts(steamId);
        foreach (var c in contracts)
            if (c.Def.Type == ContractType.DealDamage && !c.Claimed)
                c.Progress = Math.Min(c.Def.Target, c.Progress + amount);
        SyncContracts(steamId, contracts);
    }

    public void OnHeadshot(ulong steamId)
    {
        var contracts = GetContracts(steamId);
        foreach (var c in contracts)
            if (c.Def.Type == ContractType.GetHeadshots && !c.Claimed)
                c.Progress = Math.Min(c.Def.Target, c.Progress + 1);
        SyncContracts(steamId, contracts);
    }

    public void OnPlayTime(ulong steamId, float seconds)
    {
        int elapsed = (int)seconds;
        if (elapsed <= 0) return;
        var contracts = GetContracts(steamId);
        foreach (var c in contracts)
            if (c.Def.Type == ContractType.PlayMinutes && !c.Claimed)
                c.Progress = Math.Min(c.Def.Target, c.Progress + elapsed);
        SyncContracts(steamId, contracts);
    }

    public void OnSigilUsed(ulong steamId)
    {
        var contracts = GetContracts(steamId);
        foreach (var c in contracts)
            if (c.Def.Type == ContractType.UseSigils && !c.Claimed)
                c.Progress = Math.Min(c.Def.Target, c.Progress + 1);
        SyncContracts(steamId, contracts);
    }

    public bool Claim(CCSPlayerController p, int index)
    {
        var contracts = GetContracts(p.SteamID);
        if (index < 0 || index >= contracts.Count) return false;
        var c = contracts[index];
        if (c.Claimed || c.Progress < c.Def.Target) return false;

        c.Claimed = true;
        var d = _dataFn(p.SteamID);
        int streak = _killStreak.GetValueOrDefault(p.SteamID);
        int totalGold = c.Def.GoldReward + (streak >= 3 ? c.Def.BonusGoldOnStreak : 0);
        SyncContracts(p.SteamID, contracts);
        EconomySystem.AddGold(d, totalGold);
        var raceDef = _raceDefFn?.Invoke(d.CurrentRaceId);
        LevelSystem.AddXp(d, d.GetRace(d.CurrentRaceId), raceDef?.TierEnum ?? RaceTier.T1_Spark, c.Def.XpReward);
        _saveFn(d);
        p.PrintToChat($" \x06[AETHERION] 📋 Контракт выполнен: {c.Def.Name}! +{totalGold}з +{c.Def.XpReward}XP");
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
            int pct = Math.Min(100, c.Def.Target > 0 ? c.Progress * 100 / c.Def.Target : 0);
            p.PrintToChat($"  {status} {i + 1}. {c.Def.Desc} ({c.Progress}/{c.Def.Target}) [{pct}%] — {c.Def.GoldReward + c.Def.BonusGoldOnStreak}з +{c.Def.XpReward}XP");
        }
        p.PrintToChat("  \x04!claim <1-3>\x01 — забрать награду");
    }
}
