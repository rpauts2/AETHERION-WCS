using WcsInfinity.Models;

namespace WcsInfinity.Database;

public interface IPlayerStore
{
    void Init();
    PlayerData? Load(ulong steamId);
    void Save(PlayerData data);
    List<(ulong SteamId, string Name, long TotalXp, int TopLevel, long Gold)> TopPlayers(int count, string sortField = "level");
}
