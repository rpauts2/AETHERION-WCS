using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using WcsInfinity.Models;

namespace WcsInfinity.Systems;

public class AdminMenu
{
    private readonly Func<ulong, PlayerData?> _data;
    private readonly Action<CCSPlayerController> _openMainMenu;

    public AdminMenu(Func<ulong, PlayerData?> data, Action<CCSPlayerController> openMainMenu)
    {
        _data = data;
        _openMainMenu = openMainMenu;
    }

    public void OpenAdminMenu(CCSPlayerController? p)
    {
        if (p == null || !p.IsValid || p.IsBot) return;
        if (!IsAdmin(p)) { p.PrintToChat(" \x02[AETHERION] Нет прав (@css/root)."); return; }

        p.PrintToChat(" \x06[AETHERION] \x04Админ-меню\x06: используйте \x04!admin\x06 для списка команд.");
        ShowHelp(p);
    }

    public void ShowHelp(CCSPlayerController? p)
    {
        if (p == null || !p.IsValid) return;
        p.PrintToChat(" \x06Команды:\x04 !admin grant <steamid> <levels> \x06— выдать уровни");
        p.PrintToChat(" \x06\x04!admin gold <steamid> <amount> \x06— выдать голду");
        p.PrintToChat(" \x06\x04!admin kick <part_of_name> \x06— кикнуть");
        p.PrintToChat(" \x06\x04!admin stats \x06— статистика сервера");
    }

    private bool IsAdmin(CCSPlayerController p) =>
        CounterStrikeSharp.API.Modules.Admin.AdminManager.PlayerHasPermissions(p, "@css/root") ||
        CounterStrikeSharp.API.Modules.Admin.AdminManager.PlayerHasPermissions(p, "@css/generic");
}
