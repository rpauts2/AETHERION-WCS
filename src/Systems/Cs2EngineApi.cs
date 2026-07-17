using System;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════╗
// ║  CS2 ENGINE API — реальные вызовы CounterStrikeSharp.             ║
// ║  Здесь живёт ВСЯ боевая магия: HP, скорость, телепорт, поджог,    ║
// ║  отбрасывание, невидимость, лучи, частицы. Навыки рас дёргают     ║
// ║  эти методы — единая точка интеграции с движком Source 2.         ║
// ╚══════════════════════════════════════════════════════════════════╝
public interface IEngineApi
{
    void SetHealth(int slot, int hp);
    void AddHealth(int slot, int amount, int max);
    void SetSpeed(int slot, float multiplier);
    void SetGravity(int slot, float multiplier);
    (float x, float y, float z) GetPosition(int slot);
    void Teleport(int slot, float x, float y, float z);
    void Knockback(int slot, float fromX, float fromY, float force);
    void Ignite(int slot, float seconds);
    void SetInvisible(int slot, bool on);
    void PrintToCenter(int slot, string html);
    void SpawnParticle(string path, float x, float y, float z);
    void Beam(float x1,float y1,float z1,float x2,float y2,float z2,int r,int g,int b,float life);
    void PlaySound(int slot, string sound);
    // — Phase 1: расширение боевого движка —
    int GetHealth(int slot);
    int GetArmor(int slot);
    void SetArmor(int slot, int value);
    void AddArmor(int slot, int amount, int max);
    void SetFrozen(int slot, bool frozen);
    void Blind(int slot, float duration, float holdTime = 0.4f);
    int ForEnemiesInRadius(int slot, float radius, Action<int> action);
    int DamageRadius(int slot, float radius, int damage, bool enemiesOnly = true);
    int ForAlliesInRadius(int slot, float radius, System.Action<int> action);
    void SetRenderTint(int slot, int r, int g, int b, int glow = 1);
    (float x, float y, float z)? GetNearestSpawn(int slot, int team);
}

public class Cs2EngineApi : IEngineApi
{
    private static CCSPlayerController? P(int slot) =>
        Utilities.GetPlayerFromSlot(slot) is { IsValid: true } p ? p : null;

    private static CCSPlayerPawn? Pawn(int slot)
    {
        var p = P(slot);
        return p?.PlayerPawn?.Value is { IsValid: true } pawn ? pawn : null;
    }

    public void SetHealth(int slot, int hp)
    {
        var pawn = Pawn(slot); if (pawn == null) return;
        pawn.Health = hp;
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
    }

    public void AddHealth(int slot, int amount, int max)
    {
        var pawn = Pawn(slot); if (pawn == null) return;
        pawn.Health = Math.Min(max, pawn.Health + amount);
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
    }

    public void SetSpeed(int slot, float multiplier)
    {
        var pawn = Pawn(slot); if (pawn == null) return;
        pawn.VelocityModifier = multiplier;           // мгновенный множитель скорости
        Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier");
    }

    public void SetGravity(int slot, float multiplier)
    {
        var pawn = Pawn(slot); if (pawn == null) return;
        pawn.GravityScale = multiplier;
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_flGravityScale");
    }

    public (float x, float y, float z) GetPosition(int slot)
    {
        var pawn = Pawn(slot);
        if (pawn?.AbsOrigin == null) return (0,0,0);
        var o = pawn.AbsOrigin;
        return (o.X, o.Y, o.Z);
    }

    public void Teleport(int slot, float x, float y, float z)
    {
        var pawn = Pawn(slot); if (pawn == null) return;
        pawn.Teleport(new Vector(x,y,z), pawn.EyeAngles, new Vector(0,0,0));
    }

    public void Knockback(int slot, float fromX, float fromY, float force)
    {
        var pawn = Pawn(slot); if (pawn?.AbsOrigin == null) return;
        float dx = pawn.AbsOrigin.X - fromX, dy = pawn.AbsOrigin.Y - fromY;
        float len = MathF.Sqrt(dx*dx + dy*dy);
        if (len < 1e-3f) { dx = 1; dy = 0; len = 1; }
        var push = new Vector(dx/len*force, dy/len*force, force*0.4f);
        pawn.AbsVelocity.X += push.X; pawn.AbsVelocity.Y += push.Y; pawn.AbsVelocity.Z += push.Z;
    }

    public void Ignite(int slot, float seconds)
    {
        var pawn = Pawn(slot); if (pawn == null) return;
        // поджог через урон по тикам реализуется таймером в плагине;
        // здесь — визуальный партикл огня на позиции
        var pos = GetPosition(slot);
        SpawnParticle("particles/burning_fx/env_fire_medium.vpcf", pos.x, pos.y, pos.z);
    }

