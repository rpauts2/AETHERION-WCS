using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WcsInfinity.Models;
using WcsInfinity.Races;

namespace WcsInfinity.Systems;

public sealed class BossMapDefinition
{
    public string Map { get; set; } = "";
    public string WorkshopId { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
}

public sealed class BossMapSystem
{
    private sealed class State
    {
        public long LastVoteUnix { get; set; }
        public bool Active { get; set; }
        public string Map { get; set; } = "";
    }

    private readonly BasePlugin _plugin;
    private readonly RaceManager _races;
    private readonly Func<ulong, PlayerData> _data;
    private readonly Action<PlayerData> _save;
    private readonly Random _random = new();
    private readonly Dictionary<ulong, bool> _votes = new();
    private readonly List<BossMapDefinition> _maps = new();
    private readonly List<int> _bossRaceIds = new();
    private readonly string _statePath;
    private State _state = new();
    private bool _voteActive;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _voteTimer;

    private const int VoteSeconds = 20;
    private const long VoteCooldownSeconds = 4 * 60 * 60;

    public bool IsBossMode => _state.Active;
    public string CurrentMap => _state.Map;
    public IReadOnlyList<int> BossRaceIds => _bossRaceIds;

    public BossMapSystem(BasePlugin plugin, RaceManager races, Func<ulong, PlayerData> data, Action<PlayerData> save)
    {
        _plugin = plugin;
        _races = races;
        _data = data;
        _save = save;
        _statePath = Path.Combine(plugin.ModuleDirectory, "boss_map_state.json");
        LoadConfig();
        LoadState();
    }

