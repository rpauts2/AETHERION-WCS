using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using WcsInfinity.Models;
using WcsInfinity.Races;
using WcsInfinity.Systems;
using WcsInfinity.Plugins;
using WcsInfinity.Core;
using WcsInfinity.Database;
using WcsInfinity.UI;

namespace WcsInfinity.Core;

// ╔══════════════════════════════════════════════════════════════════════╗
// ║  AETHERION WCS — главный плагин-интегратор (game loop).                ║
// ║  Сшивает все подсистемы: расы, бой, экономику, сезоны, гильдии,        ║
// ║  боссов, дух Wisp, Печати, Nexus UI.                                   ║
// ╚══════════════════════════════════════════════════════════════════════╝
public class AetherionPlugin : BasePlugin
{
    public override string ModuleName => "AETHERION WCS";
    public override string ModuleVersion => "0.4.0";
    public override string ModuleAuthor => "AETHERION Team";

    public static ILocalizationReader Loc { get; private set; } = null!;

    // — Подсистемы —
    private readonly RaceManager _races = new();
    private readonly ModelManager _models = new();
    private readonly AudioManager _audio = new();
    private readonly AuraManager _auras = new();
    private readonly RouletteSystem _roulette = new();
    private readonly GuildManager _guilds = GuildManager.Instance;
    private readonly SeasonSystem _seasons = new();
    private readonly AetherRiftEvent _rift = new();
    private readonly RaceRuntime _runtime = new();
    private AetherNexus _nexus = null!;
    private AetherRoulette _aetherRoulette = null!;
    private AetherWisp _wisp = null!;
    private AetherSigils _sigils = null!;
    private SigilResonance _resonance = null!;
    private CombatEffects _combat = null!;
    private VipMenu _vipMenu = null!;
    private BossSystem _boss = null!;
    private IEngineApi _engine = null!;
    private IPlayerStore _store = null!;

    // — Кэши рантайма —
    private readonly Dictionary<ulong, PlayerData> _online = new();
    private readonly Dictionary<ulong, int> _ether = new();
    private const int EtherMax = 150;
    private readonly Dictionary<ulong, float> _ultCooldown = new();
    private readonly Dictionary<ulong, float> _activeCd = new();
    private readonly HashSet<ulong> _boundOnce = new();
    private readonly Dictionary<ulong, int> _roundKills = new();
    private readonly Dictionary<ulong, float> _lastKillTime = new();

    public PlayerData Data(ulong steamId)
    {
        if (_online.TryGetValue(steamId, out var d)) return d;
        d = _store.Load(steamId);
        _online[steamId] = d;
        return d;
    }

    public RaceManager Races => _races;
    public RaceRuntime Runtime => _runtime;
    public IEngineApi Engine => _engine;
    public CombatEffects Combat => _combat;
    public AuraManager Auras => _auras;
    public AudioManager Audio => _audio;
    public ModelManager Models => _models;

    // ═══════════════════════════════════════════════════════════════════════
    //  LOAD — инициализация всех подсистем и регистрация хуков/команд
    // ═══════════════════════════════════════════════════════════════════════
    public override void Load(bool hotReload)
    {
        Loc = new LocalizationService(new JsonLocalizationProvider(ModuleDirectory), "ru");
        L10n.Init(Loc);

        var cfgDir = Path.Combine(ModuleDirectory, "..", "..", "configs");
        _races.LoadFromFile(Path.Combine(cfgDir, "races.json"));
        // индексируем способности
        foreach (var r in _races.Races.Values) r.IndexAbilities();
        _models.Load(cfgDir);
        _audio.Load(cfgDir);
        RegisterListener<Listeners.OnServerPrecacheResources>(m => { _models.Precache(m); AuraManager.Precache(m); });

        _store = new SqlitePlayerStore(Path.Combine(ModuleDirectory, "aetherion.db"));
        _store.Init();

        // Движок и боевые эффекты
        _engine = new Cs2EngineApi();
        _combat = new CombatEffects(_engine);

        // UI / премиальные системы
        _nexus = new AetherNexus(this);
        _aetherRoulette = new AetherRoulette(this);
        _wisp = new AetherWisp(this);
        _sigils = new AetherSigils(this, _engine, sid => _ether.GetValueOrDefault(sid, 0), SpendEther);
        _resonance = new SigilResonance(_engine);
        _sigils.OnCast = (p, name) => _resonance.OnSigilCast(p, name);
        _vipMenu = new VipMenu();
        _boss = new BossSystem(this, null, _engine);

        // Хуки событий
        RegisterEventHandler<EventPlayerDeath>(OnDeath);
        RegisterEventHandler<EventPlayerHurt>(OnHurt);
        RegisterEventHandler<EventBombPlanted>(OnBombPlanted);
        RegisterEventHandler<EventBombDefused>(OnBombDefused);
        RegisterEventHandler<EventPlayerSpawn>(OnSpawn);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);

