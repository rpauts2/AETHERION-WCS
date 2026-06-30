using System;
using System.Collections.Generic;
using System.Linq;
using WcsInfinity.Races;
using WcsInfinity.Systems;

namespace WcsInfinity.Systems;

// 🛡️ AETHER COMMAND — креативная админка («режиссёрский пульт»).
// Не банальный ban/kick, а инструмент анти-чита и модерации с фишками,
// которых нет у конкурентов на CS2.
public enum AdminRole { None, Helper, Moderator, Admin, Owner }

public class AdminSession
{
    public ulong SteamId { get; set; }
    public AdminRole Role { get; set; }
    public bool SpectatorSpirit { get; set; }    // режим "Дух наблюдателя"
    public int WatchingSlot { get; set; } = -1;  // за кем следим
}

// Подозрительная статистика для анти-чит помощника
public class SuspicionReport
{
    public int Slot { get; set; }
    public string Name { get; set; } = "";
    public double Accuracy { get; set; }
    public double HeadshotRatio { get; set; }
    public double KdRatio { get; set; }
    public int FlagScore { get; set; }     // 0-100, чем выше — тем подозрительнее
    public List<string> Flags { get; set; } = new();
}

public class AetherCommand
{
    private readonly Dictionary<ulong, AdminRole> _roles = new();
    private readonly Dictionary<ulong, AdminSession> _sessions = new();

    public void SetRole(ulong steamId, AdminRole role) => _roles[steamId] = role;
    public AdminRole RoleOf(ulong steamId) => _roles.GetValueOrDefault(steamId, AdminRole.None);
    public bool Can(ulong steamId, AdminRole min) => RoleOf(steamId) >= min;

    // === Дух наблюдателя: невидимый облёт игроков с подсветкой ===
    public void EnterSpirit(ulong adminSteamId, IEngineApi engine, int adminSlot)
    {
        if (!Can(adminSteamId, AdminRole.Moderator)) return;
        var s = _sessions.GetValueOrDefault(adminSteamId) ?? new AdminSession { SteamId = adminSteamId, Role = RoleOf(adminSteamId) };
        s.SpectatorSpirit = true;
        _sessions[adminSteamId] = s;
        // делаем невидимым + бесшумным + свободная камера (noclip)
        engine.PrintToCenter(adminSlot, L10n.Get("Systems_AetherCommand_WatchMode", "👁 Режим Духа: !watch <id>, !back"));
    }

    public void WatchPlayer(ulong adminSteamId, int targetSlot, IEngineApi engine, int adminSlot)
    {
        if (!_sessions.TryGetValue(adminSteamId, out var s) || !s.SpectatorSpirit) return;
        s.WatchingSlot = targetSlot;
        var pos = engine.GetPosition(targetSlot);
        engine.Teleport(adminSlot, pos.x, pos.y, pos.z + 80); // зависаем над целью
    }

    // === Анти-чит помощник: эвристики подозрительности ===
    public SuspicionReport Analyze(int slot, string name, double accuracy, double hsRatio, double kd, int snapKills)
    {
        var r = new SuspicionReport { Slot=slot, Name=name, Accuracy=accuracy, HeadshotRatio=hsRatio, KdRatio=kd };
        if (accuracy > 0.55) { r.FlagScore += 35; r.Flags.Add("Аномальная точность"); }
        if (hsRatio > 0.75)  { r.FlagScore += 35; r.Flags.Add("Слишком много хедшотов"); }
        if (kd > 6.0)        { r.FlagScore += 15; r.Flags.Add("Экстремальный KD"); }
        if (snapKills > 3)   { r.FlagScore += 15; r.Flags.Add("Серия мгновенных наводок (возможно aim)"); }
        r.FlagScore = Math.Min(100, r.FlagScore);
        return r;
    }

    // === "Клетка Эфира": тихая заморозка подозреваемого до решения ===
    public string EtherCage(ulong adminSteamId, int targetSlot, IEngineApi engine)
    {
        if (!Can(adminSteamId, AdminRole.Moderator)) return "Нет прав.";
        engine.SetSpeed(targetSlot, 0f);
        engine.SpawnParticle("particles/ether_cage.vpcf", 0,0,0);
        return "🔒 Игрок помещён в Клетку Эфира (заморожен).";
    }

    public void ExitSpirit(ulong adminSteamId)
    {
        if (_sessions.TryGetValue(adminSteamId, out var s)) s.SpectatorSpirit = false;
    }
}
