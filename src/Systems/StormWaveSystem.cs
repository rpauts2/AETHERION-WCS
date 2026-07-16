using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace WcsInfinity.Systems;

public class StormWaveSystem
{
    private readonly Func<ulong, Models.PlayerData> _dataFn;
    private readonly Action<Models.PlayerData> _saveFn;
    private readonly IEngineApi _engine;

    private bool _active;
    private int _currentWave;
    private int _totalWaves = 5;
    private readonly float _waveInterval = 45f;
    private float _timer;
    private readonly Random _rng = new();

    private readonly int[] _botsPerWave = { 3, 5, 7, 10, 1 };
    private readonly float[] _hpMultiplier = { 1f, 1.5f, 2f, 2.5f, 10f };
    private int _totalKills;
    private int _roundStartPlayerCount;

    public bool IsActive => _active;
    public int CurrentWave => _currentWave;
    public int TotalWaves => _totalWaves;
    public int TotalKills => _totalKills;

    public StormWaveSystem(IEngineApi engine, Func<ulong, Models.PlayerData> dataFn, Action<Models.PlayerData> saveFn)
    {
        _engine = engine;
        _dataFn = dataFn;
        _saveFn = saveFn;
    }

    public void Start(CCSPlayerController caller)
    {
        if (_active) { caller.PrintToChat(" \x07Шторм уже идёт!"); return; }

        var alive = Utilities.GetPlayers()
            .Count(p => p != null && p.IsValid && !p.IsBot && p.PawnIsAlive);
        if (alive < 1) { caller.PrintToChat(" \x07Нужен минимум 1 игрок!"); return; }

        _active = true;
        _currentWave = 0;
        _totalKills = 0;
        _roundStartPlayerCount = alive;
        _timer = 3f;
        Server.PrintToChatAll(" \x06[AETHERION] ⚡ ШТОРМ НАЧИНАЕТСЯ! 5 волн врагов!");
    }

    public void Stop()
    {
        if (!_active) return;
        _active = false;
        Server.PrintToChatAll($@" \x06[AETHERION] ⚡ Шторм завершён! Убито врагов: {_totalKills}");
    }

    public void OnRoundEnd()
    {
        if (_active) Stop();
    }

    public void OnBotKilled()
    {
        if (!_active) return;
        _totalKills++;
    }

    public void Tick()
    {
        if (!_active) return;

        _timer -= 1f;
        if (_timer > 0) return;

        if (_currentWave >= _totalWaves)
        {
            Stop();
            Server.PrintToChatAll(" \x06[AETHERION] 🏆 ВСЕ ВОЛНЫ ПОБЕЖДЕНЫ! Награда发放中...");
            GrantWaveRewards();
            return;
        }

        SpawnWave(_currentWave);
        _currentWave++;

        if (_currentWave < _totalWaves)
        {
            Server.PrintToChatAll($" \x06[AETHERION] ⚡ Волна {_currentWave}/{_totalWaves}! Следующая через {_waveInterval}с.");
            _timer = _waveInterval;
        }
        else
        {
            Server.PrintToChatAll(" \x09[AETHERION] 💀 ФИНАЛЬНАЯ ВОЛНА! БОСС!");
            _timer = 5f;
        }
    }

    private void SpawnWave(int wave)
    {
        int count = _botsPerWave[wave];
        float hpMult = _hpMultiplier[wave];

        for (int i = 0; i < count; i++)
        {
            Server.NextFrame(() =>
            {
                var bot = Utilities.GetPlayers()
                    .FirstOrDefault(p => p != null && p.IsValid && p.IsBot && p.PawnIsAlive);
                if (bot == null) return;

                var pawn = bot.PlayerPawn?.Value;
                if (pawn == null) return;

                int baseHp = (int)(100 * hpMult);
                pawn.Health = baseHp;
                Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
                bot.PlayerName = wave >= 4 ? "💀 БОСС ШТОРМА" : $"⚡ Шторм #{wave + 1}";
            });
        }
    }

    private void GrantWaveRewards()
    {
        int goldPerPlayer = 100 + _totalKills * 10;
        int xpPerPlayer = 50 + _totalKills * 5;

        foreach (var p in Utilities.GetPlayers())
        {
            if (p == null || !p.IsValid || p.IsBot) continue;
            var d = _dataFn(p.SteamID);
            EconomySystem.AddGold(d, goldPerPlayer);
            LevelSystem.AddXp(d, d.GetRace(d.CurrentRaceId), Races.RaceTier.T1_Spark, xpPerPlayer);
            _saveFn(d);
            p.PrintToChat($" \x06[AETHERION] ⚡ Награда шторма: +{goldPerPlayer}з +{xpPerPlayer}XP");
        }
    }
}
