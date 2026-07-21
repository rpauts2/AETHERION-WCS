using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    public bool PremiumOnly { get; set; }
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

    public void Load(string path)
    {
        if (!File.Exists(path)) { Console.WriteLine($"[SeasonSystem] Config not found: {path}"); return; }
        try
        {
            var json = File.ReadAllText(path);
            var root = JsonDocument.Parse(json).RootElement;
            int activeId = root.GetProperty("active_season_id").GetInt32();
            var seasons = root.GetProperty("seasons");
            foreach (var s in seasons.EnumerateArray())
            {
                if (s.GetProperty("id").GetInt32() != activeId) continue;
                var season = new Season
                {
                    Id = activeId,
                    Name = s.GetProperty("name").GetString() ?? "",
                    NameEn = s.GetProperty("name_en").GetString() ?? "",
                    StartUtc = DateTime.Parse(s.GetProperty("start_utc").GetString()!),
                    EndUtc = DateTime.Parse(s.GetProperty("end_utc").GetString()!),
                    Weeks = s.TryGetProperty("weeks", out var w) ? w.GetInt32() : 5
                };
                if (s.TryGetProperty("season_pass", out var sp))
                {
                    season.SeasonPass = new SeasonPassConfig
                    {
                        FreeTierMaxLevel = sp.TryGetProperty("free_tier_max_level", out var ft) ? ft.GetInt32() : 50,
                        PremiumTierMaxLevel = sp.TryGetProperty("premium_tier_max_level", out var pt) ? pt.GetInt32() : 50,
                        PremiumTag = sp.TryGetProperty("premium_tag", out var ptag) ? ptag.GetString() ?? "Aether+" : "Aether+",
                        AutoClaimUnlockedFree = sp.TryGetProperty("auto_claim_unlocked_free", out var acf) && acf.GetBoolean()
                    };
                }
                if (s.TryGetProperty("balance_mods", out var bm))
                {
                    foreach (var kv in bm.EnumerateObject())
                        if (int.TryParse(kv.Name, out int rid))
                            season.RaceBalanceMods[rid] = kv.Value.GetDouble();
                }
                if (s.TryGetProperty("cosmetics", out var cos))
                {
                    foreach (var c in cos.EnumerateArray())
                    {
                        season.Cosmetics.Add(new CosmeticDefinition
                        {
                            Id = c.GetProperty("id").GetString() ?? "",
                            Type = c.GetProperty("type").GetString() ?? "",
                            LabelRu = c.TryGetProperty("label_ru", out var lr) ? lr.GetString() ?? "" : "",
                            LabelEn = c.TryGetProperty("label_en", out var le) ? le.GetString() ?? "" : "",
                            Color = c.TryGetProperty("color", out var co) ? co.GetString() ?? "" : "",
                            Rarity = c.TryGetProperty("rarity", out var ra) ? ra.GetString() ?? "" : "",
                            TierLevel = c.GetProperty("tier_level").GetInt32(),
                            PremiumOnly = c.TryGetProperty("premium_only", out var premium) && premium.GetBoolean()
                        });
                    }
                }
                Current = season;
                if (Current.Tiers.Count == 0)
                {
                    int maxTier = Current.SeasonPass.FreeTierMaxLevel;
                    for (int i = 1; i <= maxTier; i++)
                    {
                        var cosDef = Current.Cosmetics.FirstOrDefault(c => c.TierLevel == i && !c.PremiumOnly);
                        Current.Tiers.Add(new BattlePassTier
                        {
                            Level = i,
                            XpRequired = XpForTier(i),
                            Reward = cosDef != null ? Enum.Parse<RewardType>(cosDef.Type, true) : RewardType.GoldBonus,
                            RewardValue = cosDef?.LabelRu ?? $"{50 * i}з",
                            PremiumOnly = false
                        });
                        var premiumCos = Current.Cosmetics.FirstOrDefault(c => c.TierLevel == i && c.PremiumOnly);
                        if (premiumCos != null)
                        {
                            Current.Tiers.Add(new BattlePassTier
                            {
                                Level = i,
                                XpRequired = XpForTier(i),
                                Reward = Enum.Parse<RewardType>(premiumCos.Type, true),
                                RewardValue = premiumCos.LabelRu,
                                PremiumOnly = true
                            });
                        }
                    }
                }
                Console.WriteLine($"[SeasonSystem] Loaded season #{season.Id} «{season.Name}» — {Current.Tiers.Count} tiers, {season.Cosmetics.Count} cosmetics");
                return;
            }
            Console.WriteLine($"[SeasonSystem] Active season #{activeId} not found in config");
        }
        catch (Exception ex) { Console.WriteLine($"[SeasonSystem] Load error: {ex.Message}"); }
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
    public CosmeticDefinition? CosmeticForTier(int tierLevel, bool premium = false)
    {
        return Current.Cosmetics.FirstOrDefault(c => c.TierLevel == tierLevel && c.PremiumOnly == premium);
    }

    public bool HasPremiumAccess(PlayerData p) =>
        DateTimeOffset.FromUnixTimeSeconds(p.PremiumPassExpiresUnix) > DateTimeOffset.UtcNow;

    public bool TryClaim(PlayerData p, int tierLevel, bool premium)
    {
        if (!IsActive || tierLevel <= 0 || tierLevel > p.SeasonRank) return false;
        if (premium && !HasPremiumAccess(p)) return false;
        var tier = Current.Tiers.FirstOrDefault(t => t.Level == tierLevel && t.PremiumOnly == premium);
        if (tier == null) return false;
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
        var cosmetic = CosmeticForTier(tierLevel, premium);
        if (cosmetic != null && !p.OwnedCosmetics.Contains(cosmetic.Id))
            p.OwnedCosmetics.Add(cosmetic.Id);
        p.IsDirty = true;
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

    public string UiProgressFree(PlayerData p) => L("Season_TrackFreeProgress").Replace("{0}", p.SeasonRank.ToString()).Replace("{1}", Current.SeasonPass.FreeTierMaxLevel.ToString());

    public string UiProgressPremium(PlayerData p) => L("Season_TrackPremiumProgress").Replace("{0}", p.SeasonRank.ToString()).Replace("{1}", Current.SeasonPass.PremiumTierMaxLevel.ToString());

    public string UiTierLabel(BattlePassTier tier, bool premium) => premium && tier.PremiumOnly ? tier.RewardValue + " [" + Current.SeasonPass.PremiumTag + "]" : tier.RewardValue;

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
