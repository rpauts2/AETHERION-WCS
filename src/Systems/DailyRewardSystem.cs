using System;
using WcsInfinity.Models;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════╗
// ║  DAILY LOGIN STREAK — награда за ежедневный вход.                   ║
// ║  Чем больше дней подряд — тем лучше. Пропустил день — стрик сброс.  ║
// ╚══════════════════════════════════════════════════════════════════╝

public class DailyReward
{
    public int Day;            // номер дня стрика (1..7, потом цикл)
    public long Gold;
    public int FreeSpins;      // бесплатные прокруты рулетки
    public int VipDays;        // дни фри-VIP
    public long BonusXp;
    public bool IsMilestone;   // день 7 — джекпот
    public string Label = "";
}

public static class DailyRewardSystem
{
    // Базовая лестница 7 дней. После дня 7 цикл повторяется,
    // но с множителем за количество пройденных недель (streakWeeks).
    public static DailyReward Compute(int streakDay)
    {
        int dayInCycle = ((streakDay - 1) % 7) + 1;   // 1..7
        int weeks = (streakDay - 1) / 7;              // 0,1,2...
        float weekMult = 1f + weeks * 0.5f;           // +50% за каждую пройденную неделю

        var r = new DailyReward { Day = streakDay };
        switch (dayInCycle)
        {
            case 1: r.Gold = 50;   r.Label = "Разминка"; break;
            case 2: r.Gold = 100;  r.Label = "Втягиваешься"; break;
            case 3: r.Gold = 150;  r.FreeSpins = 1; r.Label = "Бесплатный спин!"; break;
            case 4: r.Gold = 200;  r.BonusXp = 500; r.Label = "Бонус опыта"; break;
            case 5: r.Gold = 300;  r.VipDays = 1; r.Label = "Фри-VIP на день!"; break;
            case 6: r.Gold = 400;  r.FreeSpins = 1; r.BonusXp = 800; r.Label = "Двойной куш"; break;
            case 7: r.Gold = 1000; r.FreeSpins = 2; r.VipDays = 2; r.BonusXp = 1500;
                    r.IsMilestone = true; r.Label = "★ ДЖЕКПОТ НЕДЕЛИ ★"; break;
        }
        r.Gold = (long)(r.Gold * weekMult);
        r.BonusXp = (long)(r.BonusXp * weekMult);
        return r;
    }

    // Проверяет вход и обновляет стрик. Возвращает награду (или null, если уже получал сегодня).
    public static DailyReward? CheckIn(PlayerData p, DateTime nowUtc)
    {
        var today = nowUtc.Date;
        long lastUnix = p.LastLoginUnix;
        DateTime last = lastUnix > 0
            ? DateTimeOffset.FromUnixTimeSeconds(lastUnix).UtcDateTime.Date
            : DateTime.MinValue;

        if (last == today) return null;                 // уже заходил сегодня

        if (last == today.AddDays(-1)) p.LoginStreak += 1;  // вчера был — стрик растёт
        else p.LoginStreak = 1;                              // пропуск — стрик заново

        p.LastLoginUnix = new DateTimeOffset(today, TimeSpan.Zero).ToUnixTimeSeconds();
        var reward = Compute(p.LoginStreak);

        // 7-дневный стрик = бесплатная разблокировка расы
        if (p.LoginStreak % 7 == 0) reward.IsMilestone = true;

        return reward;
    }
}
