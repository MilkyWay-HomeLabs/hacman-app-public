using System;
using hacman_app_back.Modules.Achievements.Domain;
using Xunit;

namespace hacman_app_back.Tests.Modules.Achievements.Domain;

public class AchievementLevelCalculatorTest
{
    private static readonly int[] Thresholds = { 10, 25, 50, 75, 100 };

    [Fact]
    public void LevelFor_ReturnsZero_WhenValueIsBelowFirstThreshold()
    {
        Assert.Equal(0, AchievementLevelCalculator.LevelFor(0, Thresholds));
        Assert.Equal(0, AchievementLevelCalculator.LevelFor(9, Thresholds));
    }

    [Fact]
    public void LevelFor_ReturnsOne_WhenValueExactlyMeetsFirstThreshold()
    {
        Assert.Equal(1, AchievementLevelCalculator.LevelFor(10, Thresholds));
    }

    [Fact]
    public void LevelFor_ReturnsIntermediateLevel_WhenValueIsBetweenThresholds()
    {
        Assert.Equal(1, AchievementLevelCalculator.LevelFor(24, Thresholds));
        Assert.Equal(2, AchievementLevelCalculator.LevelFor(49, Thresholds));
        Assert.Equal(4, AchievementLevelCalculator.LevelFor(99, Thresholds));
    }

    [Fact]
    public void LevelFor_ReturnsMaxLevel_WhenValueMeetsLastThreshold()
    {
        Assert.Equal(AchievementLevelCalculator.MaxLevel, AchievementLevelCalculator.LevelFor(100, Thresholds));
    }

    [Fact]
    public void LevelFor_ReturnsMaxLevel_WhenValueExceedsLastThreshold()
    {
        Assert.Equal(AchievementLevelCalculator.MaxLevel, AchievementLevelCalculator.LevelFor(1_000_000, Thresholds));
    }

    [Fact]
    public void LevelFor_Throws_WhenThresholdCountIsNotFive()
    {
        Assert.Throws<ArgumentException>(() => AchievementLevelCalculator.LevelFor(50, new[] { 10, 20, 30 }));
    }

    [Fact]
    public void NextThresholdFor_ReturnsFirstThreshold_WhenNotStarted()
    {
        Assert.Equal(10, AchievementLevelCalculator.NextThresholdFor(0, Thresholds));
    }

    [Fact]
    public void NextThresholdFor_ReturnsUpcomingThreshold_WhenPartiallyProgressed()
    {
        Assert.Equal(50, AchievementLevelCalculator.NextThresholdFor(25, Thresholds));
        Assert.Equal(100, AchievementLevelCalculator.NextThresholdFor(99, Thresholds));
    }

    [Fact]
    public void NextThresholdFor_ReturnsNull_WhenMaxedOut()
    {
        Assert.Null(AchievementLevelCalculator.NextThresholdFor(100, Thresholds));
        Assert.Null(AchievementLevelCalculator.NextThresholdFor(1_000_000, Thresholds));
    }

    [Fact]
    public void NextThresholdFor_Throws_WhenThresholdCountIsNotFive()
    {
        Assert.Throws<ArgumentException>(() =>
            AchievementLevelCalculator.NextThresholdFor(50, new[] { 10, 20, 30, 40, 50, 60 }));
    }
}
