using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using WcsInfinity.Systems;

namespace WcsInfinity.Plugins;

// ╔══════════════════════════════════════════════════════════════════╗
// ║  AETHER ROULETTE — анимированная рулетка для CS2 (Source 2)        ║
// ║  ПЕРВАЯ в своём роде: настоящий «крутящийся барабан» через         ║
// ║  покадровый рендер center-HTML с физикой замедления (ease-out).    ║
// ║  Раньше на Source 2 рулетки были статичным текстом — мы делаем     ║
// ║  визуальную анимацию спина с подсветкой и вспышкой выигрыша.       ║
// ╚══════════════════════════════════════════════════════════════════╝
public class AetherRoulette
{
    private readonly BasePlugin _plugin;
    public AetherRoulette(BasePlugin plugin) { _plugin = plugin; }

    // Единый рендер overlay (ставит главный плагин). Если null — fallback на прямой PrintToCenterHtml.
    public Action<CCSPlayerController, string, float>? RenderOverlay { get; set; }
    // Активна ли анимация/показ для игрока (для подавления авто-HUD)
    public Dictionary<ulong, float> BusyUntil { get; } = new();

    public enum RouletteType { Gold, Race, Donate }

    public class Prize
    {
        public string Name = "";
        public string Color = "#ffffff";
        public int Weight = 100;
        public string Rarity = "Обычный";
        public Action<CCSPlayerController>? Grant;
    }

    private static readonly Dictionary<string,string> RarityColor = new()
    {
        ["Обычный"]="#b0b0b0", ["Редкий"]="#3da9fc",
        ["Эпик"]="#a855f7", ["Легендарный"]="#f59e0b", ["Мифик"]="#ff3b6b"
    };

    private const int Window = 9;
    private const int Pointer = 4;

    public void Spin(CCSPlayerController player, RouletteType type, List<Prize> pool)
    {
        if (player == null || !player.IsValid || pool.Count == 0) return;

        int total = pool.Sum(p => p.Weight);
        int roll = Random.Shared.Next(total);
        int acc = 0, winnerIdx = 0;
        for (int i = 0; i < pool.Count; i++) { acc += pool[i].Weight; if (roll < acc) { winnerIdx = i; break; } }

        int spins = 28 + Random.Shared.Next(10);
        var strip = new List<Prize>();
        for (int i = 0; i < spins + Window; i++)
            strip.Add(pool[(winnerIdx - Pointer + i + pool.Count * 100) % pool.Count]);

        var delays = new List<float>();
        float t = 0.035f;
        for (int i = 0; i < spins; i++) { delays.Add(t); t += 0.006f + i * 0.0009f; }

        RenderFrame(player, type, strip, 0, false, pool[winnerIdx]);
        ScheduleFrames(player, type, strip, delays, 0, pool[winnerIdx]);
    }

    private void ScheduleFrames(CCSPlayerController player, RouletteType type,
        List<Prize> strip, List<float> delays, int frame, Prize winner)
    {
        if (frame >= delays.Count) { Finish(player, type, winner); return; }
        _plugin.AddTimer(delays[frame], () =>
        {
            if (player == null || !player.IsValid) return;
            bool last = frame == delays.Count - 1;
            RenderFrame(player, type, strip, frame + 1, last, winner);
            ScheduleFrames(player, type, strip, delays, frame + 1, winner);
        });
    }

