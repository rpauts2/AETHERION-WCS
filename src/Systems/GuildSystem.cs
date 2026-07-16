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
    public int Level => BannerLevel;
    public long Bank => Treasury;
    public long BannerXpNeeded => 5000L * BannerLevel * BannerLevel;
    public float GoldBonus => 0.02f * BannerLevel;
    public float XpBonus => 0.015f * BannerLevel;
    public int MaxMembers => 10 + BannerLevel * 2;
}

public class GuildManager
{
    public static GuildManager Instance { get; } = new();
    private static readonly Dictionary<int, Guild> _guilds = new();
    private static readonly Dictionary<ulong, int> _playerGuild = new();
    private static int _nextId = 1;
    public Action? OnChanged { get; set; }

    private void NotifyChanged() => OnChanged?.Invoke();

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
            if (!_playerGuild.ContainsKey(sid))
                _playerGuild[sid] = gid;
    }

    public IReadOnlyDictionary<ulong, int> PlayerGuildMap => _playerGuild;

    public Guild? Create(CCSPlayerController leader, string name, string tag)
    {
        ulong sid = leader.SteamID;
        if (_playerGuild.ContainsKey(sid)) return null;
        if (_guilds.Values.Any(g => g.Name == name || g.Tag == tag)) return null;
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
        g.Members.Remove(sid); g.Officers.Remove(sid);
        _playerGuild.Remove(sid);
        if (g.LeaderSteamId == sid)
        {
            var heir = g.Officers.FirstOrDefault(x => x != sid);
            if (heir == 0) heir = g.Members.FirstOrDefault();
            if (heir == 0) _guilds.Remove(gid);
            else g.LeaderSteamId = heir;
        }
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
        g.BannerXp += xp;
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
            menu.AddItem("Покинуть гильдию", (pl, _) => Leave(pl, plugin, g));
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
        long cost = 200;
        if (d.Gold < cost) { p.PrintToChat($" Нужно {cost} золота для укрепления знамени."); return; }
        d.Gold -= cost;
        GuildManager.Instance.AddBannerXp(g, 50);
        store.Save(d);
        p.PrintToChat($" Знамя укреплено! XP: {g.BannerXp}/{g.BannerXpNeeded} (ур. {g.BannerLevel})");
    }

    private static void Leave(CCSPlayerController p, BasePlugin plugin, Guild g)
    {
        if (GuildManager.Instance.Leave(p)) p.PrintToChat(" Ты покинул гильдию.");
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
