using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WcsInfinity.Models;

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
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS players (
            steam_id INTEGER PRIMARY KEY,
            name TEXT,
            data TEXT NOT NULL
        );";
        cmd.ExecuteNonQuery();
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
}
