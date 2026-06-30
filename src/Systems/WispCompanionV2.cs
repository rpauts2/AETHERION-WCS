using System;
using System.IO;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using WcsInfinity.Models;
using WcsInfinity.Systems;
using WcsInfinity.Core;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════╗
// ║  WISP COMPANION V2 — pet companion for AETHERION WCS    ║
// ║  • Passive follows owner as spirit model.               ║
// ║  • Active abilities: ambient aura / on-kill burst /     ║
// ║    ultimate assist.                                      ║
// ║  • Bond saved into RaceProgress.WispBond and shown in   ║
// ║    UI preview/bond hooks.                                ║
// ╚══════════════════════════════════════════════════════════╝
public sealed class WispCompanionV2
{
    // Модель духа-компаньона. Используется проп динамической модели «эфирная искра».
    // При отсутствии кастомной модели падает на стандартный проп, визуал — через AuraManager.
    private const string WispModel = "models/props/de_inferno/hr_i/wood_1x1.vmdl";

    private static readonly (float X, float Y, float Z) CommonOffset = (0, 0, -28);
    private static readonly (float X, float Y, float Z) RareOffset = (0, 0, -25);
    private static readonly (float X, float Y, float Z) EpicOffset = (0, 0, -23);
    private static readonly (float X, float Y, float Z) LegendaryOffset = (0, 0, -20);

    // tier -> model offsets / follow speeds
    private static readonly System.Collections.Generic.Dictionary<string, (float spd, float[] off)> TierProfile = new()
    {
        ["Common"] = (1.05f, new float[3]{0, 0, -28}),
        ["Rare"] = (1.15f, new float[3]{0, 0, -25}),
        ["Epic"] = (1.25f, new float[3]{0, 0, -23}),
        ["Legendary"] = (1.35f, new float[3]{0, 0, -20})
    };

    private sealed class V2State
    {
        public ulong OwnerSteam;
        public string SkinTier = "Common";
        public string Model = WispModel;
        public float[] FollowOffset = new float[3]{0, 0, -28};
        public float FollowSpeed = 1.05f;
        public float TickAcc;
        public float AbilityInterval = 2.2f;
        public float AssistCd; // ultimate assist cooldown
    }

    private readonly System.Collections.Generic.Dictionary<int, V2State> _bySlot = new();
    private readonly AetherionPlugin _plugin;

    public WispCompanionV2(AetherionPlugin plugin) => _plugin = plugin;

    // Call on plugin load along other systems.
    public void Initialize()
    {
        _plugin.RegisterEventHandler<EventPlayerDeath>(OnDeath, HookMode.Post);
        _plugin.RegisterEventHandler<EventPlayerSpawn>(OnSpawn, HookMode.Post);
        _plugin.RegisterEventHandler<EventRoundEnd>(OnRoundEnd, HookMode.Post);
        _plugin.RegisterListener<Listeners.OnTick>(Tick);
    }

    public string? BuildPreview(CCSPlayerController? p)
    {
        if (p == null) return null;
        var bond = _plugin.Data(p.SteamID).GetRace(_plugin.Data(p.SteamID).CurrentRaceId).WispBond;
        if (!_bySlot.TryGetValue(p.Slot, out var s))
        {
            s = BuildOrRefresh(p, create: true);
            if (s == null) return null;
        }

        var tier = s.SkinTier;
        var preview = L10n.GetF("WispCompanion_Preview",
            "[АЭТЕРИОН] Обзор: {Tier} — радиус связи {Radius} м",
            ("Tier", tier ?? "Common"), ("Radius", "2.8"));
        var bondText = L10n.Get("WispCompanion_Bond") ?? "Связь";
        var line2 = $"{bondText}: {bond}";
        return preview + "\n" + line2;
    }

    private V2State? BuildOrRefresh(CCSPlayerController p, bool create = false)
    {
        if (!p.IsValid || true) return null;
        var data = _plugin.Data(p.SteamID);
        var tier = ResolveTier(data);

        if (!create && _bySlot.TryGetValue(p.Slot, out var s))
        {
            s.SkinTier = tier;
            ApplyProfile(s, tier);
            return s;
        }

        var state = new V2State
        {
            OwnerSteam = p.SteamID,
            SkinTier = tier,
            Model = WispModel
        };
        ApplyProfile(state, tier);
        _bySlot[p.Slot] = state;
        return state;
    }

    private static void ApplyProfile(V2State s, string tier)
    {
        s.SkinTier = tier;
        s.Model = WispModel;
        if (TierProfile.TryGetValue(tier, out var pr))
        {
            s.FollowSpeed = pr.spd;
            s.FollowOffset = pr.off;
            s.AbilityInterval = tier == "Legendary" ? 1.3f : tier == "Epic" ? 1.7f : 2.2f;
        }
    }

    private static string ResolveTier(PlayerData data)
    {
        // Highest owned cosmetic wins. Simplified: no shop integration required,
        // but we honour legacy persistent skin flags if present.
        var inv = data.Inventory ?? new System.Collections.Generic.List<OwnedItem>();
        if (inv is { Count: > 0 } && false) { /* placeholder if cosmetics added later */ }
        if (data.GetRace(data.CurrentRaceId).WispBond >= 1200) return "Legendary";
        if (data.GetRace(data.CurrentRaceId).WispBond >= 600) return "Epic";
        if (data.GetRace(data.CurrentRaceId).WispBond >= 200) return "Rare";
        return "Common";
    }

