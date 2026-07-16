using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WcsInfinity.Models;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════════╗
// ║  AchievementSystem — ачивки, челленджи, дейлики, трекинг прогресса  ║
// ║  Типы: Kill, Headshot, Boss, Streak, Social, Skill, Mastery, Race  ║
// ╚══════════════════════════════════════════════════════════════════════╝

public sealed class AchievementDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Category { get; init; } = "kill"; // kill, headshot, boss, streak, social, skill, mastery, race
    public string Counter { get; init; } = ""; // "kills", "headshots", "boss_kills", "ult_casts", "race_2000_kills"
    public int Target { get; init; } = 1;
    public int GoldReward { get; init; } = 0;
    public int XpReward { get; init; } = 0;
    public string CosmeticReward { get; init; } = ""; // title ID, color, trail
    public bool IsHidden { get; init; } = false;
    public int Tier { get; init; } = 1; // 1=обычная, 2=редкая, 3=легендарная, 4=мифическая
}

public sealed class DailyChallengeDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Counter { get; init; } = "";
    public int Target { get; init; } = 5;
    public int GoldReward { get; init; } = 50;
    public int XpReward { get; init; } = 100;
}

public sealed class AchievementSystem
{
    private readonly List<AchievementDef> _achievements = new();
    private readonly List<DailyChallengeDef> _dailyPool = new();
    private readonly Random _rng = new();

    // Счётчики прогресса (стримятся в PlayerData.QuestCounters)
    private const string C_KILLS = "kills";
    private const string C_HEADSHOTS = "headshots";
    private const string C_ASSISTS = "assists";
    private const string C_KNIFE_KILLS = "knife_kills";
    private const string C_BOSS_KILLS = "boss_kills";
    private const string C_ULT_CASTS = "ult_casts";
    private const string C_SIGIL_CASTS = "sigil_casts";
    private const string C_FLASH_KILLS = "flash_kills";
    private const string C_ROUNDS_WON = "rounds_won";
    private const string C_ACE = "ace_rounds";
    private const string C_STREAK_3 = "streak_3";
    private const string C_STREAK_5 = "streak_5";
    private const string C_STREAK_10 = "streak_10";
    private const string C_RACE_PREFIX = "race_"; // "race_2000_kills" — киллы конкретной расой

    public IReadOnlyList<AchievementDef> Achievements => _achievements;
    public IReadOnlyList<DailyChallengeDef> DailyPool => _dailyPool;

    public AchievementSystem()
    {
        RegisterAchievements();
        RegisterDailyPool();
    }

    // ═══════════════════════════════════════════════════════════
    //  РЕГИСТРАЦИЯ АЧИВОК
    // ═══════════════════════════════════════════════════════════
    private void RegisterAchievements()
    {
        // ── KILL (килл-ачивки) ──
        Add("first_blood", "Первая кровь", "Соверши первый килл", "kill", C_KILLS, 1, 25, 15);
        Add("killer_50", "Убийца", "Соверши 50 убийств", "kill", C_KILLS, 50, 100, 100);
        Add("killer_200", "Наёмник", "Соверши 200 убийств", "kill", C_KILLS, 200, 300, 300, tier: 2);
        Add("killer_1000", "Палач", "Соверши 1000 убийств", "kill", C_KILLS, 1000, 800, 800, tier: 3);
        Add("killer_5000", "Жнец Душ", "Соверши 5000 убийств", "kill", C_KILLS, 5000, 2000, 2000, "title_reaper", tier: 4);

        // ── HEADSHOT ──
        Add("headshot_10", "Меткий глаз", "10 хедшотов", "headshot", C_HEADSHOTS, 10, 50, 50);
        Add("headshot_100", "Снайпер", "100 хедшотов", "headshot", C_HEADSHOTS, 100, 250, 250, tier: 2);
        Add("headshot_500", "Головорез", "500 хедшотов", "headshot", C_HEADSHOTS, 500, 700, 700, tier: 3);

        // ── KNIFE ──
        Add("knife_1", "Ножевой бой", "Убей ножом", "kill", C_KNIFE_KILLS, 1, 40, 30);
        Add("knife_25", "Бутcher", "25 ножевых убийств", "kill", C_KNIFE_KILLS, 25, 200, 150, tier: 2);

        // ── BOSS ──
        Add("boss_slayer", "Босс-убийца", "Убей босса", "boss", C_BOSS_KILLS, 1, 150, 200, tier: 2);
        Add("boss_master", "Гроза боссов", "Убей 10 боссов", "boss", C_BOSS_KILLS, 10, 500, 500, tier: 3);

        // ── STREAK ──
        Add("streak_3", "Тройной удар", "3 убийства за раунд без смерти", "streak", C_STREAK_3, 1, 75, 75);
        Add("streak_5", "Аннигилятор", "5 убийств за раунд", "streak", C_STREAK_5, 1, 150, 150, tier: 2);
        Add("streak_10", "Геноцид", "10 убийств за раунд", "streak", C_STREAK_10, 1, 400, 400, "title_genocide", tier: 3);

        // ── ACE ──
        Add("ace_1", "Эйс!", "Заверши раунд один (убей всех)", "streak", C_ACE, 1, 200, 200, tier: 2);
        Add("ace_10", "Эйс-мастер", "10 эйсов", "streak", C_ACE, 10, 800, 800, "title_ace", tier: 4);

        // ── SOCIAL ──
        Add("guild_join", "Братство", "Вступи в гильдию", "social", "", 1, 50, 50);
        Add("voter", "Голос", "Участвуй в 5 голосованиях за босса", "social", "boss_votes", 5, 100, 100);

        // ── SKILL ──
        Add("ult_50", "Ультимейтер", "Кастани 50 ульт", "skill", C_ULT_CASTS, 50, 200, 200);
        Add("sigil_100", "Печать Духа", "Нарисуй 100 Печатей", "skill", C_SIGIL_CASTS, 100, 300, 300, tier: 2);

        // ── RACE MASTERY (шаблон — генерируется динамически) ──
        // "race_2000_kills:100" = 100 киллов расой Пудж (id 2000)
        // Создаём для всех известных рас — они добавляются через RegisterRaceAchievements()
    }