        // Команды
        AddCommand("css_wcs", "Меню AETHERION", (p, _) => { if (p != null) OpenMainMenu(p); });
        AddCommand("css_nexus", "AETHER NEXUS", (p, _) => { if (p != null) _nexus.Toggle(p); });
        AddCommand("css_races", "Расы", CmdRaces);
        AddCommand("css_rank", "Ранг", CmdRank);
        AddCommand("css_ult", "Ультимейт", CmdUlt);
        AddCommand("css_cast", "Активная способность", (p, _) => { if (p != null) TryCastActive(p); });
        AddCommand("css_spin", "Рулетка", CmdSpin);
        AddCommand("css_guild", "Гильдия", CmdGuild);
        AddCommand("css_reset", "Сброс навыков", CmdReset);
        AddCommand("css_boss", "Голосование за босса", (p, _) => { if (p != null) _boss.StartVote(p); });
        AddCommand("css_sigil", "Рисование Печати", (p, _) => { if (p != null) _sigils.BeginDraw(p); });
        AddCommand("css_admin", "Админ-меню AETHERION", (p, _) => { if (p != null) OpenAdminMenu(p); });
        AddCommand("css_bind", "Меню биндов клавиш", (p, _) => { if (p != null) OpenBindMenu(p); });
        AddCommand("css_shop", "Магазин", (p, _) => { if (p != null) OpenShopMenu(p); });
        AddCommand("css_daily", "Ежедневная награда", (p, _) => { if (p != null) TryClaimDaily(p); });
        AddCommand("css_yes", "Да (голосование)", (p, _) => { if (p != null) _boss.StartVote(p); });
        AddCommand("css_top", "Топ игроков", CmdTop);

        // Тики
        AddTimer(0.5f, RiftTick, TimerFlags.REPEAT);
        AddTimer(1.0f, EtherRegen, TimerFlags.REPEAT);
        AddTimer(0.1f, HudTick, TimerFlags.REPEAT);

        // Интеграция Nexus
        NexusIntegration.Wire(this, _nexus, _races, _aetherRoulette, steamId => Data(steamId), pd => SaveData(pd));

