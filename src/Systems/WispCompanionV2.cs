using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using WcsInfinity.Plugins;
using WcsInfinity.Core;

namespace WcsInfinity.Systems;

public sealed class WispCompanionV2
{
    private const string FallbackModel = "models/props/de_inferno/hr_i/wood_1x1.vmdl";
    private const float HoverZ = 78f;
    private const float HoverAmp = 3.5f;

    private sealed class TierConfig
    {
        [JsonPropertyName("tier")] public string Tier { get; set; } = "Common";
        [JsonPropertyName("label_ru")] public string LabelRu { get; set; } = "";
        [JsonPropertyName("color")] public string Color { get; set; } = "#8fd3ff";
        [JsonPropertyName("model")] public string Model { get; set; } = "";
        [JsonPropertyName("aura")] public string Aura { get; set; } = "";
        [JsonPropertyName("followOffset")] public List<float> FollowOffset { get; set; } = new() { 0, 0, -25 };
        [JsonPropertyName("followSpeed")] public float FollowSpeed { get; set; } = 1.05f;
    }

    private sealed class WispFile
    {
        [JsonPropertyName("wisp")] public WispEntry? Entry { get; set; }
    }
    private sealed class WispEntry
    {
        [JsonPropertyName("tiers")] public List<TierConfig> Tiers { get; set; } = new();
    }

    private readonly Dictionary<string, TierConfig> _tierCfg = new(StringComparer.OrdinalIgnoreCase);

    private sealed class V2State
    {
        public ulong OwnerSteam;
        public string SkinTier = "Искорка";
        public int Stage = 1;
        public CDynamicProp? Prop;
        public CParticleSystem? AuraFx;
        public float TickAcc;
        public float AbilityInterval = 2.5f;
        public float HoverPhase;
        public float BackstabCd;
        public float AssistCd;
        public float AuraCd;
        public bool UltAssistReady;
        public float UltBuffTimer;
    }

    private readonly Dictionary<int, V2State> _bySlot = new();
    private readonly AetherionPlugin _plugin;
    private bool _customModelAvailable;

    public Action<CCSPlayerController, string, int>? OnEvolve { get; set; }

    public WispCompanionV2(AetherionPlugin plugin) => _plugin = plugin;

    public void Initialize()
    {
        LoadConfig();
        _plugin.RegisterEventHandler<EventPlayerSpawn>(OnSpawn, HookMode.Post);
        _plugin.RegisterEventHandler<EventRoundEnd>(OnRoundEnd, HookMode.Post);
        _plugin.RegisterListener<Listeners.OnTick>(Tick);
    }

    private void LoadConfig()
    {
        try
        {
            var path = Path.Combine(_plugin.ModuleDirectory, "..", "..", "configs", "pets", "wisp.json");
            if (!File.Exists(path)) { BuildDefaultTiers(); return; }
            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<WispFile>(json);
            if (file?.Entry?.Tiers is { Count: > 0 } tiers)
            {
                _tierCfg.Clear();
                foreach (var t in tiers) _tierCfg[t.Tier] = t;
            }
            else BuildDefaultTiers();
        }
        catch { BuildDefaultTiers(); }
    }

    private void BuildDefaultTiers()
    {
        if (_tierCfg.Count > 0) return;
        _tierCfg["Common"] = new TierConfig { Tier = "Common", Color = "#8fd3ff" };
        _tierCfg["Rare"] = new TierConfig { Tier = "Rare", Color = "#ffe9a4", Aura = "particles/wisp/dust_dawn.vpcf" };
        _tierCfg["Epic"] = new TierConfig { Tier = "Epic", Color = "#c7b9ff", Aura = "particles/wisp/arc_walk.vpcf" };
        _tierCfg["Legendary"] = new TierConfig { Tier = "Legendary", Color = "#ffd166", Aura = "particles/wisp/crown_flame.vpcf" };
    }

    private static TierConfig TierOrDefault(Dictionary<string, TierConfig> map, string tier)
    {
        if (map.TryGetValue(tier, out var t)) return t;
        return map.TryGetValue("Common", out var c) ? c
            : new TierConfig { Tier = "Common", Color = "#8fd3ff" };
    }

    private static string TierConfigKey(int stage) => stage switch
    {
        >= 5 => "Legendary",
        >= 4 => "Epic",
        >= 3 => "Rare",
        _ => "Common"
    };

    // ── PUBLIC: OwnerBonuses (used by combat code) ──
    public Dictionary<string, float> GetOwnerBonuses(CCSPlayerController p)
    {
        if (p == null || !p.IsValid || p.IsBot) return new();
        var d = _plugin.Data(p.SteamID);
        var rp = d.GetRace(d.CurrentRaceId);
        return WispEvolution.OwnerBonuses(rp.WispBond);
    }

