using System;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WcsInfinity.Systems;
using WcsInfinity.Models;

namespace WcsInfinity.Races;

// Контекст применения способности
public class AbilityContext
{
    public required CCSPlayerController Player;
    public required PlayerData Data;
    public required RaceProgress Race;
    public required IEngineApi Engine;
    public int Slot;
    public CombatEffects Combat = null!;
    public int SkillLevel;
    public int? VictimSlot;
    public float FromX, FromY;
}

// Делегат эффекта
public delegate void AbilityEffect(AbilityContext ctx);

// ── БИБЛИОТЕКА ЭФФЕКТОВ ПО ТЕГАМ ──
public static class EffectLibrary
{
    public static readonly Dictionary<string, Func<float, AbilityEffect>> Effects = new()
    {
        ["bonus_hp"] = v => ctx =>
            ctx.Engine.AddHealth(ctx.Slot, (int)(v * ctx.SkillLevel), 100 + (int)(v * ctx.SkillLevel)),

        ["speed_buff"] = v => ctx =>
            ctx.Engine.SetSpeed(ctx.Slot, 1f + v * ctx.SkillLevel),

        ["low_gravity"] = v => ctx =>
            ctx.Engine.SetGravity(ctx.Slot, Math.Max(0.3f, 1f - v * ctx.SkillLevel)),

        ["regen"] = v => ctx =>
            ctx.Engine.AddHealth(ctx.Slot, (int)(v * ctx.SkillLevel), 150),

        ["heal_self"] = v => ctx =>
            ctx.Engine.AddHealth(ctx.Slot, (int)v, 200),

        ["dash_forward"] = v => ctx =>
        {
            var pos = ctx.Engine.GetPosition(ctx.Slot);
            var pawn = ctx.Player.PlayerPawn?.Value;
            if (pawn == null || pawn.AbsOrigin == null) return;
            if (ctx.Engine.GetHealth(ctx.Slot) <= 0) return;
            float yaw = pawn.EyeAngles.Y * MathF.PI / 180f;
            ctx.Engine.Teleport(ctx.Slot, pos.x + MathF.Cos(yaw)*v, pos.y + MathF.Sin(yaw)*v, pos.z + 20);
            ctx.Engine.SpawnParticle("particles/aether_dash.vpcf", pos.x, pos.y, pos.z);
        },

        ["invisibility"] = v => ctx =>
            ctx.Engine.SetInvisible(ctx.Slot, true),

        ["lightning_strike"] = v => ctx =>
        {
            var pos = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Engine.Beam(pos.x, pos.y, pos.z + 300, pos.x, pos.y, pos.z, 255, 225, 77, 0.4f);
            ctx.Engine.SpawnParticle("particles/aether_thunder.vpcf", pos.x, pos.y, pos.z);
        },

        ["aoe_explosion"] = v => ctx =>
        {
            var pos = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Engine.SpawnParticle("particles/aether_explosion.vpcf", pos.x, pos.y, pos.z);
        },

        ["teleport_spawn"] = v => ctx =>
        {
            var pos = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Engine.SpawnParticle("particles/aether_recall.vpcf", pos.x, pos.y, pos.z);
        },

        ["lifesteal"] = v => ctx =>
            ctx.Combat.Apply(EffectTag.Lifesteal, ctx.Slot, v, 6f),

        ["soul_collect"] = v => ctx =>
            ctx.Engine.AddHealth(ctx.Slot, (int)v, 250),

        ["leap"] = v => ctx =>
        {
            var pos = ctx.Engine.GetPosition(ctx.Slot);
            var pawn = ctx.Player.PlayerPawn?.Value;
            if (pawn == null || pawn.AbsOrigin == null) return;
            if (ctx.Engine.GetHealth(ctx.Slot) <= 0) return;
            float yaw = pawn.EyeAngles.Y * MathF.PI / 180f;
            ctx.Engine.Teleport(ctx.Slot, pos.x + MathF.Cos(yaw)*v, pos.y + MathF.Sin(yaw)*v, pos.z + 20);
            ctx.Engine.SpawnParticle("particles/aether_dash.vpcf", pos.x, pos.y, pos.z);
        },

        ["blink"] = v => ctx =>
        {
            var pos = ctx.Engine.GetPosition(ctx.Slot);
            var pawn = ctx.Player.PlayerPawn?.Value;
            if (pawn == null || pawn.AbsOrigin == null) return;
            if (ctx.Engine.GetHealth(ctx.Slot) <= 0) return;
            float yaw = pawn.EyeAngles.Y * MathF.PI / 180f;
            float tx = pos.x + MathF.Cos(yaw) * v;
            float ty = pos.y + MathF.Sin(yaw) * v;
            ctx.Engine.Teleport(ctx.Slot, tx, ty, pos.z + 20);
            ctx.Engine.SpawnParticle("particles/aether_dash.vpcf", tx, ty, pos.z);
        },

        ["crit_chance"] = v => ctx => { /* hook into damage multiplier */ },

        ["evasion_chance"] = v => ctx =>
            ctx.Combat.Apply(EffectTag.Evasion, ctx.Slot, v, 5f),

        ["Execute_low_hp"] = v => ctx =>
        {
            if (ctx.VictimSlot.HasValue && ctx.Engine.GetHealth(ctx.VictimSlot.Value) < 30)
                ctx.Engine.SetHealth(ctx.VictimSlot.Value, 0);
        },

        ["armor_break"] = v => ctx => { /* armor reduction hook */ },

        ["mana_shield"] = v => ctx =>
            ctx.Combat.Apply(EffectTag.ManaShield, ctx.Slot, v, 8f),

        ["aoe_damage"] = v => ctx =>
            ctx.Engine.DamageRadius(ctx.Slot, 250f + 15f*ctx.SkillLevel, (int)(v + v*0.25f*ctx.SkillLevel)),

        ["nova_knockback"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            ctx.Combat.AoeApply(p.x,p.y,p.z, 280f, team, EffectTag.Stun, 1f, 1.2f, (int)v, ctx.Slot);
            foreach(var s in ctx.Combat.EnemiesInRadius(p.x,p.y,p.z,280f,team))
                ctx.Engine.Knockback(s, p.x, p.y, 400f);
        },

        ["freeze_aoe"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            ctx.Combat.AoeApply(p.x,p.y,p.z, 240f, team, EffectTag.Freeze, 1f, 2f+0.2f*ctx.SkillLevel, (int)v, ctx.Slot);
        },

        ["team_speed"] = v => ctx =>
            ctx.Engine.ForAlliesInRadius(ctx.Slot, 350f, s => ctx.Engine.SetSpeed(s, 1.0f + 0.15f)),

        ["shield"] = v => ctx =>
        {
            ctx.Combat.Apply(EffectTag.Shield, ctx.Slot, v+10*ctx.SkillLevel, 8f);
            ctx.Engine.AddArmor(ctx.Slot,(int)(v+10*ctx.SkillLevel),200);
        },

        ["slow_aura"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            ctx.Combat.AoeApply(p.x,p.y,p.z, 220f+10f*ctx.SkillLevel, team, EffectTag.Slow, 0.5f, 3f, (int)(v*0.5f), ctx.Slot);
        },

        ["fear_aoe"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            ctx.Combat.AoeApply(p.x,p.y,p.z, 300f, team, EffectTag.Fear, 1f, 2.5f, (int)(v*0.3f), ctx.Slot);
        },

        ["stun_aoe"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            ctx.Combat.AoeApply(p.x,p.y,p.z, 250f, team, EffectTag.Stun, 1f, 1.5f, (int)v, ctx.Slot);
        },

        ["root_aoe"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            ctx.Combat.AoeApply(p.x,p.y,p.z, 240f, team, EffectTag.Root, 1f, 2.5f, (int)(v*0.4f), ctx.Slot);
        },

        ["poison_dot"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            foreach(var s in ctx.Combat.EnemiesInRadius(p.x,p.y,p.z,220f,team))
                ctx.Combat.Apply(EffectTag.Poison,s,2+ctx.SkillLevel,5f,1f,ctx.Slot);
        },

        ["ignite_aoe"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            foreach(var s in ctx.Combat.EnemiesInRadius(p.x,p.y,p.z,230f+10f*ctx.SkillLevel,team))
            {
                ctx.Combat.Apply(EffectTag.Burn,s,3+ctx.SkillLevel,4f,1f,ctx.Slot);
            }
        },

        ["chain_lightning"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            int dmg=(int)v+5*ctx.SkillLevel;
            foreach(var s in ctx.Combat.EnemiesInRadius(p.x,p.y,p.z,350f,team))
            {
                ctx.Engine.SetHealth(s,Math.Max(0,ctx.Engine.GetHealth(s)-dmg));
                var ep=ctx.Engine.GetPosition(s);
                ctx.Engine.Beam(p.x,p.y,p.z,ep.x,ep.y,ep.z,120,180,255,0.4f);
                dmg=(int)(dmg*0.7f);
            }
        },

        ["berserk"] = v => ctx =>
        {
            ctx.Combat.Apply(EffectTag.Berserk, ctx.Slot, v, 5f);
            ctx.Engine.SetSpeed(ctx.Slot, 1.25f);
        },

        ["death_save"] = v => ctx =>
            ctx.Combat.Apply(EffectTag.Shield, ctx.Slot, 100, 4f),

        ["hook_pull"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            int best = -1; float bestD = 1e9f; (float x,float y,float z) bp = (0,0,0);
            foreach(var s in ctx.Combat.EnemiesInRadius(p.x, p.y, p.z, 900f, team))
            {
                var ep = ctx.Engine.GetPosition(s);
                float dx = ep.x-p.x, dy = ep.y-p.y, dz = ep.z-p.z;
                float dd = MathF.Sqrt(dx*dx+dy*dy+dz*dz);
                if(dd < bestD){ bestD=dd; best=s; bp=ep; }
            }
            if(best < 0) return;
            ctx.Engine.Beam(p.x, p.y, p.z+45, bp.x, bp.y, bp.z+45, 120, 220, 255, 0.5f);
            float ux=p.x-bp.x, uy=p.y-bp.y; float ul=MathF.Sqrt(ux*ux+uy*uy); if(ul<1e-3f) ul=1;
            ctx.Engine.Teleport(best, p.x-ux/ul*80f, p.y-uy/ul*80f, p.z+10);
            ctx.Engine.SetHealth(best, Math.Max(0, ctx.Engine.GetHealth(best) - (int)(v + 5*ctx.SkillLevel)));
            ctx.Engine.SpawnParticle("particles/aether_thunder.vpcf", bp.x, bp.y, bp.z);
        },

        ["totem_heal"] = v => ctx =>
        {
            ctx.Engine.ForAlliesInRadius(ctx.Slot, 350f, s => ctx.Engine.AddHealth(s, (int)(v+5*ctx.SkillLevel), 200));
            var p = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Engine.SpawnParticle("particles/aether_recall.vpcf", p.x, p.y, p.z);
        },

        ["team_heal"] = v => ctx =>
            ctx.Engine.ForAlliesInRadius(ctx.Slot, 420f, s => ctx.Engine.AddHealth(s, (int)(v+5*ctx.SkillLevel), 200)),

        ["lifesteal_aoe"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot); int team = ctx.Player.TeamNum; int healed = 0;
            foreach(var s in ctx.Combat.EnemiesInRadius(p.x,p.y,p.z,270f,team))
            { ctx.Engine.SetHealth(s, Math.Max(0, ctx.Engine.GetHealth(s) - (int)v)); healed += (int)(v*0.5f); }
            ctx.Engine.AddHealth(ctx.Slot, healed, 250);
            ctx.Engine.SpawnParticle("particles/aether_explosion.vpcf", p.x, p.y, p.z);
        },

