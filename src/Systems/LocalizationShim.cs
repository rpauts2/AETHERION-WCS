using CounterStrikeSharp.API.Core;

namespace WcsInfinity.Systems;

public static class L10n
{
    private static ILocalizationReader? _loc;
    public static void Init(ILocalizationReader loc) => _loc = loc;

    public static string? Get(string key) => _loc?.Get(key);
    public static string? GetF(string key, params (string Key, object? Value)[] args) => _loc?.GetF(key, args);
    public static string? Get(string key, string fallback) => _loc?.Get(key) ?? fallback;
    public static string? GetF(string key, string fallback, params (string Key, object? Value)[] args) => _loc?.GetF(key, args) ?? fallback;
}
