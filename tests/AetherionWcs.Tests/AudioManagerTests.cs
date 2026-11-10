using System;
using System.Reflection;
using WcsInfinity.Systems;
using Xunit;

namespace AetherionWcs.Tests;

public sealed class AudioManagerTests
{
    private static readonly Type ManagerType = typeof(AudioManager);

    private static string? Resolve(string? primary, string? fallback, string? custom)
    {
        var method = ManagerType.GetMethod("Resolve", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("AudioManager.Resolve was not found.");
        return (string?)method.Invoke(null, new object?[] { primary, fallback, custom });
    }

    private static bool IsDirectSoundAsset(string sound)
    {
        var method = ManagerType.GetMethod("IsDirectSoundAsset", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("AudioManager.IsDirectSoundAsset was not found.");
        return (bool)(method.Invoke(null, new object?[] { sound }) ?? false);
    }

    [Fact]
    public void Resolve_PrefersExplicitCustomSoundOverBuiltInDefaults()
    {
        const string custom = "sounds/race/dragon_roar.vsnd";
        Assert.Equal(custom, Resolve("event.default_ult", "event.stock", custom));
    }

    [Fact]
    public void Resolve_UsesPrimaryWhenCustomSoundIsBlank()
    {
        Assert.Equal("event.race_ult", Resolve("event.race_ult", "event.stock", " "));
    }

    [Fact]
    public void Resolve_UsesFallbackWhenPrimaryAndCustomAreMissing()
    {
        Assert.Equal("event.stock", Resolve(null, "event.stock", null));
    }

    [Fact]
    public void Resolve_ReturnsNullWhenEveryCandidateIsMissing()
    {
        Assert.Null(Resolve(null, null, null));
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
}