    public void SetInvisible(int slot, bool on)
    {
        var pawn = Pawn(slot); if (pawn == null) return;
        var render = pawn.Render;
        pawn.Render = System.Drawing.Color.FromArgb(on ? 0 : 255, render.R, render.G, render.B);
        Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
    }

    public void PrintToCenter(int slot, string html) => P(slot)?.PrintToCenterHtml(html);

    public void SpawnParticle(string path, float x, float y, float z)
    {
        var ent = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
        if (ent == null) return;
        ent.EffectName = path;
        ent.StartActive = true;
        ent.Teleport(new Vector(x,y,z), new QAngle(0,0,0), new Vector(0,0,0));
        ent.DispatchSpawn();
        ent.AcceptInput("Start");
        Server.NextFrame(() =>
        {
            try { if (ent.IsValid) ent.Remove(); } catch { }
        });
    }

    public void Beam(float x1,float y1,float z1,float x2,float y2,float z2,int r,int g,int b,float life)
    {
        var beam = Utilities.CreateEntityByName<CEnvBeam>("env_beam");
        if (beam == null) return;
        beam.Render = System.Drawing.Color.FromArgb(255, r, g, b);
        beam.Width = 3.0f;
        beam.Teleport(new Vector(x1,y1,z1), new QAngle(0,0,0), new Vector(0,0,0));
        beam.EndPos.X = x2; beam.EndPos.Y = y2; beam.EndPos.Z = z2;
        beam.DispatchSpawn();
        // Beam is a static visual — remove next frame after it renders once
        _pendingBeamRemovals.Add(beam);
    }

    private static readonly List<CEnvBeam> _pendingBeamRemovals = new();
    private static bool _cleanupRegistered;

    internal static void FlushPendingBeams()
    {
        for (int i = _pendingBeamRemovals.Count - 1; i >= 0; i--)
        {
            var b = _pendingBeamRemovals[i];
            _pendingBeamRemovals.RemoveAt(i);
            try { if (b != null && b.IsValid) b.Remove(); } catch { }
        }
    }

    public void PlaySound(int slot, string sound)
    {
        var p = P(slot);
        p?.ExecuteClientCommand($"play {sound}");
    }

    // AOE: урон всем ВРАГАМ в радиусе вокруг точки кастующего. Возвращает число задетых.
    public int DamageRadius(int slot, float radius, int damage, bool enemiesOnly = true)
    {
        int hit = 0;
        var caster = Utilities.GetPlayerFromSlot(slot);
        if (caster?.PlayerPawn?.Value?.AbsOrigin == null) return 0;
        var origin = caster.PlayerPawn.Value.AbsOrigin;
        int casterTeam = caster.TeamNum;
        foreach (var t in Utilities.GetPlayers())
        {
            if (t == null || !t.PawnIsAlive || t.Slot == slot) continue;
            if (enemiesOnly && t.TeamNum == casterTeam) continue;
            var pawn = t.PlayerPawn?.Value; if (pawn?.AbsOrigin == null) continue;
            var d = origin - pawn.AbsOrigin;
            float dist = MathF.Sqrt(d.X*d.X + d.Y*d.Y + d.Z*d.Z);
            if (dist <= radius)
            {
                int nhp = Math.Max(0, pawn.Health - damage);
                SetHealth(t.Slot, nhp);
                hit++;
            }
        }
        return hit;
    }

    // AOE: применить действие ко всем СОЮЗНИКАМ в радиусе (хил/бафф). Возвращает число.
    public int ForAlliesInRadius(int slot, float radius, Action<int> action)
    {
        int n = 0;
        var caster = Utilities.GetPlayerFromSlot(slot);
        if (caster?.PlayerPawn?.Value?.AbsOrigin == null) return 0;
        var origin = caster.PlayerPawn.Value.AbsOrigin;
        int team = caster.TeamNum;
        foreach (var t in Utilities.GetPlayers())
        {
            if (t == null || !t.PawnIsAlive || t.TeamNum != team) continue;
            var pawn = t.PlayerPawn?.Value; if (pawn?.AbsOrigin == null) continue;
            var d = origin - pawn.AbsOrigin;
            if (MathF.Sqrt(d.X*d.X + d.Y*d.Y + d.Z*d.Z) <= radius) { action(t.Slot); n++; }
        }
        return n;
    }