    // Генерирует расовые ачивки (вызывается при загрузке рас)
    public void RegisterRaceAchievements(IEnumerable<int> raceIds)
    {
        foreach (var raceId in raceIds)
        {
            string counter = $"{C_RACE_PREFIX}{raceId}_kills";
            Add($"race_{raceId}_50", $"Ученик расы #{raceId}", "50 киллов этой расой", "race", counter, 50, 100, 100);
            Add($"race_{raceId}_200", $"Мастер расы #{raceId}", "200 киллов", "race", counter, 200, 400, 400, tier: 2);
            Add($"race_{raceId}_1000", $"Легенда расы #{raceId}", "1000 киллов", "race", counter, 1000, 1500, 1500, tier: 3);
        }
    }

    // ── DAILY POOL ──
    private void RegisterDailyPool()
    {
        _dailyPool.AddRange(new[]
        {
            new DailyChallengeDef { Id = "daily_kill_10", Name = "Охотник", Description = "Убей 10 врагов", Counter = C_KILLS, Target = 10, GoldReward = 80, XpReward = 200 },
            new DailyChallengeDef { Id = "daily_kill_25", Name = "Наёмник дня", Description = "Убей 25 врагов", Counter = C_KILLS, Target = 25, GoldReward = 150, XpReward = 400 },
            new DailyChallengeDef { Id = "daily_hs_5", Name = "Меткость", Description = "5 хедшотов", Counter = C_HEADSHOTS, Target = 5, GoldReward = 60, XpReward = 150 },
            new DailyChallengeDef { Id = "daily_hs_15", Name = "Снайпер дня", Description = "15 хедшотов", Counter = C_HEADSHOTS, Target = 15, GoldReward = 120, XpReward = 300 },
            new DailyChallengeDef { Id = "daily_ult_5", Name = "Чародей", Description = "Кастани 5 ульт", Counter = C_ULT_CASTS, Target = 5, GoldReward = 70, XpReward = 180 },
            new DailyChallengeDef { Id = "daily_rounds_5", Name = "Победитель", Description = "Выиграй 5 раундов", Counter = C_ROUNDS_WON, Target = 5, GoldReward = 80, XpReward = 200 },
            new DailyChallengeDef { Id = "daily_knife_3", Name = "Ножевой день", Description = "3 ножевых убийства", Counter = C_KNIFE_KILLS, Target = 3, GoldReward = 100, XpReward = 250 },
            new DailyChallengeDef { Id = "daily_sigil_10", Name = "Печать дня", Description = "Нарисуй 10 Печатей", Counter = C_SIGIL_CASTS, Target = 10, GoldReward = 70, XpReward = 180 },
            new DailyChallengeDef { Id = "daily_assists_8", Name = "Помощник", Description = "8 ассистов", Counter = C_ASSISTS, Target = 8, GoldReward = 60, XpReward = 150 },
            new DailyChallengeDef { Id = "daily_flash_3", Name = "Ослепитель", Description = "Убей 3 ослеплённых", Counter = C_FLASH_KILLS, Target = 3, GoldReward = 90, XpReward = 200 },
        });
    }