    private void RenderFrame(CCSPlayerController player, RouletteType type,
        List<Prize> strip, int offset, bool flash, Prize winner)
    {
        var sb = new StringBuilder();
        string title = type switch {
            RouletteType.Gold => "🪙 РУЛЕТКА ЗОЛОТА",
            RouletteType.Race => "🧬 РУЛЕТКА РАС",
            _ => "💎 ДОНАТ-РУЛЕТКА" };
        sb.Append($"<font class='fontSize-l' color='#7c5cff'>{title}</font><br>");
        // ВЕРТИКАЛЬНЫЙ БАРАБАН: пред / текущий / след — без наложений
        int c = Math.Min(offset + Pointer, strip.Count - 1);
        var prev = strip[Math.Max(0, c - 1)];
        var cur  = strip[c];
        var next = strip[Math.Min(strip.Count - 1, c + 1)];
        sb.Append($"<font class='fontSize-sm' color='#4a4a4a'>{Short(prev.Name)}</font><br>");
        string curCol = flash ? "#ffffff" : cur.Color;
        sb.Append($"<font class='fontSize-l' color='{curCol}'>\u25b6 {Short(cur.Name)} \u25c0</font><br>");
        sb.Append($"<font class='fontSize-sm' color='#4a4a4a'>{Short(next.Name)}</font><br>");
        if (flash)
        {
            string rc = RarityColor.GetValueOrDefault(winner.Rarity, "#ffffff");
            sb.Append($"<font class='fontSize-m' color='{rc}'>\u2726 {winner.Rarity} \u2726</font>");
        }
        var __html = sb.ToString();
        BusyUntil[player.SteamID] = Server.CurrentTime + 0.5f;
        if (RenderOverlay != null) RenderOverlay(player, __html, 0.5f); else player.PrintToCenterHtml(__html);
    }

    private void Finish(CCSPlayerController player, RouletteType type, Prize winner)
    {
        _plugin.AddTimer(0.1f, () => { if (player.IsValid) winner.Grant?.Invoke(player); });
        string rc = RarityColor.GetValueOrDefault(winner.Rarity, "#ffffff");
        player.PrintToChat($" [AETHER] Рулетка: ты выбил {winner.Name} ({winner.Rarity})!");
        string winHtml =
            $"<font class='fontSize-l' color='#7c5cff'>✦ ВЫИГРЫШ ✦</font><br>" +
            $"<font class='fontSize-l' color='{rc}'>{winner.Name}</font><br>" +
            $"<font class='fontSize-m' color='#cccccc'>{winner.Rarity}</font>";
        BusyUntil[player.SteamID] = Server.CurrentTime + 3.5f;
        if (RenderOverlay != null) RenderOverlay(player, winHtml, 3.5f);
        else for (int k = 1; k <= 35; k++) _plugin.AddTimer(k * 0.1f, () => { if (player.IsValid) player.PrintToCenterHtml(winHtml); });
    }

    private static string Short(string s) => s.Length <= 18 ? s : s.Substring(0, 17) + "\u2026";

    public static List<Prize> GoldPool() => new()
    {
        new Prize{ Name="50 золота", Color="#b0b0b0", Weight=400, Rarity="Обычный",
            Grant=p => { var d = WcsInfinity.Core.AetherionPlugin.Instance?.Data(p.SteamID); if(d!=null) { EconomySystem.AddGold(d, 50); } } },
        new Prize{ Name="150 золота", Color="#b0b0b0", Weight=250, Rarity="Обычный",
            Grant=p => { var d = WcsInfinity.Core.AetherionPlugin.Instance?.Data(p.SteamID); if(d!=null) { EconomySystem.AddGold(d, 150); } } },
        new Prize{ Name="400 золота", Color="#3da9fc", Weight=150, Rarity="Редкий",
            Grant=p => { var d = WcsInfinity.Core.AetherionPlugin.Instance?.Data(p.SteamID); if(d!=null) { EconomySystem.AddGold(d, 400); } } },
        new Prize{ Name="1000 золота", Color="#a855f7", Weight=70, Rarity="Эпик",
            Grant=p => { var d = WcsInfinity.Core.AetherionPlugin.Instance?.Data(p.SteamID); if(d!=null) { EconomySystem.AddGold(d, 1000); } } },
        new Prize{ Name="Сундук", Color="#f59e0b", Weight=25, Rarity="Легендарный",
            Grant=p => { var d = WcsInfinity.Core.AetherionPlugin.Instance?.Data(p.SteamID); if(d!=null) { EconomySystem.AddGold(d, 2500); d.LevelBank+=5; } } },
        new Prize{ Name="ДЖЕКПОТ 5000", Color="#ff3b6b", Weight=5, Rarity="Мифик",
            Grant=p => { var d = WcsInfinity.Core.AetherionPlugin.Instance?.Data(p.SteamID); if(d!=null) { EconomySystem.AddGold(d, 5000); d.LevelBank+=15; } } },
    };

