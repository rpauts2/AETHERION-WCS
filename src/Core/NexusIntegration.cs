using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using WcsInfinity.Models;
using WcsInfinity.Races;
using WcsInfinity.Systems;
using WcsInfinity.Plugins;

namespace WcsInfinity.Core;

// Интеграция AETHER NEXUS в главный плагин:
// - регистрирует провайдеры контента для каждого сектора;
// - ловит нажатие E (+use) через сравнение кнопок в OnTick;
// - открывает/закрывает Nexus по команде !nexus или !wcs.
//
// Подключается из AetherionPlugin.Load() вызовом NexusIntegration.Wire(...).
public static class NexusIntegration
{
    // Запоминаем прошлые кнопки игрока, чтобы поймать момент нажатия E
    private static readonly Dictionary<int, bool> _lastUse = new();

    public static void Wire(
        AetherionPlugin plugin,
        AetherNexus nexus,
        RaceManager races,
        AetherRoulette roulette,
        Func<ulong, PlayerData> data,
        Action<PlayerData> save)
    {
        // ── Сектор РАС: открытые/закрытые расы текущего дивизиона ──
        nexus.RacesProvider = p =>
        {
            var d = data(p.SteamID);
            var divRaces = races.RacesForDivision(d.Division)
                                .Where(r => r.Division == d.Division)
                                .OrderBy(r => r.Id).ToList();
            var nodes = new List<NexusNode>();
            foreach (var r in divRaces.Take(6))
            {
                bool ok = UnlockSystem.IsUnlocked(d, r, divRaces);
                int rid = r.Id;
                nodes.Add(new NexusNode
                {
                    Label = r.Name,
                    Enabled = ok,
                    OnSelect = pl =>
                    {
                        if (!ok) { pl.PrintToChat(" \x07Раса не разблокирована!"); return; }
                        var pd = data(pl.SteamID);
                        pd.CurrentRaceId = rid;
                        save(pd);
                        pl.PrintToChat($" [NEXUS] Выбрана раса: {r.Name}");
                        nexus.Close(pl);
                    }
                });
            }
            nodes.Add(new NexusNode { Label = "↩ НАЗАД", GoTo = NexusSector.Hub });
            return nodes;
        };

        // ── Сектор МАГАЗИНА: предметы за золото ──
        nexus.ShopProvider = p =>
        {
            var d = data(p.SteamID);
            var nodes = new List<NexusNode>();
            foreach (var item in ItemShop.Catalog.Take(6))
            {
                int id = item.Id;
                nodes.Add(new NexusNode
                {
                    Label = $"{item.Name} — {item.Price}з",
                    Enabled = d.Gold >= item.Price,
                    OnSelect = pl =>
                    {
                        var pd = data(pl.SteamID);
                        var (ok, msg) = ItemShop.Buy(pd, id);
                        pl.PrintToChat($" [МАГАЗИН] {msg}");
                        if (ok) save(pd);
                    }
                });
            }
            nodes.Add(new NexusNode { Label = "↩ НАЗАД", GoTo = NexusSector.Hub });
            return nodes;
        };

        // ── Сектор ГИЛЬДИИ ──
        nexus.GuildProvider = p =>
        {
            var g = GuildManager.Instance.Of(p.SteamID);
            var nodes = new List<NexusNode>();
            if (g == null)
                nodes.Add(new NexusNode { Label = "Создать гильдию (!guild create)", Enabled = false });
            else
            {
                nodes.Add(new NexusNode { Label = $"[{g.Tag}] {g.Name}", Enabled = false });
                nodes.Add(new NexusNode { Label = $"Знамя ур.{g.BannerLevel}", Enabled = false });
                nodes.Add(new NexusNode { Label = $"Казна: {g.Treasury}з", Enabled = false });
            }
            nodes.Add(new NexusNode { Label = "↩ НАЗАД", GoTo = NexusSector.Hub });
            return nodes;
        };

        // ── Сектор СЕЗОНА ──
        nexus.SeasonProvider = p =>
        {
            var d = data(p.SteamID);
            return new List<NexusNode>
            {
                new NexusNode { Label = $"Сезонный ранг: {d.SeasonRank}", Enabled = false },
                new NexusNode { Label = $"Сезонный XP: {d.SeasonXp}", Enabled = false },
                new NexusNode { Label = "↩ НАЗАД", GoTo = NexusSector.Hub },
            };
        };

        // ── Рулетка ──
        nexus.RouletteAction = p =>
        {
            var d = data(p.SteamID);
            roulette.Spin(p, AetherRoulette.RouletteType.Gold, AetherRoulette.GoldPool());
            nexus.Close(p);
        };

        // ── Хук нажатия E (+use) для подтверждения выбора ──
        plugin.RegisterListener<Listeners.OnTick>(() =>
        {
            foreach (var p in Utilities.GetPlayers())
            {
                if (p == null || !p.IsValid || p.PlayerPawn?.Value == null) continue;
                if (!nexus.IsOpen(p)) { _lastUse[p.Slot] = false; continue; }

                bool useDown = (p.Buttons & PlayerButtons.Use) != 0;
                bool wasDown = _lastUse.GetValueOrDefault(p.Slot);
                if (useDown && !wasDown) nexus.Confirm(p); // момент нажатия
                _lastUse[p.Slot] = useDown;
            }
        });
    }
}
