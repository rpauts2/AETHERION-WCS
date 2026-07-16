using System;
using System.Collections.Generic;
using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════╗
// ║  NAMEPLATE MANAGER — надпись над головой игрока (ФАЗА 2b).   ║
// ║  • point_worldtext парентится к павну и держится над головой.║
// ║  • Содержимое: имя · раса ур.N · дивизион · связь духа.      ║
// ║  • Цвет рендера = команда игрока (T — янтарь, CT — голубой). ║
// ║  • Обновление позиции по тику (парент + страховка ручной    ║
// ║    корректировки, если родитель сбросился при респавне).     ║
// ╚══════════════════════════════════════════════════════════════╝
public sealed class NameplateManager
{
    // Высота над макушкой павна (CS2 pawn ~72 юнита, корона ~75, неймплейт выше).
    private const float HeadOffsetZ = 92f;

    // Угол: текст лежит горизонтально лицом вверх — читается сверху и спереди,
    // стабильно виден с любой стороны (маркерный стиль CS2).
    private static readonly QAngle PlateAngle = new(90f, 0f, 0f);

    private sealed class Plate
    {
        public CPointWorldText? Entity;
        public CCSPlayerPawn? BoundPawn;
        public string Html = "";
        public Color Color = Color.White;
    }

    private readonly Dictionary<int, Plate> _plates = new();

    // Создать/пересоздать неймплейт для игрока. html — уже сформированная разметка.
    public void Attach(CCSPlayerController p, string html)
    {
        if (p == null || !p.IsValid) return;
        var pawn = p.PlayerPawn?.Value;
        if (pawn == null || pawn.AbsOrigin == null) return;

        Remove(p.Slot);

        var ent = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
        if (ent == null) return;

        var color = TeamColor(p.TeamNum);
        ent.MessageText = html;
        ent.Enabled = true;
        ent.FontSize = 18;
        ent.Color = color;
        ent.Fullbright = true;
        ent.WorldUnitsPerPx = 0.04f;
        ent.DepthOffset = 0f;
        ent.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER;
        ent.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;

        var o = pawn.AbsOrigin;
        ent.Teleport(new Vector(o.X, o.Y, o.Z + HeadOffsetZ), PlateAngle, new Vector(0, 0, 0));
        ent.DispatchSpawn();

        // Парентим к павну — текст едет вместе с игроком автоматически.
        try { ent.AcceptInput("SetParent", pawn, null, "!activator"); } catch (Exception ex) { Console.WriteLine($"[Nameplate] attach err: {ex.Message}"); }

        _plates[p.Slot] = new Plate
        {
            Entity = ent,
            BoundPawn = pawn,
            Html = html,
            Color = color
        };
    }

    // Обновить только текст (левелап, смена расы) — без пересоздания сущности.
    public void UpdateText(int slot, string html)
    {
        if (!_plates.TryGetValue(slot, out var plate) || plate.Entity == null || !plate.Entity.IsValid) return;
        plate.Html = html;
        try
        {
            plate.Entity.MessageText = html;
            Utilities.SetStateChanged(plate.Entity, "CPointWorldText", "m_MessageText");
        }
        catch (Exception ex) { Console.WriteLine($"[Nameplate] text update err: {ex.Message}"); }
    }

    // Обновить цвет по текущей команде (после смены команды).
    public void Recolor(int slot, int team)
    {
        if (!_plates.TryGetValue(slot, out var plate) || plate.Entity == null || !plate.Entity.IsValid) return;
        var c = TeamColor(team);
        plate.Color = c;
        try
        {
            plate.Entity.Color = c;
            Utilities.SetStateChanged(plate.Entity, "CPointWorldText", "m_Color");
        }
        catch (Exception ex) { Console.WriteLine($"[Nameplate] color update err: {ex.Message}"); }
    }

    // Тиковая страховка: если парент потерян (респавн), поднимаем текст обратно над головой.
    public void Tick()
    {
        foreach (var kv in _plates)
        {
            var slot = kv.Key;
            var plate = kv.Value;
            var ent = plate.Entity;
            if (ent == null || !ent.IsValid)
            {
                _plates.Remove(slot);
                continue;
            }
            var controller = Utilities.GetPlayerFromSlot(slot);
            var pawn = controller?.PlayerPawn?.Value;
            if (controller == null || !controller.IsValid || pawn == null || pawn.AbsOrigin == null || !controller.PawnIsAlive)
            {
                // мёртвый/ушёл — гасим текст, но кэш держим до респавна/Remove
                try { ent.Enabled = false; } catch (Exception ex) { Console.WriteLine($"[Nameplate] disable err: {ex.Message}"); }
                continue;
            }
            try { ent.Enabled = true; } catch (Exception ex) { Console.WriteLine($"[Nameplate] enable err: {ex.Message}"); }

            // Если родитель «слетел» — перепарентить и выставить позицию вручную.
            if (plate.BoundPawn == null || !plate.BoundPawn.IsValid || plate.BoundPawn != pawn)
            {
        try { ent.AcceptInput("SetParent", pawn, null, "!activator"); } catch (Exception ex) { Console.WriteLine($"[Nameplate] SetParent err: {ex.Message}"); }
                plate.BoundPawn = pawn;
            }
            var o = pawn.AbsOrigin;
            try
            {
                ent.Teleport(new Vector(o.X, o.Y, o.Z + HeadOffsetZ), PlateAngle, new Vector(0, 0, 0));
            }
            catch (Exception ex) { Console.WriteLine($"[Nameplate] teleport err: {ex.Message}"); }
        }
    }

    public void Remove(int slot)
    {
        if (_plates.TryGetValue(slot, out var plate))
        {
            if (plate.Entity != null && plate.Entity.IsValid)
                try { plate.Entity.Remove(); } catch (Exception ex) { Console.WriteLine($"[Nameplate] remove err: {ex.Message}"); }
            _plates.Remove(slot);
        }
    }

    public void Clear()
    {
        foreach (var slot in _plates.Keys.ToArray()) Remove(slot);
    }

    private static Color TeamColor(int team) => team switch
    {
        3 => Color.FromArgb(255, 110, 200, 255),   // CT — голубой лёд
        2 => Color.FromArgb(255, 255, 170, 70),    // T  — янтарь
        _ => Color.FromArgb(255, 200, 200, 200),   // спектаторы/прочие — серый
    };
}
