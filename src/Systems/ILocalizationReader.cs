// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  ILocalizationReader — минимальный интерфейс локализации для AETHERION ║
// ╠══════════════════════════════════════════════════════════════════════════╣
// ║  Назначение:                                                             ║
// ║  1. Единая точка доступа к строкам из любого класса плагина.            ║
// ║  2. Поддержка JSON-словарей (lang/{locale}.json) без жёстких констант.  ║
// ║  3. Провайдеры: только чтение, без записи — безопасно для многопоточ.   ║
// ║  4. Fallback на английский (en.json) при отсутствии ключа.               ║
// ║  5. Ключи рекомендуется хранить файлово-ориентированными                 ║
// ║     (подключение через partial static-классы — см. Strings.ru.cs).       ║
// ╚══════════════════════════════════════════════════════════════════════════╝
//
// Использование:
//   // В любом месте плагина (в т.ч. AetherionPlugin, Systems, UI, Plugins):
//   string text = Loc.Get("Core_AetherionPlugin_NoRace");
//   // С параметрами:
//   string msg = Loc.GetF("Core_AetherionPlugin_DuelNeedBet", ("BET", 500));
//   // С fallback:
//   string msg = Loc.GetOr("ru", "LevelSystem_XpKill", "+{0} XP", 150);

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WcsInfinity.Systems;

/// <summary>
/// Тип провайдера строк: получает словарь {key, value} из файла.
/// </summary>
public interface ILocalizationProvider
{
    /// <summary>
    /// Загрузить все строки для указанной локаль (например "ru", "en").
    /// Результат — плоский словарь {ключ, строка}. Может быть null/пустой при ошибке.
    /// </summary>
    IReadOnlyDictionary<string, string>? Load(string locale);
}

/// <summary>
/// Читатель локализованных строк.
/// Исполнительная логика (загрузка JSON, fallback, форматирование) вынесена
/// в отдельный сервис, чтобы системы UI/Combat не знали о формате файла.
/// </summary>
public interface ILocalizationReader
{
    /// <summary>
    /// Получить строку по ключу из текущей локали. Вернёт null, если ключ не найден
    /// (потребитель обязан сам вывести fallback).
    /// </summary>
    string? Get(string key);

    /// <summary>
    /// Получить строку по ключу и локоли. Полезно для команды !lang или смены языка
    /// без перезагрузки плагина.
    /// </summary>
    string? Get(string locale, string key);

    /// <summary>
    /// Форматировать строку по ключу: Loc.GetF("ShopMsg", ("name", "Alexi")) ->
    /// подставит name = "Alexi" во внутренний вызов Get(key).Format(...)
    /// </summary>
    string? GetF(string key, params (string name, object value)[] args);

    /// <summary>
    /// Получить строку или вернуть fallback, если ключ не найден.
    /// </summary>
    string GetOr(string key, string fallback);

    /// <summary>
    /// Попытка получить строку. true — успех, out string value — локализованный текст.
    /// </summary>
    bool TryGet(string key, out string? value);

    /// <summary>
    /// Текущая выбранная локаль ("ru", "en", ...).
    /// </summary>
    string CurrentLocale { get; }

    /// <summary>
    /// Сменить локаль в рантайме (например, по команде !lang ru).
    /// Вызывает перезагрузку словаря провайдером. Возвращает true при успехе.
    /// </summary>
    bool SetLocale(string locale);
}

/// <summary>
/// JsonLocalizationProvider — JSON-file-backed реализация ILocalizationProvider.
/// Ищет lang/{locale}.json относительно ModuleDirectory/../../configs (root проекта).
/// </summary>
public sealed class JsonLocalizationProvider : ILocalizationProvider, IDisposable
{
    private readonly string _baseDir;
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>?> _cache = new();

    public JsonLocalizationProvider(string? moduleDir = null)
    {
        // lang/ лежит рядом с configs/, на 2 уровня выше ModuleDirectory плагина
        _baseDir = moduleDir != null
            ? Path.GetFullPath(Path.Combine(moduleDir, "..", "..", "lang"))
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "lang"));
    }

    public IReadOnlyDictionary<string, string>? Load(string locale)
    {
        if (_cache.TryGetValue(locale, out var cached)) return cached;

        var path = Path.Combine(_baseDir, $"{locale}.json");
        if (!File.Exists(path)) { _cache[locale] = null; return null; }

        try
        {
            var json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            _cache[locale] = dict;
            return dict;
        }
        catch
        {
            _cache[locale] = null;
            return null;
        }
    }

    public void Dispose()
    {
        _cache.Clear();
    }
}

/// <summary>
/// Сервис локализации (singleton/injectable).
/// Собирает ILocalizationProvider + текущую локаль + fallback (по умолчанию "en").
///
/// Рекомендуемый способ регистрации:
///
///   // В AetherionPlugin.Load():
///   var locProvider = new JsonLocalizationProvider(ModuleDirectory);
///   Loc.Initialize(locProvider, "ru");   // дефолт ru, fallback на en
///
/// Использование из любого System / Plugin / UI:
///
///   // Быстрый доступ (статический прокси):
///   public static class Loc
///   {
///       public static string? Get(string key) => _reader!.Get(key);
///       public static void Initialize(ILocalizationProvider provider, string? locale = null)
///       {
///           _reader = new LocalizationService(provider, locale ?? "en");
///       }
///       // ...
///   }
/// </summary>
public sealed class LocalizationService : ILocalizationReader, IDisposable
{
    private readonly ILocalizationProvider _provider;
    private string _locale;
    private IReadOnlyDictionary<string, string>? _dict;
    private const string DefaultFallback = "en";

    public LocalizationService(ILocalizationProvider provider, string? locale = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _locale = locale ?? DefaultFallback;
        LoadCurrent();
    }

    public string CurrentLocale => _locale;

    public string? Get(string key)
    {
        if (_dict != null && _dict.TryGetValue(key, out var val)) return val;
        // fallback
        if (_locale != DefaultFallback)
        {
            var en = _provider.Load(DefaultFallback);
            if (en != null && en.TryGetValue(key, out var enVal)) return enVal;
        }
        return null;
    }

    public string? Get(string locale, string key)
    {
        if (locale == _locale) return Get(key);
        var d = _provider.Load(locale);
        return d != null && d.TryGetValue(key, out var v) ? v : null;
    }

    public string? GetF(string key, params (string name, object value)[] args)
    {
        var template = Get(key);
        if (template == null) return null;
        if (args.Length == 0) return template;
        // Простая подстановка {name}. Строгость: необязательно — исключение не пробрасываем.
        try
        {
            // C# 6+ string interpolation не подойдёт, ключи динамические.
            // Выполняем замену вручную через индексаторы.
            var sb = new System.Text.StringBuilder(template);
            foreach (var (name, value) in args)
                sb.Replace("{" + name + "}", value?.ToString() ?? string.Empty);
            return sb.ToString();
        }
        catch { return template; }
    }

    public string GetOr(string key, string fallback)
    {
        return Get(key) ?? fallback;
    }

    public bool TryGet(string key, out string? value)
    {
        value = Get(key);
        return value != null;
    }

    public bool SetLocale(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return false;
        _locale = locale;
        LoadCurrent();
        return _dict != null;
    }

    private void LoadCurrent()
    {
        _dict = _provider.Load(_locale) ?? _provider.Load(DefaultFallback);
    }

    public void Dispose()
    {
        _dict = null;
    }
}
