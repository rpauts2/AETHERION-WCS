using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CS2MenuManager.API.Menu;
using CS2MenuManager.API.Enum;

namespace WcsInfinity.Systems;

public class Guild
{
    public int Id;
    public string Name = "";
    public string Tag = "";
    public ulong LeaderSteamId;
    public List<ulong> Members = new();
    public List<ulong> Officers = new();
    public long Treasury;
    public int BannerLevel = 1;
    public long BannerXp;
    public DateTime CreatedUtc = DateTime.UtcNow;
    public int WeeklyFrags;
    public HashSet<int> CraftedIds = new();
    public int Level => BannerLevel;
    public GuildTier Tier => BannerLevel switch
    {
        <= 5 => GuildTier.Wood,
        <= 10 => GuildTier.Iron,
        <= 20 => GuildTier.Bronze,
        <= 35 => GuildTier.Silver,
        <= 50 => GuildTier.Gold,
        _ => GuildTier.Aether,
    };
    public long Bank => Treasury;
    public long BannerXpNeeded => 5000L * BannerLevel * BannerLevel;
    public float GoldBonus => 0.02f * BannerLevel + CraftedGoldBonus();
    public float XpBonus => 0.015f * BannerLevel + CraftedXpBonus();
    public float BannerXpMultiplier => 1f + CraftedBannerXpBonus();
    public int MaxMembers => 10 + BannerLevel * 2 + CraftedMemberSlots();

    private float CraftedGoldBonus()
    {
        float b = 0f;
        foreach (var id in CraftedIds)
        {
            var r = GuildCraftSystem.GetRecipe(id);
            if (r == null) continue;
            if (r.Effect == "gold_bonus") b += 0.02f;
            else if (r.Effect == "all_bonuses") b += 0.02f;
            else if (r.Effect == "all_bonuses_big") b += 0.03f;
            else if (r.Effect == "guild_heart") b += 0.05f;
        }
        return b;
    }

    private float CraftedXpBonus()
    {
        float b = 0f;
        foreach (var id in CraftedIds)
        {
            var r = GuildCraftSystem.GetRecipe(id);
            if (r == null) continue;
            if (r.Effect == "xp_bonus") b += 0.02f;
            else if (r.Effect == "all_bonuses") b += 0.02f;
            else if (r.Effect == "all_bonuses_big") b += 0.03f;
            else if (r.Effect == "guild_heart") b += 0.05f;
        }
        return b;
    }

    private int CraftedMemberSlots()
    {
        int s = 0;
        foreach (var id in CraftedIds)
        {
            var r = GuildCraftSystem.GetRecipe(id);
            if (r == null) continue;
            if (r.Effect == "member_slot") s += 1;
            else if (r.Effect == "guild_heart") s += 2;
        }
        return s;
    }

    private float CraftedBannerXpBonus() => CraftedIds
        .Select(GuildCraftSystem.GetRecipe)
        .Where(r => r?.Effect == "banner_xp_boost")
        .Sum(_ => 0.05f);
}

public class GuildManager
{
    public static GuildManager Instance { get; } = new();
    private static readonly Dictionary<int, Guild> _guilds = new();
    private static readonly Dictionary<ulong, int> _playerGuild = new();
    private static int _nextId = 1;
    public Action? OnChanged { get; set; }
    public List<int> DisbandedGuildIds { get; } = new();

    private void NotifyChanged() => OnChanged?.Invoke();

    public void ClearAll()
    {
        _guilds.Clear();
        _playerGuild.Clear();
        _nextId = 1;
    }

    public void RestoreGuild(Guild g)
    {
        _guilds[g.Id] = g;
        if (g.Id >= _nextId) _nextId = g.Id + 1;
        foreach (var sid in g.Members)
            _playerGuild[sid] = g.Id;
    }

    public void RestoreMemberMap(Dictionary<ulong, int> map)
    {
        foreach (var (sid, gid) in map)
            if (!_playerGuild.ContainsKey(sid) && _guilds.ContainsKey(gid))
                _playerGuild[sid] = gid;
    }

    public IReadOnlyDictionary<ulong, int> PlayerGuildMap => _playerGuild;

