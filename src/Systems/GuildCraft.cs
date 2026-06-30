using System;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WcsInfinity.Systems;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════╗
// ║  GUILD CRAFT — локальные локальные прокачка предметов гильдии.    ║
// ║  Только мастерство и локальная валюта, никаких серверных вызовов. ║
// ╚══════════════════════════════════════════════════════════════════╝
public enum GuildTier { Wood, Iron, Bronze, Silver, Gold, Aether }

public class GuildCraftRecipe
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public GuildTier Tier { get; set; } = GuildTier.Wood;
    public long GoldCost { get; set; } = 100;
    public int BannerXpReward { get; set; } = 25;
    public string Effect { get; set; } = ""; // локальный эффект на гильдию
}

public class GuildCraftSystem
{
    private static readonly Dictionary<int, GuildCraftRecipe> _recipes = new();
    private static int _nextId = 1;

    public static GuildCraftRecipe Register(string name, GuildTier tier, long gold, int bannerXp, string effect)
    {
        var r = new GuildCraftRecipe { Id = _nextId++, Name = name, Tier = tier, GoldCost = gold, BannerXpReward = bannerXp, Effect = effect };
        _recipes[r.Id] = r;
        return r;
    }

    public static IEnumerable<GuildCraftRecipe> ForTier(GuildTier tier) => _recipes.Values.Where(r => r.Tier <= tier);

    public static bool TryCraft(Guild g, int recipeId)
    {
        if (!_recipes.TryGetValue(recipeId, out var r)) return false;
        if (g.Treasury < r.GoldCost) return false;
        g.Treasury -= r.GoldCost;
        GuildManager.Instance.AddBannerXp(g, r.BannerXpReward);
        return true;
    }

    public static void InitDefaults()
    {
        Register("ÆРУДНАЯ СВИТКА", GuildTier.Wood, 100, 25, "gold_bonus");
        Register("ЖЕЗЛ КОМАНДИРА", GuildTier.Iron, 300, 50, "xp_bonus");
        Register("ХРАНИТЕЛЬ ЗНАМЕНИ", GuildTier.Bronze, 700, 80, "member_slot_+1");
        Register("БАННЕР ЭФИРА", GuildTier.Silver, 1500, 120, "all_bonuses");
        Register("КУПОЛ ГИЛЬДИИ", GuildTier.Gold, 3500, 200, "shield_dome");
    }
}