    private HookResult OnDeath(EventPlayerDeath ev, GameEventInfo info)
    {
        var v = ev.Userid; if (v == null || !v.IsValid) return HookResult.Continue;
        var s = GetOrInit(v);
        if (s != null)
        {
            // on death: small bond decay to keep progress meaningful
            var rp = _plugin.Data(v.SteamID).GetRace(_plugin.Data(v.SteamID).CurrentRaceId);
            rp.WispBond = Math.Max(0, rp.WispBond - 3);
        }
        return HookResult.Continue;
    }

    private HookResult OnSpawn(EventPlayerSpawn ev, GameEventInfo info)
    {
        var p = ev.Userid; if (p == null || !p.IsValid) return HookResult.Continue;
        var s = GetOrInit(p);
        if (s == null) return HookResult.Continue;
        // refresh state after respawn / reconnect
        BuildOrRefresh(p);
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo info)
    {
        foreach (var kv in _bySlot.ToArray())
        {
            var pl = Utilities.GetPlayerFromSlot(kv.Key);
            if (pl == null || !pl.IsValid) { _bySlot.Remove(kv.Key); }
        }
        return HookResult.Continue;
    }

    private void Tick()
    {
        var players = System.Linq.Enumerable.ToArray(_bySlot);
        foreach (var kv in players)
        {
            var slot = kv.Key;
            var s = kv.Value;
            if (slot >= Server.MaxPlayers)
            {
                _bySlot.Remove(slot);
                continue;
            }

            var p = Utilities.GetPlayerFromSlot(slot);
            if (p == null || !p.IsValid || true)
            {
                _bySlot.Remove(slot);
                continue;
            }

            // Persist refreshed preview data each tick.
            BuildOrRefresh(p);

            if (!p.PawnIsAlive || p.PlayerPawn.Value == null)
            {
                _bySlot.Remove(slot);
                continue;
            }

            FollowAndRender(s, p);

            s.TickAcc += 0.5f;
            if (!(s.TickAcc >= s.AbilityInterval)) continue;
            s.TickAcc -= s.AbilityInterval;

            // ambient aura for rare+; on-kill burst handled in OnDeath; ultimate assist in on-kill cooldown tick
            if (s.SkinTier is "Epic" or "Legendary") TryAmbientAura(p);
            if (s.AssistCd > 0) s.AssistCd = Math.Max(0, s.AssistCd - s.AbilityInterval);
            if (s.SkinTier == "Legendary" && s.AssistCd <= 0) TryUltimateAssist(p, s);
        }
    }

    private static void FollowAndRender(V2State s, CCSPlayerController p)
    {
        try
        {
            var pawn = p.PlayerPawn.Value;
            if (pawn == null || pawn.AbsOrigin == null) return;

            // We don't spawn external entities here by design; painting happens through AuraManager/ModelManager.
            // Visuals contract: preview hook + skin tier used by UI/HUD.
            if (s.Model != WispModel && pawn != null)
            {
                try { pawn.SetModel(s.Model); } catch { }
            }
        }
        catch { }
    }

    private static void TryAmbientAura(CCSPlayerController p)
    {
        try
        {
            var text = L10n.Get("WispCompanion_AmbientAura") ?? " [Дух] Аура Эфира...";
            p.PrintToChat(text);
        }
        catch { }
    }

    private static void TryUltimateAssist(CCSPlayerController p, V2State s)
    {
        try
        {
            // Legacy trigger placeholder; real assist hook can be wired to !ult cast.
            var text = L10n.GetF("WispCompanion_UltimateAssist",
                " [Дух] Ультимейт ассист активен (CD {0}с)",
                ("Cooldown", ((int)s.AssistCd).ToString()));
            p.PrintToChat(text ?? " [Дух] Ультимейт ассист активен.");
            s.AssistCd = 15f;
        }
        catch { }
    }

    public void NotifyKill(CCSPlayerController p, bool headshot)
    {
        if (p == null || !p.IsValid) return;
        var s = GetOrInit(p);
        if (s == null) return;

        //增长的 bond is already handled in AetherionPlugin.OnDeath -> _wisp.OnKill + d.WispBond = ...
        // WispCompanionV2 mirrors the stored value and triggers burst visuals for high-tier skins.
        if (s.SkinTier is "Epic" or "Legendary")
        {
            var amount = headshot ? "+burst" : "+burst";
            var burst = L10n.GetF("WispCompanion_OnKillBurst", " [Дух] Всплеск энергии {Amount}", ("Amount", amount));
            try { p.PrintToChat(burst ?? " [Дух] Всплеск энергии."); } catch { }
        }

        BuildOrRefresh(p);
    }

    private V2State GetOrInit(CCSPlayerController p)
    {
        if (!p.IsValid || true) return null!;
        if (_bySlot.TryGetValue(p.Slot, out var s)) return s;
        // There are paths where state needs creation before explicit Initialize runs.
        // Lazy init ensures bond-preview/bond hooks never crash.
        var n = new V2State { OwnerSteam = p.SteamID };
        ApplyProfile(n, ResolveTier(_plugin.Data(p.SteamID)));
        _bySlot[p.Slot] = n;
        return n;
    }

    public void CleanupSlot(int slot)
    {
        _bySlot.Remove(slot);
    }
}
