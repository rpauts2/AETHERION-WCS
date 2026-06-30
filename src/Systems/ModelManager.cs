using System;
using WcsInfinity.Systems;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;


namespace WcsInfinity.Systems;
// Управление моделями рас: precache при загрузке, SetModel при спавне.
// Карта раса->модель в configs/assets/models.json (baseModel рабочий, customModel — слот под кастом).
public class ModelManager
{
    public class Entry { public string race {get;set;}="" ; public int division {get;set;}
        public string baseModel {get;set;}="" ; public string customModel {get;set;}="" ; public string fx {get;set;}="" ; public string aura {get;set;}="" ; public string accessory {get;set;}="" ; public int[] tint {get;set;}=new int[0]; public int glow {get;set;}=1; }

    private readonly Dictionary<int, Entry> _map = new();
    private readonly HashSet<string> _toPrecache = new();

    public void Load(string configDir)
    {
        var path = Path.Combine(configDir, "assets", "models.json");
        var fbMissing = "[AETHERION] models.json не найден: {0}";
        var missing = L10n.GetF("Systems_ModelManager_Missing", fbMissing, ("0", path));
        if (!File.Exists(path)) { Console.WriteLine(missing ?? fbMissing); return; }
        var raw = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(path));
        if (raw == null) return;
        foreach (var kv in raw)
        {
            if (int.TryParse(kv.Key, out var id))
            {
                _map[id] = kv.Value;
                var m = string.IsNullOrEmpty(kv.Value.customModel) ? kv.Value.baseModel : kv.Value.customModel;
                if (!string.IsNullOrEmpty(m)) _toPrecache.Add(m);
                if (!string.IsNullOrEmpty(kv.Value.fx)) _toPrecache.Add(kv.Value.fx);
                if (!string.IsNullOrEmpty(kv.Value.aura)) _toPrecache.Add(kv.Value.aura);
                if (!string.IsNullOrEmpty(kv.Value.accessory)) _toPrecache.Add(kv.Value.accessory);
            }
        }
        var fbLoaded = "[AETHERION] ModelManager: загружено {0} моделей, {1} ассетов к precache";
        var loaded = L10n.GetF("Systems_ModelManager_Loaded", fbLoaded, ("0", _map.Count), ("1", _toPrecache.Count));
        Console.WriteLine(loaded ?? string.Format(fbLoaded, _map.Count, _toPrecache.Count));
    }

    // Вызывать в OnServerPrecacheResources
    public void Precache(ResourceManifest manifest)
    {
        foreach (var asset in _toPrecache) manifest.AddResource(asset);
    }

    // Применить модель расы к игроку при спавне
    public void ApplyModel(CCSPlayerController player, int raceId)
    {
        if (player?.PlayerPawn?.Value == null) return;
        if (!_map.TryGetValue(raceId, out var e)) return;
        var model = string.IsNullOrEmpty(e.customModel) ? e.baseModel : e.customModel;
        if (string.IsNullOrEmpty(model)) return;
        try { player.PlayerPawn.Value.SetModel(model); }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] SetModel fail race={raceId}: {ex.Message}"); }
    }

    public string? GetFx(int raceId) => _map.TryGetValue(raceId, out var e) ? e.fx : null;
    public void ApplyTint(IEngineApi engine, int slot, int raceId)
    {
        if (_map.TryGetValue(raceId, out var e) && e.tint != null && e.tint.Length == 3)
            engine.SetRenderTint(slot, e.tint[0], e.tint[1], e.tint[2], e.glow);
    }
    public string? GetAccessory(int raceId) => _map.TryGetValue(raceId, out var e) ? e.accessory : null;
}