    public static List<Prize> RacePool() => new()
    {
        new Prize{ Name="300 золота", Color="#b0b0b0", Weight=300, Rarity="Обычный",
            Grant=p => { var d = WcsInfinity.Core.AetherionPlugin.Instance?.Data(p.SteamID); if(d!=null) { EconomySystem.AddGold(d, 300); } } },
        new Prize{ Name="1 LevelBank", Color="#b0b0b0", Weight=200, Rarity="Обычный",
            Grant=p => { var d = WcsInfinity.Core.AetherionPlugin.Instance?.Data(p.SteamID); if(d!=null) { d.LevelBank+=1; } } },
        new Prize{ Name="Случайная раса", Color="#3da9fc", Weight=200, Rarity="Редкий",
            Grant=p => {
                var plugin = WcsInfinity.Core.AetherionPlugin.Instance;
                if (plugin == null) return;
                var d = plugin.Data(p.SteamID);
                if (d == null) return;
                var races = plugin.GetEligibleRacePool(p.SteamID);
                if (races.Count == 0) { EconomySystem.AddGold(d, 500); return; }
                var raceId = races[Random.Shared.Next(races.Count)];
                d.UnlockedRaces.Add(raceId);
                var rDef = plugin.GetRaceDef(raceId);
                p.PrintToChat($" \x06✦ Разблокирована раса: {rDef?.Name ?? $"#{raceId}"}!");
            }},
        new Prize{ Name="Редкая раса", Color="#a855f7", Weight=100, Rarity="Эпик",
            Grant=p => {
                var plugin = WcsInfinity.Core.AetherionPlugin.Instance;
                if (plugin == null) return;
                var d = plugin.Data(p.SteamID);
                if (d == null) return;
                var races = plugin.GetEligibleRacePool(p.SteamID).Take(30).ToList();
                if (races.Count == 0) { EconomySystem.AddGold(d, 1000); return; }
                var raceId = races[Random.Shared.Next(races.Count)];
                d.UnlockedRaces.Add(raceId);
                var rDef = plugin.GetRaceDef(raceId);
                p.PrintToChat($" \x06✦ Разблокирована редкая раса: {rDef?.Name ?? $"#{raceId}"}!");
            }},
        new Prize{ Name="3 LevelBank + 500з", Color="#f59e0b", Weight=50, Rarity="Легендарный",
            Grant=p => { var d = WcsInfinity.Core.AetherionPlugin.Instance?.Data(p.SteamID); if(d!=null) { d.LevelBank+=3; EconomySystem.AddGold(d, 500); } } },
        new Prize{ Name="★ МЕГА-ДЖЕКПОТ ★", Color="#ff3b6b", Weight=10, Rarity="Мифик",
            Grant=p => {
                var plugin = WcsInfinity.Core.AetherionPlugin.Instance;
                if (plugin == null) return;
                var d = plugin.Data(p.SteamID);
                if (d == null) return;
                var locked = plugin.GetEligibleRacePool(p.SteamID);
                int count = Math.Min(5, locked.Count);
                for (int i = 0; i < count; i++) d.UnlockedRaces.Add(locked[i]);
                d.LevelBank += 5;
                EconomySystem.AddGold(d, 2000);
                p.PrintToChat($" \x06✦ ★ МЕГА-ДЖЕКПОТ: +{count} рас + 5 LB + 2000з!");
            }},
    };
}