    public bool HasUltAssistBuff(CCSPlayerController p)
    {
        if (p == null || !_bySlot.TryGetValue(p.Slot, out var s)) return false;
        return s.UltBuffTimer > 0;
    }

    public float ConsumeUltAssistBuff(CCSPlayerController p)
    {
        if (p == null || !_bySlot.TryGetValue(p.Slot, out var s)) return 1f;
        if (s.UltBuffTimer > 0)
        {
            s.UltBuffTimer = 0;
            s.UltAssistReady = false;
            return 1.5f;
        }
        return 1f;
    }

    public bool HasDeathSave(CCSPlayerController p)
    {
        var bonuses = GetOwnerBonuses(p);
        return bonuses.ContainsKey("death_save") && bonuses["death_save"] > 0;
    }

    public string BuildPreview(CCSPlayerController? p)
    {
        if (p == null) return "";
        var d = _plugin.Data(p.SteamID);
        var rp = d.GetRace(d.CurrentRaceId);
        var stage = WispEvolution.StageFor(rp.WispBond);
        var (progress, remaining, next) = WispEvolution.Progress(rp.WispBond);
        int barLen = 12;
        int filled = (int)(progress * barLen);
        string bar = new string('█', filled) + new string('░', barLen - filled);
        var nextText = next != null ? $" → {next.Title} ({remaining} Bond)" : " [МАКСИМУМ]";
        return $"\x04◈ {stage.Title} \x01[ {bar} ]{nextText}\n \x01Связь: \x06{rp.WispBond} | {stage.PassivePerk}";
    }

    // ── ТИРИЗАЦИЯ ──

    private HookResult OnSpawn(EventPlayerSpawn ev, GameEventInfo info)
    {
        var p = ev.Userid;
        if (p == null || !p.IsValid || p.IsBot) return HookResult.Continue;
        GetOrInit(p);
        SpawnCompanion(p);
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo info)
    {
        foreach (var kv in _bySlot.ToArray())
        {
            var pl = Utilities.GetPlayerFromSlot(kv.Key);
            if (pl == null || !pl.IsValid) Cleanup(kv.Key);
        }
        return HookResult.Continue;
    }

    private void Tick()
    {
        var arr = _bySlot.ToArray();
        foreach (var kv in arr)
        {
            var slot = kv.Key;
            var s = kv.Value;
            var p = Utilities.GetPlayerFromSlot(slot);
            if (p == null || !p.IsValid || p.IsBot) { Cleanup(slot); continue; }
            if (!p.PawnIsAlive || p.PlayerPawn?.Value?.AbsOrigin == null)
            {
                DetachVisuals(s);
                continue;
            }

            ReparentIfNeeded(s, p.PlayerPawn.Value);

            // Hover animation
            s.HoverPhase += 0.05f;
            if (s.Prop != null && s.Prop.IsValid && p.PlayerPawn.Value.AbsOrigin != null)
            {
                var o = p.PlayerPawn.Value.AbsOrigin;
                float z = o.Z + HoverZ + MathF.Sin(s.HoverPhase) * HoverAmp;
                try { s.Prop.Teleport(new Vector(o.X, o.Y, z), new QAngle(0, s.HoverPhase * 40f, 0), new Vector(0, 0, 0)); }
                catch (Exception ex) { Console.WriteLine($"[Wisp] teleport err: {ex.Message}"); }
            }

            // Ability ticks
            s.TickAcc += 0.5f;
            if (s.TickAcc < s.AbilityInterval) continue;
            s.TickAcc -= s.AbilityInterval;

            if (s.BackstabCd > 0) s.BackstabCd = Math.Max(0, s.BackstabCd - s.AbilityInterval);
            if (s.AssistCd > 0) s.AssistCd = Math.Max(0, s.AssistCd - s.AbilityInterval);
            if (s.AuraCd > 0) s.AuraCd = Math.Max(0, s.AuraCd - s.AbilityInterval);

            // Stage 3+: backstab warning
            if (s.Stage >= 3 && s.BackstabCd <= 0)
                TryBackstabWarn(p, s);

            // Stage 4+: ambient aura (heal nearby allies)
            if (s.Stage >= 4 && s.AuraCd <= 0)
                TryAmbientAura(p, s);

            // Stage 5: ultimate assist buff
            if (s.Stage >= 5 && s.AssistCd <= 0 && !s.UltAssistReady)
                TryUltimateAssist(p, s);

            // Tick down ult buff
            if (s.UltBuffTimer > 0) s.UltBuffTimer -= s.AbilityInterval;
        }
    }

    // ── ABILITIES ──

