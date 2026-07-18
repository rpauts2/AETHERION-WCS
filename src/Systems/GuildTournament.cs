using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════╗
// ║  GUILD TOURNAMENT — гильдейские турниры и 1v1 вызовы.            ║
// ║  • !gt create <name> — создать турнир                           ║
// ║  • !gt join <id> — зарегистрировать гильдию                     ║
// ║  • !gt start <id> — начать (админ)                              ║
// ║  • !gt 1v1 <player> — вызов игрока из другой гильдии            ║
// ║  Автоматический подсчёт фрагов, награды + знамя.               ║
// ╚══════════════════════════════════════════════════════════════════╝
public enum TournamentState { Idle, Registration, Running, Finished }

public class GuildTournament
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime StartUtc { get; set; }
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(30);
    public TournamentState State { get; set; } = TournamentState.Idle;

    public List<Guild> SideA { get; set; } = new();
    public List<Guild> SideB { get; set; } = new();

    public int ScoreA { get; private set; }
    public int ScoreB { get; private set; }

    public int PrizeGold { get; set; } = 500;
    public int PrizeXp { get; set; } = 200;

    public TimeSpan TimeLeft => State == TournamentState.Running
        ? StartUtc + Duration - DateTime.UtcNow : TimeSpan.Zero;

    public bool IsActive => State == TournamentState.Running;

    public void Start()
    {
        State = TournamentState.Running;
        StartUtc = DateTime.UtcNow;
        ScoreA = 0; ScoreB = 0;
        Server.PrintToChatAll($"\x06[AETHERION] ⚔ Турнир «{Name}» начался! {SideA.Count} vs {SideB.Count} гильдий.");
    }

    public void Finish()
    {
        State = TournamentState.Finished;
        var winner = ScoreA >= ScoreB ? SideA : SideB;
        var loser = ScoreA >= ScoreB ? SideB : SideA;
        string winnerName = ScoreA >= ScoreB ? "Команда A" : "Команда B";
        foreach (var g in winner)
        {
            GuildManager.Instance.AddBannerXp(g, PrizeXp);
            g.Treasury += PrizeGold;
        }
        foreach (var g in loser)
            GuildManager.Instance.AddBannerXp(g, PrizeXp / 2);
        Server.PrintToChatAll($"\x06[AETHERION] 🏆 Турнир «{Name}» завершён! Победа {winnerName} ({ScoreA}:{ScoreB}) — +{PrizeGold}з +{PrizeXp}XP гильдиям!");
    }

    public void Register(Guild g)
    {
        if (SideA.Any(x => x.Id == g.Id) || SideB.Any(x => x.Id == g.Id)) return;
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

// 1v1 Дуэль между игроками из разных гильдий
public class Duel1v1
{
    public ulong Challenger;
    public ulong Opponent;
    public string ChallengerName = "";
    public string OpponentName = "";
    public int ChallengerWins;
    public int OpponentWins;
    public int MaxRounds = 5;
    public bool Active;
    public DateTime CreatedUtc = DateTime.UtcNow;
}

public static class GuildTournamentSystem
{
    private static readonly Dictionary<int, GuildTournament> _tournaments = new();
    private static int _nextId = 1;

    // 1v1 дуэли
    private static Duel1v1? _pendingDuel;
    private static readonly Dictionary<ulong, int> _duelWins = new();

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

    public static GuildTournament? Get(int id) => _tournaments.TryGetValue(id, out var t) ? t : null;

    public static GuildTournament? ActiveTournament => _tournaments.Values.FirstOrDefault(t => t.IsActive);

    public static void OnRoundEnd()
    {
        var t = ActiveTournament;
        if (t == null) return;
        if (t.TimeLeft <= TimeSpan.Zero) t.Finish();
    }

    public static void OnKill(CCSPlayerController killer)
    {
        ActiveTournament?.OnFrag(killer);
    }

    // ── 1v1 ДУЭЛИ ──

    public static bool ChallengeDuel(CCSPlayerController challenger, CCSPlayerController opponent)
    {
        if (challenger.SteamID == opponent.SteamID) return false;
        if (_pendingDuel != null && _pendingDuel.Active)
        {
            challenger.PrintToChat(" \x07Уже идёт дуэль!"); return false;
        }

        _pendingDuel = new Duel1v1
        {
            Challenger = challenger.SteamID,
            Opponent = opponent.SteamID,
            ChallengerName = challenger.PlayerName,
            OpponentName = opponent.PlayerName,
            Active = false
        };

        opponent.PrintToChat($"\x06⚔ {challenger.PlayerName} вызывает тебя на дуэль! !duel accept");
        challenger.PrintToChat($"\x06⚔ Ожидание ответа от {opponent.PlayerName}...");
        return true;
    }

    public static bool AcceptDuel(CCSPlayerController player)
    {
        if (_pendingDuel == null || _pendingDuel.Opponent != player.SteamID) return false;
        _pendingDuel.Active = true;
        _pendingDuel.ChallengerWins = 0;
        _pendingDuel.OpponentWins = 0;
        Server.PrintToChatAll($"\x06⚔ ДУЭЛЬ: {_pendingDuel.ChallengerName} vs {_pendingDuel.OpponentName}! Best of {_pendingDuel.MaxRounds}!");
        return true;
    }

    public static void OnDuelKill(CCSPlayerController killer, CCSPlayerController victim)
    {
        if (_pendingDuel == null || !_pendingDuel.Active) return;
        if (killer.SteamID == _pendingDuel.Challenger && victim.SteamID == _pendingDuel.Opponent)
            _pendingDuel.ChallengerWins++;
        else if (killer.SteamID == _pendingDuel.Opponent && victim.SteamID == _pendingDuel.Challenger)
            _pendingDuel.OpponentWins++;
        else return;

        int winsNeeded = (_pendingDuel.MaxRounds + 1) / 2;
        Server.PrintToChatAll($"\x09⚔ Счёт: {_pendingDuel.ChallengerName} {_pendingDuel.ChallengerWins}:{_pendingDuel.OpponentWins} {_pendingDuel.OpponentName}");

        if (_pendingDuel.ChallengerWins >= winsNeeded || _pendingDuel.OpponentWins >= winsNeeded)
        {
            string winner = _pendingDuel.ChallengerWins > _pendingDuel.OpponentWins
                ? _pendingDuel.ChallengerName : _pendingDuel.OpponentName;
            Server.PrintToChatAll($"\x06🏆 {winner} победил в дуэли!");
            _duelWins[killer.SteamID] = _duelWins.GetValueOrDefault(killer.SteamID, 0) + 1;
            _pendingDuel = null;
        }
    }

    public static void ShowTournaments(CCSPlayerController p)
    {
        var open = _tournaments.Values.Where(t => t.State == TournamentState.Registration).ToList();
        p.PrintToChat(" \x0B═══ ТУРНИРЫ ═══");
        if (open.Count == 0) p.PrintToChat(" Нет открытых турниров.");
        foreach (var t in open)
            p.PrintToChat($" #{t.Id} «{t.Name}» | {t.SideA.Count + t.SideB.Count} гильдий | Осталось: {t.TimeLeft.Minutes}м");
    }

    public static void ShowDuelStats(CCSPlayerController p)
    {
        int wins = _duelWins.GetValueOrDefault(p.SteamID, 0);
        p.PrintToChat(" \x0B═══ ДУЭЛИ ═══");
        p.PrintToChat($" Побед: {wins}");
        if (_pendingDuel != null && _pendingDuel.Active)
            p.PrintToChat($" ⚔ Идёт: {_pendingDuel.ChallengerName} vs {_pendingDuel.OpponentName}");
        else
            p.PrintToChat(" Используй !duel <игрок> для вызова");
    }
}
