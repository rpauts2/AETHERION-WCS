using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using WcsInfinity.Systems;

namespace WcsInfinity.Systems;

public sealed class BossSystem
{
    private readonly ILogger<BossSystem>? _logger;
    private readonly IEngineApi? _engine;
    private readonly BasePlugin? _plugin;

    private bool _bossActive;
    private int _bossHp;
    private int _bossMaxHp;
    private int _bossSkinIndex;
    private float _lifetime;
    private float _despawnTimer;
    private readonly Dictionary<int, int> _damageTable = new();

    private float _lastVoteTime = -999f;
    private bool _votePending;
    private float _voteEndTime;
    private readonly Dictionary<ulong, bool> _voteChoices = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _voteSummaryTimer;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _lifetimeTimer;

    private const int VOTE_COOLDOWN_SEC = 60;
    private const int VOTE_DURATION_SEC = 20;
    private const float BASE_BOSS_HP = 3000f;
    private const float HP_PER_PLAYER = 500f;
    private const float BOSS_LIFETIME_SEC = 240f;
    private const float REWARD_GOLD_PARTICIPANT_BASE = 300;
    private const float REWARD_XP_PARTICIPANT_BASE = 100;
    private const float REWARD_GOLD_KILLER_MULT = 2;
    private const float REWARD_XP_KILLER_MULT = 2;

    private static readonly IReadOnlyList<(string Name, int R, int G, int B, int Gold, int Xp)> Bosses = new List<(string, int, int, int, int, int)>
    {
        ("СКИТАЛЕЦ БЕЗДНЫ", 90, 40, 120, 1800, 500),
        ("ЛЕДЯНОЙ ТИТАН", 80, 180, 255, 2400, 700),
        ("ШТОРМОВОЙ ЛОРД", 235, 220, 80, 2100, 650),
        ("ПОВЕЛИТЕЛЬ ЧУМЫ", 100, 220, 110, 3200, 1100),
    };

    private readonly Random _rng = new();

    public BossSystem(BasePlugin? plugin = null, ILogger<BossSystem>? logger = null, IEngineApi? engine = null)
    {
        _plugin = plugin;
        _logger = logger;
        _engine = engine;
    }

    public void StartVote(CCSPlayerController? initiator)
    {
        try
        {
            if (initiator == null || !initiator.IsValid || initiator.IsBot)
            {
                initiator?.PrintToChat(" \x02[AETHERION] Голосование может начать только живой игрок.");
                return;
            }

            float now = Server.CurrentTime;
            if (now - _lastVoteTime < VOTE_COOLDOWN_SEC)
            {
                int left = (int)(VOTE_COOLDOWN_SEC - (now - _lastVoteTime));
                initiator.PrintToChat($@" \x02[AETHERION] Перезарядка голосования: {left}с.");
                return;
            }

            if (_bossActive)
            {
                initiator.PrintToChat(@" \x02[AETHERION] Босс уже призван!");
                return;
            }

            if (_votePending)
            {
                initiator.PrintToChat(@" \x02[AETHERION] Голосование уже идёт!");
                return;
            }

            StartVoteInternal(initiator);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[BossSystem] StartVote exception");
        }
    }

    public void TickBoss(CCSPlayerController? pawn)
    {
        if (!_bossActive)
        {
            return;
        }

        _lifetime -= 1f;
        if (_lifetime <= 0f)
        {
            Server.PrintToChatAll(@" \x06[AETHERION] Босс исчез: время вышло.");
            Stop();
        }
    }

    public void OnPlayerDeath(CCSPlayerController? victim, CCSPlayerController? attacker, int damage)
    {
        if (!_bossActive || _bossHp > 0)
        {
            return;
        }

        OnBossDefeated();
    }

    public void OnPlayerHurt(CCSPlayerController? victim, CCSPlayerController? attacker, int damage)
    {
        try
        {
            if (!_bossActive || attacker == null || !attacker.IsValid || attacker.IsBot || damage <= 0)
            {
                return;
            }

            int key = attacker.Slot;
            _damageTable.TryGetValue(key, out int current);
            _damageTable[key] = current + damage;
        }
        catch { }
    }

    public void Stop()
    {
        try
        {
            _bossActive = false;
            _votePending = false;
            _bossHp = 0;
            _bossMaxHp = 0;
            _bossSkinIndex = 0;
            _lifetime = 0f;
            _despawnTimer = 0f;
            _damageTable.Clear();
            _voteChoices.Clear();

            _voteSummaryTimer?.Kill();
            _voteSummaryTimer = null;
            _lifetimeTimer?.Kill();
            _lifetimeTimer = null;

            _logger?.LogInformation("[BossSystem] Stop: состояние сброшено.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[BossSystem] Stop exception");
        }
    }

