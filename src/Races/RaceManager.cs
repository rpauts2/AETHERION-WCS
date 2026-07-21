using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WcsInfinity.Races;

// Реестр всех рас. Грузит races.json и (в будущем) поведенческие хуки.
public class RaceManager
{
    private readonly Dictionary<int, RaceDefinition> _races = new();
    private readonly List<string> _loadedPaths = new();
    private readonly Dictionary<int, string> _sourcePaths = new();

    public IReadOnlyDictionary<int, RaceDefinition> Races => _races;

    public void LoadFromFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            if (!_loadedPaths.Contains(path)) _loadedPaths.Add(path);
            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<RaceDefinition>>(json) ?? new();
            foreach (var r in list)
            {
                r.IndexAbilities();
                _races[r.Id] = r;
                _sourcePaths[r.Id] = path;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AETHERION] Ошибка загрузки рас из {path}: {ex.Message}");
        }
    }

    public void LoadFromMultiple(string[] paths)
    {
        foreach (var p in paths) LoadFromFile(p);
    }

    public RaceDefinition? Get(int id) => _races.TryGetValue(id, out var r) ? r : null;

    public IEnumerable<RaceDefinition> RacesForDivision(int division) =>
        _races.Values.Where(r => r.Division <= division).OrderBy(r => r.Id);

    public int Count => _races.Count;

    public List<RaceDefinition> AllD3Plus() => _races.Values.Where(r => r.Division >= 3).ToList();

    public void Reload()
    {
        _races.Clear();
        _sourcePaths.Clear();
        foreach (var path in _loadedPaths) LoadFromFile(path);
    }

    public bool SaveLoadedFiles()
    {
        try
        {
            foreach (var group in _sourcePaths.GroupBy(kv => kv.Value))
            {
                var list = group.Select(kv => _races[kv.Key]).OrderBy(r => r.Id).ToList();
                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(group.Key, json);
            }
            return true;
        }
        catch { return false; }
    }
}
