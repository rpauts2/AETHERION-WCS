using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WcsInfinity.Plugins;

public sealed class VoteOption
{
    public string LocKey { get; set; } = "";
    public string Text { get; set; } = "";
    public Action OnWin { get; set; } = () => { };
    public int TieBreakPriority { get; set; }
}

public sealed class VoteHost : IDisposable
{
    public interface IPanoramaApi
    {
        void SendVoteConfig(int channelId, VoteConfig cfg);
        void ShowYesNoChannel(int channelId, string question, int durationSec);
        void ClearChannel(int channelId);
    }

    private int _nextChannel = 1;
    private readonly Dictionary<int, VoteSession> _sessions = new();
    private readonly object _gate = new();
    private readonly BasePlugin _plugin;
    private IPanoramaApi _panorama = null!;
    private bool _disposed;

    public IPanoramaApi Panorama
    {
        set { _panorama = value; }
        get => _panorama;
    }

    public VoteHost(BasePlugin plugin)
    {
        _plugin = plugin;
        _panorama = new NullPanoramaApi();
    }

    public int Start(VoteConfig cfg, int durationSec = 20)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(VoteHost));
        if (cfg == null) throw new ArgumentNullException(nameof(cfg));

        int channelId = Interlocked.Increment(ref _nextChannel);
        var session = new VoteSession
        {
            ChannelId = channelId,
            Config = cfg,
            Votes = cfg.Options.ToDictionary(o => o, _ => 0),
            EndUtc = DateTime.UtcNow.AddSeconds(durationSec),
            YesNo = cfg.YesNo,
            DurationSec = durationSec,
            Closed = false
        };

        lock (_gate) { _sessions[channelId] = session; }

        _panorama.SendVoteConfig(channelId, cfg);

        if (session.YesNo)
        {
            _panorama.ShowYesNoChannel(channelId, cfg.Title, durationSec);
        }
        else
        {
            _plugin.AddTimer(durationSec, () => End(channelId));
        }

        return channelId;
    }

    public void ReceiveClientVotes(int channelId, Dictionary<int, int> votes)
    {
        if (votes == null) return;
        VoteSession? session;
        lock (_gate) _sessions.TryGetValue(channelId, out session);
        if (session == null || session.Closed) return;

        DateTime utcNow = DateTime.UtcNow;
        if (utcNow >= session.EndUtc) { End(channelId); return; }

        foreach (var kv in votes)
        {
            if (kv.Key < 0 || kv.Key >= session.Config.Options.Count) continue;
            var opt = session.Config.Options[kv.Key];
            session.Votes[opt] += kv.Value;
            session.TotalVotes += kv.Value;
        }
    }

    public void End(int channelId)
    {
        VoteSession? session;
        lock (_gate) _sessions.TryGetValue(channelId, out session);
        if (session == null || session.Closed) return;
        session.Closed = true;

        if (_disposed) return;

        try { _panorama.ClearChannel(channelId); } catch (Exception ex) { Console.WriteLine($"[VOTE] clear channel err: {ex.Message}"); }

        if (session.TotalVotes == 0)
        {
            Console.WriteLine("[VOTE] Голосование не состоялось.");
            return;
        }

        var winner = session.Votes
            .OrderByDescending(kv => kv.Value)
            .ThenByDescending(kv => kv.Key.TieBreakPriority)
            .FirstOrDefault();

        if (winner.Key != null)
        {
            Console.WriteLine($"[VOTE] Результат: {winner.Key.Text} ({winner.Value}/{session.TotalVotes})");
            try { winner.Key.OnWin(); } catch (Exception ex) { Console.WriteLine($"[VOTE] OnWin err: {ex.Message}"); }
        }
    }

    public string DumpResults(int channelId)
    {
        VoteSession? session;
        lock (_gate) _sessions.TryGetValue(channelId, out session);
        if (session == null || session.Closed) return "— нет активного голосования —";

        var parts = session.Config.Options
            .Select(o => $"{o.Text}: {session.Votes.GetValueOrDefault(o, 0)}");
        return string.Join(" | ", parts);
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_gate)
        {
            foreach (var channel in _sessions.Keys.ToList())
            {
                try { End(channel); } catch (Exception ex) { Console.WriteLine($"[VOTE] dispose cleanup err: {ex.Message}"); }
            }
            _sessions.Clear();
        }
        _disposed = true;
    }

    private sealed class NullPanoramaApi : IPanoramaApi
    {
        public void ClearChannel(int channelId) { }
        public void SendVoteConfig(int channelId, VoteConfig cfg) { }
        public void ShowYesNoChannel(int channelId, string question, int durationSec) { }
    }

    private sealed class VoteSession
    {
        public int ChannelId;
        public VoteConfig Config;
        public Dictionary<VoteOption, int> Votes = new();
        public int TotalVotes;
        public DateTime EndUtc;
        public bool YesNo;
        public int DurationSec;
        public bool Closed;
    }
}

public sealed class VoteConfig
{
    public string Title { get; set; } = "";
    public bool YesNo { get; set; }
    public List<VoteOption> Options { get; set; } = new();
    public Dictionary<string, string> Meta { get; set; } = new();
}
