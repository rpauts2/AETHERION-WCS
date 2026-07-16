using System;
using System.Collections.Generic;
using CounterStrikeSharp.API;

namespace WcsInfinity.Systems;

public class ChatSystem
{
    private int _idx;
    private static readonly string[] _default =
    {
        " [AETHERION] Добро пожаловать на сервер! Используйте !wcs для открытия меню.",
        " [AETHERION] Советы: !bind — настройка хоткеев, !spin — рулетка голды и рас.",
        " [AETHERION] Ивенты: !event или !boss для голосования за призыв босса.",
        " [AETHERION] VIP (+%XP и +%gold) доступен в рулетке !spin donate или !daily.",
        " [AETHERION] Гильдии: !guild create <имя> <тег> — объединяйтесь и побеждайте!",
        " [AETHERION] Эфирная Буря раз в раунд усиливает весь сервер — собирайте бонусы!",
        " [AETHERION] Дуэли: !duel <ник> / !duel accept — ставка 200 голды.",
        " [AETHERION] Дух-компаньон: !wisp — призывает спутника, который усиливает фарм.",
        " [AETHERION] Ежедневный вход: !daily — фри-спины и бонусы."
    };

    public void Tick()
    {
        try
        {
            var msg = _default[_idx % _default.Length];
            _idx++;
            foreach (var p in CounterStrikeSharp.API.Utilities.GetPlayers())
            {
                if (p == null || !p.IsValid || p.IsBot) continue;
                p.PrintToChat(msg);
            }
        }
        catch { }
    }
}