    private void Add(string id, string name, string desc, string category, string counter, int target,
        int gold, int xp, string cosmetic = "", bool hidden = false, int tier = 1)
    {
        _achievements.Add(new AchievementDef
        {
            Id = id, Name = name, Description = desc, Category = category,
            Counter = counter, Target = target, GoldReward = gold, XpReward = xp,
            CosmeticReward = cosmetic, IsHidden = hidden, Tier = tier
        });
    }

    // ═══════════════════════════════════════════════════════════
    //  ТРЕКИНГ СОБЫТИЙ (вызываются из AetherionPlugin)
    // ═══════════════════════════════════════════════════════════

    public void OnKill(PlayerData d, CCSPlayerController killer, CCSPlayerController? victim, bool hs, string weapon)
    {
        if (d == null) return;
        Inc(d, C_KILLS);
        if (hs) Inc(d, C_HEADSHOTS);
        if (weapon.Contains("knife") || weapon.Contains("bayonet"))
            Inc(d, C_KNIFE_KILLS);
        if (victim != null)
        {
            // Расовые киллы
            string raceKey = $"{C_RACE_PREFIX}{d.CurrentRaceId}_kills";
            Inc(d, raceKey);
        }
        // Проверяем ачивки с небольшим batch (не каждую секунду)
        if (d.QuestCounters.GetValueOrDefault(C_KILLS) % 10 == 0 || d.QuestCounters.GetValueOrDefault(C_KILLS) <= 5)
            CheckAchievements(killer, d);
        CheckDailies(killer, d);
    }

    public void OnAssist(PlayerData d, CCSPlayerController p)
    {
        Inc(d, C_ASSISTS);
        CheckDailies(p, d);
    }

    public void OnUltCast(PlayerData d, CCSPlayerController p)
    {
        Inc(d, C_ULT_CASTS);
        CheckAchievements(p, d);
        CheckDailies(p, d);
    }

    public void OnSigilCast(PlayerData d, CCSPlayerController p)
    {
        Inc(d, C_SIGIL_CASTS);
        CheckAchievements(p, d);
        CheckDailies(p, d);
    }

    public void OnBossKill(PlayerData d, CCSPlayerController p)
    {
        Inc(d, C_BOSS_KILLS);
        CheckAchievements(p, d);
    }

    public void OnRoundEnd(PlayerData d, CCSPlayerController p, bool won)
    {
        if (won) Inc(d, C_ROUNDS_WON);
        CheckAchievements(p, d);
        CheckDailies(p, d);
    }

    // Стрик-киллов в раунде (вызывается из плагина)
    public void OnRoundStreak(PlayerData d, CCSPlayerController p, int streak)
    {
        if (streak >= 3) Inc(d, C_STREAK_3);
        if (streak >= 5) { Inc(d, C_STREAK_5); CheckAchievements(p, d); }
        if (streak >= 10) { Inc(d, C_STREAK_10); CheckAchievements(p, d); }
        // Эйс = 5 убийств (на сервере 5v5) — упрощённо
        if (streak >= 5)
        {
            Inc(d, C_ACE);
            CheckAchievements(p, d);
        }
    }

