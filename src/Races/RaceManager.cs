using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WcsInfinity.Races;

// Реестр всех рас. Грузит races.json и (в будущем) поведенческие хуки.
public class RaceManager
{
    private readonly Dictionary<int, RaceDefinition> _races = new(); // опциональное кастомное поведение

    public IReadOnlyDictionary<int, RaceDefinition> Races => _races;

    public void LoadFromFile(string path)
    {
        if (!File.Exists(path)) return;
        var json = File.ReadAllText(path);
        var list = JsonSerializer.Deserialize<List<RaceDefinition>>(json) ?? new();
        foreach (var r in list) { r.IndexAbilities(); _races[r.Id] = r; }
    }

    public void LoadFromMultiple(string[] paths)
    {
        foreach (var p in paths) LoadFromFile(p);
    }

    public RaceDefinition? Get(int id) => _races.TryGetValue(id, out var r) ? r : null;

    // Расы, доступные игроку по его дивизиону
    public IEnumerable<RaceDefinition> RacesForDivision(int division) =>
        _races.Values.Where(r => r.Division <= division).OrderBy(r => r.Id);

    public int Count => _races.Count;

    public System.Collections.Generic.List<RaceDefinition> AllD3Plus() => _races.Values.Where(r => r.Division >= 3).ToList();
}