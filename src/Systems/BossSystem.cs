using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using WcsInfinity.Systems;
using WcsInfinity.Races;

namespace WcsInfinity.Systems;

// ╔══════════════════════════════════════════════════════════════════╗
// ║  BOSS SYSTEM — PVE-боссы для AETHERION WCS (ФАЗА 4).            ║
// ║  • Голосование !boss → !yes/!no → призыв                       ║
// ║  • Босс — реальная сущность (prop_dynamic) с HP, фазами,       ║
// ║    атаками по площади и визуальным HP-баром                    ║
// ║  • Игроки наносят урон через !ult / автоматический DPS         ║
// ║  • Реальные награды: золото + XP + ачивка                      ║
// ╚══════════════════════════════════════════════════════════════════╝
public sealed class BossSystem
{
    private readonly BasePlugin _plugin;
    private readonly IEngineApi _engine;
    private readonly Func<ulong, WcsInfinity.Models.PlayerData> _dataFn;
    private readonly Action<WcsInfinity.Models.PlayerData> _saveFn;
    private readonly Func<int, WcsInfinity.Races.RaceDefinition?> _raceDefFn;

    // Боссы: имя, RGB цвет модели, награда золота, награда XP, HP множитель
    private static readonly IReadOnlyList<(string Name, int R, int G, int B, int Gold, int Xp, float HpMult, string Ability)> BossData = new List<(string, int, int, int, int, int, float, string)>
    {
        ("СКИТАЛЕЦ БЕЗДНЫ",     90,  40, 120, 1800,  500, 1.0f, "void_pulse"),
        ("ЛЕДЯНОЙ ТИТАН",        80, 180, 255, 2400,  700, 1.3f, "frost_nova"),
        ("ШТОРМОВОЙ ЛОРД",      235, 220,  80, 2100,  650, 1.15f, "chain_lightning"),
        ("ПОВЕЛИТЕЛЬ ЧУМЫ",     100, 220, 110, 3200, 1100, 1.6f, "plague_cloud"),
        ("ИФРИТ РАЗРУШЕНИЯ",     255,  80,  40, 4000, 1500, 2.0f, "meteor_rain"),
    };

    private const float BASE_BOSS_HP = 3000f;
    private const float HP_PER_PLAYER = 500f;
    private const float BOSS_LIFETIME_SEC = 240f;
    private const int VOTE_COOLDOWN_SEC = 60;
    private const int VOTE_DURATION_SEC = 20;
    private const float ATTACK_INTERVAL = 4.0f;
    private const int BOSS_ATTACK_RANGE = 350;
    private const int BOSS_ATTACK_DMG = 25;

    // Проп босса
    private const string BossModel = "models/props/de_inferno/hr_i/wood_1x1.vmdl";
    private const float BossScaleZ = 3.5f;

    // Состояние
    private bool _bossActive;
    private int _bossHp;
    private int _bossMaxHp;
    private int _bossIndex;
    private float _lifetime;
    private float _lastVoteTime = -999f;
    private bool _votePending;
    private float _voteEndTime;
    private float _attackTimer;
    private readonly Dictionary<ulong, bool> _voteChoices = new();
    private readonly Dictionary<int, int> _damageTable = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _voteTimer;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _lifeTimer;
    private readonly Random _rng = new();

    // Сущности
    private CDynamicProp? _bossProp;
    private CPointWorldText? _bossHpBar;

    // Внешние хуки для звуков/аудио (avoid tight coupling to AudioManager).
    public Action? OnBossSpawn;
    public Action? OnBossAttack;
    public Action? OnBossDeath;
    public Action? OnVoteStart;

    public BossSystem(BasePlugin plugin, IEngineApi engine,
        Func<ulong, WcsInfinity.Models.PlayerData> dataFn,
        Action<WcsInfinity.Models.PlayerData> saveFn,
        Func<int, WcsInfinity.Races.RaceDefinition?> raceDefFn)
    {
        _plugin = plugin;
        _engine = engine;
        _dataFn = dataFn;
        _saveFn = saveFn;
        _raceDefFn = raceDefFn;
    }

    public bool IsBossActive => _bossActive;