    public void OnJoinGuild(PlayerData d, CCSPlayerController p)
    {
        if (!d.Achievements.Contains("guild_join"))
        {
            d.Achievements.Add("guild_join");
            p.PrintToChat(" \x10★ АЧИВКА: Братство! +50з +50XP");
            EconomySystem.AddGold(d, 50);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  ДЕЙЛИКИ (ежедневные челленджи)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Обновляет дейлики если наступил новый день (UTC). Возвращает список текущих.
    /// </summary>
    public List<DailyChallengeDef> RefreshDailies(PlayerData d)
    {
        long today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).ToUnixTimeSeconds();
        if (d.LastDailyRefreshUnix < today)
        {
            // Новый день — генерируем 3 случайных дейлика
            d.DailyChallenges.Clear();
            var pool = _dailyPool.OrderBy(_ => _rng.Next()).Take(3).ToList();
            foreach (var ch in pool)
                d.DailyChallenges[ch.Id] = 0; // прогресс = 0
            d.LastDailyRefreshUnix = today;
        }
        return GetCurrentDailies(d);
    }

    public List<DailyChallengeDef> GetCurrentDailies(PlayerData d)
    {
        var result = new List<DailyChallengeDef>();
        foreach (var kv in d.DailyChallenges)
        {
            var def = _dailyPool.FirstOrDefault(c => c.Id == kv.Key);
            if (def != null) result.Add(def);
        }
        return result;
    }

    private void CheckDailies(CCSPlayerController p, PlayerData d)
    {
        foreach (var kv in d.DailyChallenges.ToList())
        {
            var def = _dailyPool.FirstOrDefault(c => c.Id == kv.Key);
            if (def == null) continue;

            int progress = d.QuestCounters.GetValueOrDefault(def.Counter, 0);
            d.DailyChallenges[kv.Key] = Math.Min(progress, def.Target);

            // Проверяем завершение (каждый N-й прогресс для снижения нагрузки)
            if (progress > 0 && progress >= def.Target && !d.ClaimedAchievements.Contains("daily_" + def.Id))
            {
                d.ClaimedAchievements.Add("daily_" + def.Id);
                EconomySystem.AddGold(d, def.GoldReward);
                p.PrintToChat($" \x06★ ДЕЙЛИК: {def.Name} выполнен! +{def.GoldReward}з +{def.XpReward}XP");
            }
        }
    }

    /// <summary>
    /// Принудительно закрывает дейлик и выдаёт награду (для UI-кнопки Claim).
    /// </summary>
    public bool TryClaimDaily(PlayerData d, CCSPlayerController p, string dailyId)
    {
        string claimKey = "daily_" + dailyId;
        if (d.ClaimedAchievements.Contains(claimKey)) return false;

        var def = _dailyPool.FirstOrDefault(c => c.Id == dailyId);
        if (def == null) return false;

        int progress = d.QuestCounters.GetValueOrDefault(def.Counter, 0);
        if (progress < def.Target) return false;

        d.ClaimedAchievements.Add(claimKey);
        EconomySystem.AddGold(d, def.GoldReward);
        p.PrintToChat($" \x06★ ДЕЙЛИК ЗАКРЫТ: {def.Name} → +{def.GoldReward}з");
        return true;
    }

    // ═══════════════════════════════════════════════════════════
    //  ПРОВЕРКА АЧИВОК
    // ═══════════════════════════════════════════════════════════

    private void CheckAchievements(CCSPlayerController? p, PlayerData d)
    {
        if (p == null || d == null) return;
        foreach (var ach in _achievements)
        {
            if (d.Achievements.Contains(ach.Id)) continue;
            if (string.IsNullOrEmpty(ach.Counter)) continue;

            int progress = d.QuestCounters.GetValueOrDefault(ach.Counter, 0);
            if (progress < ach.Target) continue;

            // Ачивка выполнена!
            d.Achievements.Add(ach.Id);
            EconomySystem.AddGold(d, ach.GoldReward);
            string tierColor = ach.Tier switch
            {
                1 => "\x01",   // белый (обычная)
                2 => "\x06",   // жёлтый (редкая)
                3 => "\x0E",   // оранжевый (легендарная)
                4 => "\x0B",   // синий (мифическая)
                _ => "\x01"
            };
            p.PrintToChat($" {tierColor}★ АЧИВКА [{ach.Tier}★]: {ach.Name}!");
            p.PrintToChat($" {tierColor}  {ach.Description} → +{ach.GoldReward}з +{ach.XpReward}XP");
            if (!string.IsNullOrEmpty(ach.CosmeticReward))
                p.PrintToChat($" {tierColor}  Косметическая награда: {ach.CosmeticReward}");
        }
    }

    /// <summary>
    /// UI-информация: все ачивки для категории (для меню !ach)
    /// </summary>
    public List<(AchievementDef Def, int Progress, bool Unlocked)> GetByCategory(PlayerData d, string category)
    {
        return _achievements
            .Where(a => a.Category == category && (a.IsHidden || d.Achievements.Contains(a.Id) || d.QuestCounters.GetValueOrDefault(a.Counter, 0) > 0))
            .Select(a => (a, d.QuestCounters.GetValueOrDefault(a.Counter, 0), d.Achievements.Contains(a.Id)))
            .ToList();
    }

    /// <summary>
    /// Статистика: сколько ачивок разблокировано из N
    /// </summary>
    public (int Unlocked, int Total) GetProgress(PlayerData d)
    {
        int total = _achievements.Count;
        int unlocked = _achievements.Count(a => d.Achievements.Contains(a.Id));
        return (unlocked, total);
    }

    // ═══════════════════════════════════════════════════════════
    //  УТИЛИТЫ
    // ═══════════════════════════════════════════════════════════

    private void Inc(PlayerData d, string counter)
    {
        d.QuestCounters.TryGetValue(counter, out var v);
        d.QuestCounters[counter] = v + 1;
    }
}
