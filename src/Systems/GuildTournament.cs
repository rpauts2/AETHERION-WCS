using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WcsInfinity.Systems;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════╗
// ║  GUILD TOURNAMENT — локальные турниры гильдий.                  ║
// ║  Собирает двух гильдий в «матче» на本轮, подсчитывает фраги     ║
// ║  членов команд, в конце выдает призы и начисляет очки знамени   ║
// ╚══════════════════════════════════════════════════════════════════╝
public enum TournamentState { Idle, Registration, Running, Finished }

public class GuildTournament
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime StartUtc { get; set; }
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(30);
    public TournamentState State { get; set; } = TournamentState.Idle;

    // Команды
    public List<Guild> SideA { get; set; } = new();
    public List<Guild> SideB { get; set; } = new();

    // Статус
    public int ScoreA { get; private set; }
    public int ScoreB { get; private set; }

    public TimeSpan TimeLeft => State == TournamentState.Running
        ? StartUtc + Duration - DateTime.UtcNow : TimeSpan.Zero;

    public bool IsActive => State == TournamentState.Running;

    public void Start()
    {
        State = TournamentState.Running;
        StartUtc = DateTime.UtcNow;
        ScoreA = 0; ScoreB = 0;
    }

    public void Finish()
    {
        State = TournamentState.Finished;
        // Награды: знамя победителей
        var winner = ScoreA >= ScoreB ? SideA : SideB;
        foreach (var g in winner)
            GuildManager.Instance.AddBannerXp(g, 500L);
    }

    public void Register(Guild g)
    {
        if (SideA.Count <= SideB.Count) SideA.Add(g); else SideB.Add(g);
    }

    public void OnFrag(CCSPlayerController p)
    {
        if (State != TournamentState.Running) return;
        var sid = p.SteamID;
        if (SideA.Any(g => g.Members.Contains(sid))) ScoreA++;
        else if (SideB.Any(g => g.Members.Contains(sid))) ScoreB++;
    }
}

public static class GuildTournamentSystem
{
    private static readonly Dictionary<int, GuildTournament> _tournaments = new();
    private static int _nextId = 1;

    public static GuildTournament Create(string name, TimeSpan? duration = null)
    {
        var t = new GuildTournament
        {
            Id = _nextId++,
            Name = name,
            Duration = duration ?? TimeSpan.FromMinutes(30)
        };
        _tournaments[t.Id] = t;
        return t;
    }

    public static GuildTournament? ActiveTournament => _tournaments.Values.FirstOrDefault(t => t.IsActive);

    public static void OnRoundEnd()
    {
        var t = ActiveTournament;
        if (t == null) return;
        if (t.TimeLeft <= TimeSpan.Zero) t.Finish();
    }
}
