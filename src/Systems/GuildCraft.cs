using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WcsInfinity.Systems;

namespace WcsInfinity.Systems;

public enum GuildTier { Wood, Iron, Bronze, Silver, Gold, Aether }

public class GuildCraftRecipe
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public GuildTier Tier { get; set; } = GuildTier.Wood;
    public long GoldCost { get; set; } = 100;
    public int BannerXpReward { get; set; } = 25;
    public string Effect { get; set; } = "";
    public string EffectDesc { get; set; } = "";
}

public static class GuildCraftSystem
{
    private static readonly Dictionary<int, GuildCraftRecipe> _recipes = new();
    private static int _nextId = 1;

    public static GuildCraftRecipe Register(string name, string desc, GuildTier tier, long gold, int bannerXp, string effect, string effectDesc)
    {
        var r = new GuildCraftRecipe { Id = _nextId++, Name = name, Description = desc, Tier = tier, GoldCost = gold, BannerXpReward = bannerXp, Effect = effect, EffectDesc = effectDesc };
        _recipes[r.Id] = r;
        return r;
    }

    public static IEnumerable<GuildCraftRecipe> ForTier(GuildTier tier) => _recipes.Values.Where(r => r.Tier <= tier);

    public static bool TryCraft(Guild g, int recipeId)
    {
        if (!_recipes.TryGetValue(recipeId, out var r)) return false;
        if (g.Treasury < r.GoldCost) return false;
        if (g.CraftedIds.Contains(recipeId)) return false;
        // Check tier requirement
        if (r.Tier > g.Tier) return false;
        g.Treasury -= r.GoldCost;
        g.CraftedIds.Add(recipeId);
        GuildManager.Instance.AddBannerXp(g, r.BannerXpReward);

        return true;
    }

    public static void InitDefaults()
    {
        if (_recipes.Count > 0) return;
        Register("РУДНАЯ СВИТКА", "Каждый крафт даёт +2% золота ко всем доходам гильдии",
            GuildTier.Wood, 150, 30, "gold_bonus", "+2% золото");

        Register("ЖЕЗЛ КОМАНДИРА", "Каждый крафт даёт +2% опыта ко всем доходам гильдии",
            GuildTier.Iron, 350, 55, "xp_bonus", "+2% XP");

        Register("ХРАНИТЕЛЬ ЗНАМЕНИ", "Разблокирует +1 слот в гильдии",
            GuildTier.Bronze, 800, 90, "member_slot", "+1 слот");

        Register("БАННЕР ЭФИРА", "+2% золото и +2% XP одновременно",
            GuildTier.Silver, 1800, 130, "all_bonuses", "+2% золото и XP");

        Register("КУПОЛ ГИЛЬДИИ", "Раз в раунд: аура лечения союзников в радиусе",
            GuildTier.Silver, 2200, 150, "heal_aura", "Лечение союзников");

        Register("КЛЮЧ ЭФИРА", "+5% XP знамени за все действия",
            GuildTier.Gold, 3000, 180, "banner_xp_boost", "+5% XP знамени");

        Register("ЗНАМЯ ПОБЕДЫ", "+3% золото и +3% XP (совмещённый бонус)",
            GuildTier.Gold, 4500, 220, "all_bonuses_big", "+3% золото и XP");

        Register("СЕРДЦЕ ГИЛЬДИИ", "Максимальный бонус: +5% золото, +5% XP, +2 слота",
            GuildTier.Aether, 8000, 400, "guild_heart", "+5% золото, +5% XP, +2 слота");
    }

    public static GuildCraftRecipe? GetRecipe(int id) => _recipes.GetValueOrDefault(id);
}
