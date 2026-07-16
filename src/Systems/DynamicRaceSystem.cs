using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API.Core;
using WcsInfinity.Races;
using WcsInfinity.Systems;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════╗
// ║  DYNAMIC RACES — локальное изменение pool рас на основе         ║
// ║  текущих условий (погода, онлайны, сезон, победитель            ║
// ║  предыдущего турнира). Гибридный пул обновляется локально.      ║
// ╚══════════════════════════════════════════════════════════════════╝
public static class DynamicRaceSystem
{
    private static readonly HashSet<int> _locked = new();
    private static readonly HashSet<int> _featured = new();

    // Локальный фильтр доступных рас по дивизиону игрока
    public static IEnumerable<RaceDefinition> Filter(RaceManager rm, CCSPlayerController player, int division)
    {
        if (rm == null || player == null || !player.IsValid) return Array.Empty<RaceDefinition>();
        return rm.RacesForDivision(division).Where(r => !_locked.Contains(r.Id));
    }

    public static void Lock(int raceId) => _locked.Add(raceId);
    public static void Unlock(int raceId) => _locked.Remove(raceId);
    public static void SetFeatured(IEnumerable<int> ids) { _featured.Clear(); _featured.UnionWith(ids); }
    public static IEnumerable<int> Featured() => _featured;
    public static bool IsLocked(int raceId) => _locked.Contains(raceId);

    // Случайная D3+ раса (в духе D1/D2/D3 miracle lotteries)
    public static RaceDefinition? DailyDynamicRace(RaceManager rm, Random? rng = null)
    {
        rng ??= new Random();
        var pool = rm.AllD3Plus().Where(r => !_locked.Contains(r.Id)).ToList();
        return pool.Count == 0 ? null : pool[rng.Next(pool.Count)];
    }
}