    private void StartVoteInternal(CCSPlayerController initiator)
    {
        _lastVoteTime = Server.CurrentTime;
        _votePending = true;
        _voteEndTime = Server.CurrentTime + VOTE_DURATION_SEC;
        _voteChoices.Clear();

        Server.PrintToChatAll(@" \x06[AETHERION] \x09" + initiator.PlayerName + @"\x06 запустил голосование за \x09ПРИЗЫВ МИРОВОГО БОССА\x06!");
        Server.PrintToChatAll(@" \x06Отправляйте \x04!yes\x06 или \x04!no\x06 в чат. Голосование завершится через " + VOTE_DURATION_SEC + "с.");

        _voteSummaryTimer?.Kill();
        _voteSummaryTimer = _plugin?.AddTimer(VOTE_DURATION_SEC, () => ConcludeVote());
    }

    private void ConcludeVote()
    {
        try
        {
            if (!_votePending)
            {
                return;
            }

            _votePending = false;

            int yes = _voteChoices.Values.Count(v => v);
            int no = _voteChoices.Values.Count(v => !v);

            if (yes > no && yes >= 1)
            {
                Server.PrintToChatAll(@" \x09[AETHERION] ⚔ ГОЛОСОВАНИЕ ПРОЙДЕНО — БОСС ПРИЗЫВАЕТСЯ! ⚔");
                TriggerBossEvent();
            }
            else
            {
                Server.PrintToChatAll(@" \x02[AETHERION] Призыв босса отклонён сообществом.");
            }

            _voteChoices.Clear();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[BossSystem] ConcludeVote exception");
        }
    }

    private void TriggerBossEvent()
    {
        int online = Math.Max(1, Utilities.GetPlayers().Count(p => p != null && p.IsValid && !p.IsBot));
        _bossMaxHp = (int)(BASE_BOSS_HP + Math.Max(0, online - 4) * HP_PER_PLAYER);
        _bossHp = _bossMaxHp;
        _bossActive = true;
        _bossSkinIndex = _rng.Next(Bosses.Count);
        _lifetime = BOSS_LIFETIME_SEC;
        _damageTable.Clear();

        if (_engine != null)
        {
            var skin = Bosses[_bossSkinIndex];
            _engine.SetRenderTint(0, skin.R, skin.G, skin.B, 1);
        }

        Server.PrintToChatAll(@" \x09[AETHERION] ⚡ " + Bosses[_bossSkinIndex].Name + @" ⚡");

        _lifetimeTimer?.Kill();
        _lifetimeTimer = _plugin?.AddTimer(1f, () => LifetimeTick());
    }

    private void LifetimeTick()
    {
        if (!_bossActive)
        {
            _lifetimeTimer?.Kill();
            return;
        }

        _lifetime -= 1f;
        if (_lifetime <= 0f)
        {
            Server.PrintToChatAll(@" \x06[AETHERION] Босс исчез: время вышло.");
            Stop();
        }
    }

    private void OnBossDefeated()
    {
        int totalDamage = _damageTable.Values.Sum();
        if (totalDamage <= 0)
        {
            totalDamage = 1;
        }

        foreach (var kv in _damageTable)
        {
            var attacker = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && p.Slot == kv.Key);
            if (attacker == null)
            {
                continue;
            }

            float share = kv.Value / (float)totalDamage;
            int gold = (int)(REWARD_GOLD_PARTICIPANT_BASE * share);
            int xp = (int)(REWARD_XP_PARTICIPANT_BASE * share);
            GiveReward(attacker, gold, xp);
        }

        var best = _damageTable.OrderByDescending(kv => kv.Value).FirstOrDefault();
        if (best.Value > 0)
        {
            var top = Utilities.GetPlayers().FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && p.Slot == best.Key);
            if (top != null)
            {
                GiveReward(top, (int)(REWARD_GOLD_KILLER_MULT * REWARD_GOLD_PARTICIPANT_BASE), (int)(REWARD_XP_KILLER_MULT * REWARD_XP_PARTICIPANT_BASE));
                Server.PrintToChatAll(@" \x06[AETHERION] Финальный удар: \x09" + top.PlayerName + @"\x06 получает двойную награду!");
            }
        }

        Server.PrintToChatAll(@" \x09[AETHERION] БОСС УБИТ! Участники получили золото и опыт.");
        Stop();
    }

    private void GiveReward(CCSPlayerController player, int gold, int xp)
    {
        try
        {
            player.PrintToChat($@" \x06[AETHERION] Награда за босса: \x09{gold} золота\x06, \x09{xp} XP");
        }
        catch { }
    }
}