    public Guild? Create(CCSPlayerController leader, string name, string tag)
    {
        ulong sid = leader.SteamID;
        if (_playerGuild.ContainsKey(sid)) return null;
        if (_guilds.Values.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(g.Tag, tag, StringComparison.OrdinalIgnoreCase))) return null;
        var g = new Guild { Id = _nextId++, Name = name, Tag = tag, LeaderSteamId = sid };
        g.Members.Add(sid); g.Officers.Add(sid);
        _guilds[g.Id] = g; _playerGuild[sid] = g.Id;
        NotifyChanged();
        return g;
    }

    public bool Join(CCSPlayerController p, int guildId)
    {
        ulong sid = p.SteamID;
        if (_playerGuild.ContainsKey(sid)) return false;
        if (!_guilds.TryGetValue(guildId, out var g)) return false;
        if (g.Members.Count >= g.MaxMembers) return false;
        g.Members.Add(sid); _playerGuild[sid] = guildId;
        NotifyChanged();
        return true;
    }

    public bool Leave(CCSPlayerController p)
    {
        ulong sid = p.SteamID;
        if (!_playerGuild.TryGetValue(sid, out var gid)) return false;
        var g = _guilds[gid];

        // Find heir BEFORE removing the leader from Officers
        if (g.LeaderSteamId == sid)
        {
            var heir = g.Officers.FirstOrDefault(x => x != sid);
            if (heir == 0) heir = g.Members.FirstOrDefault(x => x != sid);
            if (heir == 0) { _guilds.Remove(gid); g.Members.Clear(); g.Officers.Clear(); DisbandedGuildIds.Add(gid); }
            else g.LeaderSteamId = heir;
        }

        g.Members.Remove(sid); g.Officers.Remove(sid);
        _playerGuild.Remove(sid);
        NotifyChanged();
        return true;
    }

    public Guild? Of(ulong steamId) =>
        _playerGuild.TryGetValue(steamId, out var gid) ? _guilds.GetValueOrDefault(gid) : null;

    public int OnlineMembers(Guild g, IEnumerable<CCSPlayerController> online) =>
        online.Count(p => p.IsValid && g.Members.Contains(p.SteamID));

    public float GuildRaceMultiplier(Guild g, int onlineMembers) =>
        1f + MathF.Min(0.40f, 0.04f * onlineMembers);

    public void AddBannerXp(Guild g, long xp)
    {
        g.BannerXp += (long)(xp * g.BannerXpMultiplier);
        while (g.BannerXp >= g.BannerXpNeeded) { g.BannerXp -= g.BannerXpNeeded; g.BannerLevel++; }
        NotifyChanged();
    }

    public IEnumerable<Guild> TopByBanner(int n) =>
        _guilds.Values.OrderByDescending(g => g.BannerLevel).ThenByDescending(g => g.BannerXp).Take(n);

    public IReadOnlyDictionary<int, Guild> All => _guilds;

    public static void EnsureLoaded() { /* in-memory store */ }
}

public static class GuildMenu
{
    public static void OpenMain(CCSPlayerController p, BasePlugin plugin, Database.IPlayerStore? store = null)
    {
        var g = GuildManager.Instance.Of(p.SteamID);
        var menu = new WasdMenu("Гильдия AETHERION", plugin);
        if (g == null)
        {
            menu.AddItem("Создать гильдию", (pl, _) => OpenCreate(pl, plugin));
            menu.AddItem("Вступить в гильдию (по ID)", (pl, _) => OpenJoin(pl, plugin));
            menu.AddItem("Рейтинг знамён", (pl, _) => OpenRating(pl, plugin));
        }
        else
        {
            menu.AddItem($"[{g.Tag}] {g.Name} ур. {g.Level}", null);
            menu.AddItem($"Казна: {g.Treasury}", null);
            menu.AddItem($"Члены: {g.Members.Count}/{g.MaxMembers}", null);
            menu.AddItem("Внести золото", (pl, _) => OpenDonate(pl, plugin, g));
            menu.AddItem("Усилить знамя (XP)", (pl, _) => BannerUp(pl, plugin, g, store));
            menu.AddItem("Покинуть гильдию", (pl, _) => Leave(pl, plugin, g, store));
            menu.AddItem("Рейтинг знамён", (pl, _) => OpenRating(pl, plugin));
        }
        menu.Display(p, 0);
    }

    private static void OpenCreate(CCSPlayerController p, BasePlugin plugin)
    {
        // Для простоты в CS2 консольный ввод — даём команду
        p.PrintToChat(" Создать: введи в чат !guild create <имя> <тег>, например: !guild create Эфир ЭФ");
    }

    private static void OpenJoin(CCSPlayerController p, BasePlugin plugin)
    {
        p.PrintToChat(" Вступить: введи в чат !guild join <id>, например: !guild join 3");
    }

    private static void OpenDonate(CCSPlayerController p, BasePlugin plugin, Guild g)
    {
        p.PrintToChat($" Внести золото: !guild donate <сумма>. Например: !guild donate 500. Казна: {g.Treasury}");
    }

    private static void BannerUp(CCSPlayerController p, BasePlugin plugin, Guild g, Database.IPlayerStore? store = null)
    {
        if (store == null) { p.PrintToChat(" Ошибка: хранилище данных недоступно."); return; }
        var d = store.Load(p.SteamID) ?? new Models.PlayerData { SteamId = p.SteamID, Name = p.PlayerName };
        long cost = 200 + g.BannerLevel * 50;
        if (!EconomySystem.SpendGold(d, cost)) { p.PrintToChat($" Нужно {cost} золота для укрепления знамени."); return; }
        long xpGain = 50 + g.BannerLevel * 10;
        GuildManager.Instance.AddBannerXp(g, xpGain);
        store.Save(d);
        p.PrintToChat($" Знамя укреплено! +{xpGain} XP → {g.BannerXp}/{g.BannerXpNeeded} (ур. {g.BannerLevel})");
    }

    private static void Leave(CCSPlayerController p, BasePlugin plugin, Guild g, Database.IPlayerStore? store)
    {
        if (GuildManager.Instance.Leave(p))
        {
            if (store != null)
            {
                var d = store.Load(p.SteamID);
                if (d != null) { d.GuildId = 0; store.Save(d); }
            }
            p.PrintToChat(" Ты покинул гильдию.");
        }
        else p.PrintToChat(" Не получилось.");
    }

    private static void OpenRating(CCSPlayerController p, BasePlugin plugin)
    {
        var list = GuildManager.Instance.TopByBanner(10).ToList();
        p.PrintToChat(" Рейтинг знамён:");
        int i = 1;
        foreach (var g in list)
        {
            p.PrintToChat($" {i++}. [{g.Tag}] {g.Name} ур. {g.BannerLevel} XP: {g.BannerXp}");
        }
    }

    public static void OnFrag(ulong steamId)
    {
        var g = GuildManager.Instance.Of(steamId);
        if (g == null) return;
        g.WeeklyFrags++;
        GuildManager.Instance.AddBannerXp(g, 5);
    }
}
