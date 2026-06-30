using System;
using System.Reflection;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WcsInfinity.Plugins;

// ╔══════════════════════════════════════════════════════════════════════╗
// ║  AETHER NEXUS — первый «диегетический» интерфейс WCS на Source 2.      ║
// ║  Вместо плоских чат-команд игрок входит в ПЕРСОНАЛЬНЫЙ 3D-зал,         ║
// ║  построенный из point_worldtext + beam + particle прямо в мире игры.  ║
// ║  Навигация взглядом + E. UI встроен в мир (как в VR-играх).           ║
// ║                                                                        ║
// ║  Это КОРНЕВАЯ система UI — голо-меню рас, рулетка, магазин, знамя      ║
// ║  гильдии становятся «секторами» Nexus.                                ║
// ╚══════════════════════════════════════════════════════════════════════╝
public enum NexusSector { Hub, Races, Shop, Roulette, Guild, Season }

public class NexusNode
{
    public string Label = "";
    public NexusSector? GoTo;        // сектор, в который ведёт узел (если это портал)
    public Action<CCSPlayerController>? OnSelect;
    public bool Enabled = true;
    public CPointWorldText? Text;
    public Vector Pos = new();
    public Color BaseColor = Color.FromArgb(255,124,92,255);
}

public class NexusSession
{
    public NexusSector Current = NexusSector.Hub;
    public List<NexusNode> Nodes = new();
    public int Hovered = -1;
    public Vector Center = new();
    public float BaseYaw;
    public bool Open;
}

public class AetherNexus
{

    // ФИКС API-рассинхрона (305 compile / 369 runtime): EyeAngles переехал с CCSPlayerPawnBase.
    // Рефлексия находит геттер на актуальном рантайм-типе, без жёсткой привязки при компиляции.
    private static QAngle? EyeOf(CCSPlayerPawn pawn)
    {
        try { return pawn.GetType().GetProperty("EyeAngles")?.GetValue(pawn) as QAngle; }
        catch { return null; }
    }

    private readonly BasePlugin _plugin;
    public AetherNexus(BasePlugin plugin) { _plugin = plugin; }

    private readonly Dictionary<int, NexusSession> _sessions = new();

    // Поставщики контента секторов (заполняются главным плагином):
    public Func<CCSPlayerController, List<NexusNode>>? RacesProvider;
    public Func<CCSPlayerController, List<NexusNode>>? ShopProvider;
    public Func<CCSPlayerController, List<NexusNode>>? GuildProvider;
    public Func<CCSPlayerController, List<NexusNode>>? SeasonProvider;
    public Action<CCSPlayerController>? RouletteAction;

    public bool IsOpen(CCSPlayerController p) => _sessions.TryGetValue(p.Slot, out var s) && s.Open;

    public void Toggle(CCSPlayerController p)
    {
        if (IsOpen(p)) Close(p); else Open(p, NexusSector.Hub);
    }

    public void Open(CCSPlayerController player, NexusSector sector)
    {
        var pawn = player?.PlayerPawn?.Value;
        if (pawn?.AbsOrigin == null) return;
        ClearEntities(player!);

        var s = _sessions.GetValueOrDefault(player.Slot) ?? new NexusSession();
        s.Current = sector;
        s.Center = new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z);
        var __eyeA = EyeOf(pawn);
        s.BaseYaw = __eyeA?.Y ?? 0f;
        s.Hovered = -1;
        s.Open = true;
        s.Nodes = BuildSector(player, sector);
        _sessions[player.Slot] = s;

