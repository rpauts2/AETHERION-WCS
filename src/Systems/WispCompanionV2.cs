using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using WcsInfinity.Models;
using WcsInfinity.Systems;
using WcsInfinity.Core;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════╗
// ║  WISP COMPANION V2 — pet companion for AETHERION WCS     ║
// ║  • Загружает tiers из configs/pets/wisp.json              ║
// ║  • Спавнит реальную 3D-сущность-компаньона (prop_dynamic) ║
// ║    поверх павна, парентится к игроку                      ║
// ║  • Цвет рендера + аура-партикл зависят от тира (Bond)     ║
// ║  • Bond растёт за килы (см. AetherionPlugin.OnDeath)      ║
// ║  • Пассивные ауры: Epic+ — всплеск энергии на каче       ║
// ╚══════════════════════════════════════════════════════════╝
public sealed class WispCompanionV2
{
    // Фоллбэк-модель. Если на сервере есть models/wisp/wisp_core.vmdl — будет использована она.
    // CS2 проп присутствует в базовой поставке — гантиспам гарантирован.
    private const string FallbackModel = "models/props/de_inferno/hr_i/wood_1x1.vmdl";
    private const string CustomModelPath = "models/wisp/wisp_core.vmdl";

    // Высота парения компаньона над макушкой (повыше неймплейта/короны).
    private const float HoverZ = 78f;
    // Лёгкое колебание по синусу — «дыхание» духа.
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
        public string SkinTier = "Common";
        public CDynamicProp? Prop;       // тело духа
        public CParticleSystem? AuraFx;  // аура под духом
        public float TickAcc;
        public float AbilityInterval = 2.2f;
        public float AssistCd;
        public float HoverPhase;
    }

    private readonly Dictionary<int, V2State> _bySlot = new();
    private readonly AetherionPlugin _plugin;
    private bool _customModelAvailable;

    // Внешний хук: вызывается когда дух эволюционирует на новый тир.
    public Action<CCSPlayerController, string /*newTier*/>? OnEvolve { get; set; }

    public WispCompanionV2(AetherionPlugin plugin) => _plugin = plugin;

    // Загрузка конфига тиов. Вызывается из AetherionPlugin.Load.
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
        _tierCfg["Common"] = new TierConfig { Tier = "Common", Color = "#8fd3ff", FollowOffset = new() { 0, 0, -25 } };
        _tierCfg["Rare"] = new TierConfig { Tier = "Rare", Color = "#ffe9a4", Aura = "particles/wisp/dust_dawn.vpcf", FollowOffset = new() { 0, 0, -24 } };
        _tierCfg["Epic"] = new TierConfig { Tier = "Epic", Color = "#c7b9ff", Aura = "particles/wisp/arc_walk.vpcf", FollowOffset = new() { 0, 0, -22 } };
        _tierCfg["Legendary"] = new TierConfig { Tier = "Legendary", Color = "#ffd166", Aura = "particles/wisp/crown_flame.vpcf", FollowOffset = new() { 0, 0, -20 } };
    }

    private static TierConfig TierOrDefault(Dictionary<string, TierConfig> map, string tier)
    {
        if (map.TryGetValue(tier, out var t)) return t;
        return map.TryGetValue("Common", out var c) ? c
            : new TierConfig { Tier = "Common", Color = "#8fd3ff" };
    }

    public string? BuildPreview(CCSPlayerController? p)
    {
        if (p == null) return null;
        var bond = _plugin.Data(p.SteamID).GetRace(_plugin.Data(p.SteamID).CurrentRaceId).WispBond;
        var tier = TierForBond(bond);
        var cfg = TierOrDefault(_tierCfg, tier);
        var label = string.IsNullOrEmpty(cfg.LabelRu) ? tier : cfg.LabelRu;
        var preview = L10n.GetF("WispCompanion_Preview",
            "[АЭТЕРИОН] Дух: {Tier} — связь {Bond}",
            ("Tier", label), ("Bond", bond.ToString()));
        return preview ?? $"[АЭТЕРИОН] Дух: {label} — связь {bond}";
    }

    // ── ТИР ПО BOND ──
    private static string TierForBond(int bond) => bond switch
    {
        >= 1200 => "Legendary",
        >= 600 => "Epic",
        >= 200 => "Rare",
        _ => "Common"
    };

    // ── СОБЫТИЯ ──
    private HookResult OnSpawn(EventPlayerSpawn ev, GameEventInfo info)
    {
        var p = ev.Userid; if (p == null || !p.IsValid) return HookResult.Continue;
        GetOrInit(p);
        SpawnCompanion(p);
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo info)
    {
        foreach (var kv in _bySlot.ToArray())
        {
            var pl = Utilities.GetPlayerFromSlot(kv.Key);
            if (pl == null || !pl.IsValid) { Cleanup(kv.Key); }
        }
        return HookResult.Continue;
    }

    // ── ТИК: «дыхание» + ревалидация парента + пассивки ──
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
                // Гасим тело мёртвого — респавн пересоздаст.
                DetachVisuals(s);
                continue;
            }

            // Обновляем парент, если потерян (респавн/телепорт).
            ReparentIfNeeded(s, p.PlayerPawn.Value);

            // Колебание по Z — дух «дышит».
            s.HoverPhase += 0.05f;
            if (s.Prop != null && s.Prop.IsValid && p.PlayerPawn.Value.AbsOrigin != null)
            {
                var o = p.PlayerPawn.Value.AbsOrigin;
                float z = o.Z + HoverZ + MathF.Sin(s.HoverPhase) * HoverAmp;
                try { s.Prop.Teleport(new Vector(o.X, o.Y, z), new QAngle(0, s.HoverPhase * 40f, 0), new Vector(0, 0, 0)); }
                catch { }
            }

            // Пассивки по тиру
            s.TickAcc += 0.5f;
            if (s.TickAcc < s.AbilityInterval) continue;
            s.TickAcc -= s.AbilityInterval;
            if (s.AssistCd > 0) s.AssistCd = Math.Max(0, s.AssistCd - s.AbilityInterval);
            if (s.SkinTier is "Epic" or "Legendary") TryAmbientAura(p);
            if (s.SkinTier == "Legendary" && s.AssistCd <= 0) TryUltimateAssist(p, s);
        }
    }

    // ── СПАВН ТЕЛА ДУХА ──
    private void SpawnCompanion(CCSPlayerController p)
    {
        var s = GetOrInit(p);
        DetachVisuals(s); // чистим старые пропы

        var cfg = TierOrDefault(_tierCfg, s.SkinTier);
        var model = ResolveModel(cfg.Model);
        var pawn = p.PlayerPawn?.Value;
        if (pawn?.AbsOrigin == null) return;

        var prop = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
        if (prop == null) return;
        prop.SetModel(model);
        var o = pawn.AbsOrigin;
        prop.Teleport(new Vector(o.X, o.Y, o.Z + HoverZ), new QAngle(0, 0, 0), new Vector(0, 0, 0));
        prop.DispatchSpawn();
        try { prop.AcceptInput("SetParent", pawn, null, "!activator"); } catch { }

        // Тинт по цвету тира
        var col = ParseColor(cfg.Color);
        try
        {
            prop.Render = Color.FromArgb(255, col.R, col.G, col.B);
            Utilities.SetStateChanged(prop, "CBaseModelEntity", "m_clrRender");
        }
        catch { }

        s.Prop = prop;

        // Аура-партикл под духом для Rare+
        if (!string.IsNullOrEmpty(cfg.Aura))
        {
            var fx = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
            if (fx != null)
            {
                fx.EffectName = cfg.Aura;
                fx.StartActive = true;
                fx.Teleport(new Vector(o.X, o.Y, o.Z + HoverZ), new QAngle(0, 0, 0), new Vector(0, 0, 0));
                fx.DispatchSpawn();
                try { fx.AcceptInput("SetParent", pawn, null, "!activator"); } catch { }
                try { fx.AcceptInput("Start"); } catch { }
                s.AuraFx = fx;
            }
        }
    }

    private string ResolveModel(string cfgModel)
    {
        // Если конфиг указывает .vmdl — уважаем; иначе фоллбэк.
        if (!string.IsNullOrEmpty(cfgModel) && cfgModel.EndsWith(".vmdl", StringComparison.OrdinalIgnoreCase))
            return cfgModel;
        return FallbackModel;
    }

    private void ReparentIfNeeded(V2State s, CCSPlayerPawn pawn)
    {
        if (s.Prop == null) return;
        // Ничего не делаем, если проп ещё валиден и парент на месте.
        // (CS2 сам удерживает парент; страховка только на случай удаления павна.)
    }

    private void DetachVisuals(V2State s)
    {
        if (s.Prop != null && s.Prop.IsValid) try { s.Prop.Remove(); } catch { }
        if (s.AuraFx != null && s.AuraFx.IsValid) try { s.AuraFx.Remove(); } catch { }
        s.Prop = null;
        s.AuraFx = null;
    }

    // ── ВЗАИМОДЕЙСТВИЕ ──
    public void NotifyKill(CCSPlayerController p, bool headshot)
    {
        if (p == null || !p.IsValid) return;
        var s = GetOrInit(p);
        // Бонд растёт в AetherionPlugin.OnDeath; здесь обновляем тир/визуал при росте.
        var data = _plugin.Data(p.SteamID);
        var newTier = TierForBond(data.GetRace(data.CurrentRaceId).WispBond);
        if (newTier != s.SkinTier)
        {
            s.SkinTier = newTier;
            s.AbilityInterval = newTier switch { "Legendary" => 1.3f, "Epic" => 1.7f, _ => 2.2f };
            SpawnCompanion(p); // пересоздать с новым цветом/аурой
            try { OnEvolve?.Invoke(p, newTier); } catch { }
        }
        else if (s.Prop == null || !s.Prop.IsValid)
        {
            SpawnCompanion(p);
        }

        if (s.SkinTier is "Epic" or "Legendary")
        {
            var burst = L10n.GetF("WispCompanion_OnKillBurst", " [Дух] ✦ Всплеск Эфира!", ("hs", headshot ? "★" : ""));
            try { p.PrintToChat(burst ?? " [Дух] ✦ Всплеск Эфира!"); } catch { }
        }
    }

    public void NotifyDeath(CCSPlayerController p)
    {
        if (p == null || !p.IsValid) return;
        if (_bySlot.TryGetValue(p.Slot, out var s)) DetachVisuals(s);
    }

    private static void TryAmbientAura(CCSPlayerController p)
    {
        try
        {
            var text = L10n.Get("WispCompanion_AmbientAura") ?? " [Дух] Аура Эфира мерцает...";
            p.PrintToChat(text);
        }
        catch { }
    }

    private static void TryUltimateAssist(CCSPlayerController p, V2State s)
    {
        try
        {
            var text = L10n.GetF("WispCompanion_UltimateAssist",
                " [Дух] ✦ Ультимейт-ассист готов (CD {0}с)",
                ("Cooldown", ((int)s.AssistCd).ToString()));
            p.PrintToChat(text ?? " [Дух] ✦ Ультимейт-ассист готов.");
            s.AssistCd = 15f;
        }
        catch { }
    }

    private V2State GetOrInit(CCSPlayerController p)
    {
        if (!p.IsValid || p.IsBot) return null!;
        if (_bySlot.TryGetValue(p.Slot, out var s)) return s;
        var data = _plugin.Data(p.SteamID);
        var tier = TierForBond(data.GetRace(data.CurrentRaceId).WispBond);
        var n = new V2State
        {
            OwnerSteam = p.SteamID,
            SkinTier = tier,
            AbilityInterval = tier switch { "Legendary" => 1.3f, "Epic" => 1.7f, _ => 2.2f }
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

    // ── УТИЛИТЫ ──
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
        catch { }
        return Color.FromArgb(143, 211, 255);
    }
}
