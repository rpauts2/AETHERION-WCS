using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WcsInfinity.Models;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════╗
// ║  COSMETICS — титулы, цвета ника, трейлы за достижения.          ║
// ║  Разблокируются за уровни рас, гильдии, ачивки, турниры.        ║
// ║  !cosmetics — открыть меню косметики                           ║
// ║  !title <id> — выбрать титул                                   ║
// ║  !color <id> — выбрать цвет ника                               ║
// ╚══════════════════════════════════════════════════════════════════╝

public enum CosmeticType { Title, NameColor, Trail, Badge }

public class CosmeticDef
{
    public string Id = "";
    public CosmeticType Type;
    public string Name = "";
    public string Desc = "";
    public string Value = "";  // title text, CS2 color code, particle path
    public int RequiredLevel;  // min total level across all races
    public int RequiredDivision;
    public string RequiredAchievement = "";  // achievement ID
    public bool RequiredGuild;  // must be in a guild
}

public static class CosmeticCatalog
{
    public static readonly List<CosmeticDef> Cosmetics = new()
    {
        // ── ТИТУЛЫ ──
        new() { Id="title_novice", Type=CosmeticType.Title, Name="Новичок", Value="Новичок", RequiredLevel=0 },
        new() { Id="title_warrior", Type=CosmeticType.Title, Name="Воин", Value="Воин", RequiredLevel=25 },
        new() { Id="title_veteran", Type=CosmeticType.Title, Name="Ветеран", Value="Ветеран", RequiredLevel=50 },
        new() { Id="title_legend", Type=CosmeticType.Title, Name="Легенда", Value="Легенда", RequiredLevel=100 },
        new() { Id="title_godslayer", Type=CosmeticType.Title, Name="Богоборец", Value="Богоборец", RequiredLevel=200, RequiredDivision=5 },
        new() { Id="title_eternal", Type=CosmeticType.Title, Name="Вечный", Value="Вечный", RequiredLevel=500, RequiredDivision=7 },
        new() { Id="title_dragon", Type=CosmeticType.Title, Name="Дракон", Value="🐉 Дракон", RequiredAchievement="boss_master" },
        new() { Id="title_guild_member", Type=CosmeticType.Title, Name="Братство", Value="⚔ Братство", RequiredGuild=true },

        // ── ЦВЕТА НИКА ──
        new() { Id="color_white", Type=CosmeticType.NameColor, Name="Белый", Value="#FFFFFF", RequiredLevel=0 },
        new() { Id="color_green", Type=CosmeticType.NameColor, Name="Зелёный", Value="#00FF00", RequiredLevel=10 },
        new() { Id="color_cyan", Type=CosmeticType.NameColor, Name="Голубой", Value="#00FFFF", RequiredLevel=20 },
        new() { Id="color_purple", Type=CosmeticType.NameColor, Name="Фиолетовый", Value="#FF00FF", RequiredLevel=35 },
        new() { Id="color_gold", Type=CosmeticType.NameColor, Name="Золотой", Value="#FFD700", RequiredLevel=60, RequiredDivision=3 },
        new() { Id="color_red", Type=CosmeticType.NameColor, Name="Красный", Value="#FF4444", RequiredLevel=80, RequiredDivision=4 },
        new() { Id="color_ember", Type=CosmeticType.NameColor, Name="Пламя", Value="#FF6600", RequiredAchievement="killer_1000" },
        new() { Id="color_ice", Type=CosmeticType.NameColor, Name="Лёд", Value="#88DDFF", RequiredAchievement="streak_10" },

        // ── БЕЙДЖИ ──
        new() { Id="badge_first_blood", Type=CosmeticType.Badge, Name="Первая кровь", Value="🩸", RequiredAchievement="first_blood" },
        new() { Id="badge_boss_slayer", Type=CosmeticType.Badge, Name="Убийца Боссов", Value="💀", RequiredAchievement="boss_slayer" },
        new() { Id="badge_guild_hero", Type=CosmeticType.Badge, Name="Герой Гильдии", Value="⚔", RequiredGuild=true, RequiredLevel=50 },
        new() { Id="badge_ace", Type=CosmeticType.Badge, Name="Эйс", Value="🎯", RequiredAchievement="ace_1" },
        new() { Id="badge_wisp_friend", Type=CosmeticType.Badge, Name="Друг Духа", Value="✨", RequiredAchievement="guild_join" },
    };

