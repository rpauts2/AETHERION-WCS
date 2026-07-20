using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WcsInfinity.Models;
using WcsInfinity.Systems;

namespace WcsInfinity.Database;

// Хранение профилей. Прогресс по тысячам рас сериализуется в JSON-колонку
// (компактно и масштабируемо). Для продакшена легко заменить на MySQL.
public class SqlitePlayerStore : IPlayerStore
{
    private readonly string _connStr;
    public SqlitePlayerStore(string dbPath) => _connStr = $"Data Source={dbPath}";

    public void Init()
    {
        using var con = new SqliteConnection(_connStr);
        con.Open();

        string[] creates = {
            @"CREATE TABLE IF NOT EXISTS players (
                steam_id INTEGER PRIMARY KEY,
                name TEXT,
                data TEXT NOT NULL
            );",
            @"CREATE TABLE IF NOT EXISTS guilds (
                id INTEGER PRIMARY KEY,
                name TEXT NOT NULL,
                tag TEXT NOT NULL,
                leader_id INTEGER NOT NULL,
                members TEXT NOT NULL DEFAULT '[]',
                officers TEXT NOT NULL DEFAULT '[]',
                treasury INTEGER NOT NULL DEFAULT 0,
                banner_level INTEGER NOT NULL DEFAULT 1,
                banner_xp INTEGER NOT NULL DEFAULT 0,
                weekly_frags INTEGER NOT NULL DEFAULT 0
            );",
            @"CREATE TABLE IF NOT EXISTS guild_members (
                steam_id INTEGER PRIMARY KEY,
                guild_id INTEGER NOT NULL
            );"
        };
        foreach (var sql in creates)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }

    public PlayerData? Load(ulong steamId)
    {
        using var con = new SqliteConnection(_connStr);
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT data FROM players WHERE steam_id=$id";
        cmd.Parameters.AddWithValue("$id", (long)steamId);
        var res = cmd.ExecuteScalar() as string;
        return res == null ? null : JsonSerializer.Deserialize<PlayerData>(res);
    }

    public void Save(PlayerData data)
    {
        using var con = new SqliteConnection(_connStr);
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"INSERT INTO players (steam_id, name, data) VALUES ($id,$n,$d)
            ON CONFLICT(steam_id) DO UPDATE SET name=$n, data=$d";
        cmd.Parameters.AddWithValue("$id", (long)data.SteamId);
        cmd.Parameters.AddWithValue("$n", data.Name);
        cmd.Parameters.AddWithValue("$d", JsonSerializer.Serialize(data));
        cmd.ExecuteNonQuery();
    }

    // ── Guild persistence ────────────────────────────────────────────────
    public List<Guild> LoadAllGuilds()
    {
        var result = new List<Guild>();
        using var con = new SqliteConnection(_connStr);
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT id,name,tag,leader_id,members,officers,treasury,banner_level,banner_xp,weekly_frags FROM guilds";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var g = new Guild
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                Tag = r.GetString(2),
                LeaderSteamId = (ulong)r.GetInt64(3),
                Members = JsonSerializer.Deserialize<List<ulong>>(r.GetString(4)) ?? new(),
                Officers = JsonSerializer.Deserialize<List<ulong>>(r.GetString(5)) ?? new(),
                Treasury = r.GetInt64(6),
                BannerLevel = r.GetInt32(7),
                BannerXp = r.GetInt64(8),
                WeeklyFrags = r.GetInt32(9)
            };
            result.Add(g);
        }
        return result;
    }

    public void SaveGuild(Guild g)
    {
        using var con = new SqliteConnection(_connStr);
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"INSERT INTO guilds (id,name,tag,leader_id,members,officers,treasury,banner_level,banner_xp,weekly_frags)
            VALUES ($id,$name,$tag,$leader,$members,$officers,$treasury,$bl,$bx,$wf)
            ON CONFLICT(id) DO UPDATE SET name=$name,tag=$tag,leader_id=$leader,members=$members,officers=$officers,
            treasury=$treasury,banner_level=$bl,banner_xp=$bx,weekly_frags=$wf";
        cmd.Parameters.AddWithValue("$id", g.Id);
        cmd.Parameters.AddWithValue("$name", g.Name);
        cmd.Parameters.AddWithValue("$tag", g.Tag);
        cmd.Parameters.AddWithValue("$leader", (long)g.LeaderSteamId);
        cmd.Parameters.AddWithValue("$members", JsonSerializer.Serialize(g.Members));
        cmd.Parameters.AddWithValue("$officers", JsonSerializer.Serialize(g.Officers));
        cmd.Parameters.AddWithValue("$treasury", g.Treasury);
        cmd.Parameters.AddWithValue("$bl", g.BannerLevel);
        cmd.Parameters.AddWithValue("$bx", g.BannerXp);
        cmd.Parameters.AddWithValue("$wf", g.WeeklyFrags);
        cmd.ExecuteNonQuery();
    }

    public void DeleteGuild(int guildId)
    {
        using var con = new SqliteConnection(_connStr);
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "DELETE FROM guilds WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", guildId);
        cmd.ExecuteNonQuery();
    }

    public Dictionary<ulong, int> LoadGuildMembers()
    {
        var result = new Dictionary<ulong, int>();
        using var con = new SqliteConnection(_connStr);
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT steam_id,guild_id FROM guild_members";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result[(ulong)r.GetInt64(0)] = r.GetInt32(1);
        return result;
    }

    public void SaveGuildMembership(ulong steamId, int guildId)
    {
        using var con = new SqliteConnection(_connStr);
        con.Open();
        using var cmd = con.CreateCommand();
        if (guildId <= 0)
        {
            cmd.CommandText = "DELETE FROM guild_members WHERE steam_id=$sid";
            cmd.Parameters.AddWithValue("$sid", (long)steamId);
        }
        else
        {
            cmd.CommandText = @"INSERT INTO guild_members (steam_id,guild_id) VALUES ($sid,$gid)
                ON CONFLICT(steam_id) DO UPDATE SET guild_id=$gid";
            cmd.Parameters.AddWithValue("$sid", (long)steamId);
            cmd.Parameters.AddWithValue("$gid", guildId);
        }
        cmd.ExecuteNonQuery();
    }

    public void SaveAllGuilds(IEnumerable<Guild> guilds, IReadOnlyDictionary<ulong, int> memberMap)
    {
        using var con = new SqliteConnection(_connStr);
        con.Open();
        using var tx = con.BeginTransaction();
        using (var del = con.CreateCommand()) { del.CommandText = "DELETE FROM guilds"; del.Transaction = tx; del.ExecuteNonQuery(); }
        using (var del2 = con.CreateCommand()) { del2.CommandText = "DELETE FROM guild_members"; del2.Transaction = tx; del2.ExecuteNonQuery(); }
        foreach (var g in guilds)
        {
            using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO guilds (id,name,tag,leader_id,members,officers,treasury,banner_level,banner_xp,weekly_frags)
                VALUES ($id,$name,$tag,$leader,$members,$officers,$treasury,$bl,$bx,$wf)";
            cmd.Parameters.AddWithValue("$id", g.Id);
            cmd.Parameters.AddWithValue("$name", g.Name);
            cmd.Parameters.AddWithValue("$tag", g.Tag);
            cmd.Parameters.AddWithValue("$leader", (long)g.LeaderSteamId);
            cmd.Parameters.AddWithValue("$members", JsonSerializer.Serialize(g.Members));
            cmd.Parameters.AddWithValue("$officers", JsonSerializer.Serialize(g.Officers));
            cmd.Parameters.AddWithValue("$treasury", g.Treasury);
            cmd.Parameters.AddWithValue("$bl", g.BannerLevel);
            cmd.Parameters.AddWithValue("$bx", g.BannerXp);
            cmd.Parameters.AddWithValue("$wf", g.WeeklyFrags);
            cmd.ExecuteNonQuery();
        }
        foreach (var (sid, gid) in memberMap)
        {
            using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO guild_members (steam_id,guild_id) VALUES ($sid,$gid)";
            cmd.Parameters.AddWithValue("$sid", (long)sid);
            cmd.Parameters.AddWithValue("$gid", gid);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    // ── Leaderboard ──────────────────────────────────────────────────────
    public List<(ulong SteamId, string Name, long TotalXp, int TopLevel, long Gold, long Kills)> TopPlayers(int count, string sortField = "level")
    {
        var result = new List<(ulong, string, long, int, long, long)>();
        using var con = new SqliteConnection(_connStr);
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT steam_id, name, data FROM players";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            try
            {
                ulong sid = (ulong)r.GetInt64(0);
                string name = r.GetString(1);
                var pd = JsonSerializer.Deserialize<PlayerData>(r.GetString(2));
                if (pd == null) continue;
                long totalXp = 0;
                int topLevel = 0;
                foreach (var rp in pd.Races.Values)
                {
                    totalXp += rp.Xp;
                    topLevel = Math.Max(topLevel, rp.Level);
                }
                long kills = pd.QuestCounters.GetValueOrDefault("kills", 0);
                result.Add((sid, name, totalXp, topLevel, pd.Gold, kills));
            }
            catch { }
        }
        return sortField switch
        {
            "gold" => result.OrderByDescending(x => x.Item5).Take(count).ToList(),
            "xp" => result.OrderByDescending(x => x.Item3).Take(count).ToList(),
            "kills" => result.OrderByDescending(x => x.Item6).Take(count).ToList(),
            _ => result.OrderByDescending(x => x.Item4).ThenByDescending(x => x.Item3).Take(count).ToList()
        };
    }
}