        ["summon"] = v => ctx =>
        {
            ctx.Combat.Apply(EffectTag.Shield, ctx.Slot, v+10*ctx.SkillLevel, 10f);
            ctx.Engine.AddArmor(ctx.Slot, (int)v, 150);
            var p = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Engine.SpawnParticle("particles/aether_explosion.vpcf", p.x, p.y, p.z);
        },

        ["self_freeze_aoe"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            ctx.Combat.AoeApply(p.x,p.y,p.z, 240f, team, EffectTag.Freeze, 1f, 1.5f, (int)(v + 5*ctx.SkillLevel), ctx.Slot);
        },

        ["fire_dot"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
                ctx.Combat.Apply(EffectTag.Burn, ctx.VictimSlot.Value, 3+ctx.SkillLevel, 6f, 1f, ctx.Slot);
        },

        ["fireball"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            foreach(var s in ctx.Combat.EnemiesInRadius(p.x,p.y,p.z,280f,team))
            {
                ctx.Engine.SetHealth(s, Math.Max(0, ctx.Engine.GetHealth(s) - (int)v));
                ctx.Combat.Apply(EffectTag.Burn, s, 3+ctx.SkillLevel, 4f, 1f, ctx.Slot);
            }
        },

        ["execute"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue && ctx.Engine.GetHealth(ctx.VictimSlot.Value) < 40)
                ctx.Engine.SetHealth(ctx.VictimSlot.Value, 0);
        },

        ["chase_speed"] = v => ctx =>
            ctx.Engine.SetSpeed(ctx.Slot, 1f + v*0.01f * ctx.SkillLevel),

        ["execute_ult"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
            {
                ctx.Engine.SetHealth(ctx.VictimSlot.Value, Math.Max(0, ctx.Engine.GetHealth(ctx.VictimSlot.Value) - (int)v));
                ctx.Combat.Apply(EffectTag.Slow, ctx.VictimSlot.Value, 35, 3f);
            }
        },

        ["flash_ult"] = v => ctx =>
        {
            int team = ctx.Player.TeamNum;
            foreach(var s in ctx.Combat.EnemiesInRadius(0,0,0, 9999f, team)) { /* flash hook removed */ }
        },

        ["damage_reduction"] = v => ctx =>
            ctx.Combat.Apply(EffectTag.Shield, ctx.Slot, (int)(v*2), 5f),

        ["heal_target"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
                ctx.Engine.AddHealth(ctx.VictimSlot.Value, (int)v, 250);
        },

        ["armor_buff"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
                ctx.Engine.AddArmor(ctx.VictimSlot.Value, (int)(v+10*ctx.SkillLevel), 200);
        },

        ["projectile"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
                ctx.Engine.SetHealth(ctx.VictimSlot.Value, Math.Max(0, ctx.Engine.GetHealth(ctx.VictimSlot.Value) - (int)v));
        },

        ["slow_on_hit"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
                ctx.Combat.Apply(EffectTag.Slow, ctx.VictimSlot.Value, v, 3f);
        },

        ["self_shield"] = v => ctx =>
            ctx.Combat.Apply(EffectTag.Shield, ctx.Slot, v+15*ctx.SkillLevel, 5f),

        ["aoe_damage_ult"] = v => ctx =>
            ctx.Engine.DamageRadius(ctx.Slot, 250f+15f*ctx.SkillLevel, (int)(v + 5*ctx.SkillLevel)),

        ["trap"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Engine.SpawnParticle("particles/ui/trap.vpcf", p.x,p.y,p.z);
            ctx.Combat.AoeApply(p.x,p.y,p.z, 140f, ctx.Player.TeamNum, EffectTag.Stun, 1f, 2f, (int)v, ctx.Slot);
        },

        ["radar"] = v => ctx => { /* radar map hook */ },

        ["trap_remote"] = v => ctx => { /* remote detonate */ },

        ["aoe_trap"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Engine.SpawnParticle("particles/aether_explosion.vpcf", p.x,p.y,p.z);
            ctx.Engine.DamageRadius(ctx.Slot, 220f, (int)v);
        },

        ["pierce_projectile"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
                ctx.Engine.SetHealth(ctx.VictimSlot.Value, Math.Max(0, ctx.Engine.GetHealth(ctx.VictimSlot.Value) - (int)v));
        },

        ["multishot"] = v => ctx =>
        {
            for(int i=0;i<3;i++)
                if(ctx.VictimSlot.HasValue)
                    ctx.Engine.SetHealth(ctx.VictimSlot.Value, Math.Max(0, ctx.Engine.GetHealth(ctx.VictimSlot.Value) - (int)v));
        },

        ["mark"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
                ctx.Combat.Apply(EffectTag.CritChance, ctx.VictimSlot.Value, v, 5f);
        },

        ["ally_shield"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
                ctx.Combat.Apply(EffectTag.Shield, ctx.VictimSlot.Value, v+10*ctx.SkillLevel, 6f);
        },

        ["aoe_heal_damage"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            ctx.Combat.AoeApply(p.x,p.y,p.z, 180f, team, EffectTag.Shield, 1f, 2f, (int)v, ctx.Slot);
            foreach(var s in ctx.Combat.EnemiesInRadius(p.x,p.y,p.z,180f,team))
                ctx.Engine.SetHealth(s, Math.Max(0, ctx.Engine.GetHealth(s) - (int)v));
        },

        ["backstab_freeze"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
            {
                ctx.Engine.SetHealth(ctx.VictimSlot.Value, Math.Max(0, ctx.Engine.GetHealth(ctx.VictimSlot.Value) - (int)v));
                ctx.Combat.Apply(EffectTag.Freeze, ctx.VictimSlot.Value, 1, 1.5f);
            }
        },

        ["blink_slice"] = v => ctx =>
        {
            var pos = ctx.Engine.GetPosition(ctx.Slot);
            var pawn = ctx.Player.PlayerPawn?.Value;
            if(pawn == null || pawn.AbsOrigin == null) return;
            if (ctx.Engine.GetHealth(ctx.Slot) <= 0) return;
            float yaw = pawn.EyeAngles.Y * MathF.PI / 180f;
            ctx.Engine.Teleport(ctx.Slot, pos.x+MathF.Cos(yaw)*420f, pos.y+MathF.Sin(yaw)*420f, pos.z+20);
            ctx.Combat.AoeApply(pos.x+MathF.Cos(yaw)*420f, pos.y+MathF.Sin(yaw)*420f, pos.z, 150f, ctx.Player.TeamNum, EffectTag.Stun, 1f, 1f, (int)v, ctx.Slot);
        },

        ["counter"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
                ctx.Engine.SetHealth(ctx.VictimSlot.Value, Math.Max(0, ctx.Engine.GetHealth(ctx.VictimSlot.Value) - (int)v));
        },

        ["aoe_knockback"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            foreach(var s in ctx.Combat.EnemiesInRadius(p.x,p.y,p.z,170f, ctx.Player.TeamNum))
                ctx.Engine.Knockback(s, p.x, p.y, 350f);
        },

        ["combo_slash"] = v => ctx =>
        {
            for(int i=0;i<3;i++)
                if(ctx.VictimSlot.HasValue)
                    ctx.Engine.SetHealth(ctx.VictimSlot.Value, Math.Max(0, ctx.Engine.GetHealth(ctx.VictimSlot.Value) - (int)v));
        },

        ["fear_aura"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Combat.AoeApply(p.x,p.y,p.z, 160f, ctx.Player.TeamNum, EffectTag.Fear, 1f, 2f, (int)(v*0.3f), ctx.Slot);
        },

        ["projectile_fear"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
            {
                ctx.Engine.SetHealth(ctx.VictimSlot.Value, Math.Max(0, ctx.Engine.GetHealth(ctx.VictimSlot.Value) - (int)v));
                ctx.Combat.Apply(EffectTag.Fear, ctx.VictimSlot.Value, 2f, 2f);
            }
        },

        ["silence_shot"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
                ctx.Combat.Apply(EffectTag.Stun, ctx.VictimSlot.Value, v * 0.3f, 3f);
        },

        ["silence_aura"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            ctx.Combat.AoeApply(p.x,p.y,p.z, 300f, team, EffectTag.Stun, 1f, 2f, (int)(v*0.3f), ctx.Slot);
        },

        ["heal_aura"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            ctx.Combat.AoeApply(p.x,p.y,p.z, 350f, team, EffectTag.Shield, 1f, 3f, (int)(v*0.5f), ctx.Slot);
            ctx.Engine.ForAlliesInRadius(ctx.Slot, 350f, s => ctx.Engine.AddHealth(s, (int)(v+3*ctx.SkillLevel), 200));
        },

        ["kill_heal"] = v => ctx =>
            ctx.Engine.AddHealth(ctx.Slot, (int)(v + 2*ctx.SkillLevel), 200),

        ["kill_heal_aura"] = v => ctx =>
            ctx.Engine.AddHealth(ctx.Slot, (int)(v + 2*ctx.SkillLevel), 200),

        ["slow"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
                ctx.Combat.Apply(EffectTag.Slow, ctx.VictimSlot.Value, v, 3f);
        },

        ["movement_speed"] = v => ctx =>
            ctx.Engine.SetSpeed(ctx.Slot, 1f + v * 0.01f * ctx.SkillLevel),

        ["haste"] = v => ctx =>
            ctx.Engine.SetSpeed(ctx.Slot, 1f + v * 0.01f * ctx.SkillLevel),

        ["ice_bolt"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
            {
                ctx.Engine.SetHealth(ctx.VictimSlot.Value, Math.Max(0, ctx.Engine.GetHealth(ctx.VictimSlot.Value) - (int)v));
                ctx.Combat.Apply(EffectTag.Freeze, ctx.VictimSlot.Value, 1, 1.5f+0.1f*ctx.SkillLevel);
            }
        },

        ["fire_breath"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            int team = ctx.Player.TeamNum;
            foreach(var s in ctx.Combat.EnemiesInRadius(p.x,p.y,p.z, 250f+10f*ctx.SkillLevel,team))
            {
                ctx.Engine.SetHealth(s, Math.Max(0, ctx.Engine.GetHealth(s) - (int)(v+3*ctx.SkillLevel)));
                ctx.Combat.Apply(EffectTag.Burn, s, 2+ctx.SkillLevel, 4f, 1f, ctx.Slot);
            }
            ctx.Engine.SpawnParticle("particles/aether_explosion.vpcf", p.x, p.y, p.z);
        },

        ["smite"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
            {
                ctx.Engine.SetHealth(ctx.VictimSlot.Value, Math.Max(0, ctx.Engine.GetHealth(ctx.VictimSlot.Value) - (int)(v+5*ctx.SkillLevel)));
                var ep = ctx.Engine.GetPosition(ctx.VictimSlot.Value);
                ctx.Engine.Beam(ep.x, ep.y, ep.z+300, ep.x, ep.y, ep.z, 255, 225, 100, 0.3f);
            }
        },

        ["armor_reduce"] = v => ctx => { /* armor hook: reduce target armor on hit */ },

        ["accuracy_debuff"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
                ctx.Combat.Apply(EffectTag.Slow, ctx.VictimSlot.Value, v*0.3f, 4f);
        },

        ["parry"] = v => ctx =>
            ctx.Combat.Apply(EffectTag.Reflect, ctx.Slot, v, 5f),

        ["revive"] = v => ctx =>
            ctx.Combat.Apply(EffectTag.Shield, ctx.Slot, 100, 8f),

        ["radar"] = v => ctx => { /* radar map hook */ },

        ["turret"] = v => ctx => { /* turret hook */ },

        ["bullet_wall"] = v => ctx => { /* wall hook */ },

        ["silent_steps"] = v => ctx => { /* volume hook */ },

        ["clone_ult"] = v => ctx => { /* clone hook */ },

        ["drone_storm"] = v => ctx => { /* drone hook */ },

        ["illuison"] = v => ctx => { /* clone hook */ },

        ["orbiting_blades"] = v => ctx => { /* orbiting hook */ },

        ["charge_stun"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
            {
                ctx.Engine.SetHealth(ctx.VictimSlot.Value, Math.Max(0, ctx.Engine.GetHealth(ctx.VictimSlot.Value) - (int)v));
                ctx.Combat.Apply(EffectTag.Stun, ctx.VictimSlot.Value, 1, 1.5f);
            }
        },

        ["speed_boost"] = v => ctx =>
            ctx.Engine.SetSpeed(ctx.Slot, 1f + v*0.01f * ctx.SkillLevel),

        ["dash_damage"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Engine.Teleport(ctx.Slot, p.x, p.y, p.z+10);
            ctx.Combat.AoeApply(p.x,p.y,p.z, 170f, ctx.Player.TeamNum, EffectTag.Stun, 1f, 1f, (int)v, ctx.Slot);
        },

        ["cyclone"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Engine.DamageRadius(ctx.Slot, 230f, (int)(v + 5*ctx.SkillLevel));
            foreach(var s in ctx.Combat.EnemiesInRadius(p.x,p.y,p.z,230f, ctx.Player.TeamNum))
                ctx.Engine.Knockback(s, p.x, p.y, 280f);
        },

        ["disarm"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
                ctx.Combat.Apply(EffectTag.Stun, ctx.VictimSlot.Value, 1, 3.5f);
        },

        ["execute_bonus"] = v => ctx => { /* bonus vs low hp */ },

        ["double_slash"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
            {
                ctx.Engine.SetHealth(ctx.VictimSlot.Value, Math.Max(0, ctx.Engine.GetHealth(ctx.VictimSlot.Value) - (int)(v*2)));
                ctx.Combat.Apply(EffectTag.Burn, ctx.VictimSlot.Value, 3+ctx.SkillLevel, 8f, 1f, ctx.Slot);
            }
        },

        ["kill_heal_aura"] = v => ctx =>
            ctx.Engine.ForAlliesInRadius(ctx.Slot, 120f, s => ctx.Engine.AddHealth(s, (int)(v*ctx.SkillLevel), 200)),

        ["dash_slash_ult"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Engine.Teleport(ctx.Slot, p.x, p.y, p.z+10);
            ctx.Engine.DamageRadius(ctx.Slot, 210f, (int)(v + 8*ctx.SkillLevel));
        },

        ["knockback"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            if(ctx.VictimSlot.HasValue)
            {
                var vPos = ctx.Engine.GetPosition(ctx.VictimSlot.Value);
                ctx.Engine.Knockback(ctx.VictimSlot.Value, vPos.x, vPos.y, v);
            }
            else
            {
                foreach(var s in ctx.Combat.EnemiesInRadius(p.x,p.y,p.z, 250f, ctx.Player.TeamNum))
                    ctx.Engine.Knockback(s, p.x, p.y, v);
            }
        },

        ["berserk_self"] = v => ctx =>
        {
            ctx.Combat.Apply(EffectTag.Berserk, ctx.Slot, v, 6f);
            ctx.Engine.SetSpeed(ctx.Slot, 1.5f);
        },

        ["hp_cost_aoe"] = v => ctx =>
        {
            int cost = (int)v;
            if(ctx.Engine.GetHealth(ctx.Slot) > cost)
            {
                ctx.Engine.SetHealth(ctx.Slot, ctx.Engine.GetHealth(ctx.Slot) - cost);
                var p = ctx.Engine.GetPosition(ctx.Slot);
                ctx.Engine.DamageRadius(ctx.Slot, 190f, cost);
            }
        },

        ["horror_ult"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Combat.AoeApply(p.x,p.y,p.z, 240f, ctx.Player.TeamNum, EffectTag.Fear, 1f, 3f, (int)(v*0.5f), ctx.Slot);
            ctx.Engine.DamageRadius(ctx.Slot, 240f, (int)v);
        },

        ["blind_chance"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue && v > 0)
                ctx.Engine.Blind(ctx.VictimSlot.Value, v * ctx.SkillLevel * 0.5f);
        },

        ["lightning"] = v => ctx =>
        {
            var pos = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Engine.Beam(pos.x, pos.y, pos.z+400, pos.x, pos.y, pos.z, 200, 220, 255, 0.6f);
            ctx.Engine.SpawnParticle("particles/aether_thunder.vpcf", pos.x, pos.y, pos.z);
            ctx.Engine.DamageRadius(ctx.Slot, 180f, (int)v);
        },

        ["stun_ult"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Combat.AoeApply(p.x,p.y,p.z, 230f, ctx.Player.TeamNum, EffectTag.Stun, 1f, 3.5f, (int)v, ctx.Slot);
        },

        ["silent_projectile"] = v => ctx => { /* tracer hook */ },

        ["invisibility_combo"] = v => ctx =>
        {
            ctx.Engine.SetInvisible(ctx.Slot, true);
            ctx.Combat.Apply(EffectTag.CritChance, ctx.Slot, v-1, 4f);
        },

        ["disarm_damage"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue)
            {
                ctx.Engine.SetHealth(ctx.VictimSlot.Value, Math.Max(0, ctx.Engine.GetHealth(ctx.VictimSlot.Value) - (int)v));
                ctx.Combat.Apply(EffectTag.Stun, ctx.VictimSlot.Value, 1, 3f);
            }
        },

        ["deafen_ult"] = v => ctx =>
        {
            var p = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Engine.DamageRadius(ctx.Slot, 220f, (int)v);
            foreach(var s in ctx.Combat.EnemiesInRadius(p.x,p.y,p.z,220f, ctx.Player.TeamNum)) { }
        },

        ["stack_kill"] = v => ctx => { /* kill stack hook */ },

        ["freeze_chance"] = v => ctx =>
        {
            if(ctx.VictimSlot.HasValue && v > 0)
                ctx.Combat.Apply(EffectTag.Freeze, ctx.VictimSlot.Value, 1, 1.2f);
        },

        ["backstab"] = v => ctx =>
            ctx.Engine.SetSpeed(ctx.Slot, 1f + v*0.01f),

        ["blinded_bonus"] = v => ctx =>
            ctx.Engine.SetSpeed(ctx.Slot, 1f + v*0.005f),

        ["summon_wisp"] = v => ctx => { /* wisp spawn */ },
    };

    public static AbilityEffect? Resolve(string effect, float value) =>
        Effects.TryGetValue(effect, out var f) ? f(value) : SmartFallback(effect, value);

    // ── УМНЫЙ FALLBACK ──
    // Если эффект не зарегистрирован явно, выводим эффект по шаблону имени.
    // Это позволяет добавлять расы с новыми эффектами без правки кода —
    // RaceRuntime выведет корректное поведение из имени и полей способности.
    private static AbilityEffect? SmartFallback(string effect, float value)
    {
        var e = effect.ToLowerInvariant();
        var v = value;
        // Урон по цели / AoE-урон
        if (e.Contains("damage") || e.Contains("blast") || e.Contains("explosion") ||
            e.Contains("storm") || e.Contains("strike") || e.Contains("smite") ||
            e.Contains("meteor") || e.Contains("eruption") || e.Contains("thunder"))
            return ctx =>
            {
                var pos = ctx.Engine.GetPosition(ctx.Slot);
                ctx.Engine.SpawnParticle("particles/aether_explosion.vpcf", pos.x, pos.y, pos.z);
                ctx.Engine.DamageRadius(ctx.Slot, 300, (int)(v * ctx.SkillLevel));
            };
        // Поджог / огонь
        if (e.Contains("fire") || e.Contains("burn") || e.Contains("flame") || e.Contains("ignite"))
            return ctx =>
            {
                var pos = ctx.Engine.GetPosition(ctx.Slot);
                ctx.Engine.SpawnParticle("particles/aether_fire.vpcf", pos.x, pos.y, pos.z);
                ctx.Combat.Apply(EffectTag.Burn, ctx.Slot, v, 5f);
            };
        // Заморозка / лёд
        if (e.Contains("freeze") || e.Contains("frost") || e.Contains("ice") || e.Contains("winter") || e.Contains("blizzard"))
            return ctx =>
            {
                ctx.Combat.Apply(EffectTag.Freeze, ctx.Slot, v, 2f);
            };
        // Яд
        if (e.Contains("poison") || e.Contains("plague") || e.Contains("epidemic"))
            return ctx => { ctx.Combat.Apply(EffectTag.Poison, ctx.Slot, v, 6f); };
        // Оглушение
        if (e.Contains("stun") || e.Contains("charge_stun") || e.Contains("flash"))
            return ctx => { ctx.Combat.Apply(EffectTag.Stun, ctx.Slot, v, 2f); };
        // Замедление
        if (e.Contains("slow") || e.Contains("dilation") || e.Contains("gravity"))
            return ctx => { ctx.Combat.Apply(EffectTag.Slow, ctx.Slot, v, 3f); };
        // Телепорт / blink / dash
        if (e.Contains("blink") || e.Contains("teleport") || e.Contains("dash") || e.Contains("leap") || e.Contains("charge"))
            return ctx =>
            {
                var pos = ctx.Engine.GetPosition(ctx.Slot);
                var pawn = ctx.Player.PlayerPawn?.Value;
                if (pawn == null || pawn.AbsOrigin == null) return;
                float yaw = pawn.EyeAngles.Y * MathF.PI / 180f;
                ctx.Engine.Teleport(ctx.Slot, pos.x + MathF.Cos(yaw) * v, pos.y + MathF.Sin(yaw) * v, pos.z + 20);
                ctx.Engine.SpawnParticle("particles/aether_dash.vpcf", pos.x, pos.y, pos.z);
            };
        // Невидимость
        if (e.Contains("invis") || e.Contains("cloak") || e.Contains("shadow") || e.Contains("phantom"))
            return ctx => { ctx.Engine.SetInvisible(ctx.Slot, true); };
        // Лечение
        if (e.Contains("heal") || e.Contains("regen"))
            return ctx => { ctx.Engine.AddHealth(ctx.Slot, (int)(v * ctx.SkillLevel), 250); };
        // Щит
        if (e.Contains("shield") || e.Contains("barrier") || e.Contains("wall") || e.Contains("bastion"))
            return ctx => { ctx.Combat.Apply(EffectTag.Shield, ctx.Slot, v, 5f); };
        // Вампиризм
        if (e.Contains("lifesteal") || e.Contains("vampir"))
            return ctx => { ctx.Combat.Apply(EffectTag.Lifesteal, ctx.Slot, v, 6f); };
        // Притяжение / чёрная дыра
        if (e.Contains("pull") || e.Contains("black_hole"))
            return ctx => { ctx.Combat.Apply(EffectTag.Root, ctx.Slot, v, 2f); };
        // Отталкивание / нокбэк
        if (e.Contains("knockback") || e.Contains("push") || e.Contains("cyclone"))
            return ctx =>
            {
                var pos = ctx.Engine.GetPosition(ctx.Slot);
                ctx.Engine.Knockback(ctx.Slot, pos.x, pos.y, v);
            };
        // Молния
        if (e.Contains("lightning") || e.Contains("thunder"))
            return ctx =>
            {
                var pos = ctx.Engine.GetPosition(ctx.Slot);
                ctx.Engine.Beam(pos.x, pos.y, pos.z + 300, pos.x, pos.y, pos.z, 255, 225, 77, 0.4f);
                ctx.Engine.SpawnParticle("particles/aether_thunder.vpcf", pos.x, pos.y, pos.z);
            };
        // Ускорение / haste
        if (e.Contains("speed") || e.Contains("haste") || e.Contains("frenzy"))
            return ctx => { ctx.Engine.SetSpeed(ctx.Slot, 1f + v * 0.01f * ctx.SkillLevel); };
        // Отражение / parry
        if (e.Contains("parry") || e.Contains("reflect") || e.Contains("mirror"))
            return ctx => { ctx.Combat.Apply(EffectTag.Reflect, ctx.Slot, v, 5f); };
        // Казнь / execute → крит
        if (e.Contains("execute") || e.Contains("reaper"))
            return ctx => { ctx.Combat.Apply(EffectTag.CritChance, ctx.Slot, v, 5f); };
        // Страх / паника
        if (e.Contains("fear") || e.Contains("horror") || e.Contains("panic"))
            return ctx => { ctx.Combat.Apply(EffectTag.Fear, ctx.Slot, v, 2f); };
        // Безмолвие / disarm → стан слабее
        if (e.Contains("disarm") || e.Contains("silence") || e.Contains("deafen"))
            return ctx => { ctx.Combat.Apply(EffectTag.Stun, ctx.Slot, v * 0.3f, 3f); };
        // Базовый: если не распознали — AoE-урон по умолчанию
        return ctx =>
        {
            var pos = ctx.Engine.GetPosition(ctx.Slot);
            ctx.Engine.SpawnParticle("particles/aether_cast.vpcf", pos.x, pos.y, pos.z);
            ctx.Engine.DamageRadius(ctx.Slot, 200, (int)(v * ctx.SkillLevel));
        };
    }
}

