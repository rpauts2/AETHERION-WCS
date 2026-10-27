using WcsInfinity.Models;
using WcsInfinity.Races;
using WcsInfinity.Systems;
using Xunit;

namespace AetherionWcs.Tests;

public sealed class EconomyAndProgressionTests
{
    [Fact]
    public void AddGold_CapsBalanceAtConfiguredMaximum()
    {
        var player = new PlayerData { Gold = 999_990 };
        EconomySystem.AddGold(player, 100);
        Assert.Equal(999_999, player.Gold);
        Assert.True(player.IsDirty);
    }

    [Fact]
    public void AddGold_LargeGrantCannotOverflowIntoNegativeBalance()
    {
        var player = new PlayerData { Gold = 10 };
        EconomySystem.AddGold(player, long.MaxValue);
        Assert.Equal(999_999, player.Gold);
    }

    [Fact]
    public void AddGold_NegativeGrantDoesNotRemoveCurrency()
    {
        var player = new PlayerData { Gold = 123 };
        EconomySystem.AddGold(player, -50);
        Assert.Equal(123, player.Gold);
    }

    [Fact]
    public void SpendGold_RejectsInvalidOrUnaffordableAmounts()
    {
        var player = new PlayerData { Gold = 100 };
        Assert.False(EconomySystem.SpendGold(player, 0));
        Assert.False(EconomySystem.SpendGold(player, -1));
        Assert.False(EconomySystem.SpendGold(player, 101));
        Assert.Equal(100, player.Gold);
    }

    [Fact]
    public void SpendGold_DeductsOnlyWhenAffordable()
    {
        var player = new PlayerData { Gold = 100 };
        Assert.True(EconomySystem.SpendGold(player, 40));
        Assert.Equal(60, player.Gold);
        Assert.True(player.IsDirty);
    }

    [Fact]
    public void XpForLevel_IncreasesWithLevelWithinDivision()
    {
        var first = LevelSystem.XpForLevel(1, RaceTier.T1_Spark, 1);
        var later = LevelSystem.XpForLevel(10, RaceTier.T1_Spark, 1);
        Assert.True(first > 0);
        Assert.True(later > first);
    }

    [Fact]
    public void AddXp_LevelsUpAtExactThresholdAndAwardsPoint()
    {
        var player = new PlayerData { XpBoostMultiplier = 1.0, Division = 1 };
        var race = new RaceProgress { RaceId = 2000, Level = 1, Xp = 0 };
        var threshold = LevelSystem.XpForLevel(1, RaceTier.T1_Spark, 1);

        var levels = LevelSystem.AddXp(player, race, RaceTier.T1_Spark, threshold);

        Assert.Equal(1, levels);
        Assert.Equal(2, race.Level);
        Assert.Equal(0, race.Xp);
        Assert.Equal(1, race.UnspentPoints);
    }

    [Fact]
    public void AddXp_RejectsNegativeGrant()
    {
        var player = new PlayerData { XpBoostMultiplier = 1.0 };
        var race = new RaceProgress { Level = 1, Xp = 25 };
        var levels = LevelSystem.AddXp(player, race, RaceTier.T1_Spark, -500);
        Assert.Equal(0, levels);
        Assert.Equal(25, race.Xp);
    }

    [Fact]
    public void AddXp_RejectsNonFiniteMultiplier()
    {
        var player = new PlayerData { XpBoostMultiplier = double.NaN };
        var race = new RaceProgress { Level = 1, Xp = 25 };
        var levels = LevelSystem.AddXp(player, race, RaceTier.T1_Spark, 100);
        Assert.Equal(0, levels);
        Assert.Equal(25, race.Xp);
    }
}