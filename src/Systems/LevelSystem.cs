using System;
using WcsInfinity.Models;
using WcsInfinity.Races;

namespace WcsInfinity.Systems;

// Система опыта и уровней. Реализует дивизионную кривую:
// чем дальше дивизион расы — тем больше шаг XP между уровнями.
public static class LevelSystem
{
    // XP-награды
    public const long XpKill     = 35;
    public const long XpHeadshot = 20;  // бонус сверху
    public const long XpAssist   = 12;
    public const long XpBombPlant = 25;
    public const long XpBombDefuse = 25;
    public const long XpRoundWin = 15;

    // Базовая стоимость уровня зависит от дивизиона расы (твоя кривая 20-30 / 30-35 / 40-60)
    // Возвращает сколько XP нужно для перехода с level -> level+1
    public static long XpForLevel(int level, RaceTier tier, int division)
    {
        // базовый шаг по дивизиону
        long step = division switch
        {
            1 => 80,    // D1: быстрый онбординг (~1 карта на уровень)
            2 => 130,
            3 => 200,
            4 => 300,
            5 => 430,
            6 => 600,
            7 => 820,
            _ => 1100   // D8 / Paragon — для задротов
        };
        // плавный рост внутри расы
        return step + (long)(level * step * 0.07);
    }

    // XP до следующего УРОВНЯ РАСЫ; «шаг между расами» = суммарный XP всех уровней расы.
    public static int MaxLevelForRace(RaceTier tier) => 1000; // единый кап (баг "/8" исправлен)

    // Реальный кап = сумма максимумов всех способностей расы (можно прокачать всё).
    public static int MaxLevelForRace(WcsInfinity.Races.RaceDefinition def)
    {
        if (def == null) return 8;
        return 1000; // глобальный кап уровня расы
    }

    // Начислить опыт с расой-источником (правильный кап + правильный дивизион расы).
    public static int AddXp(PlayerData p, RaceProgress rp, WcsInfinity.Races.RaceDefinition def, long amount)
        => AddXpInternal(p, rp, def != null ? def.TierEnum : RaceTier.T1_Spark, MaxLevelForRace(def), amount, def?.Division ?? p.Division);

    // Начислить опыт. Возвращает кол-во поднятых уровней (для уведомлений).
    public static int AddXp(PlayerData p, RaceProgress rp, RaceTier tier, long amount)
        => AddXpInternal(p, rp, tier, MaxLevelForRace(tier), amount, p.Division);

    private static int AddXpInternal(PlayerData p, RaceProgress rp, RaceTier tier, int max, long amount, int division)
    {
        amount = (long)(amount * p.XpBoostMultiplier);
        rp.Xp += amount;
        int gained = 0;
        while (rp.Level < max)
        {
            long need = XpForLevel(rp.Level, tier, division);
            if (rp.Xp < need) break;
            rp.Xp -= need;
            rp.Level++;
            rp.UnspentPoints++;
            gained++;
        }
        // Paragon: cost grows 10% per paragon level
        if (rp.Level >= max)
        {
            long paragonNeed = (long)(XpForLevel(rp.Level, tier, 8) * (1 + rp.ParagonLevel * 0.10));
            while (rp.Xp >= paragonNeed)
            {
                rp.Xp -= paragonNeed;
                rp.ParagonLevel++;
                paragonNeed = (long)(XpForLevel(rp.Level, tier, 8) * (1 + rp.ParagonLevel * 0.10));
            }
        }
        return gained;
    }
}