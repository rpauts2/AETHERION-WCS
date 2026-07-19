using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════╗
// ║  COMBAT EFFECTS — длительные эффекты и CC (Phase 1: 100% покрытие) ║
// ╚══════════════════════════════════════════════════════════════════╝

public enum EffectTag {
    Burn, Poison, Regen,           // периодический урон/лечение
    Invisible, SpeedBuff, Slow,    // мобильность/видимость
    Freeze, Stun, Root, Fear,      // контроль (CC)
    Shield, Berserk, Lifesteal, LifestealPassive, // защита/усиление + вампиризм пассивный
    Reflect, Evasion, CritChance, ManaShield, CooldownReduction, // D1/D2 + индикатор CDR
    Pull, Push, Execute, Disarm, Mark, SlowAura, // расширение для новых рас
    ArmorBreak, Silence, Blind, Haste, TimeDilation // D3+ эффекты
}

public class ActiveEffect
{
    public EffectTag Tag;
    public int Slot;
    public float Value;
    public DateTime ExpiresUtc;
    public DateTime NextTickUtc;
    public float TickInterval = 1f;
    public int? SourceSlot;
}

public class CombatEffects
{
    private readonly IEngineApi _engine;
    private readonly List<ActiveEffect> _effects = new();

    // Callback for AOE damage — allows systems like BossSystem to react
    public Action<float, float, float, float, int>? OnAoeDamage { get; set; }

    public CombatEffects(IEngineApi engine) { _engine = engine; }

    public void Apply(EffectTag tag, int slot, float value, float durationSec,
        float tickInterval = 1f, int? source = null)
    {
        if (durationSec <= 0 || slot < 0) return;
        tickInterval = Math.Max(0.1f, tickInterval);
        var now = DateTime.UtcNow;
        var ex = _effects.FirstOrDefault(e => e.Tag == tag && e.Slot == slot);
        if (ex != null) { ex.Value = value; ex.ExpiresUtc = now.AddSeconds(durationSec); return; }
        _effects.Add(new ActiveEffect {
            Tag = tag, Slot = slot, Value = value,
            ExpiresUtc = now.AddSeconds(durationSec),
            NextTickUtc = now.AddSeconds(tickInterval),
            TickInterval = tickInterval, SourceSlot = source });
        OnApply(tag, slot, value, durationSec);
    }

    private void OnApply(EffectTag tag, int slot, float value, float dur)
    {
        switch (tag)
        {
            case EffectTag.Invisible: _engine.SetInvisible(slot, true); break;
            case EffectTag.SpeedBuff: _engine.SetSpeed(slot, 1f + value); break;
            case EffectTag.Slow:      _engine.SetSpeed(slot, Math.Max(0.2f, 1f - value)); break;
            // — CC —
            case EffectTag.Freeze:
            case EffectTag.Stun:
                _engine.SetFrozen(slot, true);
                _engine.Blind(slot, dur, dur * 0.6f);
                break;
            case EffectTag.Root:
                _engine.SetSpeed(slot, 0.05f);          // прикован, но может целиться
                break;
            case EffectTag.Fear:
                _engine.Blind(slot, dur, dur * 0.5f);   // дезориентация
                _engine.SetSpeed(slot, 0.6f);
                break;
            case EffectTag.Shield:
                _engine.AddArmor(slot, (int)value, 200);
                break;
            case EffectTag.Reflect:
            case EffectTag.ManaShield:
                _engine.AddArmor(slot, (int)value, 250);
                break;
            case EffectTag.Evasion:
            case EffectTag.CritChance:
                break;
        }
    }

    private void OnExpire(ActiveEffect e)
    {
        switch (e.Tag)
        {
            case EffectTag.Invisible: _engine.SetInvisible(e.Slot, false); break;
            case EffectTag.SpeedBuff:
            case EffectTag.Slow:
            case EffectTag.Root:
            case EffectTag.Fear:      _engine.SetSpeed(e.Slot, 1f); break;
            case EffectTag.Freeze:
            case EffectTag.Stun:      _engine.SetFrozen(e.Slot, false); break;
        }
    }

