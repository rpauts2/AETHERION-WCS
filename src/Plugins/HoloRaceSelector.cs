using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WcsInfinity.Plugins;

// ╔══════════════════════════════════════════════════════════════════╗
// ║  HOLO RACE SELECTOR — голографическое 3D-меню рас в мире.          ║
// ║  ПЕРВОЕ на Source 2: вместо плоского текста спавним point_worldtext║
// ║  дугой ПЕРЕД игроком. Игрок крутит обзор — подсвечивается раса,    ║
// ║  на которую он смотрит. Выбор по \"+use\". Настоящее 3D-меню в мире.║
// ╚══════════════════════════════════════════════════════════════════╝
public class HoloRaceSelector
{
    private readonly BasePlugin _plugin;
    public HoloRaceSelector(BasePlugin plugin) { _plugin = plugin; }

    public class HoloEntry
    {
        public int RaceId;
        public string Label = "";
        public bool Unlocked;
        public CPointWorldText? Entity;
        public Vector Pos = new();
    }

    // Активные голограммы по игроку
    private readonly Dictionary<int, List<HoloEntry>> _active = new();
    private readonly Dictionary<int, int> _hovered = new();

    // Открыть голо-меню: расставить расы дугой перед игроком
    public void Open(CCSPlayerController player, List<(int id, string name, bool unlocked)> races)
    {
        if (player?.PlayerPawn?.Value == null) return;
        Close(player);

        var pawn = player.PlayerPawn.Value;
        var eye = pawn.AbsOrigin!;
        var ang = pawn.EyeAngles;
        float yawBase = ang.Y;

        var list = new List<HoloEntry>();
        int n = Math.Min(races.Count, 7);              // дуга из 7 рас
        float spread = 70f;                             // градусов влево/вправо
        float radius = 120f;                            // дистанция от лица
        for (int i = 0; i < n; i++)
        {
            float frac = n == 1 ? 0.5f : (float)i / (n - 1);
            float yaw = yawBase - spread / 2f + spread * frac;
            float rad = yaw * MathF.PI / 180f;
            var pos = new Vector(
                eye.X + radius * MathF.Cos(rad),
                eye.Y + radius * MathF.Sin(rad),
                eye.Z + 55f);

            var (id, name, unlocked) = races[i];
            var ent = SpawnWorldText(
                (unlocked ? "» " : "🔒 ") + name,
                pos,
                new QAngle(0, yaw + 90f, 90f),
                unlocked ? Color.FromArgb(255,124,92,255) : Color.FromArgb(255,90,90,90));

            list.Add(new HoloEntry { RaceId = id, Label = name, Unlocked = unlocked, Entity = ent, Pos = pos });
        }
        _active[player.Slot] = list;
        _hovered[player.Slot] = -1;

        // тик-апдейт подсветки наведения
        StartHoverLoop(player);
    }

    private void StartHoverLoop(CCSPlayerController player)
    {
        _plugin.AddTimer(0.1f, () =>
        {
            if (!player.IsValid || !_active.ContainsKey(player.Slot)) return;
            UpdateHover(player);
            StartHoverLoop(player); // повторяем, пока меню открыто
        });
    }

    // Какая раса под прицелом (минимальный угол между взглядом и голограммой)
    private void UpdateHover(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn?.Value;
        if (pawn?.AbsOrigin == null) return;
        var eye = pawn.AbsOrigin;
        var ang = pawn.EyeAngles;
        var fwd = AngleToForward(ang);

        int best = -1; float bestDot = 0.93f; // порог ~21°
        var list = _active[player.Slot];
        for (int i = 0; i < list.Count; i++)
        {
            var to = Norm(new Vector(list[i].Pos.X - eye.X, list[i].Pos.Y - eye.Y, list[i].Pos.Z - (eye.Z + 55f)));
            float dot = fwd.X*to.X + fwd.Y*to.Y + fwd.Z*to.Z;
            if (dot > bestDot) { bestDot = dot; best = i; }
        }

        if (best != _hovered[player.Slot])
        {
            _hovered[player.Slot] = best;
            // переподсветка: наведённая раса ярче и крупнее
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e.Entity == null || !e.Entity.IsValid) continue;
                bool hov = i == best;
                var c = !e.Unlocked ? Color.FromArgb(255,90,90,90)
                       : hov ? Color.FromArgb(255,255,255,255)
                       : Color.FromArgb(255,124,92,255);
                e.Entity.Color = c;
                e.Entity.FontSize = hov ? 36 : 24;
                Utilities.SetStateChanged(e.Entity, "CPointWorldText", "m_Color");
            }
            if (best >= 0)
                player.PrintToCenterHtml($"<font color='#7c5cff'>Выбор расы:</font> <font color='#ffffff'>{list[best].Label}</font><br><font class='fontSize-sm' color='#aaaaaa'>Нажми E чтобы выбрать</font>");
        }
    }

    // Подтвердить выбор (вешается на +use). Возвращает выбранный RaceId или -1
    public int Confirm(CCSPlayerController player)
    {
        if (!_active.ContainsKey(player.Slot)) return -1;
        int h = _hovered.GetValueOrDefault(player.Slot, -1);
        if (h < 0) return -1;
        var entry = _active[player.Slot][h];
        Close(player);
        if (!entry.Unlocked)
        {
            player.PrintToChat(" [AETHER] Эта раса ещё закрыта — качай общий уровень!");
            return -1;
        }
        return entry.RaceId;
    }

    public void Close(CCSPlayerController player)
    {
        if (_active.TryGetValue(player.Slot, out var list))
        {
            foreach (var e in list)
                if (e.Entity != null && e.Entity.IsValid) e.Entity.Remove();
            _active.Remove(player.Slot);
        }
        _hovered.Remove(player.Slot);
    }

    // ── helpers ──────────────────────────────────────────────
    private CPointWorldText? SpawnWorldText(string text, Vector pos, QAngle ang, Color color)
    {
        var ent = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
        if (ent == null) return null;
        ent.MessageText = text;
        ent.Enabled = true;
        ent.FontSize = 24;
        ent.Color = color;
        ent.Fullbright = true;
        ent.WorldUnitsPerPx = 0.05f;
        ent.DepthOffset = 0.0f;
        ent.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER;
        ent.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;
        ent.Teleport(pos, ang, new Vector(0,0,0));
        ent.DispatchSpawn();
        return ent;
    }

    private static Vector AngleToForward(QAngle a)
    {
        float p = a.X * MathF.PI/180f, y = a.Y * MathF.PI/180f;
        return new Vector(MathF.Cos(p)*MathF.Cos(y), MathF.Cos(p)*MathF.Sin(y), -MathF.Sin(p));
    }
    private static Vector Norm(Vector v)
    {
        float m = MathF.Sqrt(v.X*v.X+v.Y*v.Y+v.Z*v.Z);
        return m < 1e-4f ? new Vector(0,0,0) : new Vector(v.X/m, v.Y/m, v.Z/m);
    }
}
