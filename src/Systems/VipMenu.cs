using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using WcsInfinity.Models;

namespace WcsInfinity.Systems;

/// <summary>
/// VipMenu — операционное VIP-меню.
/// </summary>
public class VipMenu
{
    private Func<ulong, PlayerData?> _data;
    private Action<CCSPlayerController> _openMainMenu;
    private Action<string, string, CommandInfo.CommandCallback?>? _registerCommand;

    private const int VipXpBonusPercent = 25;
    private const int VipGoldBonusPercent = 15;
    private const long VipDefaultDays = 1;
    private const long SecondsPerDay = 86400L;

    public VipMenu()
    {
        _data = _ => null!;
        _openMainMenu = _ => { };
        _registerCommand = null;
    }

    public VipMenu(
        Func<ulong, PlayerData?> data,
        Action<CCSPlayerController> openMainMenu,
        Action<string, string, CommandInfo.CommandCallback?>? registerCommand = null)
    {
        _data = data;
        _openMainMenu = openMainMenu;
        _registerCommand = registerCommand;
    }

    public void Init(
        Func<ulong, PlayerData?> data,
        Action<CCSPlayerController> openMainMenu,
        Action<string, string, CommandInfo.CommandCallback?>? registerCommand = null)
    {
        _data = data;
        _openMainMenu = openMainMenu;
        _registerCommand = registerCommand;
    }

    public PlayerData? Data(ulong steamId) => _data(steamId);

    public bool IsVip(PlayerData? d)
    {
        if (d == null) return false;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return now < d.VipExpiresUnix;
    }

    public long VipXpMultiplier(PlayerData? d) => IsVip(d) ? VipXpBonusPercent : 0;
    public long VipGoldMultiplier(PlayerData? d) => IsVip(d) ? VipGoldBonusPercent : 0;

    public void OpenVipMenu(CCSPlayerController? p)
    {
        if (p == null || !p.IsValid) return;
        var d = _data(p.SteamID);
        if (d == null) return;

        bool active = IsVip(d);
        long remaining = active ? (d.VipExpiresUnix - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) : 0;

        string status = active
            ? $"Активен (ещё {remaining / 3600}ч {remaining % 3600 / 60}м)"
            : "Не активен";

        p.PrintToChat($" ⭐ VIP-статус: {status}");

        if (!active)
        {
            p.PrintToChat($" └─ Получить VIP можно через ежедневную награду (!daily) или рулетку за фри-спины (!spin donate).");
            p.PrintToChat($" └─ VIP даёт пропуск очереди и тематический значок в профиле (премиум-префикс).");
        }
        else
        {
            p.PrintToChat($" └─ Бонус опыта: +{VipXpBonusPercent}% ко всем источникам XP.");
            p.PrintToChat($" └─ Бонус голды: +{VipGoldBonusPercent}% с каждого убийства/объектива.");
            p.PrintToChat($" └─ Префикс VIP будет отображаться в профиле и рекордах.");
        }

        AddItem("css_vipback", "Назад (главное меню)", (_, __) => _openMainMenu(p));
    }

    public void GrantVip(PlayerData d, long days)
    {
        if (d == null) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long baseUnix = d.VipExpiresUnix > now ? d.VipExpiresUnix : now;
        d.VipExpiresUnix = baseUnix + days * SecondsPerDay;
    }

    private void AddItem(string name, string desc, CommandInfo.CommandCallback? cb)
    {
        if (_registerCommand != null)
            _registerCommand(name, desc, cb);
    }
}
