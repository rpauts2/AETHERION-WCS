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
    public int Round;
    public int ChallengerWins;
    public int OpponentWins;
    public int MaxRounds = 5;
    public float StartTime;
}

public class DuelArenaSystem
{
    private readonly IEngineApi _engine;
    private readonly Func<ulong, Models.PlayerData> _dataFn;
    private readonly Action<Models.PlayerData> _saveFn;
    private readonly Dictionary<ulong, int> _winStreak = new();
    private DuelState? _currentDuel;
    private float _duelTimeout;

    private const int ELO_K = 32;
    private const int ELO_START = 1000;
    private const float DUEL_TIMEOUT = 60f;

    public DuelArenaSystem(IEngineApi engine, Func<ulong, Models.PlayerData> dataFn, Action<Models.PlayerData> saveFn)
    {
        _engine = engine;
        _dataFn = dataFn;
        _saveFn = saveFn;
    }

    public int GetElo(ulong steamId)
    {
        var d = _dataFn(steamId);
        return d.DuelElo;
    }

    public void Invite(CCSPlayerController caller, CCSPlayerController target)
    {
        if (_currentDuel != null && _currentDuel.Active)
        { caller.PrintToChat(" \x07Уже идёт дуэль!"); return; }
        if (caller.Slot == target.Slot)
        { caller.PrintToChat(" \x07Нельзя дуэлить с собой!"); return; }
        if (target.IsBot)
        { caller.PrintToChat(" \x07Нельзя дуэлить с ботом!"); return; }

        _currentDuel = new DuelState
        {
            Challenger = caller,
            Opponent = target,
            Active = false
        };

        caller.PrintToChat($" \x06Приглашение отправлено {target.PlayerName}!");
        target.PrintToChat($" \x04{caller.PlayerName} вызывает тебя на дуэль! \x06!duel accept");
    }

    public void Accept(CCSPlayerController p)
    {
        if (_currentDuel == null || _currentDuel.Active)
        { p.PrintToChat(" \x07Нет приглашения!"); return; }
        if (_currentDuel.Opponent.Slot != p.Slot)
        { p.PrintToChat(" \x07Это приглашение не тебе!"); return; }

        // Check both players are alive
        if (!_currentDuel.Challenger.PawnIsAlive || !_currentDuel.Opponent.PawnIsAlive)
        { p.PrintToChat(" \x07Оба игрока должны быть живы!"); return; }

        _currentDuel.Active = true;
        _currentDuel.Round = 1;
        _currentDuel.ChallengerWins = 0;
        _currentDuel.OpponentWins = 0;
        _currentDuel.ChallengerHp = 100;
        _currentDuel.OpponentHp = 100;
        _currentDuel.StartTime = Server.CurrentTime;
        _duelTimeout = DUEL_TIMEOUT;

        // Teleport opponent near challenger
        var pos = _engine.GetPosition(_currentDuel.Challenger.Slot);
        _engine.Teleport(_currentDuel.Opponent.Slot, pos.x + 200, pos.y, pos.z);

        // Set both to 100 HP
        Server.PrintToChatAll($" \x06[AETHERION] ⚔ {_currentDuel.Challenger.PlayerName} vs {_currentDuel.Opponent.PlayerName} — ДУЭЛЬ (до {_currentDuel.MaxRounds} побед)!");
    }

    public void Leave(CCSPlayerController p)
    {
        if (_currentDuel == null) return;
        if (_currentDuel.Challenger.Slot == p.Slot || _currentDuel.Opponent.Slot == p.Slot)
        {
            var other = _currentDuel.Challenger.Slot == p.Slot
                ? _currentDuel.Opponent : _currentDuel.Challenger;
            EndDuel(other, "Таймаут/выход");
        }
    }

    public void OnKill(CCSPlayerController killer, CCSPlayerController victim)
    {
        if (_currentDuel == null || !_currentDuel.Active) return;
        if (killer.Slot == _currentDuel.Challenger.Slot && victim.Slot == _currentDuel.Opponent.Slot)
        {
            _currentDuel.ChallengerWins++;
            _currentDuel.Round++;
            Server.PrintToChatAll($" \x06⚔ {_currentDuel.Challenger.PlayerName} {_currentDuel.ChallengerWins}:{_currentDuel.OpponentWins} {_currentDuel.Opponent.PlayerName}");
            if (_currentDuel.ChallengerWins >= (_currentDuel.MaxRounds + 1) / 2)
                EndDuel(_currentDuel.Challenger, "Победа в матче");
            else
                PrepareNextRound();
        }
        else if (killer.Slot == _currentDuel.Opponent.Slot && victim.Slot == _currentDuel.Challenger.Slot)
        {
            _currentDuel.OpponentWins++;
            _currentDuel.Round++;
            Server.PrintToChatAll($" \x06⚔ {_currentDuel.Challenger.PlayerName} {_currentDuel.ChallengerWins}:{_currentDuel.OpponentWins} {_currentDuel.Opponent.PlayerName}");
            if (_currentDuel.OpponentWins >= (_currentDuel.MaxRounds + 1) / 2)
                EndDuel(_currentDuel.Opponent, "Победа в матче");
            else
                PrepareNextRound();
        }
    }

