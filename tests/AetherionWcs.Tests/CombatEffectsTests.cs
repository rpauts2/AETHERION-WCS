using System;
using WcsInfinity.Systems;
using Xunit;

namespace AetherionWcs.Tests;

public sealed class CombatEffectsTests
{
    [Fact]
    public void Haste_IsAppliedImmediately()
    {
        var engine = new RecordingEngine();
        var effects = new CombatEffects(engine);

        effects.Apply(EffectTag.Haste, slot: 1, value: 0.2f, durationSec: 4f);

        Assert.Equal(1.2f, engine.LastSpeed, 3);
    }

    [Fact]
    public void HasteAndSlow_ComposeMultiplicatively()
    {
        var engine = new RecordingEngine();
        var effects = new CombatEffects(engine);

        effects.Apply(EffectTag.Haste, slot: 1, value: 0.2f, durationSec: 4f);
        effects.Apply(EffectTag.Slow, slot: 1, value: 0.4f, durationSec: 3f);

        // 1.2 haste × 0.6 slow = 0.72 final movement multiplier.
        Assert.Equal(0.72f, engine.LastSpeed, 3);
    }

    [Fact]
    public void ClearPlayer_RemovesMovementModifiersAndRestoresBaseSpeed()
    {
        var engine = new RecordingEngine();
        var effects = new CombatEffects(engine);
        effects.Apply(EffectTag.Haste, slot: 1, value: 0.2f, durationSec: 4f);

        effects.ClearPlayer(1);

        Assert.Equal(1f, engine.LastSpeed, 3);
    }

    [Fact]
    public void RemoveEffect_RecomputesMovementAfterRemovingTheTag()
    {
        var engine = new RecordingEngine();
        var effects = new CombatEffects(engine);
        effects.Apply(EffectTag.Slow, slot: 1, value: 0.5f, durationSec: 4f);

        effects.RemoveEffect(1, EffectTag.Slow);

        Assert.Equal(1f, engine.LastSpeed, 3);
    }

    private sealed class RecordingEngine : IEngineApi
    {
        public float LastSpeed { get; private set; } = 1f;

        public void SetHealth(int slot, int hp) { }
        public void AddHealth(int slot, int amount, int max) { }
        public void SetSpeed(int slot, float multiplier) => LastSpeed = multiplier;
        public void SetGravity(int slot, float multiplier) { }
        public (float x, float y, float z) GetPosition(int slot) => (0, 0, 0);
        public void Teleport(int slot, float x, float y, float z) { }
        public void Knockback(int slot, float fromX, float fromY, float force) { }
        public void Ignite(int slot, float seconds) { }
        public void SetInvisible(int slot, bool on) { }
        public void PrintToCenter(int slot, string html) { }
        public void SpawnParticle(string path, float x, float y, float z) { }
        public void Beam(float x1, float y1, float z1, float x2, float y2, float z2, int r, int g, int b, float life) { }
        public void PlaySound(int slot, string sound) { }
        public int GetHealth(int slot) => 100;
        public int GetArmor(int slot) => 0;
        public void SetArmor(int slot, int value) { }
        public void AddArmor(int slot, int amount, int max) { }
        public void SetFrozen(int slot, bool frozen) { }
        public void Blind(int slot, float duration, float holdTime = 0.4f) { }
        public int ForEnemiesInRadius(int slot, float radius, Action<int> action) => 0;
        public int DamageRadius(int slot, float radius, int damage, bool enemiesOnly = true) => 0;
        public int ForAlliesInRadius(int slot, float radius, Action<int> action) => 0;
        public void SetRenderTint(int slot, int r, int g, int b, int glow = 1) { }
        public (float x, float y, float z)? GetNearestSpawn(int slot, int team) => null;
    }
}
