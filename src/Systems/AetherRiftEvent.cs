using System;
using System.Collections.Generic;
using System.Linq;

namespace WcsInfinity.Systems;

// 🌀 ЭФИРНЫЙ РАЗЛОМ — динамическое событие на карте.
// Аналогов в WCS на CS2 нет — реализуем с нуля поверх CounterStrikeSharp.
//
// ИДЕЯ: в случайной точке карты открывается Разлом. Команда, удерживающая
// точку дольше (контроль как в Domination), получает командный бафф Эфира.
// Это добавляет динамику и борьбу за точку поверх обычного режима.
//
// РЕАЛИЗАЦИЯ (т.к. в CS2/CSSharp нет готовых "control point"):
//   1. Спавним визуальный маркер (энтити-партикл) в выбранной точке.
//   2. Каждый тик считаем игроков каждой команды в радиусе захвата.
//   3. Прогресс захвата растёт в пользу доминирующей команды.
//   4. При 100% — баф команде-владельцу + звук + объявление.
public enum RiftState { Dormant, Spawning, Contested, Captured }

public class AetherRift
{
    public RiftState State { get; set; } = RiftState.Dormant;
    public (float x, float y, float z) Position { get; set; }
    public float CaptureProgress { get; set; } = 0f;   // -100..+100 (T vs CT)
    public int OwnerTeam { get; set; } = 0;            // 0 нет, 2 T, 3 CT
    public float CaptureRadius { get; set; } = 350f;
    public DateTime BuffExpiresUtc { get; set; }
}

    public class AetherRiftEvent
    {
        private readonly Random _rng = new();
        public AetherRift Rift { get; } = new();

        private readonly List<(float, float, float)> _spawnPoints = new();
        private float _resetTimer;
        private bool _needsReset;

        public void ConfigureSpawnPoints(IEnumerable<(float, float, float)> points)
        {
            _spawnPoints.Clear();
            _spawnPoints.AddRange(points);
        }

        // Auto-generate spawn points from alive player positions
        public void AutoGenerateSpawnPoints(IEngineApi engine)
        {
            _spawnPoints.Clear();
            foreach (var p in CounterStrikeSharp.API.Utilities.GetPlayers())
            {
                if (p == null || !p.IsValid || p.IsBot) continue;
                var pawn = p.PlayerPawn?.Value;
                if (pawn?.AbsOrigin != null)
                    _spawnPoints.Add((pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z));
            }
        }

        public void Spawn(IEngineApi engine)
        {
            if (_spawnPoints.Count == 0) AutoGenerateSpawnPoints(engine);
            if (_spawnPoints.Count == 0) return;
            Rift.Position = _spawnPoints[_rng.Next(_spawnPoints.Count)];
            Rift.State = RiftState.Spawning;
            Rift.CaptureProgress = 0;
            Rift.OwnerTeam = 0;
            engine.SpawnParticle("particles/aether_rift.vpcf", Rift.Position.x, Rift.Position.y, Rift.Position.z);
            Rift.State = RiftState.Contested;
        }

        public string? Tick(List<(int team, int slot)> playersInRadius, IEngineApi engine, float dt)
        {
            if (_needsReset)
            {
                _resetTimer -= dt;
                if (_resetTimer <= 0)
                {
                    _needsReset = false;
                    Reset();
                    Spawn(engine);
                }
                return null;
            }

            if (Rift.State != RiftState.Contested) return null;

            int t = playersInRadius.Count(p => p.team == 2);
            int ct = playersInRadius.Count(p => p.team == 3);
            if (t == 0 && ct == 0) return null;

            float rate = 18f * dt;
            if (t > ct) Rift.CaptureProgress -= rate * (t - ct);
            else if (ct > t) Rift.CaptureProgress += rate * (ct - t);
            Rift.CaptureProgress = Math.Clamp(Rift.CaptureProgress, -100, 100);

            if (Rift.CaptureProgress <= -100) return Capture(2, engine);
            if (Rift.CaptureProgress >= 100) return Capture(3, engine);
            return null;
        }

        private string Capture(int team, IEngineApi engine)
        {
            Rift.OwnerTeam = team;
            Rift.State = RiftState.Captured;
            Rift.BuffExpiresUtc = DateTime.UtcNow.AddSeconds(30);
            engine.SpawnParticle("particles/aether_capture.vpcf", Rift.Position.x, Rift.Position.y, Rift.Position.z);
            // Schedule reset after buff expires
            _needsReset = true;
            _resetTimer = 35f;
            string name = team == 2 ? L10n.Get("Systems_AetherRiftEvent_Terrorists", "Террористы") : L10n.Get("Systems_AetherRiftEvent_Specnaz", "Спецназ");
            return L10n.GetF("Systems_AetherRiftEvent_CaptureMsg", $"🌀 {{0}} захватили Эфирный Разлом! +25% Эфира и +10% урона на 30 сек.", ("name", name));
        }

        public bool IsBuffActive(int team) =>
            Rift.OwnerTeam == team && DateTime.UtcNow < Rift.BuffExpiresUtc;

        public void Reset()
        {
            Rift.State = RiftState.Dormant;
            Rift.CaptureProgress = 0;
            Rift.OwnerTeam = 0;
        }
    }
