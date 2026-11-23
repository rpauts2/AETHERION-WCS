using WcsInfinity.Models;

namespace WcsInfinity.Systems;

// Золото за киллы (на него крутят рулетки). Отдельно от XP.
// Баланс v2: ускорен онбординг, чтобы рулетка/магазин были достижимы за сессию.
public static class EconomySystem
{
    public const long GoldKill = 25;
    public const long GoldHeadshot = 12;
    public const long GoldRoundWin = 20;
    public const long GoldBombObjective = 30;

    public static void AddGold(PlayerData p, long amount)
    {
        // Saturating addition prevents a very large grant from overflowing long and
        // accidentally turning the player's balance negative.
        long current = Math.Clamp(p.Gold, 0, 999999);
        long grant = Math.Max(0, amount);
        p.Gold = grant >= 999999 - current ? 999999 : current + grant;
        p.IsDirty = true;
    }

    public static bool SpendGold(PlayerData p, long amount)
    {
        if (amount <= 0 || p.Gold < amount) return false;
        p.Gold -= amount;
        p.IsDirty = true;
        return true;
    }
}