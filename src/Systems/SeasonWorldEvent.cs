using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WcsInfinity.Models;
using WcsInfinity.Races;

namespace WcsInfinity.Systems;

// Сезонные world-events: действуют глобально, усиливают/ослабляют механики фракций
public class SeasonWorldEvent
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public int SeasonId { get; set; }
    public string EffectKey { get; set; } = string.Empty; // ключ в SeasonSystem modifiers
    public object? Param { get; set; }
    public bool Active { get; set; }
    public int StagesCompleted { get; set; }
    public int StagesTotal { get; set; }
    public double RewardsMultiplier { get; set; } = 1.0;
}

public static class SeasonWorldEventSystem
{
    private static readonly Dictionary<int, SeasonWorldEvent> _events = new();
    private static readonly List<int> _activeNow = new();

    public static void Register(SeasonWorldEvent ev)
    {
        _events[ev.Id] = ev;
        Refresh();
    }

    public static void Refresh()
    {
        var now = DateTimeOffset.UtcNow;
        _activeNow.Clear();
        foreach (var ev in _events.Values)
        {
            ev.Active = now >= ev.Start && now <= ev.End;
            if (ev.Active) _activeNow.Add(ev.Id);
        }
    }

    public static IEnumerable<SeasonWorldEvent> Active() => _activeNow.Select(id => _events[id]);

    public static void AdvanceStage(int eventId)
    {
        if (!_events.TryGetValue(eventId, out var ev)) return;
        ev.StagesCompleted = Math.Min(ev.StagesCompleted + 1, ev.StagesTotal);
        if (ev.StagesCompleted >= ev.StagesTotal && ev.RewardsMultiplier < 2.0)
        {
            ev.RewardsMultiplier = Math.Min(2.0, ev.RewardsMultiplier + 0.25);
            Server.PrintToChatAll($" \x09[AETHERION] Событие \"{ev.Name}\" перешло на новый этап. Бонус: x{ev.RewardsMultiplier:0.00}");
        }
    }
}
