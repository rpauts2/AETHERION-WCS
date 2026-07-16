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
}

public class EtherPortalSystem
{
    private readonly IEngineApi _engine;
    private readonly Func<ulong, Models.PlayerData> _dataFn;
    private readonly Action<Models.PlayerData> _saveFn;
    private readonly List<EtherPortal> _portals = new();
    private readonly Random _rng = new();
    private float _spawnTimer;
    private const float SPAWN_INTERVAL = 300f;
    private const float PORTAL_RADIUS = 100f;
    private const float PORTAL_LIFETIME = 120f;

    public int ActivePortals => _portals.Count(p => !p.Looted && DateTime.UtcNow < p.ExpiresUtc);

    public EtherPortalSystem(IEngineApi engine, Func<ulong, Models.PlayerData> dataFn, Action<Models.PlayerData> saveFn)
    {
        _engine = engine;
        _dataFn = dataFn;
        _saveFn = saveFn;
        _spawnTimer = 60f;
    }

    public void Tick()
    {
        _spawnTimer -= 1f;
        if (_spawnTimer <= 0)
        {
            SpawnPortal();
            _spawnTimer = SPAWN_INTERVAL;
        }

        CheckPlayerProximity();
        CleanupExpired();
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

        float x = pawn.AbsOrigin.X + _rng.Next(-2000, 2000);
        float y = pawn.AbsOrigin.Y + _rng.Next(-2000, 2000);
        float z = pawn.AbsOrigin.Z;

        _portals.Add(new EtherPortal
        {
            Slot = -1,
            X = x, Y = y, Z = z,
            ExpiresUtc = DateTime.UtcNow.AddSeconds(PORTAL_LIFETIME),
            Looted = false
        });

        Server.PrintToChatAll(" \x06[AETHERION] 🌀 Портал Эфира появился! Ищи на карте!");
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
        bool isBossLoot = _rng.NextDouble() < 0.4;
        var d = _dataFn(p.SteamID);

        if (isBossLoot)
        {
            int gold = 200 + _rng.Next(0, 300);
            int xp = 100 + _rng.Next(0, 200);
            EconomySystem.AddGold(d, gold);
            LevelSystem.AddXp(d, d.GetRace(d.CurrentRaceId), Races.RaceTier.T1_Spark, xp);
            _saveFn(d);
            p.PrintToChat($" \x06[AETHERION] 🌀 Портал: сундук! +{gold}з +{xp}XP");
        }
        else
        {
            Server.PrintToChatAll($" \x09[AETHERION] 👹 Из портала вышел мини-босс! {p.PlayerName} в опасности!");
            _engine.SetHealth(p.Slot, Math.Max(1, _engine.GetHealth(p.Slot) - 30));
            _engine.SpawnParticle("particles/aether_explosion.vpcf", portal.X, portal.Y, portal.Z);
            int gold = 100;
            int xp = 50;
            EconomySystem.AddGold(d, gold);
            LevelSystem.AddXp(d, d.GetRace(d.CurrentRaceId), Races.RaceTier.T1_Spark, xp);
            _saveFn(d);
            p.PrintToChat($" \x06[AETHERION] 🌀 Портал: отбил атаку! +{gold}з +{xp}XP");
        }
    }

    private void CleanupExpired()
    {
        _portals.RemoveAll(p => DateTime.UtcNow >= p.ExpiresUtc);
    }
}