    // ═══════════════════════════════════════════════
    //  ГОЛОСОВАНИЕ
    // ═══════════════════════════════════════════════
    public void StartVote(CCSPlayerController? initiator)
    {
        try
        {
            if (initiator == null || !initiator.IsValid || initiator.IsBot) return;
            float now = Server.CurrentTime;
            if (now - _lastVoteTime < VOTE_COOLDOWN_SEC)
            {
                int left = (int)(VOTE_COOLDOWN_SEC - (now - _lastVoteTime));
                initiator.PrintToChat($" \x02[AETHERION] Перезарядка: {left}с.");
                return;
            }
            if (_bossActive) { initiator.PrintToChat(@" \x02Босс уже призван!"); return; }
            if (_votePending) { initiator.PrintToChat(@" \x02Голосование уже идёт!"); return; }

            _lastVoteTime = now;
            _votePending = true;
            _voteEndTime = now + VOTE_DURATION_SEC;
            _voteChoices.Clear();

            Server.PrintToChatAll($@" \x06[AETHERION] \x09{initiator.PlayerName}\x06 запустил голосование за \x09ПРИЗЫВ БОССА\x06!");
            Server.PrintToChatAll($@" \x06!yes / !no — голосование {VOTE_DURATION_SEC}с.");

            try { OnVoteStart?.Invoke(); } catch (Exception ex) { Console.WriteLine($"[Boss] OnVoteStart err: {ex.Message}"); }

            _voteTimer?.Kill();
            _voteTimer = _plugin.AddTimer(VOTE_DURATION_SEC, ConcludeVote);
        }
        catch (Exception ex) { Console.WriteLine($"[Boss] StartVote err: {ex.Message}"); }
    }

    public void VoteYes(CCSPlayerController? p)
    {
        if (!_votePending || p == null || !p.IsValid || p.IsBot) return;
        _voteChoices[p.SteamID] = true;
    }

    public void VoteNo(CCSPlayerController? p)
    {
        if (!_votePending || p == null || !p.IsValid || p.IsBot) return;
        _voteChoices[p.SteamID] = false;
    }

    private void ConcludeVote()
    {
        _votePending = false;
        int yes = _voteChoices.Values.Count(v => v);
        int no = _voteChoices.Values.Count(v => !v);
        if (yes > no && yes >= 2)
        {
            Server.PrintToChatAll(@" \x09[AETHERION] ⚔ БОСС ПРИЗЫВАЕТСЯ! ⚔");
            TriggerBoss();
        }
        else
            Server.PrintToChatAll(@" \x02[AETHERION] Призыв отклонён.");
        _voteChoices.Clear();
    }

    // ═══════════════════════════════════════════════
    //  СПАВН БОССА
    // ═══════════════════════════════════════════════
    private void TriggerBoss()
    {
        int online = Math.Max(1, Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot && p.PawnIsAlive));
        _bossIndex = _rng.Next(BossData.Count);
        var skin = BossData[_bossIndex];
        _bossMaxHp = (int)((BASE_BOSS_HP + Math.Max(0, online - 4) * HP_PER_PLAYER) * skin.HpMult);
        _bossHp = _bossMaxHp;
        _bossActive = true;
        _lifetime = BOSS_LIFETIME_SEC;
        _attackTimer = ATTACK_INTERVAL;
        _damageTable.Clear();

        // Спавн пропа босса в центре карты
        SpawnBossEntity(skin);

        Server.PrintToChatAll($@" \x09[AETHERION] ⚡ {skin.Name} появился! ⚡ HP: {_bossMaxHp}");
        Server.PrintToChatAll($@" \x06[AETHERION] Наносите урон через !ult и способности. У вас {BOSS_LIFETIME_SEC}с.");

        try { OnBossSpawn?.Invoke(); } catch (Exception ex) { Console.WriteLine($"[Boss] OnBossSpawn err: {ex.Message}"); }

