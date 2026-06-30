using System.Collections.Generic;
using System.Linq;
using WcsInfinity.Models;
using WcsInfinity.Races;

namespace WcsInfinity.Systems;

// Система открытия рас по ОБЩЕМУ уровню игрока.
// Общий уровень = сумма уровней ВСЕХ прокачанных рас (+ Paragon-уровни).
// Новые расы открываются по порогам, шаг которых растёт с дивизионом:
//   D1: каждые 25-30 ур.  D2: 30-35  D3: 40-50  D∞: 60+
public static class UnlockSystem
{
    // Шаг открытия внутри дивизиона (берём середину диапазона + лёгкий рост)
    public static int StepForDivision(int division) => division switch
    {
        1 => 20,
        2 => 28,
        3 => 38,
        4 => 50,
        5 => 65,
        6 => 80,
        7 => 100,
        _ => 120
    };

    // Суммарный уровень игрока по всем расам (+ Paragon как бонусные уровни)
    public static long TotalLevel(PlayerData p) =>
        p.Races.Values.Sum(r => (long)r.Level + r.ParagonLevel);

    // Сколько рас данного дивизиона уже разблокировано общим уровнем.
    // Каждая следующая раса дивизиона требует +Step общего уровня.
    public static int UnlockedSlots(long totalLevel, int division)
    {
        int step = StepForDivision(division);
        // первая раса дивизиона бесплатна, далее каждые step
        return (int)(totalLevel / step) + 1;
    }

    // Разблокирована ли конкретная раса для игрока.
    // index = порядковый номер расы внутри её дивизиона (0,1,2,...).
    public static bool IsUnlocked(PlayerData p, RaceDefinition race, IReadOnlyList<RaceDefinition> divisionRaces)
    {
        if (p.UnlockedRaces.Contains(race.Id)) return true;   // выиграно в рулетке рас
        // дивизион игрока должен быть открыт
        if (race.Division > p.Division) return false;
        long total = TotalLevel(p);
        int index = divisionRaces.OrderBy(r => r.Id).ToList().FindIndex(r => r.Id == race.Id);
        if (index < 0) return false;
        int step = StepForDivision(race.Division);
        long requiredTotal = (long)index * step; // раса #index требует index*step общего уровня
        return total >= requiredTotal;
    }

    // Сколько общего уровня нужно до следующей закрытой расы дивизиона.
    public static long LevelsToNextUnlock(PlayerData p, int division, int unlockedInDivision)
    {
        long total = TotalLevel(p);
        int step = StepForDivision(division);
        long nextReq = (long)unlockedInDivision * step;
        return System.Math.Max(0, nextReq - total);
    }

    // Открытие нового ДИВИЗИОНА (порог общего уровня).
    // Пороги общего уровня для входа в дивизион: D1..D8
    public static readonly long[] DivThresholds = { 0, 200, 500, 1000, 1800, 3000, 4500, 7000 };
    public static int DivisionForTotalLevel(long total)
    {
        int div = 1;
        for (int i = 0; i < DivThresholds.Length; i++) if (total >= DivThresholds[i]) div = i + 1;
        return div;
    }

    // Обновить дивизион игрока по его общему уровню (вызывать после прокачки).
    public static bool RefreshDivision(PlayerData p)
    {
        int newDiv = DivisionForTotalLevel(TotalLevel(p));
        if (newDiv > p.Division) { p.Division = newDiv; return true; }
        return false;
    }
}