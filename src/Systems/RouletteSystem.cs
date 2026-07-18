using System;
using System.Collections.Generic;
using System.Linq;
using WcsInfinity.Models;
using WcsInfinity.Systems;

namespace WcsInfinity.Systems;

// Рулетки за золото. Честные шансы (видны игроку) + пити-система (гарант после N круток).
public enum RouletteType { Gold, PrivateRace, Donate, Common }

public class RoulettePrize
{
    public string Name { get; set; } = "";
    public double Chance { get; set; }       // 0..1 — показывается игроку
    public Action<PlayerData> Grant { get; set; } = _ => {};
    public bool IsJackpot { get; set; }      // для пити-гаранта
}

public class RouletteWheel
{
    public RouletteType Type { get; set; }
    public long Cost { get; set; }           // стоимость кручения в золоте
    public List<RoulettePrize> Prizes { get; set; } = new();
    public int PityThreshold { get; set; } = 50; // гарант джекпота через N круток без него
}

public class RouletteSystem
{
    private readonly Random _rng = new();
    // счётчик круток без джекпота: steamId -> count
    private readonly Dictionary<ulong, int> _pity = new();

    public (bool ok, string message, RoulettePrize? prize) Spin(PlayerData p, RouletteWheel wheel)
    {
        if (!EconomySystem.SpendGold(p, wheel.Cost))
            return (false, L10n.GetF("Systems_RouletteSystem_NoGoldLabel", "Недостаточно золота. Нужно {0}.",
                ("wheel.Cost", wheel.Cost)), null);

        int spins = _pity.GetValueOrDefault(p.SteamId) + 1;
        var jackpot = wheel.Prizes.FirstOrDefault(x => x.IsJackpot);

        if (jackpot != null && spins >= wheel.PityThreshold)
        {
            _pity[p.SteamId] = 0;
            jackpot.Grant(p);
            return (true, L10n.GetF("Systems_RouletteSystem_JackpotLabel", "🎉 ГАРАНТ! Вы выиграли: {0}",
                ("jackpot.Name", jackpot.Name)), jackpot);
        }

        double roll = _rng.NextDouble();
        double acc = 0;
        foreach (var prize in wheel.Prizes)
        {
            acc += prize.Chance;
            if (roll <= acc)
            {
                _pity[p.SteamId] = prize.IsJackpot ? 0 : spins;
                prize.Grant(p);
                return (true, L10n.GetF("Systems_RouletteSystem_WinLabel", "Вы выиграли: {0}",
                    ("prize.Name", prize.Name)), prize);
            }
        }
        _pity[p.SteamId] = spins;
        return (true, L10n.Get("Systems_RouletteSystem_NoLuckLabel") ?? "Повезёт в следующий раз!", null);
    }
}