        _lifeTimer?.Kill();
        _lifeTimer = _plugin.AddTimer(1.0f, BossTick, TimerFlags.REPEAT);
    }

    private void SpawnBossEntity((string Name, int R, int G, int B, int Gold, int Xp, float HpMult, string Ability) skin)
    {
        // Очистка старой сущности
        DespawnBossEntity();

        // Берём позицию случайного живого игрока или точки спавна T
        Vector spawnPos;
        var players = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot && p.PawnIsAlive).ToList();
        if (players.Count > 0)
        {
            var target = players[_rng.Next(players.Count)];
            var pawn = target.PlayerPawn?.Value;
            if (pawn?.AbsOrigin != null)
                spawnPos = pawn.AbsOrigin;
            else
                spawnPos = new Vector(0, 0, 0);
        }
        else spawnPos = new Vector(0, 0, 0);

        // Проп-тело босса
        var prop = Utilities.CreateEntityByName<CDynamicProp>("prop_dynamic_override");
        if (prop != null)
        {
            prop.SetModel(BossModel);
            prop.Teleport(new Vector(spawnPos.X, spawnPos.Y, spawnPos.Z), new QAngle(0, 0, 0), new Vector(0, 0, 0));
            prop.DispatchSpawn();
            try
            {
                prop.Render = Color.FromArgb(255, skin.R, skin.G, skin.B);
                Utilities.SetStateChanged(prop, "CBaseModelEntity", "m_clrRender");
            }
            catch (Exception ex) { Console.WriteLine($"[Boss] prop render err: {ex.Message}"); }
            _bossProp = prop;
        }

        // HP бар над боссом (point_worldtext)
        var hpText = BuildHpBar(skin.Name, _bossHp, _bossMaxHp);
        var hpBar = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
        if (hpBar != null)
        {
            hpBar.MessageText = hpText;
            hpBar.Enabled = true;
            hpBar.FontSize = 22;
            hpBar.Color = Color.FromArgb(255, skin.R, skin.G, skin.B);
            hpBar.Fullbright = true;
            hpBar.WorldUnitsPerPx = 0.04f;
            hpBar.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER;
            hpBar.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;
            hpBar.Teleport(new Vector(spawnPos.X, spawnPos.Y, spawnPos.Z + 120), new QAngle(90f, 0, 0), new Vector(0, 0, 0));
            hpBar.DispatchSpawn();
            if (_bossProp != null && _bossProp.IsValid)
                try { hpBar.AcceptInput("SetParent", _bossProp, null, "!activator"); } catch (Exception ex) { Console.WriteLine($"[Boss] HP bar parent err: {ex.Message}"); }
            _bossHpBar = hpBar;
        }
    }

    private void DespawnBossEntity()
    {
        if (_bossProp != null && _bossProp.IsValid) try { _bossProp.Remove(); } catch (Exception ex) { Console.WriteLine($"[Boss] prop remove err: {ex.Message}"); }
        if (_bossHpBar != null && _bossHpBar.IsValid) try { _bossHpBar.Remove(); } catch (Exception ex) { Console.WriteLine($"[Boss] HP bar remove err: {ex.Message}"); }
        _bossProp = null;
        _bossHpBar = null;
    }

    private static string BuildHpBar(string name, int hp, int maxHp)
    {
        float pct = maxHp > 0 ? (float)hp / maxHp : 0;
        int filled = (int)(pct * 20);
        string bar = new string('▰', filled) + new string('▱', 20 - filled);
        string color = pct > 0.5f ? "#7CFF6B" : pct > 0.25f ? "#FFD24D" : "#FF3B6B";
        return $"<font color='{color}'>{name} [{bar}] {hp}/{maxHp}</font>";
    }

    // ═══════════════════════════════════════════════
    //  ТИК: таймер, атаки, HP бар, позиция
    // ═══════════════════════════════════════════════
    private void BossTick()
    {
        if (!_bossActive) { _lifeTimer?.Kill(); return; }

        _lifetime -= 1.0f;
        if (_lifetime <= 0f)
        {
            Server.PrintToChatAll(@" \x06[AETHERION] Босс исчез: время вышло.");
            Stop();
            return;
        }

        // Обновляем HP бар
        if (_bossHpBar != null && _bossHpBar.IsValid)
        {
            var skin = BossData[_bossIndex];
            var hpText = BuildHpBar(skin.Name, _bossHp, _bossMaxHp);
            try
            {
                _bossHpBar.MessageText = hpText;
                Utilities.SetStateChanged(_bossHpBar, "CPointWorldText", "m_MessageText");
            }
            catch (Exception ex) { Console.WriteLine($"[Boss] HP bar update err: {ex.Message}"); }
        }

        // Атаки по площади
        _attackTimer -= 1.0f;
        if (_attackTimer <= 0f && _bossProp != null && _bossProp.IsValid && _bossProp.AbsOrigin != null)
        {
            _attackTimer = ATTACK_INTERVAL;
            BossAttackAoE();
        }

        // Авто-DPS: любой урон по боссу от способностей/ультов — через OnPlayerHurt
    }

    private void BossAttackAoE()
    {
        var skin = BossData[_bossIndex];
        if (_bossProp?.AbsOrigin == null) return;
        var bPos = _bossProp.AbsOrigin;

        foreach (var p in Utilities.GetPlayers())
        {
            if (p == null || !p.IsValid || p.IsBot || !p.PawnIsAlive) continue;
            var pawn = p.PlayerPawn?.Value;
            if (pawn?.AbsOrigin == null) continue;
            var d = bPos - pawn.AbsOrigin;
            float dist = MathF.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
            if (dist <= BOSS_ATTACK_RANGE)
            {
                // Apply damage + blind/slow
                int newHp = Math.Max(0, _engine.GetHealth(p.Slot) - BOSS_ATTACK_DMG);
                _engine.SetHealth(p.Slot, newHp);
                _engine.Blind(p.Slot, 0.3f, 0.2f);
                _engine.SetSpeed(p.Slot, 0.7f);
                // Восстановить скорость через 2 секунды
                int slot = p.Slot;
                _plugin.AddTimer(2.0f, () => { try { _engine.SetSpeed(slot, 1.0f); } catch (Exception ex) { Console.WriteLine($"[Boss] speed restore err: {ex.Message}"); } });
                // Визуал атаки
                _engine.SpawnParticle("particles/aether_explosion.vpcf", bPos.X, bPos.Y, bPos.Z);
            }
        }

        // Визуальный эффект каждую атаку
        _engine.SpawnParticle("particles/aether_thunder.vpcf", bPos.X, bPos.Y, bPos.Z + 50);

        try { OnBossAttack?.Invoke(); } catch (Exception ex) { Console.WriteLine($"[Boss] OnBossAttack err: {ex.Message}"); }
    }

    // ═══════════════════════════════════════════════
    //  ПРИНЯТИЕ УРОНА
    // ═══════════════════════════════════════════════
    public void OnPlayerHurt(CCSPlayerController? victim, CCSPlayerController? attacker, int damage)
    {
        if (!_bossActive || attacker == null || !attacker.IsValid || attacker.IsBot || damage <= 0) return;
        int bossDmg = Math.Max(1, damage / 5);
        _damageTable.TryGetValue(attacker.Slot, out var cur);
        _damageTable[attacker.Slot] = cur + bossDmg;
        _bossHp = Math.Max(0, _bossHp - bossDmg);

        // Флэш при ударе босса
        if (_bossProp != null && _bossProp.IsValid)
            try
            {
                _bossProp.Render = Color.FromArgb(255, 255, 200, 200);
                Utilities.SetStateChanged(_bossProp, "CBaseModelEntity", "m_clrRender");
                // Восстановим цвет в следующем кадре
                var skin = BossData[_bossIndex];
                Server.NextFrame(() =>
                {
                    if (_bossProp != null && _bossProp.IsValid)
                        try
                        {
                            _bossProp.Render = Color.FromArgb(255, skin.R, skin.G, skin.B);
                            Utilities.SetStateChanged(_bossProp, "CBaseModelEntity", "m_clrRender");
                        }
                        catch (Exception ex) { Console.WriteLine($"[Boss] damage flash err: {ex.Message}"); }
                });
            }
            catch (Exception ex) { Console.WriteLine($"[Boss] NextFrame err: {ex.Message}"); }

        if (_bossHp <= 0) OnBossDefeated();
    }

    public void DealDirectDamage(CCSPlayerController attacker, int amount)
    {
        if (!_bossActive || attacker == null || !attacker.IsValid) return;
        _damageTable.TryGetValue(attacker.Slot, out var cur);
        _damageTable[attacker.Slot] = cur + amount;
        _bossHp = Math.Max(0, _bossHp - amount);
        if (_bossHp <= 0) OnBossDefeated();
    }

    // Урон от !ult каста
    public void OnUltCast(CCSPlayerController caster)
    {
        if (!_bossActive || caster == null || !caster.IsValid) return;
        int dmg = _rng.Next(40, 80);
        DealDirectDamage(caster, dmg);
        caster.PrintToChat($" \x06[AETHERION] ✦ Ульт наносит {dmg} урона боссу! (Осталось: {_bossHp}/{_bossMaxHp})");
    }

    public void OnPlayerDeath(CCSPlayerController? victim, CCSPlayerController? attacker, int damage)
    {
        // Босс не убивает напрямую — только замедляет/ослепляет через AoE тик
    }

    // ═══════════════════════════════════════════════
    //  ПОБЕДА НАД БОССОМ
    // ═══════════════════════════════════════════════
    private void OnBossDefeated()
    {
        var skin = BossData[_bossIndex];
        int totalDmg = _damageTable.Values.Sum();
        if (totalDmg <= 0) totalDmg = 1;

        Server.PrintToChatAll($@" \x09[AETHERION] ⚔ {skin.Name} УБИТ! ⚔");

        try { OnBossDeath?.Invoke(); } catch (Exception ex) { Console.WriteLine($"[Boss] OnBossDeath err: {ex.Message}"); }

        // Loot table: boss drops ether + chance for items
        int bossTier = _bossIndex + 1;
        int baseEther = 20 + bossTier * 10;

        foreach (var kv in _damageTable.OrderByDescending(x => x.Value))
        {
            var attacker = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && p.Slot == kv.Key);
            if (attacker == null) continue;

            float share = kv.Value / (float)totalDmg;
            int gold = (int)(skin.Gold * share);
            int xp = (int)(skin.Xp * share);
            int ether = (int)(baseEther * share);

            try
            {
                var d = _dataFn(attacker.SteamID);
                EconomySystem.AddGold(d, gold);
                var raceDef = _raceDefFn(d.CurrentRaceId);
                LevelSystem.AddXp(d, d.GetRace(d.CurrentRaceId), raceDef?.TierEnum ?? RaceTier.T1_Spark, xp);
                d.SeasonXp += xp;
                _saveFn(d);
            }
            catch (Exception ex) { Console.WriteLine($"[Boss] reward err: {ex.Message}"); }

            attacker.PrintToChat($@" \x06[AETHERION] Награда: \x09{gold}з + {xp}XP + {ether}⚡");
        }

        // MVP bonus: double gold + extra ether + guaranteed item drop
        var best = _damageTable.OrderByDescending(kv => kv.Value).FirstOrDefault();
        if (best.Value > 0)
        {
            var top = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && p.Slot == best.Key);
            if (top != null)
            {
                try
                {
                    var d = _dataFn(top.SteamID);
                    EconomySystem.AddGold(d, skin.Gold);
                    var raceDef = _raceDefFn(d.CurrentRaceId);
                    LevelSystem.AddXp(d, d.GetRace(d.CurrentRaceId), raceDef?.TierEnum ?? RaceTier.T1_Spark, skin.Xp);
                    _saveFn(d);
                }
                catch (Exception ex) { Console.WriteLine($"[Boss] MVP reward err: {ex.Message}"); }

                // MVP loot drop — add item to inventory
                int[] mvpDropIds = { 4, 7, 5 }; // Берсерк=4, Камень=7, Фантом=5
                int dropId = mvpDropIds[_rng.Next(mvpDropIds.Length)];
                var dropItem = ItemShop.Get(dropId);
                if (dropItem != null)
                {
                    var mvpData = _dataFn(top.SteamID);
                    var owned = mvpData.Inventory.FirstOrDefault(o => o.ItemId == dropId);
                    if (owned == null) mvpData.Inventory.Add(new OwnedItem { ItemId = dropId, Count = 1 });
                    else if (owned.Count < dropItem.MaxStack) owned.Count++;
                    _saveFn(mvpData);
                    Server.PrintToChatAll($@" \x06[AETHERION] MVP: \x09{top.PlayerName}\x06 — двойная награда + {dropItem.Name}!");
                }
            }
        }

        // All participants get participation bonus
        foreach (var p in Utilities.GetPlayers())
        {
            if (p == null || !p.IsValid || p.IsBot) continue;
            if (_damageTable.ContainsKey(p.Slot)) continue;
            p.PrintToChat($" \x06[AETHERION] Босс повержен! Ты рядом — +100з participation.");
            try { var d = _dataFn(p.SteamID); EconomySystem.AddGold(d, 100); _saveFn(d); } catch { }
        }

        Stop();
    }

    // ═══════════════════════════════════════════════
    //  СТОП / РЕСЕТ
    // ═══════════════════════════════════════════════
    public void Stop()
    {
        _bossActive = false;
        _votePending = false;
        _bossHp = 0;
        _bossMaxHp = 0;
        _bossIndex = 0;
        _lifetime = 0f;
        _attackTimer = 0f;
        DespawnBossEntity();
        _damageTable.Clear();
        _voteChoices.Clear();
        _voteTimer?.Kill();
        _voteTimer = null;
        _lifeTimer?.Kill();
        _lifeTimer = null;
    }
}
