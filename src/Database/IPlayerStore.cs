using WcsInfinity.Models;

namespace WcsInfinity.Database;

public interface IPlayerStore
{
    void Init();
    PlayerData? Load(ulong steamId);
    void Save(PlayerData data);
}
