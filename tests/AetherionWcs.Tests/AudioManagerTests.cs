using System;
using System.Reflection;
using System.Text.Json;
using WcsInfinity.Systems;
using Xunit;

namespace AetherionWcs.Tests;

public sealed class AudioManagerTests
{
    private static readonly Type ManagerType = typeof(AudioManager);

    private static string? Resolve(string? primary, string? fallback, string? custom, bool enabled)
    {
        var method = ManagerType.GetMethod("Resolve", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("AudioManager.Resolve was not found.");
        return (string?)method.Invoke(null, new object?[] { primary, fallback, custom, enabled });
    }

    private static bool IsDirectSoundAsset(string sound)
    {
        var method = ManagerType.GetMethod("IsDirectSoundAsset", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("AudioManager.IsDirectSoundAsset was not found.");
        return (bool)(method.Invoke(null, new object?[] { sound }) ?? false);
    }

    private static bool IsSystemEntry(AudioManager.Entry entry)
    {
        var method = ManagerType.GetMethod("IsSystemEntry", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("AudioManager.IsSystemEntry was not found.");
        return (bool)(method.Invoke(null, new object?[] { entry }) ?? false);
    }

    private static string ResolveSoundEvent(string sound)
    {
        var method = ManagerType.GetMethod("ResolveSoundEvent", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("AudioManager.ResolveSoundEvent was not found.");
        return (string)(method.Invoke(null, new object[] { sound }) ?? string.Empty);
    }

    [Fact]
    public void Resolve_UsesCustomSoundOnlyAfterInstallerEnablesIt()
    {
        const string custom = "sounds/race/dragon_roar.vsnd";
        Assert.Equal("event.default_ult", Resolve("event.default_ult", "event.stock", custom, false));
        Assert.Equal(custom, Resolve("event.default_ult", "event.stock", custom, true));
    }

    [Fact]
    public void Resolve_UsesFallbackWhenNoPrimaryOrEnabledCustomExists()
    {
        Assert.Equal("event.stock", Resolve(null, "event.stock", "sounds/future.vsnd", false));
    }

    [Fact]
    public void Resolve_ReturnsNullWhenEveryCandidateIsMissing()
    {
        Assert.Null(Resolve(null, null, null, false));
    }

    [Theory]
    [InlineData("sounds/ui/click.vsnd", true)]
    [InlineData("SOUNDS/race/dragon_roar.vsnd", true)]
    [InlineData("weapons.awp.fire", false)]
    [InlineData("player/death1.wav", false)]
    [InlineData("sound/ui/click.wav", false)]
    [InlineData("sounds/ui/click.wav", false)]
    public void IsDirectSoundAsset_OnlyAcceptsSource2VirtualSoundPaths(string value, bool expected)
    {
        Assert.Equal(expected, IsDirectSoundAsset(value));
    }

    [Theory]
    [InlineData("{\"event\":\"system\",\"category\":\"boss\"}")]
    [InlineData("{\"event\":\"milestone\",\"category\":\"round\"}")]
    public void SystemJsonEntries_AreClassifiedAsSystemSounds(string json)
    {
        var entry = JsonSerializer.Deserialize<AudioManager.Entry>(json);
        Assert.NotNull(entry);
        Assert.True(IsSystemEntry(entry!));
    }

    [Fact]
    public void RaceEntry_IsNotClassifiedAsSystemSound()
    {
        var entry = JsonSerializer.Deserialize<AudioManager.Entry>("{\"race\":\"Пудж\",\"sound\":\"weapons.knife.hit\"}");
        Assert.NotNull(entry);
        Assert.False(IsSystemEntry(entry!));
    }

    [Theory]
    [InlineData("aetherion.boss.awaken", "ui.beacon")]
    [InlineData("aetherion.ambient.menu", "campaigns.any.theme")]
    [InlineData("aetherion.round.firstblood", "gamerules.firstblood")]
    [InlineData("aetherion.ult.pudge_tornado", "grenade.frag.explode")]
    [InlineData("aetherion.abi.pudge_hook", "weapons.knife.slash")]
    [InlineData("aetherion.abi.apostol_heal", "player.heal")]
    [InlineData("aetherion.abi.plasma_laser", "grenade.frag.explode")]
    public void MissingAddonSoundEvents_FallBackToKnownGameEvents(string input, string expected)
    {
        Assert.Equal(expected, ResolveSoundEvent(input));
    }
}