    private void LoadConfig()
    {
        try
        {
            var path = Path.Combine(_plugin.ModuleDirectory, "..", "..", "configs", "boss_maps.json");
            if (File.Exists(path))
            {
                var cfg = JsonSerializer.Deserialize<List<BossMapDefinition>>(File.ReadAllText(path)) ?? new();
                _maps.AddRange(cfg.Where(m => m.Enabled && !string.IsNullOrWhiteSpace(m.Map)));
            }
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] boss map config err: {ex.Message}"); }

        try
        {
            var path = Path.Combine(_plugin.ModuleDirectory, "..", "..", "configs", "races", "boss_races.json");
            if (File.Exists(path))
            {
                var defs = JsonSerializer.Deserialize<List<RaceDefinition>>(File.ReadAllText(path)) ?? new();
                foreach (var race in defs)
                {
                    race.IndexAbilities();
                    _races.AddOrReplace(race);
                    _bossRaceIds.Add(race.Id);
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] boss race config err: {ex.Message}"); }
    }

    private void LoadState()
    {
        try
        {
            if (File.Exists(_statePath))
                _state = JsonSerializer.Deserialize<State>(File.ReadAllText(_statePath)) ?? new();
            if (_state.Active && !string.Equals(Server.MapName, _state.Map, StringComparison.OrdinalIgnoreCase))
            {
                _state.Active = false;
                _state.Map = "";
                SaveState();
            }
        }
        catch { _state = new(); }
    }

    private void SaveState()
    {
        try { File.WriteAllText(_statePath, JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] boss map state err: {ex.Message}"); }
    }

    public void OnPluginReady(Action startBoss)
    {
        if (!IsBossMode || !string.Equals(Server.MapName, _state.Map, StringComparison.OrdinalIgnoreCase)) return;
        _plugin.AddTimer(5f, startBoss);
    }

    public void StartVote(CCSPlayerController? initiator)
    {
        if (initiator == null || !initiator.IsValid || initiator.IsBot) return;
        if (_maps.Count == 0) { initiator.PrintToChat(" \x07Нет включённых boss-карт в configs/boss_maps.json."); return; }
        if (IsBossMode) { initiator.PrintToChat(" \x07Boss-карта уже активна."); return; }
        if (_voteActive) { initiator.PrintToChat(" \x07Голосование уже идёт."); return; }
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long left = _state.LastVoteUnix + VoteCooldownSeconds - now;
        if (left > 0) { initiator.PrintToChat($" \x07Следующее голосование через {left / 3600}ч {(left % 3600) / 60}м."); return; }

        _voteActive = true;
        _votes.Clear();
        Server.PrintToChatAll($" \x0B[AETHERION]\x01 {initiator.PlayerName} запустил голосование за BOSS MAP!");
        Server.PrintToChatAll($" \x04!yes / !no\x01 — нужно большинство, окно {VoteSeconds} секунд.");
        _voteTimer?.Kill();
        _voteTimer = _plugin.AddTimer(VoteSeconds, ConcludeVote);
    }

    public void VoteYes(CCSPlayerController? p) { if (_voteActive && p != null && p.IsValid && !p.IsBot) _votes[p.SteamID] = true; }
    public void VoteNo(CCSPlayerController? p) { if (_voteActive && p != null && p.IsValid && !p.IsBot) _votes[p.SteamID] = false; }

    private void ConcludeVote()
    {
        _voteActive = false;
        int yes = _votes.Values.Count(v => v);
        int no = _votes.Values.Count(v => !v);
        _votes.Clear();
        if (yes <= no || yes < 2)
        {
            Server.PrintToChatAll(" \x07[AETHERION]\x01 Boss map отклонена.");
            return;
        }
        var map = _maps[_random.Next(_maps.Count)];
        _state = new State { LastVoteUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Active = true, Map = map.Map };
        SaveState();
        foreach (var p in Utilities.GetPlayers())
            if (p != null && p.IsValid && !p.IsBot) EnterBossMode(p);
        Server.PrintToChatAll($" \x06[AETHERION]\x01 Boss map выбрана: {map.Name} ({map.Map}). Загружаем...");
        if (!string.IsNullOrWhiteSpace(map.WorkshopId))
            Server.ExecuteCommand($"host_workshop_map {map.WorkshopId}");
        else
            Server.ExecuteCommand($"changelevel {map.Map}");
    }

    public void ApplyPlayerMode(CCSPlayerController p)
    {
        if (p == null || !p.IsValid || p.IsBot) return;
        if (IsBossMode && string.Equals(Server.MapName, _state.Map, StringComparison.OrdinalIgnoreCase)) EnterBossMode(p);
        else LeaveBossMode(p);
    }

    private void EnterBossMode(CCSPlayerController p)
    {
        var d = _data(p.SteamID);
        if (!d.BossRaceActive)
        {
            d.BossSavedRaceId = d.CurrentRaceId;
            d.BossRaceActive = true;
            d.CurrentRaceId = _bossRaceIds.Count > 0 ? _bossRaceIds[_random.Next(_bossRaceIds.Count)] : d.CurrentRaceId;
            d.BossActiveRaceId = d.CurrentRaceId;
            var bossProgress = d.GetRace(d.CurrentRaceId);
            var bossDef = _races.Get(d.CurrentRaceId);
            if (bossDef != null)
            {
                bossProgress.Level = Math.Max(1, bossDef.Abilities.Sum(a => a.MaxLevel));
                bossProgress.UnspentPoints = 0;
                foreach (var ability in bossDef.Abilities)
                    bossProgress.SkillLevels[ability.Index] = ability.MaxLevel;
            }
            _save(d);
        }
    }

    private void LeaveBossMode(CCSPlayerController p)
    {
        var d = _data(p.SteamID);
        if (!d.BossRaceActive) return;
        if (d.BossActiveRaceId > 0) d.Races.Remove(d.BossActiveRaceId);
        if (_races.Get(d.BossSavedRaceId) != null) d.CurrentRaceId = d.BossSavedRaceId;
        d.BossSavedRaceId = 0;
        d.BossActiveRaceId = 0;
        d.BossRaceActive = false;
        _save(d);
    }

    public bool SelectBossRace(PlayerData d, int raceId)
    {
        if (!IsBossMode || !_bossRaceIds.Contains(raceId)) return false;
        if (d.BossActiveRaceId == raceId) return true;
        if (d.BossActiveRaceId > 0) d.Races.Remove(d.BossActiveRaceId);
        d.CurrentRaceId = raceId;
        d.BossActiveRaceId = raceId;
        var bossProgress = d.GetRace(raceId);
        var bossDef = _races.Get(raceId);
        if (bossDef != null)
        {
            bossProgress.Level = Math.Max(1, bossDef.Abilities.Sum(a => a.MaxLevel));
            bossProgress.SkillLevels.Clear();
            foreach (var ability in bossDef.Abilities)
                bossProgress.SkillLevels[ability.Index] = ability.MaxLevel;
        }
        d.IsDirty = true;
        return true;
    }

    public void CompleteBossMap()
    {
        if (!IsBossMode) return;
        _state.Active = false;
        _state.Map = "";
        SaveState();
        foreach (var p in Utilities.GetPlayers())
            if (p != null && p.IsValid && !p.IsBot) LeaveBossMode(p);
        Server.PrintToChatAll(" \x06[AETHERION]\x01 Boss map завершена. Возвращаем обычную ротацию карт...");
        Server.ExecuteCommand("changelevel_next");
    }
}
