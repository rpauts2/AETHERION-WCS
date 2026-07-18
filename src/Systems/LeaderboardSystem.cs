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

        var top = _store.TopPlayers(15);
        p.PrintToChat(" \x0B═══ ЛИДЕРБОРД ═══");

        switch (category)
        {
            case "kills":
            case "level":
            case "gold":
            case "wisp":
                ShowGenericLeaderboard(p, top, category);
                break;
            default:
                ShowGenericLeaderboard(p, top, "kills");
                break;
        }
    }

    private void ShowGenericLeaderboard(CCSPlayerController p,
        List<(ulong SteamId, string Name, long TotalXp, int TopLevel, long Gold)> top,
        string category)
    {
        List<(string Name, long Value)> sorted = category switch
        {
            "gold" => top.Select(t => (t.Name, t.Gold)).ToList(),
            "level" => top.Select(t => (t.Name, (long)t.TopLevel)).ToList(),
            _ => top.Select(t => (t.Name, t.TotalXp)).ToList(),
        };

        sorted = sorted.OrderByDescending(x => x.Value).Take(10).ToList();

        string title = category switch
        {
            "gold" => "ЗОЛОТО",
            "level" => "УРОВНИ",
            _ => "ОПЫТ"
        };

        int rank = 1;
        foreach (var (name, value) in sorted)
        {
            string medal = rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"{rank}." };
            string val = category switch
            {
                "gold" => $"{value}з",
                _ => $"{value}"
            };
            p.PrintToChat($" {medal} {name} — {val}");
            rank++;
        }
    }
}
