using System;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace WcsInfinity.Systems;

public enum MutationType { DoubleUltDamage, SpeedBoost, LifestealAura, NoCooldowns, MegaShield }

public class MutationDef
{
    public MutationType Type;
    public string Name;
    public string Desc;
    public float Duration = 300f;
}

public class RaceMutationSystem
{
    private readonly IEngineApi _engine;
    private readonly CombatEffects _combat;
    private readonly Random _rng = new();
    private MutationDef? _activeMutation;
    private float _mutationTimer;
    private float _nextMutationIn = 3600f;

    private static readonly MutationDef[] _mutations =
    {
        new() { Type = MutationType.DoubleUltDamage, Name = "🔥 Берсерк", Desc = "Все ульты наносят x2 урон!" },
        new() { Type = MutationType.SpeedBoost, Name = "⚡ Скорость", Desc = "Все игроки получают +50% скорость!" },
        new() { Type = MutationType.LifestealAura, Name = "🩸 Вампиризм", Desc = "Все получают +20% вампиризм!" },
        new() { Type = MutationType.NoCooldowns, Name = "🌀 Хаос", Desc = "Кулдауны отключены на 5 минут!" },
        new() { Type = MutationType.MegaShield, Name = "🛡 Щит", Desc = "Все получают +100 брони!" },
    };

    public bool HasActiveMutation => _activeMutation != null;
    public MutationDef? ActiveMutation => _activeMutation;
    public float TimeLeft => _mutationTimer;

    public RaceMutationSystem(IEngineApi engine, CombatEffects combat)
    {
        _engine = engine;
        _combat = combat;
    }

    public void Tick()
    {
        if (_activeMutation != null)
        {
            _mutationTimer -= 1f;
            if (_mutationTimer <= 0)
            {
                Server.PrintToChatAll($" \x06[AETHERION] Мутация «{_activeMutation.Name}» закончилась!");
                RemoveMutationFromAll();
                _activeMutation = null;
            }
        }
        else
        {
            _nextMutationIn -= 1f;
            if (_nextMutationIn <= 0)
            {
                ActivateRandomMutation();
                _nextMutationIn = 3600f;
            }
        }
    }

    private void ActivateRandomMutation()
    {
        _activeMutation = _mutations[_rng.Next(_mutations.Length)];
        _mutationTimer = _activeMutation.Duration;

        Server.PrintToChatAll($" \x09[AETHERION] 🧬 МУТАЦИЯ: {_activeMutation.Name}!");
        Server.PrintToChatAll($" \x04{_activeMutation.Desc}");

        ApplyMutationToAll();
    }

    private void ApplyMutationToAll()
    {
        if (_activeMutation == null) return;

        foreach (var p in Utilities.GetPlayers())
        {
            if (p == null || !p.IsValid || p.IsBot) continue;

            switch (_activeMutation.Type)
            {
                case MutationType.SpeedBoost:
                    _engine.SetSpeed(p.Slot, 1.5f);
                    break;
                case MutationType.LifestealAura:
                    _combat.Apply(EffectTag.Lifesteal, p.Slot, 0.2f, _activeMutation.Duration);
                    break;
                case MutationType.MegaShield:
                    _engine.AddArmor(p.Slot, 100, 300);
                    break;
                case MutationType.DoubleUltDamage:
                case MutationType.NoCooldowns:
                    break;
            }
        }
    }

    public float GetDamageMultiplier()
    {
        return _activeMutation?.Type == MutationType.DoubleUltDamage ? 2f : 1f;
    }

    private void RemoveMutationFromAll()
    {
        if (_activeMutation == null) return;
        foreach (var p in Utilities.GetPlayers())
        {
            if (p == null || !p.IsValid || p.IsBot) continue;
            switch (_activeMutation.Type)
            {
                case MutationType.SpeedBoost:
                    _engine.SetSpeed(p.Slot, 1.0f);
                    break;
                case MutationType.LifestealAura:
                    _combat.RemoveEffect(p.Slot, EffectTag.Lifesteal);
                    break;
            }
        }
    }

    public bool IsNoCooldowns() => _activeMutation?.Type == MutationType.NoCooldowns;
}
