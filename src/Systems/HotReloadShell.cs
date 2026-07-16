using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WcsInfinity.Systems;

public struct HotReloadSnapshot
{
    public string Id { get; init; }
    public string Name { get; init; }
    public long Ticks { get; init; }
    public string Path { get; init; }
}

public sealed class HotReloadGroup
{
    public string Name { get; set; } = "";
    public List<string> SystemNames { get; set; } = new();
}

public sealed class HotReloadShell : IDisposable
{
    private readonly BasePlugin _plugin;
    private readonly string _dropDir;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _watcher;
    private readonly Dictionary<string, (string path, DateTime ts)> _inventory = new();
    private readonly Dictionary<string, object> _liveInstances = new();
    private readonly object _gate = new();
    private long _gen;
    private bool _running;

    public event Action<string, string>? OnChanged;

    public HotReloadShell(BasePlugin plugin, string dropDir)
    {
        _plugin = plugin;
        _dropDir = dropDir;
        Directory.CreateDirectory(dropDir);
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _watcher = _plugin.AddTimer(2.0f, Poll, TimerFlags.REPEAT);
    }

    public void Stop()
    {
        _running = false;
        _watcher?.Kill();
        _watcher = null;
    }

    public void Track(string name, string dllPath)
    {
        if (!File.Exists(dllPath)) return;
        lock (_gate) _inventory[name] = (dllPath, File.GetLastWriteTimeUtc(dllPath));
    }

    public IReadOnlyList<HotReloadSnapshot> Inventory
    {
        get
        {
            lock (_gate)
            {
                return _inventory.Select(kv => new HotReloadSnapshot
                {
                    Id = $"{kv.Key}:{Interlocked.Read(ref _gen)}",
                    Name = kv.Key,
                    Ticks = Interlocked.Read(ref _gen),
                    Path = kv.Value.path
                }).OrderBy(s => s.Name).ToList();
            }
        }
    }

    public bool Load(string name)
    {
        string path;
        lock (_gate)
        {
            if (!_inventory.TryGetValue(name, out var entry)) return false;
            path = entry.path;
        }

        lock (_gate)
        {
            if (_liveInstances.ContainsKey(name)) return false;
        }

        try
        {
            object instance = Activator.CreateInstance(Type.GetType("WcsInfinity.HotReloadBootstrap, WcsInfinity.HotReloadBootstrap") ?? typeof(object))!;
            _liveInstances[name] = instance;
            AddLocalizationKeysForReload(name);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HotReloadShell] Load {name} err: {ex.Message}");
            return false;
        }
    }

    public bool Unload(string name)
    {
        lock (_gate)
        {
            if (!_liveInstances.TryGetValue(name, out var inst)) return false;
            try { if (inst is IDisposable d) d.Dispose(); } catch { }
            _liveInstances.Remove(name);
            Interlocked.Increment(ref _gen);
            return true;
        }
    }

    public bool Rollback(string snapshotId)
    {
        if (string.IsNullOrEmpty(snapshotId)) return false;
        var parts = snapshotId.Split(':');
        if (parts.Length < 2) return false;
        return Load(parts[0]);
    }

    private void Poll()
    {
        if (!_running) return;
        try
        {
            var files = Directory.GetFiles(_dropDir, "*.dll");
            bool changed = false;

            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                DateTime w = File.GetLastWriteTimeUtc(file);
                bool newOrChanged = true;

                lock (_gate)
                {
                    if (_inventory.TryGetValue(name, out var entry))
                    {
                        if (entry.ts >= w) newOrChanged = false;
                        else { _inventory[name] = (file, w); changed = true; }
                    }
                    else
                    {
                        _inventory[name] = (file, w);
                        changed = true;
                    }
                }

                if (newOrChanged)
                    OnChanged?.Invoke(name, file);
            }

            if (changed) Interlocked.Increment(ref _gen);
        }
        catch { }
    }

    public void Dispose()
    {
        Stop();
        lock (_gate)
        {
            foreach (var kv in _liveInstances)
            {
                try { if (kv.Value is IDisposable d) d.Dispose(); } catch { }
            }
            _liveInstances.Clear();
        }
    }

    private void AddLocalizationKeysForReload(string name)
    {
        // kept as a placeholder so later localization bootstrap can hook reload events
        Console.WriteLine($"[HotReloadShell] Reload ready: {name}");
    }
}
