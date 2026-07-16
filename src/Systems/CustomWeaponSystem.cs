using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using WcsInfinity.Core;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════════╗
// ║  CUSTOM WEAPON SYSTEM — оружие с уникальной механикой (ФАЗА 6)      ║
// ║  • Перехват weapon_fire + bullet_impact                            ║
// ║  • Крюк Пуджа: притягивает врага к кастеру                         ║
// ║  • Бесконечный USP: 1 патрон, авто-перезарядка, +скорость          ║
// ║  • Ракетница: спавн HE-снаряда с высоким уроном                   ║
// ║  • Меч-удар: ближний бой с отбросом                                ║
// ╚══════════════════════════════════════════════════════════════════════╝
public enum CustomWeaponType { None, PudgeHook, InfiniteUsp, RocketLauncher, SwordStrike }

public sealed class CustomWeaponSystem
{
    private readonly AetherionPlugin _plugin;
    private readonly IEngineApi _engine;

    // Какое оружие носит раса (raceId -> type)
    private readonly Dictionary<int, CustomWeaponType> _raceWeapons = new();

    // Состояние игрока по кастомному оружию
    private sealed class WeaponState
    {
        public CustomWeaponType Type;
        public float HookCd;
        public float RocketCd;
        public float LastShotTime;
        public int Combo;
    }
    private readonly Dictionary<int, WeaponState> _states = new();

    // Активные крюки (для анимации полёта)
    private sealed class HookBeam
    {
        public int CasterSlot;
        public int TargetSlot;
        public Vector StartPos = new();
        public Vector EndPos = new();
        public float Life;
        public bool PullingTarget;
    }
    private readonly List<HookBeam> _activeHooks = new();

    public CustomWeaponSystem(AetherionPlugin plugin, IEngineApi engine)
    {
        _plugin = plugin;
        _engine = engine;
    }

    // Регистрация хуков. Вызывается из плагина Load.
    public void Initialize()
    {
        _plugin.RegisterEventHandler<EventWeaponFire>(OnWeaponFire, HookMode.Post);
        _plugin.RegisterEventHandler<EventBulletImpact>(OnBulletImpact, HookMode.Post);
        _plugin.RegisterListener<Listeners.OnTick>(OnTick);
    }

    // Маппинг раса -> оружие. Можно грузить из JSON, пока хардкод.
    public void RegisterRaceWeapon(int raceId, CustomWeaponType type)
    {
        if (type != CustomWeaponType.None)
            _raceWeapons[raceId] = type;
        else
            _raceWeapons.Remove(raceId);
    }

    private WeaponState GetState(int slot, CustomWeaponType type)
    {
        if (_states.TryGetValue(slot, out var s)) { s.Type = type; return s; }
        s = new WeaponState { Type = type };
        _states[slot] = s;
        return s;
    }

    // ═══════════════════════════════════════════════
    //  ОСНОВНОЙ ПЕРЕХВАТ ВЫСТРЕЛА
    // ═══════════════════════════════════════════════
    private HookResult OnWeaponFire(EventWeaponFire ev, GameEventInfo info)
    {
        var p = ev.Userid;
        if (p == null || !p.IsValid || p.IsBot) return HookResult.Continue;

        try
        {
            int raceId = _plugin.Data(p.SteamID).CurrentRaceId;
            if (!_raceWeapons.TryGetValue(raceId, out var type) || type == CustomWeaponType.None)
                return HookResult.Continue;

            var s = GetState(p.Slot, type);
            float now = Server.CurrentTime;
            s.LastShotTime = now;

            switch (type)
            {
                case CustomWeaponType.PudgeHook:
                    HandleHookFire(p, s, now);
                    break;
                case CustomWeaponType.InfiniteUsp:
                    HandleInfiniteUsp(p, s);
                    break;
                case CustomWeaponType.SwordStrike:
                    HandleSwordStrike(p, s);
                    break;
                // RocketLauncher — активируется через !ult, не через обычный выстрел
            }
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] weapon_fire err: {ex.Message}"); }
        return HookResult.Continue;
    }

    private HookResult OnBulletImpact(EventBulletImpact ev, GameEventInfo info)
    {
        var p = ev.Userid;
        if (p == null || !p.IsValid || p.IsBot) return HookResult.Continue;
        // Точка попадания пули — используется для визуала крюка
        // ev.X/Y/Z — координаты импакта
        return HookResult.Continue;
    }

