using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WcsInfinity.Systems;

public class EtherPortal
{
    public int Slot;
    public float X, Y, Z;
    public DateTime ExpiresUtc;
    public bool Looted;
    public int Quality; // 0=普通, 1=稀有, 2=传说
}

public class EtherPortalSystem
{
    private readonly IEngineApi _engine;
    private readonly Func<ulong, Models.PlayerData> _dataFn;
    private readonly Action<Models.PlayerData> _saveFn;
    private readonly Dictionary<ulong, int> _portalsLooted = new();
    private readonly List<EtherPortal> _portals = new();
    private readonly Random _rng = new();
    private float _spawnTimer;
    private float _spawnInterval;
    private const float PORTAL_RADIUS = 120f;
    private const float PORTAL_LIFETIME = 150f;

    public int ActivePortals => _portals.Count(p => !p.Looted && DateTime.UtcNow < p.ExpiresUtc);

    public EtherPortalSystem(IEngineApi engine, Func<ulong, Models.PlayerData> dataFn, Action<Models.PlayerData> saveFn)
    {
        _engine = engine;
        _dataFn = dataFn;
        _saveFn = saveFn;
        _spawnInterval = 180f + _rng.Next(0, 120);
        _spawnTimer = 60f;
    }

    public void Tick()
    {
        _spawnTimer -= 1f;
        if (_spawnTimer <= 0)
        {
            SpawnPortal();
            _spawnInterval = 150f + _rng.Next(0, 150);
            _spawnTimer = _spawnInterval;
        }

        CheckPlayerProximity();
        CleanupExpired();
    }

    public void Reset()
    {
        _portals.Clear();
        _portalsLooted.Clear();
        _spawnTimer = 60f;
    }

    private void SpawnPortal()
    {
        var players = Utilities.GetPlayers()
            .Where(p => p != null && p.IsValid && !p.IsBot && p.PawnIsAlive)
            .ToList();
        if (players.Count == 0) return;

        var anchor = players[_rng.Next(players.Count)];
        var pawn = anchor.PlayerPawn?.Value;
        if (pawn?.AbsOrigin == null) return;

        float x = pawn.AbsOrigin.X + _rng.Next(-1500, 1500);
        float y = pawn.AbsOrigin.Y + _rng.Next(-1500, 1500);
        float z = pawn.AbsOrigin.Z;

        // 5% legendary, 25% rare, 70% normal
        int quality = _rng.NextDouble() < 0.05 ? 2 : _rng.NextDouble() < 0.25 ? 1 : 0;

        _portals.Add(new EtherPortal
        {
            Slot = -1,
            X = x, Y = y, Z = z,
            ExpiresUtc = DateTime.UtcNow.AddSeconds(PORTAL_LIFETIME),
            Looted = false,
            Quality = quality
        });

        string qualityText = quality switch { 2 => "⭐ ЛЕГЕНДАРНЫЙ", 1 => "✨ РЕДКИЙ", _ => "🌀 Обычный" };
        Server.PrintToChatAll($" \x06[AETHERION] 🌀 {qualityText} портал Эфира появился! Ищи на карте!");
        _engine.SpawnParticle("particles/aether_rift.vpcf", x, y, z);
    }

    private void CheckPlayerProximity()
    {
        foreach (var portal in _portals)
        {
            if (portal.Looted || DateTime.UtcNow >= portal.ExpiresUtc) continue;

            foreach (var p in Utilities.GetPlayers())
            {
                if (p == null || !p.IsValid || p.IsBot) continue;
                var pawn = p.PlayerPawn?.Value;
                if (pawn?.AbsOrigin == null) continue;

                float dx = pawn.AbsOrigin.X - portal.X;
                float dy = pawn.AbsOrigin.Y - portal.Y;
                if (dx * dx + dy * dy <= PORTAL_RADIUS * PORTAL_RADIUS)
                {
                    portal.Looted = true;
                    GrantPortalLoot(p, portal);
                    break;
                }
            }
        }
    }

    private void GrantPortalLoot(CCSPlayerController p, EtherPortal portal)
    {
        var d = _dataFn(p.SteamID);
        bool isMiniBoss = portal.Quality == 0 ? _rng.NextDouble() < 0.35 : portal.Quality == 1 ? _rng.NextDouble() < 0.2 : false;

        if (isMiniBoss)
        {
            // Mini-boss: damage + bigger reward
            Server.PrintToChatAll($" \x09[AETHERION] 👹 Из портала вышел мини-босс! {p.PlayerName} в опасности!");
            _engine.SetHealth(p.Slot, Math.Max(1, _engine.GetHealth(p.Slot) - 25));
            _engine.SpawnParticle("particles/aether_explosion.vpcf", portal.X, portal.Y, portal.Z);

            int gold = portal.Quality switch { 2 => 400, 1 => 250, _ => 150 };
            int xp = portal.Quality switch { 2 => 250, 1 => 150, _ => 80 };
            int ether = portal.Quality switch { 2 => 30, 1 => 15, _ => 8 };
            EconomySystem.AddGold(d, gold);
            LevelSystem.AddXp(d, d.GetRace(d.CurrentRaceId), Races.RaceTier.T1_Spark, xp);
            _saveFn(d);
            p.PrintToChat($" \x06[AETHERION] 🌀 Отбил мини-босса! +{gold}з +{xp}XP +{ether}⚡");
        }
        else
        {
            // Loot chest
            int gold = portal.Quality switch { 2 => 500 + _rng.Next(0, 300), 1 => 200 + _rng.Next(0, 150), _ => 80 + _rng.Next(0, 100) };
            int xp = portal.Quality switch { 2 => 300 + _rng.Next(0, 150), 1 => 100 + _rng.Next(0, 80), _ => 40 + _rng.Next(0, 40) };
            int ether = portal.Quality switch { 2 => 40 + _rng.Next(0, 20), 1 => 15 + _rng.Next(0, 10), _ => 5 + _rng.Next(0, 5) };

            EconomySystem.AddGold(d, gold);
            LevelSystem.AddXp(d, d.GetRace(d.CurrentRaceId), Races.RaceTier.T1_Spark, xp);
            _saveFn(d);

            string qualityTag = portal.Quality switch { 2 => "⭐ ЛЕГЕНДАРНЫЙ", 1 => "✨ РЕДКИЙ", _ => "Сундук" };
            p.PrintToChat($" \x06[AETHERION] 🌀 {qualityTag} сундук! +{gold}з +{xp}XP +{ether}⚡");
            _engine.SpawnParticle("particles/aether_pickup.vpcf", portal.X, portal.Y, portal.Z);

            _portalsLooted.TryGetValue(p.SteamID, out int total);
            _portalsLooted[p.SteamID] = total + 1;
        }
    }

    private void CleanupExpired()
    {
        _portals.RemoveAll(p => DateTime.UtcNow >= p.ExpiresUtc);
    }

    public void ShowStatus(CCSPlayerController p)
    {
        int looted = _portalsLooted.GetValueOrDefault(p.SteamID);
        p.PrintToChat(" \x0B═══ ПОРТАЛЫ ЭФИРА ═══");
        p.PrintToChat($"  Активных: {ActivePortals} | Всего найдено: {looted}");
        if (ActivePortals > 0)
            p.PrintToChat("  \x06Ищи мерцающие порталы на карте!");
        else
            p.PrintToChat($"  \x08Следующий через ~{(int)_spawnInterval}с");
    }
}
