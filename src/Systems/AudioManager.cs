using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WcsInfinity.Systems;

// Звуки ультимейтов, способностей, боссов, окружения и UI.
// Карта раса->soundevent в configs/assets/sounds.json.
// sound — рабочий встроенный soundevent CS2; customSound — слот под кастомный звук (CC0/свой).
public class AudioManager
{
    public class Entry
    {
        public string race { get; set; } = string.Empty;
        public string ult { get; set; } = string.Empty;
        public string sound { get; set; } = string.Empty;
        public string customSound { get; set; } = string.Empty;
        public string? levelUp { get; set; }
        public string? castActive { get; set; }
        public string? castUltimate { get; set; }
        public string? deathEnemy { get; set; }
        public string? ability_hook { get; set; }
        public string? ability_dump { get; set; }
        public string? ability_blind { get; set; }
        public string? ability_backstab { get; set; }
        public string? ability_scan { get; set; }
        public string? ability_blink { get; set; }
        public string? ability_summon { get; set; }
        public string? ability_cleanse { get; set; }
        public string? ability_freeze { get; set; }
        public string? ability_icebolt { get; set; }
        public string? ability_laser { get; set; }
        public string? ability_hack { get; set; }
        public string? ability_fireball { get; set; }
        public string? ability_ash { get; set; }
        public string? ability_pull { get; set; }
        public string? ability_quake { get; set; }
        public string? ability_thunder { get; set; }
        public string? ability_disintegrate { get; set; }
        public string? ability_heal { get; set; }
        public string? ability_armor { get; set; }
        public string? ability_suppress { get; set; }
        public string? ability_chase { get; set; }
        public string? ability_trap { get; set; }
        public string? ability_remote { get; set; }
        public string? ability_phantom_arrow { get; set; }
        public string? ability_invisibility { get; set; }
        public string? ability_mark { get; set; }
        public string? ability_shield { get; set; }
        public string? ability_chill_step { get; set; }
        public string? ability_frost_blade { get; set; }
        public string? ability_rewind { get; set; }
        public string? ability_accel { get; set; }
        public string? ability_shadow_step { get; set; }
        public string? ability_darken { get; set; }
        public string? ability_parry { get; set; }
        public string? ability_riposte { get; set; }
        public string? ability_fortify { get; set; }
        public string? ability_turret { get; set; }
        public string? ability_hallucination { get; set; }
        public string? ability_mental_spike { get; set; }
        public string? ability_impulse { get; set; }
        public string? ability_vortex { get; set; }
        public string? ability_frenzy { get; set; }
        public string? ability_blood_dark { get; set; }
        public string? ability_verdict { get; set; }
        public string? ability_judgment { get; set; }
        public string? spawn { get; set; }
        public string? event_category { get; set; }
        public string? description { get; set; }
    }

    private readonly Dictionary<int, Entry> _races = new();
    private readonly Dictionary<int, Entry> _system = new();

    public void Load(string configDir)
    {
        var path = Path.Combine(configDir, "assets", "sounds.json");
        if (!File.Exists(path))
        {
            Console.WriteLine("[AETHERION] sounds.json не найден");
            return;
        }

        var raw = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(path));
        if (raw == null) return;

        foreach (var kv in raw)
        {
            if (!int.TryParse(kv.Key, out var id)) continue;
            if (kv.Value.event_category != null)
                _system[id] = kv.Value;
            else
                _races[id] = kv.Value;
        }