// ── ИСПОЛНИТЕЛЬ способностей расы ──
public class RaceRuntime
{
    public void ApplyPassives(AbilityContext ctxTemplate, RaceDefinition def, RaceProgress rp)
    {
        foreach(var ab in def.Abilities)
        {
            if(ab.Type != "Passive") continue;
            int lvl = rp.SkillLevels.GetValueOrDefault(ab.Index, 0);
            if(lvl <= 0 || string.IsNullOrEmpty(ab.Effect)) continue;
            var eff = EffectLibrary.Resolve(ab.Effect, ab.Value);
            if(eff == null) continue;
            ctxTemplate.SkillLevel = lvl;
            eff(ctxTemplate);
        }
    }

    public bool Activate(AbilityContext ctx, RaceDefinition def, RaceProgress rp, int abilityIndex)
    {
        var ab = def.Abilities.Find(a => a.Index == abilityIndex);
        if (ab == null || string.IsNullOrEmpty(ab.Effect)) return false;
        int lvl = rp.SkillLevels.GetValueOrDefault(ab.Index, 0);
        if(ab.Type != "Ultimate" && lvl <= 0) return false;
        if(ab.Type == "Ultimate" && lvl <= 0) return false;
        if(string.IsNullOrEmpty(ab.Effect)) return false;
        var eff = EffectLibrary.Resolve(ab.Effect, ab.Value);
        if(eff == null) return false;
        ctx.SkillLevel = Math.Max(1, lvl);
        eff(ctx);
        return true;
    }
}