    private void TryBackstabWarn(CCSPlayerController p, V2State s)
    {
        if (p.PlayerPawn?.Value?.AbsOrigin == null || p.PlayerPawn.Value.EyeAngles == null) return;
        var myPos = p.PlayerPawn.Value.AbsOrigin;
        var eyeAng = p.PlayerPawn.Value.EyeAngles;
        float myYaw = eyeAng.Y;

        bool danger = false;
        foreach (var pl in Utilities.GetPlayers())
        {
            if (pl == null || !pl.IsValid || pl.IsBot || !pl.PawnIsAlive) continue;
            if (pl.Slot == p.Slot) continue;
            if (pl.TeamNum == p.TeamNum) continue;

            var theirPawn = pl.PlayerPawn?.Value;
            if (theirPawn?.AbsOrigin == null) continue;
            var diff = theirPawn.AbsOrigin - myPos;
            float dist = diff.Length();
            if (dist > 300f) continue;

            float theirYaw = MathF.Atan2(diff.Y, diff.X) * (180f / MathF.PI);
            float delta = MathF.Abs(((theirYaw - myYaw + 540f) % 360f) - 180f);
            if (delta > 90f && delta < 270f)
            {
                danger = true;
                break;
            }
        }

        if (danger)
        {
            s.BackstabCd = 8f;
            try { p.PrintToChat($" \x02⚠ [Дух] Опасность за спиной!"); } catch { }
        }
        else
        {
            s.BackstabCd = 5f;
        }
    }

    private void TryAmbientAura(CCSPlayerController p, V2State s)
    {
        s.AuraCd = 4f;
        if (p.PlayerPawn?.Value?.AbsOrigin == null) return;
        var myPos = p.PlayerPawn.Value.AbsOrigin;
        int healed = 0;

        foreach (var pl in Utilities.GetPlayers())
        {
            if (pl == null || !pl.IsValid || pl.IsBot || !pl.PawnIsAlive) continue;
            if (pl.Slot == p.Slot) continue;
            if (pl.TeamNum != p.TeamNum) continue;

            var theirPawn = pl.PlayerPawn?.Value;
            if (theirPawn?.AbsOrigin == null || theirPawn.Health <= 0) continue;
            float dist = (theirPawn.AbsOrigin - myPos).Length();
            if (dist > 200f) continue;

            int maxHp = theirPawn.MaxHealth;
            int hp = theirPawn.Health;
            if (hp >= maxHp) continue;

            int heal = 3;
            int newHp = Math.Min(maxHp, hp + heal);
            theirPawn.Health = newHp;
            Utilities.SetStateChanged(theirPawn, "CBaseEntity", "m_iHealth");
            healed++;
        }

        if (healed > 0)
        {
            try { p.PrintToChat($" \x04✦ [Дух] Аура: +3 HP × {healed} союзник{(_ruSuffix(healed))}"); } catch { }
        }
    }

    private void TryUltimateAssist(CCSPlayerController p, V2State s)
    {
        s.AssistCd = 20f;
        s.UltAssistReady = true;
        s.UltBuffTimer = 12f;
        try { p.PrintToChat($" \x0B✦ [Дух] Ультимейт-ассист: x1.5 к следующей ульте (12с)"); } catch { }
    }

    // ── SPAWN ──

    private void SpawnCompanion(CCSPlayerController p)
    {
        var s = GetOrInit(p);
        DetachVisuals(s);

        var cfgKey = TierConfigKey(s.Stage);
        var cfg = TierOrDefault(_tierCfg, cfgKey);
        var model = ResolveModel(cfg.Model);
        var pawn = p.PlayerPawn?.Value;
        if (pawn?.AbsOrigin == null) return;

        var prop = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
        if (prop == null) return;
        prop.SetModel(model);
        var o = pawn.AbsOrigin;
        prop.Teleport(new Vector(o.X, o.Y, o.Z + HoverZ), new QAngle(0, 0, 0), new Vector(0, 0, 0));
        prop.DispatchSpawn();
        try { prop.AcceptInput("SetParent", pawn, null, "!activator"); } catch (Exception ex) { Console.WriteLine($"[Wisp] SetParent err: {ex.Message}"); }

        var col = ParseColor(cfg.Color);
        try
        {
            prop.Render = Color.FromArgb(255, col.R, col.G, col.B);
            Utilities.SetStateChanged(prop, "CBaseModelEntity", "m_clrRender");
        }
        catch (Exception ex) { Console.WriteLine($"[Wisp] render tint err: {ex.Message}"); }

        s.Prop = prop;

        if (!string.IsNullOrEmpty(cfg.Aura))
        {
            var fx = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
            if (fx != null)
            {
                fx.EffectName = cfg.Aura;
                fx.StartActive = true;
                fx.Teleport(new Vector(o.X, o.Y, o.Z + HoverZ), new QAngle(0, 0, 0), new Vector(0, 0, 0));
                fx.DispatchSpawn();
                try { fx.AcceptInput("SetParent", pawn, null, "!activator"); } catch (Exception ex) { Console.WriteLine($"[Wisp] aura parent err: {ex.Message}"); }
                try { fx.AcceptInput("Start"); } catch (Exception ex) { Console.WriteLine($"[Wisp] aura start err: {ex.Message}"); }
                s.AuraFx = fx;
            }
        }
    }

