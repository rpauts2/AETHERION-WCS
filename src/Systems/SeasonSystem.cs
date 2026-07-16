using System;
using System.Collections.Generic;
using System.Linq;
using WcsInfinity.Models;

namespace WcsInfinity.Systems;

// Сезоны (4-6 недель) + Battle Pass. Награды = КОСМЕТИКА, не сила (анти P2W).
public enum RewardType { Title, NameColor, EtherTrail, KillSound, ParticleSkin, GoldBonus, SkillTrailColor, PassFrame, PlayermodelPreview, UltimateSkin }

public class BattlePassTier
{
    public int Level { get; set; }
    public long XpRequired { get; set; }
    public RewardType Reward { get; set; }
    public string RewardValue { get; set; } = ""; // напр. "Покоритель Шторма" / "#FF00AA"
    public bool PremiumOnly { get; set; }         // только для владельцев платного пасса
    public bool AutoClaimFree { get; set; } = false;
    public bool AutoClaimPremium { get; set; } = false;
}

public class SeasonPassConfig
{
    public int FreeTierMaxLevel { get; set; } = 50;
    public int PremiumTierMaxLevel { get; set; } = 50;
    public string PremiumTag { get; set; } = "Aether+";
    public string PremiumTagEn { get; set; } = "Aether+";
    public bool AutoClaimUnlockedFree { get; set; } = true;
    public bool AutoClaimUnlockedPremium { get; set; } = false;
}

public class CosmeticDefinition
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string LabelRu { get; set; } = "";
    public string LabelEn { get; set; } = "";
    public string Color { get; set; } = "";
    public string Rarity { get; set; } = "";
    public int TierLevel { get; set; } = 0;
}

public class Season
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NameEn { get; set; } = "";
    public string Description { get; set; } = "";
    public string DescriptionEn { get; set; } = "";
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public int Weeks { get; set; } = 5;
    public SeasonPassConfig SeasonPass { get; set; } = new();
    public List<BattlePassTier> Tiers { get; set; } = new();
    public Dictionary<int, double> RaceBalanceMods { get; set; } = new();
    public List<CosmeticDefinition> Cosmetics { get; set; } = new();
}

public class SeasonSystem
{
    public Season Current { get; private set; } = new();
    private readonly Func<string, string, string> _locProvider;

    public SeasonSystem() : this((key, lang) => key)
    {
    }

    public SeasonSystem(Func<string, string, string> locProvider)
    {
        _locProvider = locProvider;
    }

    // ── XP → Tier conversion ─────────────────────────────────────────────
    public static long XpForTier(int tier) => tier switch
    {
        < 10 => 500,
        < 20 => 750,
        < 30 => 1000,
        < 40 => 1500,
        < 50 => 2000,
        _ => 2500
    };

    public static int ConvertXpToTiers(PlayerData p, int maxTier)
    {
        int gained = 0;
        while (p.SeasonRank < maxTier)
        {
            long need = XpForTier(p.SeasonRank);
            if (p.SeasonXp < need) break;
            p.SeasonXp -= need;
            p.SeasonRank++;
            gained++;
        }
        return gained;
    }

    // ── State helpers ────────────────────────────────────────────────────────
    public bool IsActive => DateTime.UtcNow < Current.EndUtc;
    public TimeSpan TimeLeft => Current.EndUtc - DateTime.UtcNow;
    public double BalanceMod(int raceId) => Current.RaceBalanceMods.TryGetValue(raceId, out var m) ? m : 1.0;

    public string TimeLeftLabel()
    {
        var ts = TimeLeft;
        if (ts <= TimeSpan.Zero) return "Сезон завершён";
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}д {ts.Hours}ч";
        return $"{ts.Hours}ч {ts.Minutes}м";
    }

    public string ProgressText(int rank, int max)
    {
        int displayMax = Math.Min(max, Current.Tiers.Count);
        return rank >= displayMax ? $"Уровни {displayMax}/{displayMax}" : $"Уровни {rank}/{displayMax}";
    }

    // ── Rewards / claim flow ─────────────────────────────────────────────────
    public CosmeticDefinition? CosmeticForTier(int tierLevel)
    {
        return Current.Cosmetics.FirstOrDefault(c => c.TierLevel == tierLevel);
    }

    public bool TryClaim(PlayerData p, int tierLevel, bool premium)
    {
        if (premium)
        {
            if (p.ClaimedPremiumPassTiers.Contains(tierLevel)) return false;
            p.ClaimedPremiumPassTiers.Add(tierLevel);
        }
        else
        {
            if (p.ClaimedFreePassTiers.Contains(tierLevel)) return false;
            p.ClaimedFreePassTiers.Add(tierLevel);
        }
        return true;
    }

    public bool HasClaimed(PlayerData p, int tierLevel, bool premium)
    {
        if (premium) return p.ClaimedPremiumPassTiers.Contains(tierLevel);
        return p.ClaimedFreePassTiers.Contains(tierLevel);
    }

    public bool IsPremium(int tierLevel) => Current.Tiers.Any(t => t.Level == tierLevel && t.PremiumOnly);

    // ── L10n helpers ─────────────────────────────────────────────────────────
    public string L(string key) => _locProvider(key, "ru");

    public string UiSeasonTitle(bool ru = true) => ru ? $"{Current.Name} Battle Pass" : $"{Current.NameEn} Battle Pass";

    public string UiSeasonStatus(PlayerData p, bool ru = true)
    {
        if (!IsActive) return L("Season_TimeRemaining").Replace("{0}", "0ч");
        string time = TimeLeftLabel();
        return (ru ? "До конца сезона: " : "Season ends in: ") + time;
    }

    public string UiProgressFree(PlayerData p) => L("Season_TrackFreeProgress").Replace("{0}", p.SeasonRank.ToString()).Replace("{1}", Current.Tiers.Count.ToString());

    public string UiProgressPremium(PlayerData p) => L("Season_TrackPremiumProgress").Replace("{0}", p.SeasonRank.ToString()).Replace("{1}", Current.Tiers.Count.ToString());

    public string UiTierLabel(BattlePassTier tier, bool premium) => premium && !tier.PremiumOnly ? tier.RewardValue + " [" + Current.SeasonPass.PremiumTag + "]" : tier.RewardValue;

    public string UiSeasonMenuTitle(PlayerData p, bool ru = true) => ru ? LocTool.Format("Season_BattlePass", Current.Name) : Current.NameEn + " Season Podium";

    public string UiRewardPreview(CosmeticDefinition def, bool ru = true) => ru ? def.LabelRu : def.LabelEn;

    public string UiClaimStatus(PlayerData p, int tierLevel, bool premium)
    {
        if (HasClaimed(p, tierLevel, premium)) return L("Season_Claimed");
        if (!premium && IsPremium(tierLevel)) return "Только для " + Current.SeasonPass.PremiumTag;
        if (p.SeasonRank < tierLevel) return L("Season_NotEnoughRank").Replace("{0}", tierLevel.ToString());
        return L("Season_ClaimReward");
    }
}

// Вспомогатель локализации, чтобы не завязываться напрямую на плагин.
public static class LocTool
{
    public static string Format(string key, string p0) => $"{key}|{p0}";
}
