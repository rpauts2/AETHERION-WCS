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
using CS2MenuManager.API.Menu;
using CS2MenuManager.API.Enum;

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

    public static AetherionPlugin? Instance { get; private set; }

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
    private AetherSigils _sigils = null!;
    private SigilResonance _resonance = null!;
    private CombatEffects _combat = null!;
    private BossSystem _boss = null!;
    private AchievementSystem _achievements = null!;
    private NameplateManager _nameplates = null!;
    private WispCompanionV2 _wispCompanion = null!;
    private RaceWeaponSystem _raceWeapons = null!;
    private CustomWeaponSystem _customWeapons = null!;
    private IEngineApi _engine = null!;
    private IPlayerStore _store = null!;
    private StormWaveSystem _stormWave = null!;
    private DuelArenaSystem _duelArena = null!;
    private EtherPortalSystem _etherPortal = null!;
    private ContractSystem _contracts = null!;
    private RaceMutationSystem _mutation = null!;
    private LeaderboardSystem _leaderboard = null!;
    private CosmeticSystem _cosmetics = null!;

    // — Кэши рантайма —
    private readonly Dictionary<ulong, PlayerData> _online = new();
    private readonly Dictionary<ulong, int> _ether = new();
    private const int EtherMax = 150;
    private readonly Dictionary<ulong, float> _ultCooldown = new();
    private readonly Dictionary<ulong, float> _activeCd = new();
    private readonly HashSet<ulong> _boundOnce = new();
    private readonly Dictionary<ulong, int> _roundKills = new();
    private readonly Dictionary<ulong, float> _lastKillTime = new();
    private bool _firstBlood;
    private int _playTimeTick;

    public PlayerData Data(ulong steamId)
    {
        if (_online.TryGetValue(steamId, out var d)) return d;
        d = _store.Load(steamId) ?? new PlayerData { SteamId = steamId, Name = "" };
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
        Instance = this;
        Loc = new LocalizationService(new JsonLocalizationProvider(ModuleDirectory), "ru");
        L10n.Init(Loc);

        var cfgDir = Path.Combine(ModuleDirectory, "..", "..", "configs");
        _races.LoadFromFile(Path.Combine(cfgDir, "races.json"));
        _races.LoadFromFile(Path.Combine(cfgDir, "races", "races_core.json"));
        // индексируем способности
        foreach (var r in _races.Races.Values) r.IndexAbilities();
        _models.Load(cfgDir);
        _audio.Load(cfgDir);
        _seasons.Load(Path.Combine(cfgDir, "seasons.json"));
        RegisterListener<Listeners.OnServerPrecacheResources>(m => { _models.Precache(m); AuraManager.Precache(m); });

        _store = new SqlitePlayerStore(Path.Combine(ModuleDirectory, "aetherion.db"));
        _store.Init();

        // Загрузка гильдий из БД
        if (_store is SqlitePlayerStore sqlite)
        {
            var savedGuilds = sqlite.LoadAllGuilds();
            var memberMap = sqlite.LoadGuildMembers();
            foreach (var g in savedGuilds)
                GuildManager.Instance.RestoreGuild(g);
            GuildManager.Instance.RestoreMemberMap(memberMap);
            GuildManager.Instance.OnChanged = () => SaveGuilds();
        }

        // Движок и боевые эффекты
        _engine = new Cs2EngineApi();
        _combat = new CombatEffects(_engine);

        // UI / премиальные системы
        _nexus = new AetherNexus(this);
        _aetherRoulette = new AetherRoulette(this);
        _sigils = new AetherSigils(this, _engine, sid => _ether.GetValueOrDefault(sid, 0), SpendEther);
        _resonance = new SigilResonance(_engine);
        _sigils.OnCast = (p, name) =>
        {
            _resonance.OnSigilCast(p, name);
            _contracts.OnSigilUsed(p.SteamID);
        };
        _boss = new BossSystem(this, _engine, sid => Data(sid), pd => SaveData(pd), id => _races.Get(id),
            (sid, amount) => { _ether[sid] = Math.Min(EtherMax, _ether.GetValueOrDefault(sid, 0) + amount); });
        _boss.OnBossSpawn = () => { foreach (var pl in Utilities.GetPlayers()) if (pl != null && pl.IsValid && !pl.IsBot) _audio.PlayBossAwaken(pl); };
        _boss.OnBossAttack = () => { foreach (var pl in Utilities.GetPlayers()) if (pl != null && pl.IsValid && !pl.IsBot) _audio.PlayBossEnrage(pl); };
        _boss.OnBossDeath = () => { foreach (var pl in Utilities.GetPlayers()) if (pl != null && pl.IsValid && !pl.IsBot) _audio.PlayBossDeath(pl); };
        _boss.OnVoteStart = () => { foreach (var pl in Utilities.GetPlayers()) if (pl != null && pl.IsValid && !pl.IsBot) _audio.PlayBossVoteStart(pl); };

        // Ачивки
        _achievements = new AchievementSystem();
        _achievements.SetRaceManager(_races);
        _achievements.RegisterRaceAchievements(_races.Races.Keys);

        // Неймплейты над головой
        _nameplates = new NameplateManager();

        // Дух-компаньон V2 (3D-модель + аура по тиру связи)
        _wispCompanion = new WispCompanionV2(this);
        _wispCompanion.Initialize();
        _wispCompanion.OnEvolve = (player, title, stage) =>
        {
            _audio.PlayWispEvolve(player);
            player.PrintToCenter($"<font color='#7CFFA0'>✦ Дух эволюционировал: {title}! ✦</font>");
        };

        // Гильдии: крафт-рецепты + турнир
        GuildCraftSystem.InitDefaults();

        // Кастомное оружие по расе/архетипу
        _raceWeapons = new RaceWeaponSystem(this);
        _raceWeapons.Initialize();

        // Кастомное оружие с уникальной механикой (ФАЗА 6)
        _customWeapons = new CustomWeaponSystem(this, _engine);
        _customWeapons.Initialize();
        RegisterCustomWeapons();

        // Новые системы: PvE, Арена, Порталы, Контракты, Мутации
        _stormWave = new StormWaveSystem(_engine, sid => Data(sid), pd => SaveData(pd));
        _duelArena = new DuelArenaSystem(_engine, sid => Data(sid), pd => SaveData(pd));
        _etherPortal = new EtherPortalSystem(_engine, sid => Data(sid), pd => SaveData(pd), (sid, amount) => {
            _ether[sid] = Math.Min(EtherMax, _ether.GetValueOrDefault(sid, 0) + amount);
        });
        _contracts = new ContractSystem(sid => Data(sid), pd => SaveData(pd));
        _contracts.SetRaceLookup(id => _races.Get(id));
        _mutation = new RaceMutationSystem(_engine, _combat);
        _leaderboard = new LeaderboardSystem(sid => Data(sid), _store);
        _cosmetics = new CosmeticSystem();

        // Хуки событий
        RegisterEventHandler<EventPlayerDeath>(OnDeath);
        RegisterEventHandler<EventPlayerHurt>(OnHurt);
        RegisterEventHandler<EventBombPlanted>(OnBombPlanted);
        RegisterEventHandler<EventBombDefused>(OnBombDefused);
        RegisterEventHandler<EventPlayerSpawn>(OnSpawn);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);

        // Слушатели
        RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);

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
        AddCommand("css_admin", "Админ-меню AETHERION", CmdAdmin);
        AddCommand("css_bind", "Меню биндов клавиш", (p, _) => { if (p != null) OpenBindMenu(p); });
        AddCommand("css_shop", "Магазин", (p, _) => { if (p != null) OpenShopMenu(p); });
        AddCommand("css_buy", "Купить предмет", CmdBuy);
        AddCommand("css_daily", "Ежедневная награда", (p, _) => { if (p != null) TryClaimDaily(p); });
        AddCommand("css_yes", "Да (голосование)", (p, _) => { if (p != null) _boss.VoteYes(p); });
        AddCommand("css_no", "Нет (голосование)", (p, _) => { if (p != null) _boss.VoteNo(p); });
        AddCommand("css_top", "Топ игроков", CmdTop);
        AddCommand("css_ach", "Ачивки и дейлики", (p, _) => { if (p != null) OpenAchievementsMenu(p); });
        AddCommand("css_wisp", "Дух-компаньон (статус)", (p, _) => { if (p != null) OpenWispMenu(p); });
        AddCommand("css_bp", "Battle Pass", CmdBattlePass);
        AddCommand("css_storm", "Шторм (PvE)", (p, _) => { if (p != null) _stormWave.Start(p); });
        AddCommand("css_storm_stop", "Остановить шторм", (p, _) => { if (p != null) _stormWave.Stop(); });
        AddCommand("css_duel", "Дуэль", CmdDuel);
        AddCommand("css_contracts", "Контракты", (p, _) => { if (p != null) _contracts.ShowContracts(p); });
        AddCommand("css_claim", "Забрать награду контракта", CmdClaim);
        AddCommand("css_lb", "Лидерборд", (p, _) => { if (p != null) _leaderboard.ShowLeaderboard(p, "kills"); });
        AddCommand("css_lb_kills", "Лидерборд убийств", (p, _) => { if (p != null) _leaderboard.ShowLeaderboard(p, "kills"); });
        AddCommand("css_lb_level", "Лидерборд уровней", (p, _) => { if (p != null) _leaderboard.ShowLeaderboard(p, "level"); });
        AddCommand("css_lb_gold", "Лидерборд золота", (p, _) => { if (p != null) _leaderboard.ShowLeaderboard(p, "gold"); });
        AddCommand("css_cosmetics", "Косметика", (p, _) => { if (p != null) { var d = Data(p.SteamID); _cosmetics.ShowMenu(p, d); } });
        AddCommand("css_title", "Выбрать титул", CmdTitle);
        AddCommand("css_color", "Выбрать цвет", CmdColor);
        AddCommand("css_gt", "Гильдейский турнир", CmdGuildTournament);
        AddCommand("css_use", "Использовать предмет", CmdUse);
        AddCommand("css_inv", "Инвентарь", CmdInventory);

        // Тики
        AddTimer(0.5f, RiftTick, TimerFlags.REPEAT);
        AddTimer(1.0f, EtherRegen, TimerFlags.REPEAT);
        AddTimer(0.1f, HudTick, TimerFlags.REPEAT);
        AddTimer(1.0f, GameTick, TimerFlags.REPEAT);
        AddTimer(60.0f, PeriodicSave, TimerFlags.REPEAT);

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

    private void SaveGuilds()
    {
        if (_store is not SqlitePlayerStore sqlite) return;
        var all = GuildManager.Instance.All.Values.ToList();
        var memberMap = GuildManager.Instance.PlayerGuildMap;
        sqlite.SaveAllGuilds(all, memberMap);
    }

    private AbilityContext BuildCtx(CCSPlayerController p, PlayerData d, RaceProgress rp, int slot)
        => new()
        {
            Player = p, Data = d, Race = rp, Engine = _engine,
            Slot = slot, Combat = _combat
        };

    private bool VipMult(PlayerData d) => DateTimeOffset.FromUnixTimeSeconds(d.VipExpiresUnix) > DateTimeOffset.UtcNow;

    private void SyncBattlePass(PlayerData d)
    {
        int gained = SeasonSystem.ConvertXpToTiers(d, _seasons.Current.SeasonPass.FreeTierMaxLevel);
        if (gained > 0)
        {
            var player = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && p.SteamID == d.SteamId);
            if (player != null)
                player.PrintToChat($" \x0B[AETHERION]\x01 Battle Pass: +{gained} тир(ов)! Ранг: \x06{d.SeasonRank}");
        }
    }

    // Построить и повесить/обновить неймплейт над игроком (ФАЗА 2b).
    private void RefreshNameplate(CCSPlayerController p, PlayerData d, RaceProgress rp, RaceDefinition? def)
    {
        try
        {
            if (def == null) return;
            var title = _cosmetics.GetTitleDisplay(d);
            var colorHex = _cosmetics.GetColorDisplay(d);
            var rankBadge = _leaderboard.GetPlayerRankDisplay(p.SteamID, "level");
            var html = AetherHud.NameplateHtml(
                p.PlayerName, def.Name, rp.Level, rp.ParagonLevel,
                d.Division, rp.WispBond, VipMult(d), def.Tier,
                title, colorHex, rankBadge);
            var cosmeticColor = NameplateManager.ParseHexColor(colorHex);
            _nameplates.Attach(p, html, cosmeticColor);
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] nameplate attach err: {ex.Message}"); }
    }

    private void ApplyPassivesOnSpawn(CCSPlayerController p, PlayerData d, RaceProgress rp)
    {
        var def = _races.Get(d.CurrentRaceId);
        if (def == null) return;
        var ctx = BuildCtx(p, d, rp, p.Slot);
        try { _runtime.ApplyPassives(ctx, def, rp); } catch (Exception ex) { Console.WriteLine($"[AETHERION] passive apply err: {ex.Message}"); }
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

            // Звук спавна
            _audio.PlaySpawn(p, d.CurrentRaceId);

            // Кастомное оружие (скин по расе/архетипу)
            _raceWeapons.EquipOnSpawn(p, def);

            // Очистка боевых эффектов от прошлой жизни (ДО пассивок!)
            _combat.ClearPlayer(p.Slot);
            ApplyPassivesOnSpawn(p, d, rp);

            // Бонус HP от духа
            var wispBon = _wispCompanion.GetOwnerBonuses(p);
            float bonusHp = wispBon.GetValueOrDefault("bonus_hp", 0f);
            if (bonusHp > 0 && p.PlayerPawn?.Value != null)
            {
                var pawn = p.PlayerPawn.Value;
                pawn.MaxHealth = (int)(pawn.MaxHealth + bonusHp);
                pawn.Health = pawn.MaxHealth;
                Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
                Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iMaxHealth");
            }

            // Бонусы от предметов магазина
            var shopFx = ItemShop.AggregateEffects(d);
            float shopHp = shopFx.GetValueOrDefault("bonus_hp", 0f);
            if (shopHp > 0 && p.PlayerPawn?.Value != null)
            {
                var pawn = p.PlayerPawn.Value;
                pawn.MaxHealth = (int)(pawn.MaxHealth + shopHp);
                pawn.Health = pawn.MaxHealth;
                Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
                Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iMaxHealth");
            }
            float shopSpeed = shopFx.GetValueOrDefault("speed", 0f);
            if (shopSpeed > 0)
                _engine.SetSpeed(p.Slot, 1f + shopSpeed);

            // Стартовый эфир
            _ether[p.SteamID] = Math.Min(EtherMax, 50);
            _ultCooldown.Remove(p.SteamID);

            // Дейлики (обновление при новом дне)
            _achievements.RefreshDailies(d);

            // Неймплейт над головой
            RefreshNameplate(p, d, rp, def);

            // Дух-компаньон V2 — спавн тела над головой
            try { _wispCompanion.EnsureSpawned(p); } catch (Exception ex) { Console.WriteLine($"[AETHERION] wisp spawn err: {ex.Message}"); }

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
            // Null checks must come BEFORE any code that uses victim/attacker.
            if (victim == null || !victim.IsValid) return HookResult.Continue;

            // — Босс: фикс смерти (вызываем до проверки attacker) —
            _boss.OnPlayerDeath(victim, attacker, 0);

            // Жертва: гасим духа до респавна
            if (!victim.IsBot)
            {
                _wispCompanion.NotifyDeath(victim);
                _contracts.OnDeath(victim.SteamID);
            }

            if (attacker == null || !attacker.IsValid || attacker.IsBot) return HookResult.Continue;
            if (attacker.Slot == victim.Slot) return HookResult.Continue;

            var d = Data(attacker.SteamID);
            var rp = d.GetRace(d.CurrentRaceId);
            var def = _races.Get(d.CurrentRaceId);

            bool hs = ev.Headshot;
            // — XP и золото —
            long xp = LevelSystem.XpKill + (hs ? LevelSystem.XpHeadshot : 0);
            long gold = EconomySystem.GoldKill + (hs ? EconomySystem.GoldHeadshot : 0);
            if (VipMult(d)) { xp = (long)(xp * 1.25); gold = (long)(gold * 1.15); }

            // — Гильдейские бонусы —
            var guild = _guilds.Of(attacker.SteamID);
            if (guild != null)
            {
                var onlineMembers = _guilds.OnlineMembers(guild, Utilities.GetPlayers());
                xp = (long)(xp * (1f + guild.XpBonus));
                gold = (long)(gold * (1f + guild.GoldBonus));
                _guilds.AddBannerXp(guild, xp / 10); // 10% XP идёт в знамя
                GuildMenu.OnFrag(attacker.SteamID);

                // Турнирный фраг
                var tourney = GuildTournamentSystem.ActiveTournament;
                tourney?.OnFrag(attacker);
            }

            // Бонусы от предметов магазина
            var shopFxAtk = ItemShop.AggregateEffects(d);

            // Бонусы от магазина: knife_dmg (бонусный XP за нож)
            float knifeDmgBonus = shopFxAtk.GetValueOrDefault("knife_dmg", 0f);
            if (knifeDmgBonus > 0)
            {
                long knifeBonusXp = (long)(xp * knifeDmgBonus);
                long knifeBonusGold = (long)(gold * knifeDmgBonus);
                xp += knifeBonusXp;
                gold += knifeBonusGold;
            }

            EconomySystem.AddGold(d, gold);
            int up = LevelSystem.AddXp(d, rp, def?.TierEnum ?? RaceTier.T1_Spark, xp);
            if (up > 0) UnlockSystem.RefreshDivision(d);

            // Античит: золото не может превысить 999999
            if (d.Gold > 999999) d.Gold = 999999;

            // Сезонный XP
            d.SeasonXp += xp;
            SyncBattlePass(d);

            // Эфир за килл + бонус духа + бонус магазина
            var wispBonuses = _wispCompanion.GetOwnerBonuses(attacker);
            float etherMult = 1f + wispBonuses.GetValueOrDefault("ether_gain", 0f);
            etherMult += shopFxAtk.GetValueOrDefault("ether_gain", 0f);
            int baseEther = hs ? 18 : 12;
            _ether[attacker.SteamID] = Math.Min(EtherMax, _ether.GetValueOrDefault(attacker.SteamID, 0) + (int)(baseEther * etherMult));

            // Дух Wisp: рост связи
            rp.WispBond += hs ? 6 : 4;
            _wispCompanion.NotifyKill(attacker, hs);

            // Стрик
            _roundKills[attacker.SteamID] = _roundKills.GetValueOrDefault(attacker.SteamID, 0) + 1;
            _lastKillTime[attacker.SteamID] = Server.CurrentTime;
            int streak = _roundKills[attacker.SteamID];

            // Звуки стрика/первой крови
            if (!_firstBlood)
            {
                _firstBlood = true;
                _audio.PlayFirstBlood(attacker);
            }
            if (streak == 5) _audio.PlayMilestoneAce(attacker);

            // Партикл-эффект при хедшоте
            if (hs)
            {
                var vpos = _engine.GetPosition(victim.Slot);
                _engine.SpawnParticle("particles/aether_explosion.vpcf", vpos.x, vpos.y, vpos.z + 40);
            }

            // Ачивки (заглушки — будет расширено в AchievementSystem)
            TrackAchievements(d, attacker, victim, hs);

            // Контракты
            _contracts.OnKill(attacker.SteamID);
            if (hs) _contracts.OnHeadshot(attacker.SteamID);

            // Арена дуэлей
            _duelArena.OnKill(attacker, victim);

            // Гильдейский турнир
            GuildTournamentSystem.OnKill(attacker);

            // 1v1 дуэли
            GuildTournamentSystem.OnDuelKill(attacker, victim);

            // Шторм: фикс убийств ботов
            if (victim.IsBot) _stormWave.OnBotKilled();

            if (up > 0)
            {
                attacker.PrintToCenter($"⬆ {def?.Name} — уровень {rp.Level}!");
                _audio.PlayLevelUp(attacker, d.CurrentRaceId);
                RefreshNameplate(attacker, d, rp, def);
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
            if (victim == null || !victim.IsValid) return HookResult.Continue;

            _boss.OnPlayerHurt(victim, attacker, ev.DmgHealth);

            // Death Save от духа: если HP < урон — оставляем 1 HP (раз/раунд)
            if (!victim.IsBot && victim.PawnIsAlive
                && _wispCompanion.HasDeathSave(victim) && victim.PlayerPawn?.Value != null)
            {
                var vPawn = victim.PlayerPawn.Value;
                if (ev.DmgHealth >= vPawn.Health && vPawn.Health > 1)
                {
                    vPawn.Health = 1;
                    Utilities.SetStateChanged(vPawn, "CBaseEntity", "m_iHealth");
                    victim.PrintToChat($" \x0B✦ [Дух] Купол спасения! Ты выжил с 1 HP!");
                }
            }

            if (attacker == null || !attacker.IsValid || attacker.IsBot) return HookResult.Continue;

            // Контракты: урон
            _contracts.OnDamage(attacker.SteamID, ev.DmgHealth);

            // Прока пассивок «на удар» — хуки combat (лестник/reflect/etc) обрабатываются в CombatEffects
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] OnHurt err: {ex.Message}"); }
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
            UnlockSystem.RefreshDivision(d);
            p.PrintToChat(" \x04[+з за установку бомбы]\x01");
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] bomb planted err: {ex.Message}"); }
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
            UnlockSystem.RefreshDivision(d);
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] bomb defused err: {ex.Message}"); }
        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart ev, GameEventInfo info)
    {
        _roundKills.Clear();
        _lastKillTime.Clear();
        _firstBlood = false;
        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd ev, GameEventInfo info)
    {
        // Турнирный тик
        GuildTournamentSystem.OnRoundEnd();

        // Шторм: остановка по окончании раунда
        _stormWave.OnRoundEnd();

        // Периодическое сохранение гильдий
        try { SaveGuilds(); } catch (Exception ex) { Console.WriteLine($"[AETHERION] guild save err: {ex.Message}"); }

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
                UnlockSystem.RefreshDivision(d);
                d.SeasonXp += bonus;
                SyncBattlePass(d);

                // Контракты: победа в раунде
                _contracts.OnRoundWin(kv.Key);

                // Ачивки и дейлики за раунд
                var player = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && p.SteamID == kv.Key);
                if (player != null)
                    _achievements.OnRoundEnd(d, player, true);

                _store.Save(d);
            }
            catch (Exception ex) { Console.WriteLine($"[AETHERION] OnRoundEnd player err: {ex.Message}"); }
        }
        return HookResult.Continue;
    }

    private void OnClientDisconnect(int slot)
    {
        try { _nameplates.Remove(slot); } catch (Exception ex) { Console.WriteLine($"[AETHERION] nameplate remove err: {ex.Message}"); }
        try { _wispCompanion.CleanupSlot(slot); } catch (Exception ex) { Console.WriteLine($"[AETHERION] wisp cleanup err: {ex.Message}"); }
        try { _customWeapons.CleanupSlot(slot); } catch (Exception ex) { Console.WriteLine($"[AETHERION] weapon cleanup err: {ex.Message}"); }
        try { _auras.Remove(slot); } catch (Exception ex) { Console.WriteLine($"[AETHERION] aura remove err: {ex.Message}"); }
        try { _auras.RemoveAcc(slot); } catch (Exception ex) { Console.WriteLine($"[AETHERION] aura acc remove err: {ex.Message}"); }
        try { _sigils.CleanupSlot(slot); } catch (Exception ex) { Console.WriteLine($"[AETHERION] sigil cleanup err: {ex.Message}"); }
        // Clean up SteamID-keyed caches
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player != null && player.IsValid)
        {
            ulong sid = player.SteamID;
            // Сохранить данные перед очисткой (защита от потери прогресса)
            if (_online.TryGetValue(sid, out var d))
            {
                try { _store.Save(d); } catch (Exception ex) { Console.WriteLine($"[AETHERION] disconnect save err: {ex.Message}"); }
            }
            _online.Remove(sid);
            _ether.Remove(sid);
            _ultCooldown.Remove(sid);
            _activeCd.Remove(sid);
            _boundOnce.Remove(sid);
            _roundKills.Remove(sid);
            _lastKillTime.Remove(sid);
            try { _duelArena.Leave(player); } catch (Exception ex) { Console.WriteLine($"[AETHERION] duel leave err: {ex.Message}"); }
            try { _nexus.Close(player); } catch (Exception ex) { Console.WriteLine($"[AETHERION] nexus close err: {ex.Message}"); }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ТИКИ
    // ═══════════════════════════════════════════════════════════════════════
    private void RiftTick()
    {
        try { _combat.TickAll(); } catch (Exception ex) { Console.WriteLine($"[AETHERION] combat tick err: {ex.Message}"); }
        // AetherRiftEvent.Tick нужен список игроков + IEngineApi
        try
        {
            var players = Utilities.GetPlayers()
                .Where(p => p != null && p.IsValid && !p.IsBot && p.PawnIsAlive)
                .Select(p => (team: (int)p.TeamNum, slot: p.Slot))
                .ToList();
            var riftEvent = _rift.Tick(players, _engine, 0.5f);
            if (riftEvent != null) Server.PrintToChatAll(riftEvent);
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] rift tick err: {ex.Message}"); }
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

    private void GameTick()
    {
        try { Cs2EngineApi.FlushPendingBeams(); } catch (Exception ex) { Console.WriteLine($"[AETHERION] beam cleanup err: {ex.Message}"); }
        try { _stormWave.Tick(); } catch (Exception ex) { Console.WriteLine($"[AETHERION] storm tick err: {ex.Message}"); }
        try { _duelArena.Tick(); } catch (Exception ex) { Console.WriteLine($"[AETHERION] duel tick err: {ex.Message}"); }
        try { _etherPortal.Tick(); } catch (Exception ex) { Console.WriteLine($"[AETHERION] portal tick err: {ex.Message}"); }
        try { _mutation.Tick(); } catch (Exception ex) { Console.WriteLine($"[AETHERION] mutation tick err: {ex.Message}"); }
        try { _contracts.DailyReset(); } catch (Exception ex) { Console.WriteLine($"[AETHERION] contract reset err: {ex.Message}"); }

        // Контракты: учёт времени (раз в ~5 секунд чтобы не спамить)
        _playTimeTick++;
        if (_playTimeTick >= 5)
        {
            _playTimeTick = 0;
            try
            {
                foreach (var p in Utilities.GetPlayers())
                {
                    if (p != null && p.IsValid && !p.IsBot && p.PawnIsAlive)
                        _contracts.OnPlayTime(p.SteamID, 5f);
                }
            }
            catch (Exception ex) { Console.WriteLine($"[AETHERION] playtime contract err: {ex.Message}"); }
        }
    }

    private void HudTick()
    {
        try { _nameplates.Tick(); } catch (Exception ex) { Console.WriteLine($"[AETHERION] nameplate tick err: {ex.Message}"); }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  АКТИВНЫЕ СПОСОБНОСТИ И УЛЬТЫ
    // ═══════════════════════════════════════════════════════════════════════
    private void TryCastActive(CCSPlayerController p)
    {
        try
        {
            if (p == null || !p.IsValid || !p.PawnIsAlive) return;
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
            if (!_mutation.IsNoCooldowns() && _activeCd.TryGetValue(p.SteamID, out var cd) && now < cd)
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
            if (p == null || !p.IsValid || !p.PawnIsAlive) return;
            var d = Data(p.SteamID);
            var rp = d.GetRace(d.CurrentRaceId);
            var def = _races.Get(d.CurrentRaceId);
            if (def == null) return;
            var ab = def.Abilities.FirstOrDefault(a => a.Type == "Ultimate");
            if (ab == null) { p.PrintToChat(" \x07[AETHERION] У этой расы нет ульты."); return; }
            int lvl = rp.SkillLevels.GetValueOrDefault(ab.Index, 0);
            if (lvl <= 0)
            {
                p.PrintToChat(" \x07[AETHERION] Ульта не прокачана.");
                return;
            }
            float now = Server.CurrentTime;
            if (!_mutation.IsNoCooldowns() && _ultCooldown.TryGetValue(p.SteamID, out var cd) && now < cd)
            { p.PrintToChat($" \x07[AETHERION] Ульта перезаряжается: {(int)(cd - now)}с."); return; }
            int etherNeed = ab.EtherCost > 0 ? ab.EtherCost : 50;
            if (_ether.GetValueOrDefault(p.SteamID, 0) < etherNeed)
            { p.PrintToChat($" \x07[AETHERION] Мало Эфира ({_ether.GetValueOrDefault(p.SteamID)}/{etherNeed})."); return; }
            SpendEther(p.SteamID, etherNeed);
            var ctx = BuildCtx(p, d, rp, p.Slot);

            // Wisp Ultimate Assist: x1.5 к урону ульты
            float ultDmgMult = _wispCompanion.ConsumeUltAssistBuff(p);
            float mutDmgMult = _mutation.GetDamageMultiplier();
            float totalDmgMult = ultDmgMult * mutDmgMult;
            if (totalDmgMult > 1f) ctx.DamageMultiplier = totalDmgMult;

            if (_runtime.Activate(ctx, def, rp, ab.Index))
            {
                // Магазин: ult_cdr снижает КД ультимейта
                var shopFxUlt = ItemShop.AggregateEffects(d);
                float cdrMult = 1f - shopFxUlt.GetValueOrDefault("ult_cdr", 0f);
                _ultCooldown[p.SteamID] = now + Math.Max(4f, ab.Cooldown * cdrMult);
                _audio.PlayCastUltimate(p, d.CurrentRaceId);
                _achievements.OnUltCast(d, p);
                _boss.OnUltCast(p);
                _contracts.OnUltUsed(p.SteamID);

                // Кастомное оружие: ракетница запускает ракету при ульте
                if (_customWeapons.GetWeaponType(d.CurrentRaceId) == CustomWeaponType.RocketLauncher)
                    _customWeapons.FireRocket(p);

                string ultInfo = totalDmgMult > 1f ? $" (x{totalDmgMult:F1})" : "";
                p.PrintToCenterHtml($"<font color='#7c5cff'>✦ УЛЬТА: {ab.Name}{ultInfo} ✦</font>");
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
        var menu = new WasdMenu("✦ AETHERION WCS ✦", this);
        menu.MenuTime = 30;
        menu.ExitButton = true;

        // Статусная строка
        menu.AddItem($"⚡ {def?.Name ?? "—"} ур.{rp.Level} | D{d.Division} | {d.Gold}з | {_ether.GetValueOrDefault(p.SteamID)}⚡", null);

        // Кастомное оружие (если есть у расы)
        var cwType = _customWeapons.GetWeaponType(d.CurrentRaceId);
        if (cwType != CustomWeaponType.None)
            menu.AddItem($"🎒 {_customWeapons.WeaponTypeName(cwType)}", null);

        menu.AddItem("⚔ Расы и навыки", (pl, _) => OpenRaceMenu(pl));
        menu.AddItem("📊 Профиль и ранг", (pl, _) => OpenProfileMenu(pl));
        menu.AddItem("🛒 Магазин Эфира", (pl, _) => OpenShopMenu(pl));
        menu.AddItem("🎰 Рулетка", (pl, _) => CmdSpin(pl, null));
        menu.AddItem("🏆 Ачивки и дейлики", (pl, _) => OpenAchievementsMenu(pl));
        menu.AddItem("🛡 Гильдия", (pl, _) => OpenGuildMenu(pl));
        menu.AddItem("👹 Босс-рейд", (pl, _) => OpenBossMenu(pl));
        menu.AddItem("✨ Дух-компаньон", (pl, _) => OpenWispMenu(pl));
        menu.AddItem("🎁 Ежедневная награда", (pl, _) => TryClaimDaily(pl));
        menu.AddItem("🎯 Ультимейт", (pl, _) => TryCastUltimate(pl));
        menu.AddItem("💥 Активная способность", (pl, _) => TryCastActive(pl));
        menu.AddItem("🎲 Печати Эфира", (pl, _) => { if (pl != null) _sigils.BeginDraw(pl); });
        menu.AddItem("🔖 Бинды клавиш", (pl, _) => OpenBindMenu(pl));
        menu.AddItem("🌐 Nexus 3D", (pl, _) => _nexus.Open(pl, NexusSector.Hub));
        menu.AddItem("📋 Рейтинг", (pl, _) => CmdTop(pl, null));

        menu.Display(p, 30);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ПОДМЕНЮ: РАСЫ И НАВЫКИ
    // ═══════════════════════════════════════════════════════════════════
    private void OpenRaceMenu(CCSPlayerController p)
    {
        var d = Data(p.SteamID);
        var list = DynamicRaceSystem.Filter(_races, p, d.Division).Take(9).ToList();
        var menu = new WasdMenu("⚔ Расы и навыки", this);
        menu.MenuTime = 30;
        menu.PrevMenu = new WasdMenu("✦ AETHERION WCS ✦", this);
        menu.AddItem($"Дивизион {d.Division} — {list.Count} рас", null);
        foreach (var r in list)
        {
            int rid = r.Id;
            bool isCurrent = r.Id == d.CurrentRaceId;
            string mark = isCurrent ? "▶" : "  ";
            string tier = new string('★', Math.Min(r.Tier, 5));
            menu.AddItem($"{mark} {tier} {r.Name} [{r.Archetype}] T{r.Tier}", (pl, _) =>
            {
                if (!UnlockSystem.IsUnlocked(Data(pl.SteamID), _races.Get(rid)!, list.ToList()))
                { pl.PrintToChat(" \x07Раса заблокирована."); return; }
                Data(pl.SteamID).CurrentRaceId = rid;
                SaveData(Data(pl.SteamID));
                pl.PrintToChat($" \x04[AETHERION]\x01 Раса: {r.Name}");
                RefreshNameplate(pl, Data(pl.SteamID), Data(pl.SteamID).GetRace(rid), r);
            });
        }
        menu.AddItem("── Навыки ──", null);
        if (d.CurrentRaceId > 0)
        {
            var rp = d.GetRace(d.CurrentRaceId);
            var def = _races.Get(d.CurrentRaceId);
            if (def != null)
            {
                foreach (var ab in def.Abilities)
                {
                    int lvl = rp.SkillLevels.GetValueOrDefault(ab.Index, 0);
                    string t = ab.Type switch { "Ultimate" => "[ULT]", "Active" => "[ACT]", _ => "[PAS]" };
                    menu.AddItem($"  {t} {ab.Name} {lvl}/{ab.MaxLevel} — {ab.Description}", null);
                }
            }
        }
        menu.Display(p, 30);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ПОДМЕНЮ: ПРОФИЛЬ
    // ═══════════════════════════════════════════════════════════════════
    private void OpenProfileMenu(CCSPlayerController p)
    {
        var d = Data(p.SteamID);
        var rp = d.GetRace(d.CurrentRaceId);
        var def = _races.Get(d.CurrentRaceId);
        var menu = new WasdMenu("📊 Профиль", this);
        menu.MenuTime = 20;
        menu.AddItem($"Раса: {def?.Name ?? "—"} ур.{rp.Level}", null);
        menu.AddItem($"Paragon: ★{rp.ParagonLevel} | Очки: {rp.UnspentPoints}", null);
        menu.AddItem($"Дивизион: D{d.Division} | Золото: {d.Gold}з", null);
        menu.AddItem($"Сезон: ранг {d.SeasonRank} | XP {d.SeasonXp}", null);
        menu.AddItem($"Банк уровней: {d.LevelBank}", null);
        menu.AddItem($"VIP: {(VipMult(d) ? "✓ активен" : "✗ нет")}", null);
        menu.AddItem($"Связь духа: {rp.WispBond} ({WispEvolution.StageFor(rp.WispBond).Title})", null);
        menu.AddItem($"", null);
        menu.AddItem("!reset — сбросить очки навыков", (pl, _) => CmdReset(pl, null));
        menu.Display(p, 20);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ПОДМЕНЮ: ГИЛЬДИЯ (через WasdMenu)
    // ═══════════════════════════════════════════════════════════════════
    private void OpenGuildMenu(CCSPlayerController p)
    {
        var g = _guilds.Of(p.SteamID);
        var menu = new WasdMenu("🛡 Гильдия", this);
        menu.MenuTime = 20;
        if (g == null)
        {
            menu.AddItem("Ты не в гильдии", null);
            menu.AddItem("Создать: !guild create <имя> <тег>", null);
            menu.AddItem("Список: !guild list", null);
            menu.AddItem("Вступить: !guild join <id>", null);
            menu.AddItem("Рейтинг: !guild top", null);
        }
        else
        {
            menu.AddItem($"[{g.Tag}] {g.Name} ур.{g.BannerLevel}", null);
            menu.AddItem($"Казна: {g.Treasury}з | Члены: {g.Members.Count}/{g.MaxMembers}", null);
            menu.AddItem($"Бонусы: +{(int)(g.GoldBonus * 100)}% золото, +{(int)(g.XpBonus * 100)}% XP", null);
            menu.AddItem($"Внести золото (!guild donate <сумма>)", null);

            // Interactive craft menu
            var tier = (GuildTier)Math.Min(5, g.BannerLevel - 1);
            var recipes = GuildCraftSystem.ForTier(tier).ToList();
            if (recipes.Count > 0)
            {
                foreach (var r in recipes)
                {
                    bool canCraft = g.Treasury >= r.GoldCost;
                    string status = canCraft ? "\x04✓" : "\x07✗";
                    menu.AddItem($"{status} {r.Name} [{r.Tier}] — {r.GoldCost}з → {r.EffectDesc}", canCraft
                        ? (pl, _) =>
                        {
                            if (GuildCraftSystem.TryCraft(g, r.Id))
                            {
                                pl.PrintToChat($" \x04[AETHERION]\x01 {r.Name} создан! Знамя +{r.BannerXpReward}XP.");
                                _audio.PlayGuildBanner(pl);
                                OpenGuildMenu(pl); // refresh
                            }
                        } : null);
                }
            }
            else menu.AddItem("Нет доступных рецептов (повысь знамя)", null);

            menu.AddItem("Повысить офицера (!guild promote <slot>)", null);
            menu.AddItem("Покинуть гильдию", (pl, _) => GuildLeave(pl));
        }
        menu.Display(p, 20);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ПОДМЕНЮ: БОСС-РЕЙД
    // ═══════════════════════════════════════════════════════════════════
    private void OpenBossMenu(CCSPlayerController p)
    {
        var menu = new WasdMenu("👹 Босс-рейд", this);
        menu.MenuTime = 20;
        if (_boss.IsBossActive)
            menu.AddItem("⚔ БОСС АКТИВЕН! Используй !ult для урона!", null);
        else
            menu.AddItem("Вызвать босса (!boss → !yes/!no)", (pl, _) => _boss.StartVote(pl));
        menu.AddItem("Нужно минимум 2 голоса за 20 секунд", null);
        menu.AddItem("Боссы: Скиталец / Ледяной Титан / Штормовой Лорд", null);
        menu.AddItem("Повелитель Чумы / Ифрит Разрушения", null);
        menu.Display(p, 20);
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
            RefreshNameplate(p, d, d.GetRace(rid), r);
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
        string sub = info.ArgCount >= 2 ? info.GetArg(1).ToLowerInvariant() : "gold";

        long cost = sub == "race" ? 500 : 200;
        var d = Data(p.SteamID);
        if (!EconomySystem.SpendGold(d, cost))
        { p.PrintToChat($" \x07Нужно {cost}з для рулетки (у тебя {d.Gold}з)."); return; }

        if (sub == "race")
            _aetherRoulette.Spin(p, AetherRoulette.RouletteType.Race, AetherRoulette.RacePool());
        else
            _aetherRoulette.Spin(p, AetherRoulette.RouletteType.Gold, AetherRoulette.GoldPool());
    }

    public List<int> GetUnlockedRaces()
    {
        return _races.Races.Keys.ToList();
    }

    private void CmdGuild(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        if (info.ArgCount < 2)
        {
            GuildInfo(p);
            return;
        }
        var sub = info.GetArg(1).ToLowerInvariant();
        switch (sub)
        {
            case "create": GuildCreate(p, info); break;
            case "join": GuildJoin(p, info); break;
            case "leave": GuildLeave(p); break;
            case "donate": GuildDonate(p, info); break;
            case "info": GuildInfo(p); break;
            case "list": GuildList(p); break;
            case "craft": GuildCraft(p, info); break;
            case "promote": GuildPromote(p, info); break;
            case "top": GuildTop(p); break;
            case "tournament": GuildTournament(p, info); break;
            default: GuildInfo(p); break;
        }
    }

    private void GuildInfo(CCSPlayerController? p)
    {
        if (p == null) return;
        var g = _guilds.Of(p.SteamID);
        if (g == null)
        {
            p.PrintToChat(" \x0B═══ ГИЛЬДИЯ ═══");
            p.PrintToChat(" \x07Ты не в гильдии.");
            p.PrintToChat(" \x04!guild create <имя> <тег>\x01 — создать");
            p.PrintToChat(" \x04!guild list\x01 — список гильдий");
            p.PrintToChat(" \x04!guild join <id>\x01 — вступить");
            p.PrintToChat(" \x04!guild top\x01 — рейтинг");
            return;
        }
        p.PrintToChat($" \x0B═══ [{g.Tag}] {g.Name} ═══");
        p.PrintToChat($" \x01Знамя: \x06ур.{g.BannerLevel}\x01 | XP: {g.BannerXp}/{g.BannerXpNeeded}");
        p.PrintToChat($" \x01Казна: \x06{g.Treasury}з\x01 | Члены: {g.Members.Count}/{g.MaxMembers}");
        p.PrintToChat($" \x01Бонусы: \x04+{(int)(g.GoldBonus * 100)}% золото\x01, \x04+{(int)(g.XpBonus * 100)}% XP\x01");
        p.PrintToChat(" \x01Действия: \x04!guild donate <сумма> · craft · promote · leave");
    }

    private void GuildCreate(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        if (_guilds.Of(p.SteamID) != null) { p.PrintToChat(" \x07Ты уже в гильдии. Сначала покинь: !guild leave"); return; }
        if (info.ArgCount < 4) { p.PrintToChat(" \x07Формат: !guild create <имя> <тег>. Пример: !guild create Эфир ЭФ"); return; }
        var name = info.GetArg(2);
        var tag = info.GetArg(3).ToUpperInvariant();
        if (name.Length > 24 || tag.Length > 5) { p.PrintToChat(" \x07Имя до 24 символов, тег до 5."); return; }
        var g = _guilds.Create(p, name, tag);
        if (g == null) { p.PrintToChat(" \x07Не удалось создать (имя/тег занят?)."); return; }
        var d = Data(p.SteamID);
        d.GuildId = g.Id;
        SaveData(d);
        p.PrintToChat($" \x04[AETHERION]\x01 Гильдия [{g.Tag}] {g.Name} создана! Ты — лидер.");
        _audio.PlayGuildJoin(p);
        Server.PrintToChatAll($" \x0B[AETHERION]\x01 {p.PlayerName} создал гильдию \x04[{g.Tag}] {g.Name}\x01!");
    }

    private void GuildJoin(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        if (_guilds.Of(p.SteamID) != null) { p.PrintToChat(" \x07Ты уже в гильдии."); return; }
        if (info.ArgCount < 3 || !int.TryParse(info.GetArg(2), out var gid))
        { p.PrintToChat(" \x07Формат: !guild join <id>. Смотри ID в !guild list"); return; }
        if (_guilds.Join(p, gid))
        {
            var g = _guilds.Of(p.SteamID)!;
            var d = Data(p.SteamID);
            d.GuildId = g.Id;
            SaveData(d);
            p.PrintToChat($" \x04[AETHERION]\x01 Ты вступил в [{g.Tag}] {g.Name}!");
            _audio.PlayGuildJoin(p);
        }
        else p.PrintToChat(" \x07Не удалось вступить (гильдия полная или не существует).");
    }

    private void GuildLeave(CCSPlayerController? p)
    {
        if (p == null) return;
        if (_guilds.Of(p.SteamID) == null) { p.PrintToChat(" \x07Ты не в гильдии."); return; }
        _guilds.Leave(p);
        var d = Data(p.SteamID);
        d.GuildId = 0;
        SaveData(d);
        p.PrintToChat(" \x04[AETHERION]\x01 Ты покинул гильдию.");
    }

    private void GuildDonate(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        var g = _guilds.Of(p.SteamID);
        if (g == null) { p.PrintToChat(" \x07Ты не в гильдии."); return; }
        if (info.ArgCount < 3 || !long.TryParse(info.GetArg(2), out var amount) || amount <= 0)
        { p.PrintToChat($" \x07Формат: !guild donate <сумма>. Казна: {g.Treasury}з"); return; }
        var d = Data(p.SteamID);
        if (!EconomySystem.SpendGold(d, amount)) { p.PrintToChat($" \x07Недостаточно золота ({d.Gold}/{amount})."); return; }
        g.Treasury += amount;
        SaveData(d);
        p.PrintToChat($" \x04[AETHERION]\x01 Внесено {amount}з в казну. Всего: {g.Treasury}з");
        _audio.PlayGoldSpend(p);
    }

    private void GuildList(CCSPlayerController? p)
    {
        if (p == null) return;
        var all = _guilds.All.Values.ToList();
        if (all.Count == 0) { p.PrintToChat(" \x08Нет созданных гильдий."); return; }
        p.PrintToChat(" \x0B═══ СПИСОК ГИЛЬДИЙ ═══");
        foreach (var g in all)
            p.PrintToChat($"  \x06#{g.Id}\x01 [{g.Tag}] {g.Name} — ур.{g.BannerLevel} ({g.Members.Count} чел.)");
    }

    private void GuildCraft(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        var g = _guilds.Of(p.SteamID);
        if (g == null) { p.PrintToChat(" \x07Ты не в гильдии."); return; }
        var tier = (GuildTier)Math.Min(5, g.BannerLevel - 1);
        var recipes = GuildCraftSystem.ForTier(tier).ToList();
        if (info.ArgCount < 3 || !int.TryParse(info.GetArg(2), out var rid))
        {
            p.PrintToChat(" \x0B═══ КРАФТ ГИЛЬДИИ ═══");
            foreach (var r in recipes)
                p.PrintToChat($"  \x06#{r.Id}\x01 {r.Name} [{r.Tier}] — {r.GoldCost}з → +{r.BannerXpReward} XP знамени");
            p.PrintToChat(" \x01Крафт: \x04!guild craft <id>");
            return;
        }
        if (GuildCraftSystem.TryCraft(g, rid))
        {
            p.PrintToChat($" \x04[AETHERION]\x01 Предмет создан! Знамя +XP.");
            _audio.PlayGuildBanner(p);
        }
        else p.PrintToChat(" \x07Не хватает золота в казне или рецепт не найден.");
    }

    private void GuildPromote(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        var g = _guilds.Of(p.SteamID);
        if (g == null) { p.PrintToChat(" \x07Ты не в гильдии."); return; }
        if (g.LeaderSteamId != p.SteamID) { p.PrintToChat(" \x07Только лидер может повышать."); return; }
        if (info.ArgCount < 3)
        { p.PrintToChat(" \x07Формат: !guild promote <#slot>. Пример: !guild promote 3"); return; }
        if (!int.TryParse(info.GetArg(2), out var targetSlot)) { p.PrintToChat(" \x07Неверный слот."); return; }
        var target = Utilities.GetPlayerFromSlot(targetSlot);
        if (target == null || !target.IsValid) { p.PrintToChat(" \x07Игрок не найден."); return; }
        if (!g.Members.Contains(target.SteamID)) { p.PrintToChat(" \x07Этот игрок не в твоей гильдии."); return; }
        if (!g.Officers.Contains(target.SteamID))
        {
            g.Officers.Add(target.SteamID);
            p.PrintToChat($" \x04[AETHERION]\x01 {target.PlayerName} повышен до офицера.");
            target.PrintToChat($" \x04[AETHERION]\x01 Лидер повысил тебя до офицера [{g.Tag}]!");
        }
        else p.PrintToChat(" \x07Этот игрок уже офицер.");
    }

    private void GuildTop(CCSPlayerController? p)
    {
        if (p == null) return;
        var list = _guilds.TopByBanner(10).ToList();
        if (list.Count == 0) { p.PrintToChat(" \x08Нет гильдий."); return; }
        p.PrintToChat(" \x0B═══ РЕЙТИНГ ГИЛЬДИЙ ═══");
        int i = 1;
        foreach (var g in list)
            p.PrintToChat($"  \x06{i++}.\x01 [{g.Tag}] {g.Name} — ур.{g.BannerLevel} XP:{g.BannerXp} Казна:{g.Treasury}з");
    }

    private void GuildTournament(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        var g = _guilds.Of(p.SteamID);
        if (g == null) { p.PrintToChat(" \x07Ты не в гильдии."); return; }

        var active = GuildTournamentSystem.ActiveTournament;
        if (active != null)
        {
            if (active.State == TournamentState.Running)
            {
                var timeLeft = (int)active.TimeLeft.TotalMinutes;
                p.PrintToChat($" \x06Турнир «{active.Name}» идёт! Осталось {timeLeft}мин. Счёт: {active.ScoreA}:{active.ScoreB}");
            }
            else if (active.State == TournamentState.Registration)
            {
                active.Register(g);
                p.PrintToChat($" \x04Гильдия [{g.Tag}] зарегистрирована в турнире!");
            }
            return;
        }

        if (info.ArgCount < 3)
        {
            p.PrintToChat(" \x04!guild tournament create <имя>\x01 — создать турнир");
            p.PrintToChat(" \x04!guild tournament join\x01 — вступить в активный");
            return;
        }

        var sub = info.GetArg(2).ToLowerInvariant();
        if (sub == "create")
        {
            string name = info.ArgCount > 3 ? info.GetArg(3) : $"Турнир {DateTime.Now:dd.MM}";
            var tourney = GuildTournamentSystem.Create(name, TimeSpan.FromMinutes(30));
            tourney.State = TournamentState.Registration;
            tourney.Register(g);
            Server.PrintToChatAll($" \x06[AETHERION] ⚔ Турнир «{name}» создан! !guild tournament join для участия.");
        }
        else if (sub == "join")
        {
            if (active == null) { p.PrintToChat(" \x07Нет активного турнира."); return; }
            active.Register(g);
            p.PrintToChat($" \x04Гильдия [{g.Tag}] зарегистрирована!");
        }
    }

    private void CmdDuel(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        if (info.ArgCount < 2)
        {
            _duelArena.ShowStatus(p);
            p.PrintToChat(" \x04!duel <name>\x01 — вызвать");
            p.PrintToChat(" \x04!duel accept\x01 — принять");
            p.PrintToChat(" \x04!duel leave\x01 — покинуть");
            return;
        }
        var sub = info.GetArg(1).ToLowerInvariant();
        if (sub == "accept") { _duelArena.Accept(p); return; }
        if (sub == "leave") { _duelArena.Leave(p); return; }
        if (sub == "stats") { GuildTournamentSystem.ShowDuelStats(p); return; }

        string targetName = info.GetArg(1);
        var target = Utilities.GetPlayers()
            .FirstOrDefault(pl => pl != null && pl.IsValid && !pl.IsBot
                && pl.PlayerName.Contains(targetName, StringComparison.OrdinalIgnoreCase));
        if (target == null) { p.PrintToChat(" \x07Игрок не найден."); return; }
        _duelArena.Invite(p, target);
    }

    private void CmdClaim(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        if (info.ArgCount < 2 || !int.TryParse(info.GetArg(1), out int idx))
        { p.PrintToChat(" \x07Формат: !claim <1-3>"); return; }
        _contracts.Claim(p, idx - 1);
    }

    private void CmdTitle(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        if (info.ArgCount < 2) { p.PrintToChat(" \x07Формат: !title <id>. Список: !cosmetics"); return; }
        var d = Data(p.SteamID);
        _cosmetics.EquipTitle(d, info.GetArg(1));
        SaveData(d);
        p.PrintToChat($" \x04Титул установлен!");
    }

    private void CmdColor(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        if (info.ArgCount < 2) { p.PrintToChat(" \x07Формат: !color <id>. Список: !cosmetics"); return; }
        var d = Data(p.SteamID);
        _cosmetics.EquipColor(d, info.GetArg(1));
        SaveData(d);
        p.PrintToChat($" \x04Цвет ника установлен!");
    }

    private void CmdUse(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null || !p.PawnIsAlive) return;
        if (info.ArgCount < 2) { p.PrintToChat(" \x07Формат: !use <id предмета>. Список: !inv"); return; }
        if (!int.TryParse(info.GetArg(1), out int itemId)) { p.PrintToChat(" \x07Неверный ID."); return; }
        var d = Data(p.SteamID);
        var owned = d.Inventory.FirstOrDefault(o => o.ItemId == itemId && o.Count > 0);
        if (owned == null) { p.PrintToChat(" \x07Предмет отсутствует в инвентаре."); return; }
        var item = ItemShop.Get(itemId);
        if (item == null) { p.PrintToChat(" \x07Предмет не найден."); return; }

        if (item.Kind == ItemKind.Consumable)
        {
            // Активация расходника
            if (item.Effects.ContainsKey("berserk"))
            {
                float dmgMult = 1f + item.Effects["berserk"];
                float dur = item.Effects.GetValueOrDefault("dur", 8f);
                _combat.Apply(EffectTag.Berserk, p.Slot, dmgMult - 1f, dur);
                p.PrintToChat($" \x04{item.Name} активирован! +{(int)(item.Effects["berserk"]*100)}% урон на {dur}с");
            }
            else if (item.Effects.ContainsKey("invis"))
            {
                float dur = item.Effects.GetValueOrDefault("dur", 4f);
                _engine.SetInvisible(p.Slot, true);
                AddTimer(dur, () => { try { _engine.SetInvisible(p.Slot, false); } catch { } });
                p.PrintToChat($" \x04{item.Name} активирован! Невидимость {dur}с");
            }
            else if (item.Effects.ContainsKey("recall"))
            {
                var spawn = _engine.GetNearestSpawn(p.Slot, p.TeamNum);
                if (spawn.HasValue)
                {
                    _engine.Teleport(p.Slot, spawn.Value.x, spawn.Value.y, spawn.Value.z);
                    p.PrintToChat($" \x04{item.Name}: телепорт на спавн!");
                }
            }
            owned.Count--;
            if (owned.Count <= 0) d.Inventory.Remove(owned);
        }
        else
        {
            p.PrintToChat(" \x07Этот предмет нельзя использовать активно.");
        }
    }

    private void CmdInventory(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        var d = Data(p.SteamID);
        p.PrintToChat(" \x0B═══ ИНВЕНТАРЬ ═══");
        if (d.Inventory.Count == 0) { p.PrintToChat(" Пусто."); return; }
        foreach (var o in d.Inventory)
        {
            var item = ItemShop.Get(o.ItemId);
            string name = item?.Name ?? $"Предмет#{o.ItemId}";
            string kind = item?.Kind switch { ItemKind.Consumable => "[РАСХОДНИК]", _ => "" };
            p.PrintToChat($" [{o.ItemId}] {name} x{o.Count} {kind}");
        }
        p.PrintToChat(" \x09!use <id> — использовать");
    }

    private void PeriodicSave()
    {
        foreach (var kv in _online)
        {
            try { _store.Save(kv.Value); } catch { }
        }
    }

    private void CmdGuildTournament(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null) return;
        var sub = info.ArgCount >= 2 ? info.GetArg(1).ToLower() : "list";

        switch (sub)
        {
            case "list":
                GuildTournamentSystem.ShowTournaments(p);
                break;
            case "create":
                if (info.ArgCount < 3) { p.PrintToChat(" \x07Формат: !gt create <имя>"); return; }
                var t = GuildTournamentSystem.Create(info.GetArg(2));
                t.State = TournamentState.Registration;
                p.PrintToChat($" \x06Турнир #{t.Id} «{t.Name}» создан! Регистрация открыта.");
                break;
            case "join":
                if (info.ArgCount < 3 || !int.TryParse(info.GetArg(2), out int tid))
                { p.PrintToChat(" \x07Формат: !gt join <id>"); return; }
                var tourney = GuildTournamentSystem.Get(tid);
                if (tourney == null) { p.PrintToChat(" \x07Турнир не найден."); return; }
                var guild = GuildManager.Instance.Of(p.SteamID);
                if (guild == null) { p.PrintToChat(" \x07Ты не в гильдии."); return; }
                tourney.Register(guild);
                p.PrintToChat($" \x06Гильдия [{guild.Tag}] зарегистрирована!");
                break;
            case "start":
                if (info.ArgCount < 3 || !int.TryParse(info.GetArg(2), out int sid2))
                { p.PrintToChat(" \x07Формат: !gt start <id>"); return; }
                if (!CounterStrikeSharp.API.Modules.Admin.AdminManager.PlayerHasPermissions(p, "@css/config")) { p.PrintToChat(" \x07Нет прав."); return; }
                var tr = GuildTournamentSystem.Get(sid2);
                if (tr != null) tr.Start();
                break;
        }
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
        if (p == null || p.IsBot) return;
        string sort = (info.ArgCount >= 2 ? info.GetArg(1).ToLower() : "level") switch
        {
            "gold" => "gold",
            "xp" => "xp",
            _ => "level"
        };
        if (_store is not SqlitePlayerStore sqlite)
        {
            p.PrintToChat(" \x07Хранилище данных недоступно.");
            return;
        }
        var top = sqlite.TopPlayers(10, sort);
        p.PrintToChat(" \x0B═══ ТОП ИГРОКОВ ═══");
        if (top.Count == 0) { p.PrintToChat(" \x08Пока нет данных."); return; }
        int i = 1;
        foreach (var (sid, name, xp, lvl, gold) in top)
            p.PrintToChat($"  \x06{i++}.\x01 {name} — ур.\x04{lvl}\x01 XP:\x06{xp}\x01 З:\x06{gold}");
        p.PrintToChat(" \x01Сортировка: \x04!top [level|xp|gold]");
    }

    private void CmdBattlePass(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null || p.IsBot) return;
        var d = Data(p.SteamID);
        int maxTier = _seasons.Current.SeasonPass.FreeTierMaxLevel;
        long need = SeasonSystem.XpForTier(d.SeasonRank);
        p.PrintToChat(" \x0B═══ ✦ BATTLE PASS ✦ ═══");
        p.PrintToChat($" \x01Сезон: \x06{_seasons.Current.Name}");
        p.PrintToChat($" \x01Ранг: \x04{d.SeasonRank}/{maxTier}\x01 | XP: \x06{d.SeasonXp}/{need}");
        if (_seasons.IsActive)
            p.PrintToChat($" \x01Осталось: \x06{_seasons.TimeLeftLabel()}");
        if (info.ArgCount >= 2 && int.TryParse(info.GetArg(1), out int tier))
        {
            bool premium = info.ArgCount >= 3 && info.GetArg(2).ToLower() == "premium";
            if (_seasons.TryClaim(d, tier, premium))
            {
                SaveData(d);
                p.PrintToChat($" \x04[AETHERION]\x01 Тир {tier} забран! (Premium: {premium})");
            }
            else
                p.PrintToChat($" \x07Не удалось забрать тир {tier}.");
        }
        else
        {
            p.PrintToChat(" \x01Забрать награду: \x04!bp <номер_тира> [premium]");
        }
    }

    private void OpenShopMenu(CCSPlayerController p)
    {
        var d = Data(p.SteamID);
        p.PrintToChat(" \x0B═══ МАГАЗИН ЭФИРА ═══");
        p.PrintToChat($" \x06Твоё золото: {d.Gold}з");
        foreach (var it in ItemShop.Catalog)
            p.PrintToChat($"  \x06#{it.Id}\x01 {it.Name} — \x06{it.Price}з\x01 [{it.Rarity}]");
        p.PrintToChat(" \x01Покупка: \x04!buy <id>");
    }

    private void CmdBuy(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null || p.IsBot) return;
        if (info.ArgCount < 2) { p.PrintToChat(" \x07Формат: !buy <id предмета>. Список: !shop"); return; }
        if (!int.TryParse(info.GetArg(1), out int itemId)) { p.PrintToChat(" \x07Неверный ID."); return; }
        var d = Data(p.SteamID);
        var (ok, msg) = ItemShop.Buy(d, itemId);
        if (ok) { SaveData(d); _audio.PlayDailyReward(p); }
        p.PrintToChat(ok ? $" \x04[AETHERION]\x01 {msg}" : $" \x07[AETHERION] {msg}");
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
        _audio.PlayDailyReward(p);
    }

    private void CmdAdmin(CCSPlayerController? p, CommandInfo info)
    {
        if (p == null || p.IsBot) return;
        if (!CounterStrikeSharp.API.Modules.Admin.AdminManager.PlayerHasPermissions(p, "@css/config"))
        {
            p.PrintToChat(" \x07Нет прав. Требуется флаг @css/config.");
            return;
        }
        if (info.ArgCount < 2) { OpenAdminMenu(p); return; }
        var sub = info.GetArg(1).ToLowerInvariant();
        switch (sub)
        {
            case "gold": AdminGold(p, info); break;
            case "vip": AdminVip(p, info); break;
            case "lvl": AdminLvl(p, info); break;
            case "boss": AdminBoss(p); break;
            case "storm": AdminStorm(p); break;
            default: OpenAdminMenu(p); break;
        }
    }

    private void OpenAdminMenu(CCSPlayerController p)
    {
        p.PrintToChat(" \x0B═══ АДМИН-МЕНЮ ═══");
        p.PrintToChat(" \x04!admin gold <имя> <сумма> — выдать золото");
        p.PrintToChat(" \x04!admin vip <имя> <дни> — выдать VIP");
        p.PrintToChat(" \x04!admin lvl <имя> <уровни> — выдать уровни расы");
        p.PrintToChat(" \x04!admin boss — призвать босса (голосование)");
        p.PrintToChat(" \x04!admin storm — запустить Эфирную Бурю");
    }

    private CCSPlayerController? FindPlayer(string nameOrId)
    {
        foreach (var pl in Utilities.GetPlayers())
        {
            if (pl == null || !pl.IsValid || pl.IsBot) continue;
            if (pl.SteamID.ToString() == nameOrId) return pl;
            if (pl.PlayerName.Contains(nameOrId, StringComparison.OrdinalIgnoreCase)) return pl;
        }
        return null;
    }

    private void AdminGold(CCSPlayerController p, CommandInfo info)
    {
        if (info.ArgCount < 4) { p.PrintToChat(" \x04!admin gold <имя> <сумма>"); return; }
        var target = FindPlayer(info.GetArg(2));
        if (target == null) { p.PrintToChat(" \x07Игрок не найден."); return; }
        if (!long.TryParse(info.GetArg(3), out long amount) || amount <= 0) { p.PrintToChat(" \x07Неверная сумма."); return; }
        var d = Data(target.SteamID);
        EconomySystem.AddGold(d, amount);
        SaveData(d);
        p.PrintToChat($" \x04[ADMIN] +{amount}з\x01 выдано \x06{target.PlayerName}");
        target.PrintToChat($" \x06[ADMIN] Тебе выдано {amount} золота.");
    }

    private void AdminVip(CCSPlayerController p, CommandInfo info)
    {
        if (info.ArgCount < 4) { p.PrintToChat(" \x04!admin vip <имя> <дни>"); return; }
        var target = FindPlayer(info.GetArg(2));
        if (target == null) { p.PrintToChat(" \x07Игрок не найден."); return; }
        if (!int.TryParse(info.GetArg(3), out int days) || days <= 0) { p.PrintToChat(" \x07Неверное кол-во дней."); return; }
        var d = Data(target.SteamID);
        long extra = days * 86400L;
        long current = d.VipExpiresUnix;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        d.VipExpiresUnix = Math.Max(current, now) + extra;
        SaveData(d);
        p.PrintToChat($" \x04[ADMIN] VIP +{days}д\x01 выдан \x06{target.PlayerName}");
        target.PrintToChat($" \x06[ADMIN] Тебе выдан VIP на {days} дней.");
    }

    private void AdminLvl(CCSPlayerController p, CommandInfo info)
    {
        if (info.ArgCount < 4) { p.PrintToChat(" \x04!admin lvl <имя> <уровни>"); return; }
        var target = FindPlayer(info.GetArg(2));
        if (target == null) { p.PrintToChat(" \x07Игрок не найден."); return; }
        if (!int.TryParse(info.GetArg(3), out int levels) || levels <= 0) { p.PrintToChat(" \x07Неверное кол-во уровней."); return; }
        var d = Data(target.SteamID);
        var rp = d.GetRace(d.CurrentRaceId);
        for (int i = 0; i < levels; i++)
        {
            rp.Level++;
            rp.UnspentPoints++;
        }
        SaveData(d);
        var def = _races.Get(d.CurrentRaceId);
        p.PrintToChat($" \x04[ADMIN] +{levels} ур.\x01 выдано \x06{target.PlayerName} (раса: {def?.Name ?? "?"})");
        target.PrintToChat($" \x06[ADMIN] Тебе выдано {levels} уровней расы.");
    }

    private void AdminBoss(CCSPlayerController p)
    {
        _boss.StartVote(p);
        p.PrintToChat(" \x04[ADMIN] Голосование за босса запущено!");
    }

    private void AdminStorm(CCSPlayerController p)
    {
        _rift.Spawn(_engine);
        p.PrintToChat(" \x04[ADMIN] Эфирная Буря запущена!");
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
            _achievements.OnKill(d, killer, victim, hs, "");
            // Стрик
            int streak = _roundKills.GetValueOrDefault(killer.SteamID, 0);
            _achievements.OnRoundStreak(d, killer, streak);
        }
        catch (Exception ex) { Console.WriteLine($"[AETHERION] kill achievement err: {ex.Message}"); }
    }

    private void OpenWispMenu(CCSPlayerController p)
    {
        var d = Data(p.SteamID);
        var rp = d.GetRace(d.CurrentRaceId);
        var stage = WispEvolution.StageFor(rp.WispBond);
        var (progress, remaining, next) = WispEvolution.Progress(rp.WispBond);
        var bonuses = _wispCompanion.GetOwnerBonuses(p);

        p.PrintToChat(" \x0B═══ ✦ ДУХ-КОМПАНЬОН ✦ ═══");

        // Текущая ступень + прогресс
        string preview = _wispCompanion.BuildPreview(p);
        p.PrintToChat($" \x04{preview}");

        // Пассивные бонусы
        p.PrintToChat(" \x10── БОНУСЫ ДУХА ──");
        if (bonuses.Count == 0)
            p.PrintToChat(" \x08 Пассивные бонусы появятся на ступени 2+");
        else
        {
            if (bonuses.TryGetValue("ether_gain", out var eg) && eg > 0) p.PrintToChat($"  \x04+{(int)(eg * 100)}% эфира за киллы");
            if (bonuses.TryGetValue("bonus_hp", out var bh) && bh > 0) p.PrintToChat($"  \x04+{(int)bh} HP при спавне");
            if (bonuses.TryGetValue("sigil_cdr", out var sc) && sc > 0) p.PrintToChat($"  \x04-{(int)(sc * 100)}% КД печатей");
            if (bonuses.TryGetValue("death_save", out var ds) && ds > 0) p.PrintToChat($"  \x04Купол спасения: 1 HP при смертельном уроне (1/раунд)");
        }

        // Активные способности
        p.PrintToChat(" \x10── СПОСОБНОСТИ ──");
        p.PrintToChat($"  \x01Ст.3 (\x06200 Bond\x01): предупреждение о враге за спиной");
        p.PrintToChat($"  \x01Ст.4 (\x06600 Bond\x01): аура лечения союзников (3 HP, 200м)");
        p.PrintToChat($"  \x01Ст.5 (\x061500 Bond\x01): ультимейт-ассист (x1.5 к ульте)");

        // Все 5 ступеней
        p.PrintToChat(" \x10── СТУПНИ ЭВОЛЮЦИИ ──");
        foreach (var s in WispEvolution.Stages)
        {
            string marker = s.Stage <= stage.Stage ? "\x04★" : "\x08☆";
            string bond = s.BondRequired > 0 ? $" ({s.BondRequired})" : "";
            p.PrintToChat($"  {marker} \x01{bond} {s.Title}: {s.PassivePerk}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  КАСТОМНОЕ ОРУЖИЕ — привязка к расам (ФАЗА 6)
    // ═══════════════════════════════════════════════════════════════════
    private void RegisterCustomWeapons()
    {
        // === КРЮК ПУДЖА (pull) ===
        _customWeapons.RegisterRaceWeapon(2000, CustomWeaponType.PudgeHook);

        // === БЕСКОНЕЧНЫЙ USP (sniper, 1 патрон) ===
        _customWeapons.RegisterRaceWeapon(2012, CustomWeaponType.InfiniteUsp);
        _customWeapons.RegisterRaceWeapon(2038, CustomWeaponType.InfiniteUsp); // Yoru
        _customWeapons.RegisterRaceWeapon(1066, CustomWeaponType.InfiniteUsp); // Тень

        // === МЕЧ-УДАР (cone melee) ===
        _customWeapons.RegisterRaceWeapon(2020, CustomWeaponType.SwordStrike);
        _customWeapons.RegisterRaceWeapon(2037, CustomWeaponType.SwordStrike); // Deadpool
        _customWeapons.RegisterRaceWeapon(1043, CustomWeaponType.SwordStrike); // Клинок
        _customWeapons.RegisterRaceWeapon(1053, CustomWeaponType.SwordStrike); // Берсерк
        _customWeapons.RegisterRaceWeapon(1058, CustomWeaponType.SwordStrike); // Когти Бури

        // === РАКЕТНИЦА (explosive) ===
        _customWeapons.RegisterRaceWeapon(2010, CustomWeaponType.RocketLauncher);
        _customWeapons.RegisterRaceWeapon(2036, CustomWeaponType.RocketLauncher); // Master Chief
        _customWeapons.RegisterRaceWeapon(1073, CustomWeaponType.RocketLauncher); // Страж Бездны
        _customWeapons.RegisterRaceWeapon(1107, CustomWeaponType.RocketLauncher); // Небесный Страж
    }

    public void SaveData(PlayerData data) => _store.Save(data);

    private void OpenAchievementsMenu(CCSPlayerController p)
    {
        var d = Data(p.SteamID);
        var (unlocked, total) = _achievements.GetProgress(d);
        var dailies = _achievements.GetCurrentDailies(d);

        p.PrintToChat(" \x0B═══ ✦ АЧИВКИ И ДЕЙЛИКИ ✦ ═══");
        p.PrintToChat($" \x01Прогресс: \x06{unlocked}/{total}\x01 ачивок разблокировано");

        // Дейлики
        p.PrintToChat(" \x10── ДЕЙЛИКИ ДНЯ ──");
        if (dailies.Count == 0)
            p.PrintToChat(" \x08 Нет активных дейликов");
        foreach (var ch in dailies)
        {
            int prog = d.DailyChallenges.GetValueOrDefault(ch.Id, 0);
            int progReal = d.QuestCounters.GetValueOrDefault(ch.Counter, 0);
            bool done = d.ClaimedAchievements.Contains("daily_" + ch.Id);
            string status = done ? "\x04✓" : progReal >= ch.Target ? "\x06⚡" : $"\x01{progReal}/{ch.Target}";
            p.PrintToChat($"  {status} \x01{ch.Name}: {ch.Description} → +{ch.GoldReward}з +{ch.XpReward}XP");
        }

        // Последние ачивки
        var recent = _achievements.Achievements
            .Where(a => d.Achievements.Contains(a.Id))
            .OrderByDescending(a => a.Tier)
            .Take(5);
        p.PrintToChat(" \x10── ПОСЛЕДНИЕ АЧИВКИ ──");
        foreach (var a in recent)
        {
            string tierColor = a.Tier switch { 2 => "\x06", 3 => "\x0E", 4 => "\x0B", _ => "\x01" };
            p.PrintToChat($"  {tierColor}★ [{a.Tier}★] {a.Name}: {a.Description}");
        }
    }
}
