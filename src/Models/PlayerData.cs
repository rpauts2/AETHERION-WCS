using System.Collections.Generic;

namespace WcsInfinity.Models;

// Профиль игрока. Хранит прогресс по ВСЕМ расам (тысячи рас).
public class PlayerData
{
    public System.Collections.Generic.HashSet<int> UnlockedRaces { get; set; } = new();
    public int FreeSpins { get; set; } = 0;
    public long LastLoginUnix { get; set; } = 0;
    public int LoginStreak { get; set; } = 0;
    public long VipExpiresUnix { get; set; } = 0;
    public int WispBond { get; set; } = 0;
    public ulong SteamId { get; set; }
    public string Name { get; set; } = "";

    // Текущая активная раса
    public int CurrentRaceId { get; set; } = 1;

    // Прогресс по каждой расе: raceId -> RaceProgress
    public Dictionary<int, RaceProgress> Races { get; set; } = new();

    // Экономика
    public long Gold { get; set; } = 0;
    public bool WelcomeBonus { get; set; } = false;

    // Банк уровней: накопленные уровни, которые можно влить в любую расу
    public long LevelBank { get; set; } = 0;

    // Инвентарь предметов из магазина (см. ItemShop)
    public System.Collections.Generic.List<WcsInfinity.Systems.OwnedItem> Inventory { get; set; } = new();

    // Дивизион игрока (открывается по сумме прокачанных рас)
    public int Division { get; set; } = 1;

    // Сезонный прогресс / Battle Pass
    public int SeasonRank { get; set; } = 0;
    public long SeasonXp { get; set; } = 0;
    public HashSet<int> ClaimedFreePassTiers { get; set; } = new();
    public HashSet<int> ClaimedPremiumPassTiers { get; set; } = new();
    public List<string> OwnedCosmetics { get; set; } = new();

    // Гильдия
    public int GuildId { get; set; } = 0;

    // Бусты, ачивки
    public HashSet<string> Achievements { get; set; } = new();
    public Dictionary<string, int> AchievementProgress { get; set; } = new();
    public HashSet<string> ClaimedAchievements { get; set; } = new();
    public Dictionary<string, int> DailyChallenges { get; set; } = new();
    public long LastDailyRefreshUnix { get; set; } = 0;
    public Dictionary<string, int> QuestCounters { get; set; } = new(); // kills, headshots, boss_kills, ult_casts, etc.
    public double XpBoostMultiplier { get; set; } = 1.0;
    public long XpBoostExpiresUnix { get; set; } = 0;

    public RaceProgress GetRace(int raceId)
    {
        if (!Races.TryGetValue(raceId, out var rp))
        {
            rp = new RaceProgress { RaceId = raceId };
            Races[raceId] = rp;
        }
        return rp;
    }
}

// Прогресс конкретной расы у игрока
public class RaceProgress
{
    public int RaceId { get; set; }
    public int Level { get; set; } = 1;
    public long Xp { get; set; } = 0;
    public int UnspentPoints { get; set; } = 0;
    // Уровни вложенные в каждую способность: skillIndex -> level
    public Dictionary<int, int> SkillLevels { get; set; } = new();
    // Paragon-уровни (бесконечный дивизион)
    public long ParagonLevel { get; set; } = 0;
    public long ParagonsTotalXp { get; set; } = 0;

    // Привязанность духа-компаньона (копится за килы, сохраняется между сессиями)
    public int WispBond { get; set; } = 0;
}
