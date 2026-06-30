using System;
using System.Collections.Generic;
using System.Text;
using WcsInfinity.Systems;

namespace WcsInfinity.UI;

// Элемент колеса
public class WheelItem
{
    public string Label = "";
    public string Icon = "•";
    public char Kind = 'A';   // A=способность, S=магазин, R=рулетка, C=смена расы, X=закрыть
    public int AbilityIndex = -1;
    public string Color = "#dddddd";
}

// Состояние колеса игрока
public class WheelState
{
    public bool Open;
    public List<WheelItem> Items = new();
    public int Hover;
    public float BaseYaw;
    public float OpenedAt;
}

// Рендер радиального меню в center-HTML.
// Ограничение Source 2: только subset HTML (font color/class, br). Идеального круга нет —
// делаем "компас" из 8 слотов, наведённый сектор подсвечиваем золотом.
public static class AetherWheel
{
    private const string Gold = "#ffcc33";
    private const string Dim  = "#6a6a6a";
    private const string Acc  = "#7d5cff";
    private const string Cyan = "#39d3ff";

    // 8 позиций по часовой от верхней
    public static string Render(WheelState s, string raceName, int ether, int etherMax)
    {
        int n = s.Items.Count;
        // подготовим 8 ячеек (по количеству предметов, остальные пустые)
        string[] cell = new string[8];
        for (int i = 0; i < 8; i++)
        {
            if (i < n)
            {
                var it = s.Items[i];
                bool hot = (i == s.Hover);
                string col = hot ? Gold : (it.Color ?? Dim);
                string txt = it.Icon + " " + it.Label;
                cell[i] = hot ? $"<font color='{Gold}'>▸{txt}◂</font>" : $"<font color='{col}'>{txt}</font>";
            }
            else cell[i] = "";
        }

        var sb = new StringBuilder();
        sb.Append($"<font class='fontSize-l' color='{Acc}'>◈ {L10n.Get("UI_AetherWheel_Title", "AETHER WHEEL")} ◈</font><br>");
        sb.Append($"<font color='#bbbbbb'>{raceName}</font><br><br>");

        // компас: индексы 0=N,1=NE,2=E,3=SE,4=S,5=SW,6=W,7=NW
        sb.Append(Center(cell[0]) + "<br>");
        sb.Append(Pair(cell[7], cell[1]) + "<br>");
        string center = "<font color='" + Cyan + "'>◆</font>";
        sb.Append(Triple(cell[6], center, cell[2]) + "<br>");
        sb.Append(Pair(cell[5], cell[3]) + "<br>");
        sb.Append(Center(cell[4]) + "<br><br>");

        // полоса Эфира
        int bars = etherMax > 0 ? (int)Math.Round(10.0 * ether / etherMax) : 0;
        bars = Math.Clamp(bars, 0, 10);
        string bar = new string('█', bars) + new string('░', 10 - bars);
        sb.Append(L10n.GetF("UI_AetherWheel_EtherBar", "Эфир [{0}] {1}/{2}", ("bar", bar), ("ether", ether), ("max", etherMax)) ?? $"Эфир [{bar}] {ether}/{etherMax}");
        sb.Append(L10n.Get("UI_AetherWheel_ControlsHint", "взгляд = выбор · css_wheel = подтвердить · R = закрыть") ?? "взгляд = выбор · css_wheel = подтвердить · R = закрыть");
        return sb.ToString();
    }

    private static string Center(string c) => string.IsNullOrEmpty(c) ? "&#8203;" : c;
    private static string Pair(string l, string r)
        => (string.IsNullOrEmpty(l) ? "" : l) + "  &#160;&#160;&#160;  " + (string.IsNullOrEmpty(r) ? "" : r);
    private static string Triple(string l, string c, string r)
        => (string.IsNullOrEmpty(l) ? "" : l + " &#160; ") + c + (string.IsNullOrEmpty(r) ? "" : " &#160; " + r);
}
