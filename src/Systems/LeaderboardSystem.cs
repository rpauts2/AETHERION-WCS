using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════╗
// ║  LEADERBOARD — рейтинги игроков: убийства, уровень, золото.     ║
// ║  !lb kills  — топ по убийствам                                 ║
// ║  !lb level  — топ по уровню                                    ║
// ║  !lb gold   — топ по золоту                                    ║
// ║  !lb wisp   — топ по связи с духом                             ║
// ╚══════════════════════════════════════════════════════════════════╝

public class LeaderboardSystem
{
    private readonly Database.IPlayerStore? _store;
    private readonly Func<ulong, Models.PlayerData?> _loadFn;

    public LeaderboardSystem(Func<ulong, Models.PlayerData?> loadFn, Database.IPlayerStore? store)
    {
        _loadFn = loadFn;
        _store = store;
    }

    public void ShowLeaderboard(CCSPlayerController p, string category)
    {
        if (_store == null) { p.PrintToChat(" \x07Лидерборд недоступен."); return; }

        string sortField = category switch
        {
            "gold" => "gold",
            "level" => "level",
            "kills" => "kills",
            _ => "level"
        };

        var top = _store.TopPlayers(15, sortField);
        p.PrintToChat(" \x0B═══ ЛИДЕРБОРД ═══");

        ShowGenericLeaderboard(p, top, category);
    }

    private void ShowGenericLeaderboard(CCSPlayerController p,
        List<(ulong SteamId, string Name, long TotalXp, int TopLevel, long Gold, long Kills)> top,
        string category)
    {
        List<(string Name, long Value)> sorted = category switch
        {
            "gold" => top.Select(t => (t.Name, t.Gold)).ToList(),
            "level" => top.Select(t => (t.Name, (long)t.TopLevel)).ToList(),
            "kills" => top.Select(t => (t.Name, t.Kills)).ToList(),
            _ => top.Select(t => (t.Name, t.TotalXp)).ToList(),
        };

        sorted = sorted.OrderByDescending(x => x.Value).Take(10).ToList();

        string title = category switch
        {
            "gold" => "ЗОЛОТО",
            "level" => "УРОВНИ",
            "kills" => "УБИЙСТВА",
            _ => "ОПЫТ"
        };

        int rank = 1;
        foreach (var (name, value) in sorted)
        {
            string medal = rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"{rank}." };
            string val = category switch
            {
                "gold" => $"{value}з",
                "kills" => $"{value} kills",
                _ => $"{value}"
            };
            p.PrintToChat($" {medal} {name} — {val}");
            rank++;
        }
    }

    public int GetPlayerRank(ulong steamId, string category)
    {
        if (_store == null) return -1;
        string sortField = category switch
        {
            "gold" => "gold",
            _ => "level"
        };
        var top = _store.TopPlayers(100, sortField);
        for (int i = 0; i < top.Count; i++)
        {
            if (top[i].SteamId == steamId) return i + 1;
        }
        return -1;
    }

    public string GetPlayerRankDisplay(ulong steamId, string category)
    {
        int rank = GetPlayerRank(steamId, category);
        if (rank <= 0) return "";
        string medal = rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"#{rank}" };
        return $"[{medal} {category}]";
    }
}