    private void PrepareNextRound()
    {
        if (_currentDuel == null) return;
        // Teleport both to start positions with full HP
        var pos = _engine.GetPosition(_currentDuel.Challenger.Slot);
        _engine.Teleport(_currentDuel.Opponent.Slot, pos.x + 200, pos.y, pos.z);
        _engine.Teleport(_currentDuel.Challenger.Slot, pos.x - 200, pos.y, pos.z);
        _duelTimeout = DUEL_TIMEOUT;
    }

    public void Tick()
    {
        if (_currentDuel == null || !_currentDuel.Active) return;
        _duelTimeout -= 1f;
        if (_duelTimeout <= 0)
        {
            // Tie: nobody wins
            if (_currentDuel.ChallengerWins == _currentDuel.OpponentWins)
            {
                Server.PrintToChatAll($" \x06⚔ Дуэль {_currentDuel.Challenger.PlayerName} vs {_currentDuel.Opponent.PlayerName} — НИЧЬЯ!");
                _currentDuel = null;
            }
            else
            {
                var leader = _currentDuel.ChallengerWins > _currentDuel.OpponentWins
                    ? _currentDuel.Challenger : _currentDuel.Opponent;
                EndDuel(leader, "Таймаут");
            }
        }
    }

    private void EndDuel(CCSPlayerController winner, string reason)
    {
        if (_currentDuel == null) return;
        var loser = winner.Slot == _currentDuel.Challenger.Slot
            ? _currentDuel.Opponent : _currentDuel.Challenger;

        var dW = _dataFn(winner.SteamID);
        var dL = _dataFn(loser.SteamID);

        int winnerElo = dW.DuelElo;
        int loserElo = dL.DuelElo;

        double expectedWin = 1.0 / (1.0 + Math.Pow(10, (loserElo - winnerElo) / 400.0));
        int delta = (int)(ELO_K * (1.0 - expectedWin));

        dW.DuelElo = winnerElo + delta;
        dW.DuelWins++;
        dL.DuelElo = Math.Max(100, loserElo - delta);
        dL.DuelLosses++;

        _winStreak[winner.SteamID] = _winStreak.GetValueOrDefault(winner.SteamID) + 1;
        _winStreak[loser.SteamID] = 0;

        int streak = _winStreak[winner.SteamID];
        int goldReward = 75 + (streak > 2 ? streak * 30 : 0) + delta;
        int xpReward = 40 + delta / 2;

        EconomySystem.AddGold(dW, goldReward);
        LevelSystem.AddXp(dW, dW.GetRace(dW.CurrentRaceId), Races.RaceTier.T1_Spark, xpReward);

        int lossXp = 15;
        LevelSystem.AddXp(dL, dL.GetRace(dL.CurrentRaceId), Races.RaceTier.T1_Spark, lossXp);

        _saveFn(dW);
        _saveFn(dL);

        string score = $"{_currentDuel.ChallengerWins}:{_currentDuel.OpponentWins}";
        winner.PrintToChat($" \x06🏆 {reason}! Счёт {score} | ELO: {dW.DuelElo} (+{delta}) +{goldReward}з");
        loser.PrintToChat($" \x07{reason}. Счёт {score} | ELO: {dL.DuelElo} (-{delta}) +{lossXp}XP");

        if (streak >= 3)
            Server.PrintToChatAll($" \x06[AETHERION] 🔥 {winner.PlayerName} на серии {streak} побед!");

        _currentDuel = null;
    }

    public void ShowStatus(CCSPlayerController p)
    {
        var d = _dataFn(p.SteamID);
        int elo = d.DuelElo;
        int streak = _winStreak.GetValueOrDefault(p.SteamID);
        p.PrintToChat(" \x0B═══ АРЕНА 1v1 ═══");
        p.PrintToChat($"  ELO: {elo} | Побед: {d.DuelWins} | Поражений: {d.DuelLosses} | Серия: {streak}");
        if (_currentDuel != null && _currentDuel.Active)
        {
            int timeLeft = (int)_duelTimeout;
            p.PrintToChat($"  ⚔ {_currentDuel.Challenger.PlayerName} ({_currentDuel.ChallengerWins}) vs ({_currentDuel.OpponentWins}) {_currentDuel.Opponent.PlayerName}");
            p.PrintToChat($"  Раунд {_currentDuel.Round}/{_currentDuel.MaxRounds} | Осталось: {timeLeft}с");
        }
    }
}
