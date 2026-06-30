using System.Text;
using WcsInfinity.Models;
using WcsInfinity.Races;
using WcsInfinity.Systems;
using WcsInfinity.UI;

namespace WcsInfinity.UI;

// AETHER HUD — креативное меню на center-html.
// Рисует "карточки рас" псевдографикой + Unicode-иконки + живую полоску Эфира.
// Обход ограничения CS2 (нет VGUI): максимально используем CenterHtml.
public static class AetherHud
{
    public static string WispStatusPreview(string tier, int bond)
    {
        var tierText = L10n.GetF("WispCompanion_Preview",
            "[АЭТЕРИОН] Дух {Tier} — связь {Bond}",
            ("Tier", tier), ("Bond", bond.ToString()));
        return tierText ?? $"[АЭТЕРИОН] Дух {tier} — связь {bond}";
    }
    // Цвета HTML, поддерживаемые center-html в CS2
    private const string Gold = "#FFD200";
    private const string Cyan = "#46E0FF";
    private const string Gray = "#9AA0A6";
    private const string Green = "#7CFF6B";

    // Главная карточка текущей расы (показывается по !wcs)
    public static string RaceCard(RaceDefinition def, RaceProgress rp, PlayerData p, int ether, int etherMax)
    {
        var sb = new StringBuilder();
        sb.Append($"<font color='{Gold}' class='fontSize-l'>⚡ {def.Name}</font><br>");
        sb.Append($"<font color='{Gray}' class='fontSize-s'>{TierStars(def.Tier)} · {L10n.GetF("UI_AetherHud_RaceCard_DivisionLabel", "Круг {0}", ("div", def.Division))}</font><br>");
        sb.Append($"<font color='{Cyan}'>{L10n.GetF("UI_AetherHud_RaceCard_Level", "Уровень {0}/{1}", ("level", rp.Level), ("max", LevelMax(def.Tier)))}</font>");
        if (rp.ParagonLevel > 0) sb.Append($" <font color='{Gold}'>★{rp.ParagonLevel}</font>");
        sb.Append("<br>");
        // XP-бар
        sb.Append($"<font color='{Green}'>{Bar(rp.Xp, NextLevelXp(rp, def), 14)}</font><br>");
        // Эфир-бар (живой ресурс)
        sb.Append($"<font color='{Cyan}'>{L10n.GetF("UI_AetherHud_RaceCard_Ether", "Эфир {0}", ("bar", Bar(ether, etherMax, 14)))}</font><br>");
        // Общий уровень игрока (сумма всех рас) + прогресс до следующего Круга
        long total = UnlockSystem.TotalLevel(p);
        int curDiv = UnlockSystem.DivisionForTotalLevel(total);
        long[] __t = {0,200,500,1000,1800,3000,4500,7000}; long nextThr = curDiv < 8 ? __t[curDiv] : 0L;
        sb.Append($"<font color='{Gold}'>{L10n.GetF("UI_AetherHud_RaceCard_TotalLevel", "Σ Общий уровень: {0}", ("total", total))}</font>");
        if (nextThr > 0) sb.Append($" <font color='{Gray}' class='fontSize-s'>{L10n.GetF("UI_AetherHud_RaceCard_ProgressHint", "(до Круга {0}: {1})", ("circle", curDiv + 1), ("needed", nextThr - total))}</font>");
        sb.Append("<br>");
        sb.Append($"<font color='{Gray}' class='fontSize-s'>{L10n.GetF("UI_AetherHud_RaceCard_GoldPoints", "Золото: {0} · Очки: {1}", ("gold", p.Gold), ("points", rp.UnspentPoints))}</font>");
        return sb.ToString();
    }

    // Список выбора рас (радиальное меню 1-9)
    public static string RaceList(IEnumerable<RaceDefinition> races, int currentId)
    {
        var sb = new StringBuilder();
        sb.Append($"<font color='{Gold}' class='fontSize-l'>ВЫБОР РАСЫ</font><br>");
        int i = 1;
        foreach (var r in races)
        {
            string col = r.Id == currentId ? Gold : Gray;
            string mark = r.Id == currentId ? "▶" : $"{i}.";
            sb.Append($"<font color='{col}'>{mark} {r.Name} {TierStars(r.Tier)}</font><br>");
            if (i++ >= 9) break;
        }
        sb.Append($"<font color='{Cyan}' class='fontSize-s'>Жми 1-9 для выбора</font>");
        return sb.ToString();
    }

    private static string TierStars(int tier) => new string('★', System.Math.Min(tier,5)) + new string('☆', System.Math.Max(0, 5 - tier));
    private static int LevelMax(int tier) => 1000; // глобальный кап
    private static long NextLevelXp(RaceProgress rp, RaceDefinition def) => 90 + (long)(rp.Level * 90 * 0.06) * def.Division;

    // ASCII прогресс-бар: ▰▰▰▱▱
    private static string Bar(long cur, long max, int width)
    {
        if (max <= 0) max = 1;
        int filled = (int)System.Math.Round((double)cur / max * width);
        filled = System.Math.Clamp(filled, 0, width);
        return new string('▰', filled) + new string('▱', width - filled);
    }
}