    public void TickAll()
    {
        var now = DateTime.UtcNow;
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            var e = _effects[i];
            if (now >= e.ExpiresUtc) { OnExpire(e); _effects.RemoveAt(i); continue; }
            if (now >= e.NextTickUtc)
            {
                e.NextTickUtc = now.AddSeconds(e.TickInterval);
                switch (e.Tag)
                {
                    case EffectTag.Burn:
                    {
                        var pos = _engine.GetPosition(e.Slot);
                        _engine.SetHealth(e.Slot, Math.Max(0, _engine.GetHealth(e.Slot) - (int)e.Value));
                        _engine.SpawnParticle("particles/burning_fx/env_fire_small.vpcf", pos.x, pos.y, pos.z);
                        break;
                    }
                    case EffectTag.Poison:
                    {
                        var pos = _engine.GetPosition(e.Slot);
                        _engine.SetHealth(e.Slot, Math.Max(0, _engine.GetHealth(e.Slot) - (int)e.Value));
                        _engine.SpawnParticle("particles/explosions_fx/explosion_smoke_grenade.vpcf", pos.x, pos.y, pos.z);
                        break;
                    }
                    case EffectTag.Regen:
                        _engine.AddHealth(e.Slot, (int)e.Value, 150);
                        break;
                    case EffectTag.Freeze:
                    case EffectTag.Stun:
                        _engine.SetFrozen(e.Slot, true);     // удерживаем заморозку каждый тик
                        break;
                }
            }
        }
    }

    // ── AOE ──
    public List<int> EnemiesInRadius(float x, float y, float z, float radius, int attackerTeam)
    {
        var hit = new List<int>();
        foreach (var p in Utilities.GetPlayers())
        {
            if (p == null || !p.IsValid || p.PlayerPawn?.Value?.AbsOrigin == null) continue;
            if (p.TeamNum == attackerTeam) continue;
            if (p.PlayerPawn.Value.Health <= 0) continue;
            var o = p.PlayerPawn.Value.AbsOrigin;
            float dx = o.X - x, dy = o.Y - y, dz = o.Z - z;
            if (dx*dx + dy*dy + dz*dz <= radius*radius) hit.Add(p.Slot);
        }
        return hit;
    }

    private const float DefaultDamageCapMultiplier = 1.5f;

    public static float GetDamageCapMultiplier(int? sourceSlot, CombatEffects combat)
    {
        if (!sourceSlot.HasValue) return DefaultDamageCapMultiplier;
        var slot = sourceSlot.Value;
        if (combat.HasEffect(slot, EffectTag.LifestealPassive)) return DefaultDamageCapMultiplier + combat.EffectValue(slot, EffectTag.LifestealPassive);
        if (combat.HasEffect(slot, EffectTag.Lifesteal)) return DefaultDamageCapMultiplier + combat.EffectValue(slot, EffectTag.Lifesteal);
        return DefaultDamageCapMultiplier;
    }

    public void AoeDamage(float x, float y, float z, float radius, int damage, int attackerTeam, int? sourceSlot = null)
    {
        float multiplier = GetDamageCapMultiplier(sourceSlot, this);
        int scaledDamage = (int)(damage * multiplier);
        foreach (var slot in EnemiesInRadius(x, y, z, radius, attackerTeam))
        {
            int victimHealth = Math.Max(1, _engine.GetHealth(slot));
            int finalDamage = Math.Min(scaledDamage, victimHealth);
            _engine.SetHealth(slot, Math.Max(0, victimHealth - finalDamage));
        }
        // Notify external systems (e.g. boss damage)
        OnAoeDamage?.Invoke(x, y, z, radius, scaledDamage);
    }

    // AOE + наложение CC-эффекта на каждого задетого врага
    public int AoeApply(float x, float y, float z, float radius, int attackerTeam,
        EffectTag cc, float ccValue, float ccDuration, int damage = 0, int? sourceSlot = null)
    {
        var hit = EnemiesInRadius(x, y, z, radius, attackerTeam);
        foreach (var slot in hit)
        {
            int effectiveDamage = damage;
            if (damage > 0)
            {
                int victimMaxHealth = Math.Max(1, _engine.GetHealth(slot));
                float multiplier = GetDamageCapMultiplier(sourceSlot, this);
                effectiveDamage = Math.Min(damage, (int)(victimMaxHealth * multiplier));
            }
            _engine.SetHealth(slot, Math.Max(0, _engine.GetHealth(slot) - effectiveDamage));
            Apply(cc, slot, ccValue, ccDuration, 0.25f, sourceSlot);
        }
        return hit.Count;
    }

    public bool HasEffect(int slot, EffectTag tag) =>
        _effects.Any(e => e.Slot == slot && e.Tag == tag);

    public float EffectValue(int slot, EffectTag tag) =>
        _effects.FirstOrDefault(e => e.Slot == slot && e.Tag == tag)?.Value ?? 0f;

    // bypass evasion/proc-assist state изменения
    public void TickEvasion(int slot) { /* marker for expiry lint; handled in TickAll */ }
    // Crit / Уклонение / Мана-щит / Рефлект — упрощённые проверки на момент удара

    public bool TryCrit(int attackerSlot, int baseDamage, out int finalDamage)
    {
        finalDamage = baseDamage;
        if (!HasEffect(attackerSlot, EffectTag.CritChance)) return false;
        float chance = EffectValue(attackerSlot, EffectTag.CritChance); // 0..1
        if (Random.Shared.NextDouble() >= chance) return false;
        float mult = 1.5f + (float)Random.Shared.NextDouble() * 1.0f; // 1.5x..2.5x
        finalDamage = (int)(baseDamage * mult);
        return true;
    }

    public bool TryEvasion(int targetSlot)
    {
        if (!HasEffect(targetSlot, EffectTag.Evasion)) return false;
        float chance = EffectValue(targetSlot, EffectTag.Evasion);
        return Random.Shared.NextDouble() < chance;
    }

    public int TryManaShield(int targetSlot, int damage)
    {
        if (!HasEffect(targetSlot, EffectTag.ManaShield)) return damage;
        float maxAbsorb = EffectValue(targetSlot, EffectTag.ManaShield);
        int absorbed = (int)Math.Min(maxAbsorb, damage);
        // здесь только расчёт; реальное снятие эфира делается в AetherionPlugin
        return Math.Max(0, damage - absorbed);
    }

    public int TryReflect(int targetSlot, int attackerSlot, int damage)
    {
        if (!HasEffect(targetSlot, EffectTag.Reflect)) return 0;
        float pct = EffectValue(targetSlot, EffectTag.Reflect); // 0..1
        int reflected = (int)(damage * pct);
        try { _engine.SetHealth(attackerSlot, Math.Max(0, _engine.GetHealth(attackerSlot) - reflected)); }
        catch { /* noop */ }
        return reflected;
    }

    public void ClearPlayer(int slot)
    {
        foreach (var e in _effects.Where(e => e.Slot == slot).ToList()) OnExpire(e);
        _effects.RemoveAll(e => e.Slot == slot);
    }

    /// <summary>
    /// Remove only a specific effect tag from a player, leaving all other effects intact.
    /// Used by systems that need to clean up one effect (e.g., mutation removal)
    /// without destroying the player's entire combat state.
    /// </summary>
    public void RemoveEffect(int slot, EffectTag tag)
    {
        foreach (var e in _effects.Where(e => e.Slot == slot && e.Tag == tag).ToList()) OnExpire(e);
        _effects.RemoveAll(e => e.Slot == slot && e.Tag == tag);
    }
}
