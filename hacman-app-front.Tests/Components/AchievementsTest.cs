using Bunit;
using hacman_app_front.Components.Pages;
using hacman_app_front.Domain;
using hacman_app_front.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace hacman_app_front.Tests.Components;

public class AchievementsTest : BunitContext
{
    private readonly Mock<IAchievementsApiService> _apiMock = new();

    public AchievementsTest()
    {
        Services.AddSingleton(_apiMock.Object);
    }

    private static AchievementProgressEntry Entry(string code, string category, int value, int level,
        int? nextThreshold, int[]? thresholds = null) => new()
    {
        Code = code,
        Category = category,
        Name = code,
        Description = $"{code} description",
        Value = value,
        Level = level,
        NextThreshold = nextThreshold,
        Thresholds = thresholds ?? new[] { 10, 25, 50, 75, 100 }
    };

    [Fact]
    public void ShowsLoadingState_ThenRendersAchievements()
    {
        _apiMock.Setup(a => a.GetMyProgress()).ReturnsAsync([
            Entry("COL-01", "COLLECTION", 30, 2, 50)
        ]);

        var cut = Render<Achievements>();

        Assert.Contains("COL-01", cut.Markup);
        Assert.Contains("30 / 50", cut.Markup);
    }

    [Fact]
    public void ShowsEmptyState_WhenNoAchievementsReturned()
    {
        _apiMock.Setup(a => a.GetMyProgress()).ReturnsAsync([]);

        var cut = Render<Achievements>();

        Assert.Contains("NO DATA RECORDED IN THE ARCHIVES", cut.Markup);
    }

    [Fact]
    public void FilterButtons_ShowAllAndPerCategoryCounts()
    {
        _apiMock.Setup(a => a.GetMyProgress()).ReturnsAsync([
            Entry("COL-01", "COLLECTION", 30, 2, 50),
            Entry("COL-02", "COLLECTION", 10, 1, 25),
            Entry("VOL-01", "VOLUME", 1000, 5, null)
        ]);

        var cut = Render<Achievements>();

        var buttons = cut.FindAll("button").Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains(buttons, b => b.Contains("All") && b.Contains("3"));
        Assert.Contains(buttons, b => b.Contains("COLLECTION") && b.Contains("2"));
        Assert.Contains(buttons, b => b.Contains("VOLUME") && b.Contains("1"));
    }

    [Fact]
    public void ClickingCategoryButton_FiltersToThatCategoryOnly()
    {
        _apiMock.Setup(a => a.GetMyProgress()).ReturnsAsync([
            Entry("COL-01", "COLLECTION", 30, 2, 50),
            Entry("VOL-01", "VOLUME", 1000, 5, null)
        ]);

        var cut = Render<Achievements>();
        cut.FindAll("button").Single(b => b.TextContent.Contains("VOLUME")).Click();

        Assert.Contains("VOL-01", cut.Markup);
        Assert.DoesNotContain("COL-01", cut.Markup);
    }

    [Fact]
    public void ActiveCategoryButton_HasActiveCssClass()
    {
        _apiMock.Setup(a => a.GetMyProgress()).ReturnsAsync([
            Entry("COL-01", "COLLECTION", 30, 2, 50)
        ]);

        var cut = Render<Achievements>();
        cut.FindAll("button").Single(b => b.TextContent.Contains("COLLECTION")).Click();

        var button = cut.FindAll("button").Single(b => b.TextContent.Contains("COLLECTION"));
        Assert.Contains("active", button.GetAttribute("class"));
    }

    [Fact]
    public void ShowsMaxedLabel_WhenNextThresholdIsNull()
    {
        _apiMock.Setup(a => a.GetMyProgress()).ReturnsAsync([
            Entry("VOL-01", "VOLUME", 1000, 5, null)
        ]);

        var cut = Render<Achievements>();

        Assert.Contains("1000 — MAXED", cut.Markup);
    }

    [Fact]
    public void ComputesOverallCompletionPercentage_AcrossAllAchievements()
    {
        // Two achievements, levels 2 and 3 out of a max of 5 each -> (2+3)/10 = 50%.
        _apiMock.Setup(a => a.GetMyProgress()).ReturnsAsync([
            Entry("COL-01", "COLLECTION", 30, 2, 50),
            Entry("VOL-01", "VOLUME", 150, 3, 400)
        ]);

        var cut = Render<Achievements>();

        Assert.Contains("50% COMPLETE (5 / 10 LEVELS)", cut.Markup);
    }

    [Fact]
    public void OverallCompletionPercentage_IsZero_WhenNoAchievementsExist()
    {
        _apiMock.Setup(a => a.GetMyProgress()).ReturnsAsync([]);

        var cut = Render<Achievements>();

        Assert.Contains("0% COMPLETE (0 / 0 LEVELS)", cut.Markup);
    }
}