    // Тинт-скин: цвет рендера модели игрока + (свечение через alpha-glow эмулируется яркостью)
    public void SetRenderTint(int slot, int r, int g, int b, int glow = 1)
    {
        var p = Utilities.GetPlayerFromSlot(slot);
        var pawn = p?.PlayerPawn?.Value;
        if (pawn == null) return;
        try
        {
            pawn.Render = System.Drawing.Color.FromArgb(255, r, g, b);
            Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] tint fail {slot}: {ex.Message}"); }
    }
    
    // ╔═ Phase 1: боевые примитивы (CC, броня, флэш) ═╗
    public int GetHealth(int slot){ var p=Pawn(slot); return p?.Health ?? 0; }

    public int GetArmor(int slot){ var pl=Utilities.GetPlayerFromSlot(slot); return pl?.PlayerPawn?.Value?.ArmorValue ?? 0; }

    public void SetArmor(int slot, int value)
    {
        var pawn = Pawn(slot); if (pawn == null) return;
        pawn.ArmorValue = Math.Max(0, value);
        Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_ArmorValue");
    }

    public void AddArmor(int slot, int amount, int max)
    {
        int cur = GetArmor(slot);
        SetArmor(slot, Math.Min(max, cur + amount));
    }

    // Заморозка/стан/рут: фиксируем движение через MoveType
    public void SetFrozen(int slot, bool frozen)
    {
        var pawn = Pawn(slot); if (pawn == null) return;
        try {
            pawn.MoveType = frozen ? MoveType_t.MOVETYPE_NONE : MoveType_t.MOVETYPE_WALK;
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType");
            if (frozen) { pawn.AbsVelocity.X = 0; pawn.AbsVelocity.Y = 0; pawn.AbsVelocity.Z = 0; }
        } catch (Exception ex){ Console.WriteLine($"[AETHERION] freeze fail {slot}: {ex.Message}"); }
    }

    // Флэш-ослепление (используется для Страха и слепоты)
    public void Blind(int slot, float duration, float holdTime = 0.4f)
    {
        var pawn = Pawn(slot); if (pawn == null) return;
        try {
            pawn.FlashDuration = duration;
            pawn.FlashMaxAlpha = 255f;
            Utilities.SetStateChanged(pawn, "CCSPlayerPawnBase", "m_flFlashDuration");
            Utilities.SetStateChanged(pawn, "CCSPlayerPawnBase", "m_flFlashMaxAlpha");
        } catch (Exception ex){ Console.WriteLine($"[AETHERION] blind fail {slot}: {ex.Message}"); }
    }

    public int ForEnemiesInRadius(int slot, float radius, Action<int> action)
    {
        int n = 0;
        var caster = Utilities.GetPlayerFromSlot(slot);
        if (caster?.PlayerPawn?.Value?.AbsOrigin == null) return 0;
        var origin = caster.PlayerPawn.Value.AbsOrigin;
        int team = caster.TeamNum;
        foreach (var t in Utilities.GetPlayers())
        {
            if (t == null || !t.PawnIsAlive || t.Slot == slot || t.TeamNum == team) continue;
            var pawn = t.PlayerPawn?.Value; if (pawn?.AbsOrigin == null) continue;
            var d = origin - pawn.AbsOrigin;
            if (MathF.Sqrt(d.X*d.X + d.Y*d.Y + d.Z*d.Z) <= radius) { action(t.Slot); n++; }
        }
        return n;
    }

    /// <summary>
    /// Finds the nearest spawn point entity for the given team.
    /// Searches info_player_terrorist (team 2) and info_player_counterterrorist (team 3).
    /// Returns null if no spawn points are found or the player position is unavailable.
    /// </summary>
    public (float x, float y, float z)? GetNearestSpawn(int slot, int team)
    {
        var pawn = Pawn(slot);
        if (pawn?.AbsOrigin == null) return null;
        var pos = pawn.AbsOrigin;

        string entityClass = team == 3 ? "info_player_counterterrorist" : "info_player_terrorist";

        float bestDist = float.MaxValue;
        (float x, float y, float z)? bestSpawn = null;

        foreach (var ent in Utilities.GetAllEntities())
        {
            if (ent == null || !ent.IsValid) continue;
            var className = ent.DesignerName ?? "";
            if (!className.Contains(entityClass, StringComparison.OrdinalIgnoreCase)) continue;
            if (ent is not CBaseEntity be || be.AbsOrigin == null) continue;
            var entPos = be.AbsOrigin;
            if (entPos == null) continue;
            float dx = entPos.X - pos.X, dy = entPos.Y - pos.Y, dz = entPos.Z - pos.Z;
            float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestSpawn = (entPos.X, entPos.Y, entPos.Z);
            }
        }

        return bestSpawn;
    }
}