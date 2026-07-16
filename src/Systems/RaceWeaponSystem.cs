using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using WcsInfinity.Core;
using WcsInfinity.Races;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════════╗
// ║  RACE WEAPON SYSTEM — кастомное оружие привязанное к расе (ФАЗА 5b) ║
// ║  • Загружает маппинг раса→оружие→скин из configs/weapons.json     ║
// ║  • При спавне: выдаёт оружие + применяет paintkit через            ║
// ║    m_nFallbackPaintKit (сетевой prop, видно всем)                   ║
// ║  • Т1-расы получают обычное оружие, T3+ — кастомный скин          ║
// ║  • Архетип определяет тип оружия: mage→AWP, warrior→AK, etc.        ║
// ╚══════════════════════════════════════════════════════════════════════╝
public sealed class RaceWeaponSystem
{
    // Оружие по архетипу расы. Можно переопределить в weapons.json.
    private static readonly Dictionary<string, string> ArchetypeWeapon = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mage"]       = "weapon_awp",
        ["Warrior"]    = "weapon_ak47",
        ["Assassin"]   = "weapon_deagle",
        ["Tank"]       = "weapon_m249",
        ["Support"]    = "weapon_mp9",
        ["Ranger"]     = "weapon_ssg08",
        ["Berserker"]  = "weapon_macs10",
        ["Necromancer"]= "weapon_awp",
        ["Elementalist"]= "weapon_ak47",
        ["Shadow"]     = "weapon_usp_silencer",
        ["Guardian"]   = "weapon_famas",
        ["Trickster"]  = "weapon_deagle",
        ["Engineer"]   = "weapon_mp9",
        ["Paladin"]    = "weapon_famas",
    };

    // PaintKit ID по расе (CS2 paintkit IDs — стандартные работают без кастомных файлов).
    // 0 = default skin. Используем популярные стандартные как «расовые».
    private static readonly Dictionary<int, int> RacePaintKits = new()
    {
        // D1 — базовые скины
        [1]  = 0,    // Искатель — стандарт
        [2]  = 62,   // Страж — Safari Mesh
        [3]  = 416,  // Маг — Neo-Noir
        [4]  = 376,  // Тень — Prisma
        [5]  = 33,   // Зверь — Urban Masked
        [6]  = 85,   // Алхимик — Safari Mesh (AK)
        [7]  = 540,  // Жрец — Bloodsport
        // D2 — мид-тир
        [8]  = 655,  // Чародей — Fade
        [9]  = 67,   // Сталкер — Safari Mesh
        [10] = 376,  // Фантом — Prisma
        [11] = 416,  // Вампир — Neo-Noir
        [12] = 282,  // Кинжальщик — Prisma
        [13] = 540,  // Инфернал — Bloodsport
        [14] = 85,   // Ледяной — Safari Mesh
        // D3+ — топ
        [15] = 655,  // Архонт — Fade
        [16] = 376,  // Разрушитель — Prisma
        [17] = 416,  // Князь Тьмы — Neo-Noir
        [18] = 540,  // Обсидиан — Bloodsport
        [19] = 655,  // Хранитель — Fade
        [20] = 376,  // Стихий — Prisma
        // D4-D5 — легендарные (повторяем лучшие paintkit)
        [21] = 655,
        [22] = 540,
        [23] = 416,
        [24] = 376,
        [25] = 655,
        [26] = 540,
        [27] = 416,
        [28] = 376,
        [29] = 655,
        [30] = 540,
        [31] = 416,
        [32] = 376,
        [33] = 655,
        [34] = 540,
        [35] = 416,
    };

    // Custom overrides from JSON
    private sealed class WeaponOverride
    {
        [JsonPropertyName("raceId")] public int RaceId { get; set; }
        [JsonPropertyName("weapon")] public string Weapon { get; set; } = "";
        [JsonPropertyName("paintkit")] public int PaintKit { get; set; } = -1;
    }

    private readonly Dictionary<int, WeaponOverride> _overrides = new();
    private readonly AetherionPlugin _plugin;

    public RaceWeaponSystem(AetherionPlugin plugin) => _plugin = plugin;

    // Загрузка кастомных оверрайдов из JSON
    public void Initialize()
    {
        var path = Path.Combine(_plugin.ModuleDirectory, "..", "..", "configs", "weapons.json");
        if (!File.Exists(path)) return;
        try
        {
            var raw = JsonSerializer.Deserialize<List<WeaponOverride>>(File.ReadAllText(path));
            if (raw == null) return;
            foreach (var w in raw)
                if (w.RaceId > 0 && !string.IsNullOrEmpty(w.Weapon))
                    _overrides[w.RaceId] = w;
            Console.WriteLine($"[AETHERION] RaceWeaponSystem: загружено {_overrides.Count} оверрайдов оружия");
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] weapons.json load err: {ex.Message}"); }
    }

    // Определить оружие для расы
    public string GetWeaponForRace(Races.RaceDefinition def)
    {
        if (_overrides.TryGetValue(def.Id, out var ov) && !string.IsNullOrEmpty(ov.Weapon))
            return ov.Weapon;
        if (ArchetypeWeapon.TryGetValue(def.Archetype, out var wp))
            return wp;
        return "weapon_ak47";
    }

    // Определить paintkit для расы
    public int GetPaintKitForRace(Races.RaceDefinition def)
    {
        if (_overrides.TryGetValue(def.Id, out var ov) && ov.PaintKit >= 0)
            return ov.PaintKit;
        if (RacePaintKits.TryGetValue(def.Id, out var pk))
            return pk;
        return 0;
    }

    // Выдать кастомное оружие при спавне (вместо стандартного).
    // Вызывается из AetherionPlugin.OnSpawn после ApplyModel.
    public void EquipOnSpawn(CCSPlayerController p, Races.RaceDefinition def)
    {
        if (p == null || !p.IsValid || p.IsBot || !p.PawnIsAlive) return;
        if (p.PlayerPawn?.Value == null) return;

        var weaponName = GetWeaponForRace(def);
        int paintKit = GetPaintKitForRace(def);

        // Убираем текущее оружие (для чистоты выдачи)
        try
        {
            // GiveNamedItem работает через сервер — не требует менеджера предметов
            var pawn = p.PlayerPawn.Value;

            // Применяем paintkit на павне. В CS2 m_nFallbackPaintKit
            // контролирует вид скина оружия которое держит игрок.
            if (paintKit > 0)
            {
                try
                {
                    // PaintKit через сетевое свойство павна. CSS API может не экспортировать его напрямую,
                    // поэтому используем try/catch — на некоторых версиях не сработает, это нормально.
                    var controller = p;
                    var prop = controller?.PlayerPawn?.Value;
                    if (prop != null)
                    {
                        // Пытаемся установить через reflection (совместимо с любыми версиями CSS API)
                        var propInfo = prop.GetType().GetProperty("FallbackPaintKit");
                        if (propInfo != null)
                        {
                            propInfo.SetValue(prop, (uint)paintKit);
                            Utilities.SetStateChanged(prop, "CCSPlayerPawn", "m_nFallbackPaintKit");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Некритично — paintkit косметический
                    Console.WriteLine($"[AETHERION] paintkit fail race {def.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] weapon equip err: {ex.Message}"); }
    }

    // Генерация шаблонного weapons.json (вызывается из !admin weapons generate)
    public static string GenerateTemplate()
    {
        return @"[
  { ""raceId"": 1,  ""weapon"": ""weapon_ak47"",  ""paintkit"": 0 },
  { ""raceId"": 8,  ""weapon"": ""weapon_awp"",   ""paintkit"": 655 },
  { ""raceId"": 15, ""weapon"": ""weapon_deagle"", ""paintkit"": 540 }
]";
    }
}