    // ═══════════════════════════════════════════════
    //  КРЮК ПУДЖА
    // ═══════════════════════════════════════════════
    private void HandleHookFire(CCSPlayerController caster, WeaponState s, float now)
    {
        if (now < s.HookCd)
        {
            caster.PrintToChat($" \x07Крюк перезаряжается: {(int)(s.HookCd - now)}с");
            return;
        }
        s.HookCd = now + 6f; // КД 6 сек

        // Ищем ближайшего врага по прицелу
        var target = FindAimTarget(caster, maxDist: 800f);
        if (target == null)
        {
            caster.PrintToChat(" \x08Крюк не зацепил никого (нет цели в радиусе 800).");
            // Визуал промаха
            var cp = _engine.GetPosition(caster.Slot);
            _engine.SpawnParticle("particles/aether_cast.vpcf", cp.x, cp.y, cp.z + 40);
            return;
        }

        // Создаём луч-крюк
        var cp2 = _engine.GetPosition(caster.Slot);
        var tp = _engine.GetPosition(target.Slot);
        _activeHooks.Add(new HookBeam
        {
            CasterSlot = caster.Slot,
            TargetSlot = target.Slot,
            StartPos = new Vector(cp2.x, cp2.y, cp2.z + 40),
            EndPos = new Vector(tp.x, tp.y, tp.z + 40),
            Life = 0.6f,
            PullingTarget = true
        });

        caster.PrintToChat($" \x04[КРЮК]\x01 Зацепил {target.PlayerName}!");
        target.PrintToChat($" \x07[КРЮК]\x01 Тебя притянули!");

        // Притягиваем цель к кастеру через 0.3 сек (анимация полёта)
        _plugin.AddTimer(0.3f, () =>
        {
            try
            {
                if (!target.IsValid || !target.PawnIsAlive) return;
                if (!caster.IsValid || !caster.PawnIsAlive) return;
                var cPos = _engine.GetPosition(caster.Slot);
                _engine.Teleport(target.Slot, cPos.x, cPos.y, cPos.z + 10);
                // Стан цели
                _engine.SetFrozen(target.Slot, true);
                _plugin.AddTimer(1.0f, () => { try { _engine.SetFrozen(target.Slot, false); } catch { } });
            }
            catch { }
        });
    }

    // Поиск врага по прицелу (raycast approximation: ближайший к линии взгляда)
    private CCSPlayerController? FindAimTarget(CCSPlayerController caster, float maxDist)
    {
        var pawn = caster.PlayerPawn?.Value;
        if (pawn?.AbsOrigin == null) return null;
        var eyePos = pawn.AbsOrigin;
        var eyeAng = pawn.EyeAngles;
        float yaw = eyeAng.Y * MathF.PI / 180f;
        float pitch = eyeAng.X * MathF.PI / 180f;
        // Направление взгляда
        float dx = MathF.Cos(pitch) * MathF.Cos(yaw);
        float dy = MathF.Cos(pitch) * MathF.Sin(yaw);
        float dz = -MathF.Sin(pitch);

        CCSPlayerController? best = null;
        float bestScore = float.MaxValue; // меньше = лучше (угловое отклонение × расстояние)
        int casterTeam = caster.TeamNum;

        foreach (var t in Utilities.GetPlayers())
        {
            if (t == null || !t.IsValid || t.IsBot || !t.PawnIsAlive) continue;
            if (t.Slot == caster.Slot || t.TeamNum == casterTeam) continue;
            var tp = t.PlayerPawn?.Value?.AbsOrigin;
            if (tp == null) continue;
            float vx = tp.X - eyePos.X, vy = tp.Y - eyePos.Y, vz = tp.Z - eyePos.Z;
            float dist = MathF.Sqrt(vx * vx + vy * vy + vz * vz);
            if (dist > maxDist || dist < 1f) continue;
            // Косинус угла между взглядом и направлением к цели
            float cosA = (vx * dx + vy * dy + vz * dz) / dist;
            if (cosA < 0.93f) continue; // ~угол < 21°
            float score = (1f - cosA) * dist; // меньше отклонение → лучше
            if (score < bestScore) { bestScore = score; best = t; }
        }
        return best;
    }

    // ═══════════════════════════════════════════════
    //  БЕСКОНЕЧНЫЙ USP (1 патрон, авто-перезарядка)
    // ═══════════════════════════════════════════════
    private void HandleInfiniteUsp(CCSPlayerController p, WeaponState s)
    {
        try
        {
            var pawn = p.PlayerPawn?.Value;
            if (pawn == null) return;
            // Найти активное оружие
            var weapon = pawn.WeaponServices?.ActiveWeapon?.Value;
            if (weapon == null) return;

            // Форсировать 1 патрон в обойме + бесконечный боезапас
            // m_iClip1 — патроны в обойме
            weapon.Clip1 = 1;
            Utilities.SetStateChanged(weapon, "CBasePlayerWeapon", "m_iClip1");
            // m_pReserveAmmo — запас
            if (weapon.ReserveAmmo != null && weapon.ReserveAmmo[0] < 99)
            {
                weapon.ReserveAmmo[0] = 99;
                Utilities.SetStateChanged(weapon, "CBasePlayerWeapon", "m_pReserveAmmo");
            }

            // Бонус скорости при стрельбе (импульс)
            _engine.SetSpeed(p.Slot, 1.15f);
            _plugin.AddTimer(0.5f, () => { try { _engine.SetSpeed(p.Slot, 1.0f); } catch { } });
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] infinite_usp err: {ex.Message}"); }
    }