    public static CosmeticDef? Get(string id) => Cosmetics.FirstOrDefault(c => c.Id == id);
}

public class CosmeticSystem
{
    public bool CanEquip(PlayerData d, CosmeticDef def)
    {
        int maxLevel = d.Races.Values.Any() ? d.Races.Values.Max(r => r.Level) : 0;
        if (def.RequiredLevel > 0 && maxLevel < def.RequiredLevel) return false;
        if (def.RequiredDivision > 0 && d.Division < def.RequiredDivision) return false;
        if (def.RequiredGuild && d.GuildId == 0) return false;
        if (!string.IsNullOrEmpty(def.RequiredAchievement) && !d.Achievements.Contains(def.RequiredAchievement)) return false;
        return true;
    }

    public void EquipTitle(PlayerData d, string titleId)
    {
        var def = CosmeticCatalog.Get(titleId);
        if (def == null || def.Type != CosmeticType.Title) return;
        if (!CanEquip(d, def)) return;
        d.EquippedTitle = titleId;
    }

    public void EquipColor(PlayerData d, string colorId)
    {
        var def = CosmeticCatalog.Get(colorId);
        if (def == null || def.Type != CosmeticType.NameColor) return;
        if (!CanEquip(d, def)) return;
        d.EquippedColor = colorId;
    }

    public string GetTitleDisplay(PlayerData d)
    {
        if (string.IsNullOrEmpty(d.EquippedTitle)) return "";
        var def = CosmeticCatalog.Get(d.EquippedTitle);
        return def?.Value ?? "";
    }

    public string GetColorDisplay(PlayerData d)
    {
        if (string.IsNullOrEmpty(d.EquippedColor)) return "#FFFFFF";
        var def = CosmeticCatalog.Get(d.EquippedColor);
        return def?.Value ?? "#FFFFFF";
    }

    public void ShowMenu(CCSPlayerController p, PlayerData d)
    {
        int maxLevel = d.Races.Values.Any() ? d.Races.Values.Max(r => r.Level) : 0;
        p.PrintToChat(" \x0B═══ КОСМЕТИКА ═══");
        p.PrintToChat($" Твой уровень: {maxLevel} | Дивизион: D{d.Division}");

        var titles = CosmeticCatalog.Cosmetics.Where(c => c.Type == CosmeticType.Title).ToList();
        p.PrintToChat(" \x06ТИТУЛЫ:");
        foreach (var t in titles)
        {
            bool owned = CanEquip(d, t);
            bool equipped = d.EquippedTitle == t.Id;
            string status = equipped ? " \x04[надет]" : owned ? " \x08[доступен]" : " \x07[locked]";
            p.PrintToChat($" {t.Value}{status}");
        }

        var colors = CosmeticCatalog.Cosmetics.Where(c => c.Type == CosmeticType.NameColor).ToList();
        p.PrintToChat(" \x06ЦВЕТА:");
        foreach (var c in colors)
        {
            bool owned = CanEquip(d, c);
            bool equipped = d.EquippedColor == c.Id;
            string status = equipped ? " \x04[надет]" : owned ? " \x08[доступен]" : " \x07[locked]";
            p.PrintToChat($" {c.Name} ({c.Value}){status}");
        }

        p.PrintToChat(" \x09Команды: !title <id> | !color <id>");

        var badges = CosmeticCatalog.Cosmetics.Where(c => c.Type == CosmeticType.Badge).ToList();
        p.PrintToChat(" \x06БЕЙДЖИ:");
        foreach (var b in badges)
        {
            bool owned = CanEquip(d, b);
            string status = owned ? " \x04[доступен]" : " \x07[locked]";
            p.PrintToChat($" {b.Value} {b.Name}{status}");
        }
    }
}
