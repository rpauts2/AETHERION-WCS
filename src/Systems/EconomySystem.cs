using WcsInfinity.Models;

namespace WcsInfinity.Systems;

// Золото за киллы (на него крутят рулетки). Отдельно от XP.
// Баланс v2: ускорен онбординг, чтобы рулетка/магазин были достижимы за сессию.
public static class EconomySystem
{
    public const long GoldKill = 25;          // было 10
    public const long GoldHeadshot = 12;      // бонус за хедшот (было 6)
    public const long GoldKnife = 30;         // бонус за нож
    public const long GoldAssist = 8;         // ассист
    public const long GoldFirstBlood = 40;    // первая кровь раунда
    public const long GoldRoundWin = 20;      // было 8
    public const long GoldRoundLossConsolation = 6; // утешительные проигравшим
    public const long GoldBombObjective = 30;  // плант/дефьюз (было 12)

    public static void AddGold(PlayerData p, long amount) => p.Gold += amount;
}