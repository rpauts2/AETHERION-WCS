using System;
using System.Reflection;
using WcsInfinity.Systems;
using Xunit;

namespace AetherionWcs.Tests;

public sealed class RaceMutationSystemTests
{
    private static int CalculateArmorAfterRemovingBonus(int currentArmor, int grantedArmor)
    {
        var method = typeof(RaceMutationSystem).GetMethod(
            "CalculateArmorAfterRemovingBonus",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Armor removal helper was not found.");

        return (int)(method.Invoke(null, new object[] { currentArmor, grantedArmor })
            ?? throw new InvalidOperationException("Armor removal helper returned null."));
    }

    [Theory]
    [InlineData(180, 100, 80)]
    [InlineData(50, 100, 0)]
    [InlineData(250, 0, 250)]
    [InlineData(-5, 100, 0)]
    [InlineData(100, -5, 100)]
    public void MegaShieldExpiry_RemovesOnlyTheGrantedArmor(
        int currentArmor,
        int grantedArmor,
        int expectedArmor)
    {
        Assert.Equal(expectedArmor, CalculateArmorAfterRemovingBonus(currentArmor, grantedArmor));
    }
}