    private string ResolveModel(string cfgModel)
    {
        if (!string.IsNullOrEmpty(cfgModel) && cfgModel.EndsWith(".vmdl", StringComparison.OrdinalIgnoreCase))
            return cfgModel;
        return FallbackModel;
    }

    private void ReparentIfNeeded(V2State s, CCSPlayerPawn pawn)
    {
        // Spawn events recreate the visuals for a new pawn. While alive, only
        // clear stale entity references; teleporting below keeps valid visuals following.
        if (s.Prop != null && !s.Prop.IsValid) s.Prop = null;
        if (s.AuraFx != null && !s.AuraFx.IsValid) s.AuraFx = null;
    }

    private void DetachVisuals(V2State s)
    {
        if (s.Prop != null && s.Prop.IsValid) try { s.Prop.Remove(); } catch (Exception ex) { Console.WriteLine($"[Wisp] prop remove err: {ex.Message}"); }
        if (s.AuraFx != null && s.AuraFx.IsValid) try { s.AuraFx.Remove(); } catch (Exception ex) { Console.WriteLine($"[Wisp] aura remove err: {ex.Message}"); }
        s.Prop = null;
        s.AuraFx = null;
    }

    // ── NOTIFICATIONS ──

    public void NotifyKill(CCSPlayerController p, int oldBond)
    {
        if (p == null || !p.IsValid || p.IsBot) return;
        var s = GetOrInit(p);
        var data = _plugin.Data(p.SteamID);
        var rp = data.GetRace(data.CurrentRaceId);
        var evolved = WispEvolution.CheckEvolve(oldBond, rp.WispBond);
        if (evolved != null)
        {
            s.Stage = evolved.Stage;
            s.SkinTier = evolved.Title;
            s.AbilityInterval = evolved.Stage switch { 5 => 1.5f, 4 => 1.8f, 3 => 2.0f, _ => 2.5f };
            SpawnCompanion(p);
            try { OnEvolve?.Invoke(p, evolved.Title, evolved.Stage); } catch (Exception ex) { Console.WriteLine($"[Wisp] OnEvolve err: {ex.Message}"); }
        }
        else if (s.Prop == null || !s.Prop.IsValid)
        {
            SpawnCompanion(p);
        }
    }

    public void EnsureSpawned(CCSPlayerController p)
    {
        if (p == null || !p.IsValid || p.IsBot) return;
        var s = GetOrInit(p);
        if (s.Prop == null || !s.Prop.IsValid)
            SpawnCompanion(p);
    }

    public void NotifyDeath(CCSPlayerController p)
    {
        if (p == null || !p.IsValid) return;
        if (_bySlot.TryGetValue(p.Slot, out var s)) DetachVisuals(s);
    }

    private V2State GetOrInit(CCSPlayerController p)
    {
        if (!p.IsValid || p.IsBot) return null!;
        if (_bySlot.TryGetValue(p.Slot, out var s)) return s;
        var data = _plugin.Data(p.SteamID);
        var rp = data.GetRace(data.CurrentRaceId);
        var stage = WispEvolution.StageFor(rp.WispBond);
        var n = new V2State
        {
            OwnerSteam = p.SteamID,
            SkinTier = stage.Title,
            Stage = stage.Stage,
            AbilityInterval = stage.Stage switch { 5 => 1.5f, 4 => 1.8f, 3 => 2.0f, _ => 2.5f }
        };
        _bySlot[p.Slot] = n;
        return n;
    }

    public void CleanupSlot(int slot) => Cleanup(slot);

    private void Cleanup(int slot)
    {
        if (_bySlot.TryGetValue(slot, out var s)) DetachVisuals(s);
        _bySlot.Remove(slot);
    }

    private static string _ruSuffix(int n)
    {
        int abs = Math.Abs(n) % 100;
        int last = abs % 10;
        if (abs > 10 && abs < 20) return "ов";
        if (last > 1 && last < 5) return "а";
        if (last == 1) return "";
        return "ов";
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            var h = hex.TrimStart('#');
            if (h.Length == 6) return Color.FromArgb(
                byte.Parse(h.Substring(0, 2), System.Globalization.NumberStyles.HexNumber),
                byte.Parse(h.Substring(2, 2), System.Globalization.NumberStyles.HexNumber),
                byte.Parse(h.Substring(4, 2), System.Globalization.NumberStyles.HexNumber));
        }
        catch (Exception) { }
        return Color.FromArgb(143, 211, 255);
    }
}