        SpawnNodes(player, s);
        player.PrintToCenterHtml(SectorTitle(sector));
        if (s.Nodes.Count > 0) HoverLoop(player);
    }

    private List<NexusNode> BuildSector(CCSPlayerController p, NexusSector sector)
    {
        switch (sector)
        {
            case NexusSector.Hub:
                return new()
                {
                    new NexusNode{ Label="🧬 РАСЫ",    GoTo=NexusSector.Races },
                    new NexusNode{ Label="🛒 МАГАЗИН", GoTo=NexusSector.Shop },
                    new NexusNode{ Label="🎰 РУЛЕТКА", GoTo=NexusSector.Roulette },
                    new NexusNode{ Label="🛡️ ГИЛЬДИЯ", GoTo=NexusSector.Guild },
                    new NexusNode{ Label="🏆 СЕЗОН",   GoTo=NexusSector.Season },
                };
            case NexusSector.Races:    return RacesProvider?.Invoke(p) ?? Empty();
            case NexusSector.Shop:     return ShopProvider?.Invoke(p) ?? Empty();
            case NexusSector.Guild:    return GuildProvider?.Invoke(p) ?? Empty();
            case NexusSector.Season:   return SeasonProvider?.Invoke(p) ?? Empty();
            case NexusSector.Roulette:
                return new()
                {
                    new NexusNode{ Label="▶ КРУТИТЬ", OnSelect = pl => RouletteAction?.Invoke(pl) },
                    new NexusNode{ Label="↩ НАЗАД",   GoTo=NexusSector.Hub },
                };
        }
        return Empty();
    }

    private static List<NexusNode> Empty() =>
        new() { new NexusNode{ Label="↩ НАЗАД", GoTo=NexusSector.Hub } };

    // Расставляем узлы дугой/кольцом перед игроком
    private void SpawnNodes(CCSPlayerController player, NexusSession s)
    {
        int n = s.Nodes.Count;
        float spread = MathF.Min(140f, 26f * n);   // ширина дуги растёт с кол-вом
        float radius = 140f;
        for (int i = 0; i < n; i++)
        {
            float frac = n == 1 ? 0.5f : (float)i / (n - 1);
            float yaw = s.BaseYaw - spread/2f + spread*frac;
            float rad = yaw * MathF.PI/180f;
            // лёгкая «волна» по высоте для объёма
            float zWave = MathF.Sin(frac * MathF.PI) * 14f;
            var pos = new Vector(
                s.Center.X + radius*MathF.Cos(rad),
                s.Center.Y + radius*MathF.Sin(rad),
                s.Center.Z + 50f + zWave);
            var node = s.Nodes[i];
            node.Pos = pos;
            node.Text = SpawnText(node.Label, pos, new QAngle(0, yaw+90f, 90f),
                node.Enabled ? node.BaseColor : Color.FromArgb(255,90,90,90));
        }
    }

    private void HoverLoop(CCSPlayerController player)
    {
        _plugin.AddTimer(0.1f, () =>
        {
            if (!player.IsValid || !IsOpen(player)) return;
            UpdateHover(player);
            HoverLoop(player);
        });
    }

    private void UpdateHover(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn?.Value;
        if (pawn?.AbsOrigin == null) return;
        var s = _sessions[player.Slot];
        var eye = new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z + 55f);
        var __eyeB = EyeOf(pawn);
        if (__eyeB == null) return;
        var fwd = Fwd(__eyeB);

        int best=-1; float bestDot=0.94f;
        for (int i=0;i<s.Nodes.Count;i++)
        {
            var nd=s.Nodes[i];
            var to=Norm(new Vector(nd.Pos.X-eye.X, nd.Pos.Y-eye.Y, nd.Pos.Z-eye.Z));
            float dot=fwd.X*to.X+fwd.Y*to.Y+fwd.Z*to.Z;
            if (dot>bestDot){bestDot=dot;best=i;}
        }
        if (best!=s.Hovered)
        {
            s.Hovered=best;
            for (int i=0;i<s.Nodes.Count;i++)
            {
                var nd=s.Nodes[i];
                if (nd.Text==null||!nd.Text.IsValid) continue;
                bool hov=i==best;
                nd.Text.Color = !nd.Enabled ? Color.FromArgb(255,90,90,90)
                              : hov ? Color.FromArgb(255,255,255,255) : nd.BaseColor;
                nd.Text.FontSize = hov?40:26;
                Utilities.SetStateChanged(nd.Text,"CPointWorldText","m_Color");
            }
            if (best>=0)
                player.PrintToCenterHtml($"{SectorTitle(s.Current)}<br><font color='#ffffff'>{s.Nodes[best].Label}</font><br><font class='fontSize-sm' color='#aaaaaa'>E — выбрать</font>");
        }
    }

    // Подтверждение (на +use). Переходит в сектор или выполняет действие.
    public void Confirm(CCSPlayerController player)
    {
        if (!IsOpen(player)) return;
        var s=_sessions[player.Slot];
        if (s.Hovered<0) return;
        var nd=s.Nodes[s.Hovered];
        if (!nd.Enabled){ player.PrintToChat(" [NEXUS] Заблокировано."); return; }
        if (nd.GoTo.HasValue){ Open(player, nd.GoTo.Value); return; }
        nd.OnSelect?.Invoke(player);
    }

    public void Close(CCSPlayerController player)
    {
        ClearEntities(player);
        if (_sessions.TryGetValue(player.Slot, out var s)) s.Open=false;
    }

    private void ClearEntities(CCSPlayerController player)
    {
        if (_sessions.TryGetValue(player.Slot, out var s))
            foreach (var nd in s.Nodes)
                if (nd.Text!=null && nd.Text.IsValid) nd.Text.Remove();
    }

    private static string SectorTitle(NexusSector s) => s switch
    {
        NexusSector.Hub      => "<font class='fontSize-l' color='#7c5cff'>✦ AETHER NEXUS ✦</font>",
        NexusSector.Races    => "<font class='fontSize-l' color='#7c5cff'>🧬 ДРЕВО РАС</font>",
        NexusSector.Shop     => "<font class='fontSize-l' color='#7c5cff'>🛒 ВИТРИНА ЭФИРА</font>",
        NexusSector.Roulette => "<font class='fontSize-l' color='#7c5cff'>🎰 БАРАБАН СУДЬБЫ</font>",
        NexusSector.Guild    => "<font class='fontSize-l' color='#7c5cff'>🛡️ ЗАЛ ГИЛЬДИИ</font>",
        NexusSector.Season   => "<font class='fontSize-l' color='#7c5cff'>🏆 ПЬЕДЕСТАЛ СЕЗОНА</font>",
        _=>""
    };

    private CPointWorldText? SpawnText(string text, Vector pos, QAngle ang, Color color)
    {
        var ent=Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
        if (ent==null) return null;
        ent.MessageText=text; ent.Enabled=true; ent.FontSize=26; ent.Color=color;
        ent.Fullbright=true; ent.WorldUnitsPerPx=0.05f; ent.DepthOffset=0f;
        ent.JustifyHorizontal=PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER;
        ent.JustifyVertical=PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;
        ent.Teleport(pos, ang, new Vector(0,0,0));
        ent.DispatchSpawn();
        return ent;
    }

    private static Vector Fwd(QAngle a)
    {
        float p=a.X*MathF.PI/180f, y=a.Y*MathF.PI/180f;
        return new Vector(MathF.Cos(p)*MathF.Cos(y), MathF.Cos(p)*MathF.Sin(y), -MathF.Sin(p));
    }
    private static Vector Norm(Vector v)
    {
        float m=MathF.Sqrt(v.X*v.X+v.Y*v.Y+v.Z*v.Z);
        return m<1e-4f?new Vector(0,0,0):new Vector(v.X/m,v.Y/m,v.Z/m);
    }
}