using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WcsInfinity.Systems;

public class DuelState
{
    public CCSPlayerController Challenger;
    public CCSPlayerController Opponent;
    public bool Active;
    public int ChallengerHp;
    public int OpponentHp;
}

public class DuelArenaSystem
{
    private readonly IEngineApi _engine;
    private readonly Func<ulong, Models.PlayerData> _dataFn;
    private readonly Action<Models.PlayerData> _saveFn;
    private readonly Dictionary<ulong, int> _elo = new();
    private readonly Dictionary<ulong, int> _winStreak = new();
    private DuelState? _currentDuel;

    private const int ELO_K = 32;
    private const int ELO_START = 1000;

    public DuelArenaSystem(IEngineApi engine, Func<ulong, Models.PlayerData> dataFn, Action<Models.PlayerData> saveFn)
    {
        _engine = engine;
        _dataFn = dataFn;
        _saveFn = saveFn;
    }

    public int GetElo(ulong steamId) => _elo.GetValueOrDefault(steamId, ELO_START);

    public void Invite(CCSPlayerController caller, CCSPlayerController target)
    {
        if (_currentDuel != null && _currentDuel.Active)
        { caller.PrintToChat(" \x07Уже идёт дуэль!"); return; }
        if (caller.Slot == target.Slot)
        { caller.PrintToChat(" \x07Нельзя дуэлить с собой!"); return; }

        _currentDuel = new DuelState
        {
            Challenger = caller,
            Opponent = target,
            Active = false
        };

        caller.PrintToChat($" \x06Приглашение отправлено {target.PlayerName}!");
        target.PrintToChat($" \x04{caller.PlayerName} вызывает тебя на дуэль! !duel accept");
    }

    public void Accept(CCSPlayerController p)
    {
        if (_currentDuel == null || _currentDuel.Active)
        { p.PrintToChat(" \x07Нет приглашения!"); return; }
        if (_currentDuel.Opponent.Slot != p.Slot)
        { p.PrintToChat(" \x07Это приглашение не тебе!"); return; }

        _currentDuel.Active = true;
        _currentDuel.ChallengerHp = 100;
        _currentDuel.OpponentHp = 100;

        var pos = _engine.GetPosition(_currentDuel.Challenger.Slot);
        _engine.Teleport(_currentDuel.Opponent.Slot, pos.x + 200, pos.y, pos.z);

        Server.PrintToChatAll($" \x06[AETHERION] ⚔ {_currentDuel.Challenger.PlayerName} vs {_currentDuel.Opponent.PlayerName} — ДУЭЛЬ!");
    }

    public void Leave(CCSPlayerController p)
    {
        if (_currentDuel == null) return;
        if (_currentDuel.Challenger.Slot == p.Slot || _currentDuel.Opponent.Slot == p.Slot)
        {
            EndDuel(_currentDuel.Challenger.Slot == p.Slot ? _currentDuel.Opponent : _currentDuel.Challenger);
        }
    }

    public void OnKill(CCSPlayerController killer, CCSPlayerController victim)
    {
        if (_currentDuel == null || !_currentDuel.Active) return;
        if (killer.Slot == _currentDuel.Challenger.Slot && victim.Slot == _currentDuel.Opponent.Slot)
            EndDuel(_currentDuel.Challenger);
        else if (killer.Slot == _currentDuel.Opponent.Slot && victim.Slot == _currentDuel.Challenger.Slot)
            EndDuel(_currentDuel.Opponent);
    }

    private void EndDuel(CCSPlayerController winner)
    {
        if (_currentDuel == null) return;
        var loser = winner.Slot == _currentDuel.Challenger.Slot
            ? _currentDuel.Opponent : _currentDuel.Challenger;

        int winnerElo = GetElo(winner.SteamID);
        int loserElo = GetElo(loser.SteamID);

        double expectedWin = 1.0 / (1.0 + Math.Pow(10, (loserElo - winnerElo) / 400.0));
        int delta = (int)(ELO_K * (1.0 - expectedWin));

        _elo[winner.SteamID] = winnerElo + delta;
        _elo[loser.SteamID] = Math.Max(100, loserElo - delta);

        _winStreak[winner.SteamID] = _winStreak.GetValueOrDefault(winner.SteamID) + 1;
        _winStreak[loser.SteamID] = 0;

        int streak = _winStreak[winner.SteamID];
        int goldReward = 50 + (streak > 2 ? streak * 25 : 0);
        int xpReward = 30;

            var d = _dataFn(winner.SteamID);
            EconomySystem.AddGold(d, goldReward);
            LevelSystem.AddXp(d, d.GetRace(d.CurrentRaceId), Races.RaceTier.T1_Spark, xpReward);
            _saveFn(d);

        winner.PrintToChat($" \x06🏆 Победа! ELO: {_elo[winner.SteamID]} (+{delta}) +{goldReward}з");
        loser.PrintToChat($" \x07Поражение. ELO: {_elo[loser.SteamID]} (-{delta})");

        if (streak >= 3)
            Server.PrintToChatAll($" \x06[AETHERION] 🔥 {winner.PlayerName} на серии {streak} побед!");

        _currentDuel = null;
    }

    public void ShowStatus(CCSPlayerController p)
    {
        int elo = GetElo(p.SteamID);
        int streak = _winStreak.GetValueOrDefault(p.SteamID);
        p.PrintToChat($" \x0B═══ АРЕНА ═══");
        p.PrintToChat($"  ELO: {elo} | Серия: {streak}");
        if (_currentDuel != null && _currentDuel.Active)
            p.PrintToChat($"  ⚔ {_currentDuel.Challenger.PlayerName} vs {_currentDuel.Opponent.PlayerName}");
    }
}