        Console.WriteLine($"[AETHERION] Загружено рас: {_races.Count}. Плагин v{ModuleVersion} готов.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ПОМОЩНИКИ
    // ═══════════════════════════════════════════════════════════════════════
    private void SpendEther(ulong sid, int amount)
    {
        if (_ether.TryGetValue(sid, out var v)) _ether[sid] = Math.Max(0, v - amount);
    }

    private AbilityContext BuildCtx(CCSPlayerController p, PlayerData d, RaceProgress rp, int slot)
        => new()
        {
            Player = p, Data = d, Race = rp, Engine = _engine,
            Slot = slot, Combat = _combat
        };

    private bool VipMult(PlayerData d) => DateTimeOffset.FromUnixTimeSeconds(d.VipExpiresUnix) > DateTimeOffset.UtcNow;

    private void ApplyPassivesOnSpawn(CCSPlayerController p, PlayerData d, RaceProgress rp)
    {
        var def = _races.Get(d.CurrentRaceId);
        if (def == null) return;
        var ctx = BuildCtx(p, d, rp, p.Slot);
        try { _runtime.ApplyPassives(ctx, def, rp); } catch { }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  СОБЫТИЯ
    // ═══════════════════════════════════════════════════════════════════════
    private HookResult OnSpawn(EventPlayerSpawn ev, GameEventInfo info)
    {
        var p = ev.Userid;
        if (p == null || !p.IsValid || p.IsBot) return HookResult.Continue;
        try
        {
            var d = Data(p.SteamID);
            var rp = d.GetRace(d.CurrentRaceId);
            var def = _races.Get(d.CurrentRaceId);

            // Применить модель + корона (аура) + пассивки
            _models.ApplyModel(p, d.CurrentRaceId);
            _models.ApplyTint(_engine, p.Slot, d.CurrentRaceId);
            _auras.Attach(p, d.Division);
            ApplyPassivesOnSpawn(p, d, rp);

            // Стартовый эфир
            _ether[p.SteamID] = Math.Min(EtherMax, 50);
            _ultCooldown.Remove(p.SteamID);

            // Welcome-биндинг (один раз)
            if (!_boundOnce.Contains(p.SteamID))
            {
                _boundOnce.Add(p.SteamID);
                p.PrintToChat(" \x0B[AETHERION]\x01 Привет! Команды: \x04!wcs\x01 (меню), \x04!ult\x01 (ульта), \x04!sigil\x01 (Печати).");
            }
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] OnSpawn err: {ex.Message}"); }
        return HookResult.Continue;
    }

    private HookResult OnDeath(EventPlayerDeath ev, GameEventInfo info)
    {
        var victim = ev.Userid;
        var attacker = ev.Attacker;
        try
        {
            // — Босс: фикс урона/смерти —
            _boss.OnPlayerDeath(victim, attacker, 0);

            if (attacker == null || !attacker.IsValid || attacker.IsBot) return HookResult.Continue;
            if (victim == null || !victim.IsValid) return HookResult.Continue;
            if (attacker.Slot == victim.Slot) return HookResult.Continue;

            var d = Data(attacker.SteamID);
            var rp = d.GetRace(d.CurrentRaceId);
            var def = _races.Get(d.CurrentRaceId);

            bool hs = ev.Headshot;
            // — XP и золото —
            long xp = LevelSystem.XpKill + (hs ? LevelSystem.XpHeadshot : 0);
            long gold = EconomySystem.GoldKill + (hs ? EconomySystem.GoldHeadshot : 0);
            if (VipMult(d)) { xp = (long)(xp * 1.25); gold = (long)(gold * 1.15); }
            EconomySystem.AddGold(d, gold);
            int up = LevelSystem.AddXp(d, rp, def?.TierEnum ?? RaceTier.T1_Spark, xp);

            // Сезонный XP
            d.SeasonXp += xp;

            // Эфир за килл
            _ether[attacker.SteamID] = Math.Min(EtherMax, _ether.GetValueOrDefault(attacker.SteamID, 0) + (hs ? 18 : 12));

            // Дух Wisp: рост связи
            rp.WispBond += hs ? 6 : 4;
            _wisp.OnKill(attacker, hs);

            // Стрик
            _roundKills[attacker.SteamID] = _roundKills.GetValueOrDefault(attacker.SteamID, 0) + 1;
            _lastKillTime[attacker.SteamID] = Server.CurrentTime;

            // Ачивки (заглушки — будет расширено в AchievementSystem)
            TrackAchievements(d, attacker, victim, hs);

            if (up > 0)
            {
                attacker.PrintToCenter($"⬆ {def?.Name} — уровень {rp.Level}!");
                _audio.PlayLevelUp(attacker, d.CurrentRaceId);
            }
            attacker.PrintToChat($" \x04[+{xp} XP, +{gold}з]\x01 {def?.Name} ур.{rp.Level} | Эфир: {_ether.GetValueOrDefault(attacker.SteamID)}");
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] OnDeath err: {ex.Message}"); }
        return HookResult.Continue;
    }

    private HookResult OnHurt(EventPlayerHurt ev, GameEventInfo info)
    {
        var victim = ev.Userid;
        var attacker = ev.Attacker;
        try
        {
            _boss.OnPlayerHurt(victim, attacker, ev.DmgHealth);
            if (attacker == null || !attacker.IsValid || attacker.IsBot) return HookResult.Continue;
            if (victim == null || !victim.IsValid) return HookResult.Continue;

            // Прока пассивок «на удар» — хуки combat (лестник/reflect/etc) обрабатываются в CombatEffects
        }
        catch { }
        return HookResult.Continue;
    }

    private HookResult OnBombPlanted(EventBombPlanted ev, GameEventInfo info)
    {
        var p = ev.Userid;
        if (p == null || !p.IsValid) return HookResult.Continue;
        try
        {
            var d = Data(p.SteamID);
            EconomySystem.AddGold(d, EconomySystem.GoldBombObjective);
            LevelSystem.AddXp(d, d.GetRace(d.CurrentRaceId), _races.Get(d.CurrentRaceId)?.TierEnum ?? RaceTier.T1_Spark, 60);
            p.PrintToChat(" \x04[+з за установку бомбы]\x01");
        }
        catch { }
        return HookResult.Continue;
    }

    private HookResult OnBombDefused(EventBombDefused ev, GameEventInfo info)
    {
        var p = ev.Userid;
        if (p == null || !p.IsValid) return HookResult.Continue;
        try
        {
            var d = Data(p.SteamID);
            EconomySystem.AddGold(d, EconomySystem.GoldBombObjective);
            LevelSystem.AddXp(d, d.GetRace(d.CurrentRaceId), _races.Get(d.CurrentRaceId)?.TierEnum ?? RaceTier.T1_Spark, 60);
        }
        catch { }
        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart ev, GameEventInfo info)
    {
        _roundKills.Clear();
        _lastKillTime.Clear();
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo info)
    {
        // Награда всем онлайн-игрокам
        foreach (var kv in _online)
        {
            try
            {
                var d = kv.Value;
                var rp = d.GetRace(d.CurrentRaceId);
                var def = _races.Get(d.CurrentRaceId);
                long bonus = LevelSystem.XpRoundWin;
                EconomySystem.AddGold(d, EconomySystem.GoldRoundWin);
                LevelSystem.AddXp(d, rp, def?.TierEnum ?? RaceTier.T1_Spark, bonus);
                d.SeasonXp += bonus;
                _store.Save(d);
            }
            catch { }
        }
        return HookResult.Continue;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ТИКИ
    // ═══════════════════════════════════════════════════════════════════════
    private void RiftTick()
    {
        try { _combat.TickAll(); } catch { }
        // AetherRiftEvent.Tick нужен список игроков + IEngineApi
        try
        {
            var players = Utilities.GetPlayers()
                .Where(p => p != null && p.IsValid && !p.IsBot && p.PawnIsAlive)
                .Select(p => (team: (int)p.TeamNum, slot: p.Slot))
                .ToList();
            _rift.Tick(players, _engine, 0.5f);
        }
        catch { }
    }

    private void EtherRegen()
    {
        // Пассивная регенерация эфира для живых игроков
        foreach (var p in Utilities.GetPlayers())
        {
            if (p == null || !p.IsValid || p.IsBot || !p.PawnIsAlive) continue;
            _ether[p.SteamID] = Math.Min(EtherMax, _ether.GetValueOrDefault(p.SteamID, 0) + 1);
        }
    }

    private void HudTick()
    {
        // Лёгкий HUD: раса/уровень/эфир — PrintToCenterFreq ограничен, делаем раз в ~1.5с
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  АКТИВНЫЕ СПОСОБНОСТИ И УЛЬТЫ
    // ═══════════════════════════════════════════════════════════════════════
    private void TryCastActive(CCSPlayerController p)
    {
        try
        {
            var d = Data(p.SteamID);
            var rp = d.GetRace(d.CurrentRaceId);
            var def = _races.Get(d.CurrentRaceId);
            if (def == null) return;
            // Найти первую Active-способность с прокачкой
            var ab = def.Abilities.FirstOrDefault(a => a.Type == "Active");
            if (ab == null) { p.PrintToChat(" \x07[AETHERION] У этой расы нет активной способности."); return; }
            int lvl = rp.SkillLevels.GetValueOrDefault(ab.Index, 0);
            if (lvl <= 0) { p.PrintToChat(" \x07[AETHERION] Сначала прокачай способность."); return; }
            float now = Server.CurrentTime;
            if (_activeCd.TryGetValue(p.SteamID, out var cd) && now < cd)
            { p.PrintToChat($" \x07[AETHERION] Перезарядка: {(int)(cd - now)}с."); return; }
            var ctx = BuildCtx(p, d, rp, p.Slot);
            if (_runtime.Activate(ctx, def, rp, ab.Index))
            {
                _activeCd[p.SteamID] = now + Math.Max(1.5f, ab.Cooldown);
                if (ab.EtherCost > 0) SpendEther(p.SteamID, ab.EtherCost);
                _audio.PlayCastActive(p, d.CurrentRaceId);
            }
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] CastActive err: {ex.Message}"); }
    }

    private void TryCastUltimate(CCSPlayerController p)
    {
        try
        {
            var d = Data(p.SteamID);
            var rp = d.GetRace(d.CurrentRaceId);
            var def = _races.Get(d.CurrentRaceId);
            if (def == null) return;
            var ab = def.Abilities.FirstOrDefault(a => a.Type == "Ultimate");
            if (ab == null) { p.PrintToChat(" \x07[AETHERION] У этой расы нет ульты."); return; }
            int lvl = rp.SkillLevels.GetValueOrDefault(ab.Index, 0);
            if (lvl <= 0) { p.PrintToChat(" \x07[AETHERION] Ульта не прокачана."); return; }
            float now = Server.CurrentTime;
            if (_ultCooldown.TryGetValue(p.SteamID, out var cd) && now < cd)
            { p.PrintToChat($" \x07[AETHERION] Ульта перезаряжается: {(int)(cd - now)}с."); return; }
            int etherNeed = ab.EtherCost > 0 ? ab.EtherCost : 50;
            if (_ether.GetValueOrDefault(p.SteamID, 0) < etherNeed)
            { p.PrintToChat($" \x07[AETHERION] Мало Эфира ({_ether.GetValueOrDefault(p.SteamID)}/{etherNeed})."); return; }
            SpendEther(p.SteamID, etherNeed);
            var ctx = BuildCtx(p, d, rp, p.Slot);
            if (_runtime.Activate(ctx, def, rp, ab.Index))
            {
                _ultCooldown[p.SteamID] = now + Math.Max(8f, ab.Cooldown);
                _audio.PlayCastUltimate(p, d.CurrentRaceId);
                p.PrintToCenterHtml($"<font color='#7c5cff'>✦ УЛЬТА: {ab.Name} ✦</font>");
            }
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] CastUlt err: {ex.Message}"); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  КОМАНДЫ
    // ═══════════════════════════════════════════════════════════════════════
    private void OpenMainMenu(CCSPlayerController p)
    {
        var d = Data(p.SteamID);
        var def = _races.Get(d.CurrentRaceId);
        var rp = d.GetRace(d.CurrentRaceId);
        p.PrintToChat(" \x0B═══ ✦ AETHERION WCS ✦ ═══");
        p.PrintToChat($" \x01Раса: \x04{def?.Name ?? "—"}\x01 ур.\x06{rp.Level} \x01| Дивизион \x0B{d.Division}");
        p.PrintToChat($" \x01Золото: \x06{d.Gold}\x01 | Эфир: \x06{_ether.GetValueOrDefault(p.SteamID)}\x01 | Сезон: \x06{d.SeasonRank}");
        p.PrintToChat(" \x01Меню: \x04!races !rank !ult !cast !shop !spin !daily");
        p.PrintToChat(" \x01Соц: \x04!guild !boss !top \x01| Премиум: \x04!nexus !sigil");
        if (def != null)
        {
            p.PrintToChat($" \x0BЛор:\x01 {def.Lore}");
            foreach (var ab in def.Abilities)
            {
                int lvl = rp.SkillLevels.GetValueOrDefault(ab.Index, 0);
                string t = ab.Type == "Ultimate" ? "\x10[ULT]\x01" : ab.Type == "Active" ? "\x05[ACT]\x01" : "\x08[PAS]\x01";
                p.PrintToChat($"  {t} {ab.Name} — ур.{lvl}/{ab.MaxLevel}. {ab.Description}");
            }
        }
        // Открыть Nexus как 3D-меню
        _nexus.Open(p, NexusSector.Hub);
    }

    private void CmdRaces(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        var d = Data(p.SteamID);
        var list = _races.RacesForDivision(d.Division).Take(15).ToList();
        p.PrintToChat($" \x0B═══ РАСЫ ДИВИЗИОНА {d.Division} ═══");
        foreach (var r in list)
            p.PrintToChat($"  \x06#{r.Id}\x01 {r.Name} [\x10{r.Archetype}\x01] T{r.Tier}");
        p.PrintToChat(" \x01Выбор: \x04!races <id>");

        if (info.ArgCount > 1 && int.TryParse(info.GetArg(1), out var rid))
        {
            var r = _races.Get(rid);
            if (r == null) { p.PrintToChat(" \x07Раса не найдена."); return; }
            if (!UnlockSystem.IsUnlocked(d, r, list.ToList()))
            { p.PrintToChat(" \x07Раса заблокирована. Нужно больше общего уровня."); return; }
            d.CurrentRaceId = rid;
            SaveData(d);
            p.PrintToChat($" \x04[AETHERION]\x01 Выбрана раса: {r.Name}");
        }
    }

    private void CmdRank(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        var d = Data(p.SteamID);
        var rp = d.GetRace(d.CurrentRaceId);
        var def = _races.Get(d.CurrentRaceId);
        p.PrintToChat(" \x0B═══ ПРОФИЛЬ ═══");
        p.PrintToChat($" \x01{def?.Name} ур.\x06{rp.Level}\x01 | Paragon:\x06{rp.ParagonLevel}\x01 | Очки:\x06{rp.UnspentPoints}");
        p.PrintToChat($" \x01Дивизион:\x06{d.Division}\x01 | Золото:\x06{d.Gold}\x01 | Сезон:\x06{d.SeasonRank}");
        p.PrintToChat($" \x01Уровней в банке:\x06{d.LevelBank}\x01 | VIP:\x06{(VipMult(d) ? "да" : "нет")}");
    }

    private void CmdUlt(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        TryCastUltimate(p);
    }

    private void CmdSpin(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        _aetherRoulette.Spin(p, AetherRoulette.RouletteType.Gold, AetherRoulette.GoldPool());
    }

    private void CmdGuild(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        var g = _guilds.Of(p.SteamID);
        if (g == null) { p.PrintToChat(" \x07Ты не в гильдии. Создай: !guild create <имя>"); return; }
        p.PrintToChat($" \x0B[{g.Tag}] {g.Name}\x01 | Знамя ур.{g.BannerLevel} | Казна:{g.Treasury}з");
    }

    private void CmdReset(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        var d = Data(p.SteamID);
        var rp = d.GetRace(d.CurrentRaceId);
        int total = rp.SkillLevels.Values.Sum();
        rp.SkillLevels.Clear();
        rp.UnspentPoints += total;
        SaveData(d);
        p.PrintToChat($" \x04[AETHERION]\x01 Сброшено {total} очков. Теперь у тебя {rp.UnspentPoints}.");
    }

    private void CmdTop(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        p.PrintToChat(" \x0B═══ ТОП ИГРОКОВ ═══");
        p.PrintToChat(" \x08(Включается после накопления статистики на сервере)");
    }

    private void OpenShopMenu(CCSPlayerController p)
    {
        var d = Data(p.SteamID);
        p.PrintToChat(" \x0B═══ МАГАЗИН ЭФИРА ═══");
        foreach (var it in ItemShop.Catalog)
            p.PrintToChat($"  \x06#{it.Id}\x01 {it.Name} — \x06{it.Price}з\x01 [{it.Rarity}]");
        p.PrintToChat(" \x01Покупка: \x04!buy <id>");
    }

    private void TryClaimDaily(CCSPlayerController p)
    {
        var d = Data(p.SteamID);
        var reward = DailyRewardSystem.CheckIn(d, DateTime.UtcNow);
        if (reward == null) { p.PrintToChat(" \x07[ЕЖЕДНЕВКА] Уже забрал сегодня. Приходи завтра!"); return; }
        EconomySystem.AddGold(d, reward.Gold);
        d.FreeSpins += reward.FreeSpins;
        SaveData(d);
        p.PrintToChat($" \x06[ЕЖЕДНЕВКА] День {reward.Day}: +{reward.Gold}з, +{reward.FreeSpins} спинов!");
    }

    private void OpenAdminMenu(CCSPlayerController p)
    {
        p.PrintToChat(" \x0B═══ АДМИН-МЕНЮ ═══");
        p.PrintToChat(" \x04!admin gold <игрок> <сумма> — выдать золото");
        p.PrintToChat(" \x04!admin vip <игрок> <дни> — выдать VIP");
        p.PrintToChat(" \x04!admin lvl <игрок> <уровень> — выдать уровни");
        p.PrintToChat(" \x04!admin boss — призвать босса");
        p.PrintToChat(" \x04!admin storm — запустить Эфирную Бурю");
    }

    private void OpenBindMenu(CCSPlayerController p)
    {
        p.PrintToChat(" \x0B═══ БИНДЫ КЛАВИШ ═══");
        p.PrintToChat(" \x04bind x ult\x01 — ульта на X");
        p.PrintToChat(" \x04bind c cast\x01 — активная на C");
        p.PrintToChat(" \x04bind v sigil\x01 — Печати на V");
        p.PrintToChat(" \x04bind z wcs\x01 — меню на Z");
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  АЧИВКИ (заглушка под AchievementSystem — ФАЗА 2)
    // ═══════════════════════════════════════════════════════════════════════
    private void TrackAchievements(PlayerData d, CCSPlayerController killer, CCSPlayerController victim, bool hs)
    {
        try
        {
            // «Первая кровь» — первый килл вообще
            if (!d.Achievements.Contains("first_blood"))
            { d.Achievements.Add("first_blood"); killer.PrintToChat(" \x10★ АЧИВКА: Первая кровь!"); }
        }
        catch { }
    }

    public void SaveData(PlayerData data) => _store.Save(data);
}
