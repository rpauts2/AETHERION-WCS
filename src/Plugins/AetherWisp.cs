using System;
using System.Collections.Generic;
using System.Drawing;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace WcsInfinity.Plugins;

public enum WispMood { Calm, Happy, Excited, Worried, Furious }

public class Wisp
{
    public string Name = "Виспи";
    public int Stage = 1;
    public int Bond;
    public int Streak;
    public DateTime LastLine;
    public WispMood Mood = WispMood.Calm;
}

public class AetherWisp
{
    private readonly BasePlugin _plugin;
    public AetherWisp(BasePlugin plugin) { _plugin = plugin; }
    private readonly Dictionary<int, Wisp> _wisps = new();

    private static string MoodColor(WispMood m) => m switch
    {
        WispMood.Happy => "#7CFFA0",
        WispMood.Excited => "#FFD24D",
        WispMood.Worried => "#FF9D3D",
        WispMood.Furious => "#FF3B6B",
        _ => "#7C5CFF"
    };
    private static string MoodGlyph(WispMood m) => m switch
    {
        WispMood.Happy => "◕‿◕",
        WispMood.Excited => "★ω★",
        WispMood.Worried => "◑﹏◑",
        WispMood.Furious => "ﾉಠ益ಠ",
        _ => "◔ ◡ ◔"
    };

    public void Spawn(CCSPlayerController p)
    {
        Despawn(p);
        var w = new Wisp { Name = "Виспи" };
        _wisps[p.Slot] = w;
        Say(p, $"Привет, {p.PlayerName}! Я {w.Name}, твой дух Эфира.");
    }

    public void OnKill(CCSPlayerController p, bool headshot)
    {
        if (!_wisps.TryGetValue(p.Slot, out var w)) return;
        int oldBond = w.Bond;
        w.Streak++;
        w.Bond += headshot ? 2 : 1;

        var evolved = WispEvolution.CheckEvolve(oldBond, w.Bond);
        if (evolved != null)
        {
            w.Stage = evolved.Stage;
            w.Name = evolved.Title;
            p.PrintToChat($" [ДУХ ЭВОЛЮЦИОНИРОВАЛ] {evolved.Title}!");
            p.PrintToChat($" [{evolved.Title}] {evolved.UnlockLine}");
            SetMood(p, WispMood.Excited);
            return;
        }

        if (w.Streak >= 4) { SetMood(p, WispMood.Furious); Say(p, $"РАЗНОС! x{w.Streak} — Эфир боится тебя!"); }
        else if (headshot) { SetMood(p, WispMood.Excited); Say(p, "Точная мушка! Хедшот!"); }
        else if (w.Streak == 2) { SetMood(p, WispMood.Happy); Say(p, "Дабл — подбираем темп!"); }
    }

    public void RestoreBond(CCSPlayerController p, int bond)
    {
        if (!_wisps.TryGetValue(p.Slot, out var w)) return;
        w.Bond = bond;
        var st = WispEvolution.StageFor(bond);
        w.Stage = st.Stage; w.Name = st.Title;
    }

    public int Bond(CCSPlayerController p) => _wisps.TryGetValue(p.Slot, out var w) ? w.Bond : 0;

    public void OnDeath(CCSPlayerController p)
    {
        if (!_wisps.TryGetValue(p.Slot, out var w)) return;
        w.Streak = 0;
        SetMood(p, WispMood.Calm);
        Say(p, "Пауза. В следующем раунде снова в строю.");
    }

    public void Despawn(CCSPlayerController p)
    {
        _wisps.Remove(p.Slot);
    }

    private void SetMood(CCSPlayerController p, WispMood mood)
    {
        if (!_wisps.TryGetValue(p.Slot, out var w)) return;
        w.Mood = mood;
    }

    private void Say(CCSPlayerController p, string line)
    {
        if (!_wisps.TryGetValue(p.Slot, out var w)) return;
        if ((DateTime.UtcNow - w.LastLine).TotalSeconds < 2.5) return;
        w.LastLine = DateTime.UtcNow;
        p.PrintToChat($" [{w.Name}] {line}");
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