        Console.WriteLine($"[AETHERION] AudioManager: загружено {_races.Count} рас + {_system.Count} системных звуков");
    }

    // ── Race events ──────────────────────────────────────────────────
    public void PlayUlt(CCSPlayerController player, int raceId)
    {
        if (player == null || !_races.TryGetValue(raceId, out var entry)) return;
        var sound = Resolve(entry.castUltimate, entry.sound, entry.customSound);
        Execute(player, sound);
    }

    public void PlayLevelUp(CCSPlayerController player, int raceId)
    {
        if (player == null) return;
        if (_races.TryGetValue(raceId, out var entry) && !string.IsNullOrEmpty(entry.levelUp))
        {
            Execute(player, entry.levelUp);
            return;
        }
        Execute(player, "UIPanorama.round_report_match_won");
    }

    public void PlayCastActive(CCSPlayerController player, int? raceId)
    {
        if (player == null) return;
        if (raceId.HasValue && _races.TryGetValue(raceId.Value, out var entry) && !string.IsNullOrEmpty(entry.castActive))
        {
            Execute(player, entry.castActive);
            return;
        }
        Execute(player, "weapons/hegrenade/beep.wav");
    }

    public void PlayCastUltimate(CCSPlayerController player, int raceId)
    {
        PlayUlt(player, raceId);
    }

    public void PlayDeathEnemy(CCSPlayerController player, int? raceId)
    {
        if (player == null) return;
        if (raceId.HasValue && _races.TryGetValue(raceId.Value, out var entry) && !string.IsNullOrEmpty(entry.deathEnemy))
        {
            Execute(player, entry.deathEnemy);
            return;
        }
        Execute(player, "player/death1.wav");
    }

    public void PlaySpawn(CCSPlayerController player, int raceId)
    {
        if (player == null) return;
        if (_races.TryGetValue(raceId, out var entry) && !string.IsNullOrEmpty(entry.spawn))
        {
            Execute(player, entry.spawn);
            return;
        }
        Execute(player, "player/spawn.wav");
    }

    public void PlayAbility(CCSPlayerController player, int raceId, string abilityKey)
    {
        if (player == null || !_races.TryGetValue(raceId, out var entry)) return;
        var sound = abilityKey switch
        {
            "ability_hook" => entry.ability_hook,
            "ability_dump" => entry.ability_dump,
            "ability_blind" => entry.ability_blind,
            "ability_backstab" => entry.ability_backstab,
            "ability_scan" => entry.ability_scan,
            "ability_blink" => entry.ability_blink,
            "ability_summon" => entry.ability_summon,
            "ability_cleanse" => entry.ability_cleanse,
            "ability_freeze" => entry.ability_freeze,
            "ability_icebolt" => entry.ability_icebolt,
            "ability_laser" => entry.ability_laser,
            "ability_hack" => entry.ability_hack,
            "ability_fireball" => entry.ability_fireball,
            "ability_ash" => entry.ability_ash,
            "ability_pull" => entry.ability_pull,
            "ability_quake" => entry.ability_quake,
            "ability_thunder" => entry.ability_thunder,
            "ability_disintegrate" => entry.ability_disintegrate,
            "ability_heal" => entry.ability_heal,
            "ability_armor" => entry.ability_armor,
            "ability_suppress" => entry.ability_suppress,
            "ability_chase" => entry.ability_chase,
            "ability_trap" => entry.ability_trap,
            "ability_remote" => entry.ability_remote,
            "ability_phantom_arrow" => entry.ability_phantom_arrow,
            "ability_invisibility" => entry.ability_invisibility,
            "ability_mark" => entry.ability_mark,
            "ability_shield" => entry.ability_shield,
            "ability_chill_step" => entry.ability_chill_step,
            "ability_frost_blade" => entry.ability_frost_blade,
            "ability_rewind" => entry.ability_rewind,
            "ability_accel" => entry.ability_accel,
            "ability_shadow_step" => entry.ability_shadow_step,
            "ability_darken" => entry.ability_darken,
            "ability_parry" => entry.ability_parry,
            "ability_riposte" => entry.ability_riposte,
            "ability_fortify" => entry.ability_fortify,
            "ability_turret" => entry.ability_turret,
            "ability_hallucination" => entry.ability_hallucination,
            "ability_mental_spike" => entry.ability_mental_spike,
            "ability_impulse" => entry.ability_impulse,
            "ability_vortex" => entry.ability_vortex,
            "ability_frenzy" => entry.ability_frenzy,
            "ability_blood_dark" => entry.ability_blood_dark,
            "ability_verdict" => entry.ability_verdict,
            "ability_judgment" => entry.ability_judgment,
            _ => null
        };
        if (!string.IsNullOrEmpty(sound))
            Execute(player, sound);
    }

    // ── Boss events ─────────────────────────────────────────────────
    public void PlayBossAwaken(CCSPlayerController? player)
    {
        if (player == null) return;
        PlaySystemForPlayer(player, 3000, "UIPanorama.round_report_match_won");
    }

    public void PlayBossEnrage(CCSPlayerController? player)
    {
        if (player == null) return;
        PlaySystemForPlayer(player, 3001, "UIPanorama.round_report_round_won");
    }

    public void PlayBossDeath(CCSPlayerController? player)
    {
        if (player == null) return;
        PlaySystemForPlayer(player, 3002, "player/kill.wav");
    }

    public void PlayBossHurt(CCSPlayerController? player)
    {
        if (player == null) return;
        PlaySystemForPlayer(player, 3003, "player/damage.wav");
    }

    public void PlayBossVoteStart(CCSPlayerController? player = null)
    {
        BroadcastSystem(3004, "ui.beacon");
    }

    public void PlayBossVotePass()
    {
        BroadcastSystem(3005, "ui.rankup");
    }

    public void PlayBossVoteFail()
    {
        BroadcastSystem(3006, "ui.deny");
    }

    // ── Ambient / map music hooks ────────────────────────────────────
    public void PlayAmbientMenu(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 4000);
    }

    public void PlayAmbientLobby(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 4001);
    }

    public void PlayAmbientStorm(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 4002);
    }

    public void PlayAmbientRift(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 4003);
    }

    public void PlayAmbientMap(CCSPlayerController player, string mapTheme)
    {
        int id = mapTheme.ToLowerInvariant() switch
        {
            "desert" => 4004,
            "arctic" => 4005,
            "cyber" => 4006,
            "void" => 4007,
            _ => 4000
        };
        PlaySystemForPlayer(player, id);
    }

    // ── UI soundscape ────────────────────────────────────────────────
    public void PlayUiClick(CCSPlayerController player)
    {
        if (player == null) return;
        Execute(player, "ui.button_click");
    }

    public void PlayUiHover(CCSPlayerController player)
    {
        if (player == null) return;
        Execute(player, "ui.button_hover");
    }

    public void PlayUiWheelOpen(CCSPlayerController player)
    {
        if (player == null) return;
        Execute(player, "ui.wheel_open");
    }

    public void PlayUiWheelClose(CCSPlayerController player)
    {
        if (player == null) return;
        Execute(player, "ui.wheel_close");
    }

    public void PlayUiAbilitySelect(CCSPlayerController player)
    {
        if (player == null) return;
        Execute(player, "ui.ability_select");
    }

    public void PlayUiSuccess(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 5005);
    }

    public void PlayUiError(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 5006);
    }

    public void PlayUiNotify(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 5007);
    }

    // ── Round-start and milestone audio ──────────────────────────────
    public void PlayRoundStart(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6000);
    }

    public void PlayRoundEndWin(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6001);
    }

    public void PlayRoundEndLose(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6002);
    }

    public void PlayFirstBlood(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6003);
    }

    public void PlayBombPlant(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6004);
    }

    public void PlayBombDefuse(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6005);
    }

    public void PlayMilestoneLevel(CCSPlayerController player, int level)
    {
        int id = level switch
        {
            >= 50 => 6008,
            >= 25 => 6007,
            >= 10 => 6006,
            _ => 6006
        };
        PlaySystemForPlayer(player, id);
    }

    public void PlayMilestoneDivisionUp(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6009);
    }

    public void PlayMilestoneAce(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6010);
    }

    public void PlayMilestoneMvp(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6011);
    }

    public void PlayDuelStart(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6013);
    }

    public void PlayDuelWin(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6012);
    }

    public void PlayGuildWarStart(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6014);
    }

    public void PlayStormStart(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6015);
    }

    public void PlayStormX2xp(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6016);
    }

    public void PlayStormX3xp(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6017);
    }

    public void PlayRiftOpen(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6018);
    }

    public void PlayRiftCollect(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6019);
    }

    public void PlayRiftDetonate(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6020);
    }

    public void PlayGoldGain(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6021);
    }

    public void PlayGoldSpend(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6022);
    }

    public void PlayAbilityMiss(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6023);
    }

    public void PlayAbilityHit(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6024);
    }

    public void PlayLowMana(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6025);
    }

    public void PlayAbilityCooldown(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6026);
    }

    public void PlayDailyLogin(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6027);
    }

    public void PlayDailyReward(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6028);
    }

    public void PlayWispBond(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6029);
    }

    public void PlayWispEvolve(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6030);
    }

    public void PlayGuildJoin(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6031);
    }

    public void PlayGuildBanner(CCSPlayerController player)
    {
        PlaySystemForPlayer(player, 6033);
    }

    public string? GetSound(int raceId) =>
        _races.TryGetValue(raceId, out var entry)
            ? (string.IsNullOrEmpty(entry.customSound) ? entry.sound : entry.customSound)
            : null;

    // ── Helpers ──────────────────────────────────────────────────────
    private static string? Resolve(string? primary, string? fallback, string? custom)
    {
        if (!string.IsNullOrEmpty(primary)) return primary;
        if (!string.IsNullOrEmpty(fallback)) return fallback;
        if (!string.IsNullOrEmpty(custom)) return custom;
        return null;
    }

    // Remap custom sound paths loaded from sounds.json to built-in CS2 sounds
    private static readonly Dictionary<string, string> SoundRemap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aether/resonance_blade"] = "weapons/smg_ssg08/fire1.wav",
        ["aether/apocalypse"] = "weapons/rifle_ak47/fire1.wav",
        ["race/pudge_pull"] = "weapons/shotgun_xm1014/fire1.wav",
        ["boss/boss_awaken"] = "UIPanorama.round_report_match_won",
        ["boss/boss_death"] = "player/death1.wav",
        ["ambient/menu_loop"] = "ui.button_click",
        ["ambient/storm_loop"] = "ui.button_hover",
    };

    private static void Execute(CCSPlayerController player, string? sound)
    {
        if (player == null || string.IsNullOrEmpty(sound)) return;
        string resolved = SoundRemap.TryGetValue(sound, out var remap) ? remap : sound;
        try { player.ExecuteClientCommand($"play {resolved}"); }
        catch (Exception ex)
        {
            Console.WriteLine($"[AETHERION] sound fail: {ex.Message}");
        }
    }

    private void PlaySystemForPlayer(CCSPlayerController? player, int id, string fallback = "")
    {
        if (player == null) return;
        if (_system.TryGetValue(id, out var entry))
        {
            var sound = Resolve(entry.sound, fallback, entry.customSound);
            Execute(player, sound);
        }
        else if (!string.IsNullOrEmpty(fallback))
        {
            Execute(player, fallback);
        }
    }

    private static void BroadcastSystem(int id, string fallback = "")
    {
        foreach (var p in Utilities.GetPlayers())
        {
            if (p == null || !p.IsValid || p.IsBot) continue;
            Execute(p, fallback);
        }
    }
}