    // ═══════════════════════════════════════════════
    //  МЕЧ-УДАР (ближний бой, отброс)
    // ═══════════════════════════════════════════════
    private void HandleSwordStrike(CCSPlayerController p, WeaponState s)
    {
        var pos = _engine.GetPosition(p.Slot);
        // Конусный урон перед кастером
        int team = p.TeamNum;
        foreach (var t in Utilities.GetPlayers())
        {
            if (t == null || !t.IsValid || t.IsBot || !t.PawnIsAlive || t.TeamNum == team || t.Slot == p.Slot) continue;
            var tp = t.PlayerPawn?.Value?.AbsOrigin;
            if (tp == null) continue;
            float dx = tp.X - pos.x, dy = tp.Y - pos.y, dz = tp.Z - pos.z;
            float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (dist <= 120f)
            {
                // Отброс
                _engine.Knockback(t.Slot, pos.x, pos.y, 400);
                _engine.AddHealth(t.Slot, -25, 100);
                _engine.SpawnParticle("particles/aether_explosion.vpcf", tp.X, tp.Y, tp.Z);
            }
        }
    }

    // ═══════════════════════════════════════════════
    //  РАКЕТНИЦА (через !ult или активную способность)
    // ═══════════════════════════════════════════════
    public void FireRocket(CCSPlayerController caster)
    {
        try
        {
            var s = GetState(caster.Slot, CustomWeaponType.RocketLauncher);
            float now = Server.CurrentTime;
            if (now < s.RocketCd)
            {
                caster.PrintToChat($" \x07Ракетница перезаряжается: {(int)(s.RocketCd - now)}с");
                return;
            }
            s.RocketCd = now + 10f;

            // Спавн HE-снаряда в направлении взгляда с усиленным уроном
            var pawn = caster.PlayerPawn?.Value;
            if (pawn?.AbsOrigin == null) return;
            var startPos = pawn.AbsOrigin;
            var eyeAng = pawn.EyeAngles;
            float yaw = eyeAng.Y * MathF.PI / 180f;
            float pitch = eyeAng.X * MathF.PI / 180f;
            float speed = 1200f;
            var velocity = new Vector(
                MathF.Cos(pitch) * MathF.Cos(yaw) * speed,
                MathF.Cos(pitch) * MathF.Sin(yaw) * speed,
                -MathF.Sin(pitch) * speed + 100f
            );

            // Создаём HE-снаряд как ракету
            var rocket = Utilities.CreateEntityByName<CHEGrenadeProjectile>("hegrenade_projectile");
            if (rocket == null) return;
            rocket.Teleport(
                new Vector(startPos.X, startPos.Y, startPos.Z + 40),
                new QAngle(0, eyeAng.Y, 0),
                velocity
            );
            rocket.DispatchSpawn();
            // Усиленный урон
            rocket.Damage = 120f;
            rocket.DmgRadius = 250f;

            // Эффект запуска
            _engine.SpawnParticle("particles/aether_explosion.vpcf", startPos.X, startPos.Y, startPos.Z + 40);
            caster.PrintToChat(" \x04[РАКЕТНИЦА]\x01 Запущена ракета!");
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] rocket err: {ex.Message}"); }
    }

    // ═══════════════════════════════════════════════
    //  ТИК: визуал активных крюков
    // ═══════════════════════════════════════════════
    private void OnTick()
    {
        if (_activeHooks.Count == 0) return;
        for (int i = _activeHooks.Count - 1; i >= 0; i--)
        {
            var h = _activeHooks[i];
            // Рисуем луч каждые 0.05 сек
            _engine.Beam(
                h.StartPos.X, h.StartPos.Y, h.StartPos.Z,
                h.EndPos.X, h.EndPos.Y, h.EndPos.Z,
                255, 200, 60, 0.1f
            );
            h.Life -= 0.1f;
            if (h.Life <= 0) _activeHooks.RemoveAt(i);
        }
    }

    // ═══════════════════════════════════════════════
    //  ПОЛУЧЕНИЕ ТИПА ОРУЖИЯ
    // ═══════════════════════════════════════════════
    public CustomWeaponType GetWeaponType(int raceId) =>
        _raceWeapons.TryGetValue(raceId, out var t) ? t : CustomWeaponType.None;

    public string WeaponTypeName(CustomWeaponType t) => t switch
    {
        CustomWeaponType.PudgeHook => "🪝 Крюк Пуджа",
        CustomWeaponType.InfiniteUsp => "🔫 Бесконечный USP",
        CustomWeaponType.RocketLauncher => "🚀 Ракетница",
        CustomWeaponType.SwordStrike => "⚔️ Меч-удар",
        _ => "—"
    };

    public void CleanupSlot(int slot) => _states.Remove(slot);
}
