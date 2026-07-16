using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WcsInfinity.Systems;

public sealed class SubTickBudgetConfig
{
    public double MaxMsPerTick { get; set; } = 3.0;
    public bool YieldOnOverrun { get; set; } = true;
    public int MaxItemsPerTick { get; set; } = 500;
    public float DriverIntervalSec { get; set; } = 0.05f;
}

public enum SubTickStatus { Ok, Yielded, Exhausted }

public sealed class SubTickMetrics
{
    public long TotalTicks;
    public long YieldedTicks;
    public long ExhaustedTicks;
    public long WorstTickUs;
    public long PendingAtLastTick;
    public long TotalSlotsProcessed;
}

public sealed class SubTickWorkItem
{
    public int Id { get; set; }
    public Action<int>? Action { get; set; }
    public int Priority { get; set; }
    public int? PlayerSlot { get; set; }
}

public sealed class SubTickSafeLoop
{
    private readonly BasePlugin _plugin;
    private readonly SubTickBudgetConfig _config;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly Dictionary<int, SubTickWorkItem> _byId = new();
    private readonly PriorityQueue<SubTickWorkItem, int> _queue = new();
    private readonly SubTickMetrics _metrics = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _driver;
    private int _nextId = 1;
    private volatile bool _running;

    public SubTickMetrics Metrics => _metrics;
    public int PendingCount => _queue.Count;
    public bool IsRunning => _running;

    public SubTickSafeLoop(BasePlugin plugin, SubTickBudgetConfig? config = null)
    {
        _plugin = plugin;
        _config = config ?? new SubTickBudgetConfig();
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _driver = _plugin.AddTimer(_config.DriverIntervalSec, Tick, TimerFlags.REPEAT);
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _driver?.Kill();
        _driver = null;
    }

    public int Enqueue(Action<int> action, int priority = 0, int? playerSlot = null)
    {
        int id = Interlocked.Increment(ref _nextId);
        if (id <= 0) id = Interlocked.Exchange(ref _nextId, 1) + 1;
        var item = new SubTickWorkItem
        {
            Id = id,
            Action = action,
            Priority = -priority,
            PlayerSlot = playerSlot
        };
        _byId[id] = item;
        _queue.Enqueue(item, item.Priority);
        return id;
    }

    public bool Cancel(int id)
    {
        if (_byId.Remove(id))
        {
            RebuildQueue();
            return true;
        }
        return false;
    }

    public void Clear()
    {
        _byId.Clear();
        while (_queue.Count > 0) _queue.Dequeue();
    }

    public IEnumerable<int> GetPendingForPlayer(int playerSlot)
    {
        var res = new List<int>();
        foreach (var kv in _byId.Values)
            if (kv.PlayerSlot == playerSlot) res.Add(kv.Id);
        foreach (var it in _queue.UnorderedItems)
            if (it.Element.PlayerSlot == playerSlot) res.Add(it.Element.Id);
        return res;
    }

    private void Tick()
    {
        if (!_running) return;
        _metrics.TotalTicks++;
        _metrics.PendingAtLastTick = _queue.Count;

        double maxUs = _config.MaxMsPerTick * 1000.0;
        int processed = 0;
        SubTickStatus status = SubTickStatus.Ok;

        while (_queue.Count > 0 && processed < _config.MaxItemsPerTick)
        {
            if (_sw.Elapsed.TotalMilliseconds * 1000.0 >= maxUs)
            {
                status = _config.YieldOnOverrun ? SubTickStatus.Yielded : SubTickStatus.Exhausted;
                break;
            }

            var item = _queue.Dequeue();
            if (!_byId.TryGetValue(item.Id, out var live) || live.Action == null)
            {
                _byId.Remove(item.Id);
                continue;
            }

            try
            {
                _sw.Restart();
                live.Action(item.PlayerSlot ?? item.Id);
                long us = (long)(_sw.Elapsed.TotalMilliseconds * 1000.0);
                if (us > _metrics.WorstTickUs) _metrics.WorstTickUs = us;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SubTickWork] id={item.Id} err: {ex.Message}");
            }
            finally
            {
                _byId.Remove(item.Id);
            }

            processed++;
        }

        _metrics.TotalSlotsProcessed += processed;
        if (status == SubTickStatus.Yielded) _metrics.YieldedTicks++;
        if (status == SubTickStatus.Exhausted) _metrics.ExhaustedTicks++;
    }

    private void RebuildQueue()
    {
        var temp = new List<(SubTickWorkItem item, int priority)>();
        while (_queue.Count > 0)
        {
            var it = _queue.Dequeue();
            if (_byId.ContainsKey(it.Id))
                temp.Add((it, it.Priority));
        }
        temp.Sort((a, b) => a.priority.CompareTo(b.priority));
        foreach (var (item, p) in temp)
            _queue.Enqueue(item, p);
    }
}
