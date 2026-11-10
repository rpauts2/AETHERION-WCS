using System;
using System.Reflection;
using WcsInfinity.Systems;
using Xunit;

namespace AetherionWcs.Tests;

public sealed class AudioManagerTests
{
    private static readonly MethodInfo ResolveMethod =
        typeof(AudioManager).GetMethod("Resolve", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("AudioManager.Resolve was not found.");

    private static string? Resolve(string? primary, string? fallback, string? custom) =>
        (string?)ResolveMethod.Invoke(null, new object?[] { primary, fallback, custom });

    [Fact]
    public void Resolve_PrefersExplicitCustomSoundOverBuiltInDefaults()
    {
        const string custom = "sound/race/dragon_roar.wav";
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
}
