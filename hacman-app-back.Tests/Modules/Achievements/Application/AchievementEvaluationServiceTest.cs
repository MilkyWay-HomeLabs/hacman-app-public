#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hacman_app_back.Modules.Achievements.Application;
using hacman_app_back.Modules.Achievements.Domain;
using hacman_app_back.Modules.Achievements.Infrastructure;
using hacman_app_back.Modules.Identity.Domain;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace hacman_app_back.Tests.Modules.Achievements.Application;

public class AchievementEvaluationServiceTest
{
    private static AchievementDef BuildDef(string code, string metric) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Category = AchievementCategory.VOLUME,
        Name = code,
        Description = code,
        Metric = metric,
        Thresholds = new[] { 1, 2, 3, 4, 5 }
    };

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenDefRepositoryIsNull()
    {
        IAchievementDefRepository defRepository = null!;
        Assert.Throws<ArgumentNullException>(() => new AchievementEvaluationService(
            defRepository, Mock.Of<IAchievementProgressRepository>(), Mock.Of<IAchievementMetricCalculatorProvider>(),
            new Mock<ILogger<AchievementEvaluationService>>().Object));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenProgressRepositoryIsNull()
    {
        IAchievementProgressRepository progressRepository = null!;
        Assert.Throws<ArgumentNullException>(() => new AchievementEvaluationService(
            Mock.Of<IAchievementDefRepository>(), progressRepository, Mock.Of<IAchievementMetricCalculatorProvider>(),
            new Mock<ILogger<AchievementEvaluationService>>().Object));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenCalculatorProviderIsNull()
    {
        IAchievementMetricCalculatorProvider calculatorProvider = null!;
        Assert.Throws<ArgumentNullException>(() => new AchievementEvaluationService(
            Mock.Of<IAchievementDefRepository>(), Mock.Of<IAchievementProgressRepository>(), calculatorProvider,
            new Mock<ILogger<AchievementEvaluationService>>().Object));
    }

    [Fact]
    public async Task EvaluateForUserAsync_UpsertsAndReportsChanged_WhenCalculatedValueDiffersFromStored()
    {
        var userId = Guid.NewGuid();
        var def = BuildDef("VOL-01", "total_clears");

        var defRepositoryMock = new Mock<IAchievementDefRepository>();
        defRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AchievementDef> { def });

        var progressRepositoryMock = new Mock<IAchievementProgressRepository>();
        progressRepositoryMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(new List<AchievementProgress>
        {
            new() { User = new User { Id = userId }, Achievement = new AchievementDef { Id = def.Id }, Value = 3 }
        });

        var calculatorMock = new Mock<IAchievementMetricCalculator>();
        calculatorMock.Setup(c => c.CalculateAsync(userId)).ReturnsAsync(7);

        var calculatorProviderMock = new Mock<IAchievementMetricCalculatorProvider>();
        calculatorProviderMock.Setup(p => p.TryGet("total_clears")).Returns(calculatorMock.Object);

        var service = new AchievementEvaluationService(defRepositoryMock.Object, progressRepositoryMock.Object,
            calculatorProviderMock.Object, new Mock<ILogger<AchievementEvaluationService>>().Object);

        var changed = (await service.EvaluateForUserAsync(userId)).ToList();

        Assert.Single(changed);
        Assert.Equal("VOL-01", changed[0].Def.Code);
        Assert.Equal(3, changed[0].OldValue);
        Assert.Equal(7, changed[0].NewValue);
        Assert.True(changed[0].LeveledUp, "value 3->7 against thresholds {1,2,3,4,5} crosses level 3 to level 5.");
        progressRepositoryMock.Verify(r => r.UpsertAsync(userId, def.Id, 7, null, null), Times.Once);
    }

    [Fact]
    public async Task EvaluateForUserAsync_ReportsNotLeveledUp_WhenValueChangesWithoutCrossingANewThreshold()
    {
        var userId = Guid.NewGuid();
        var def = BuildDef("VOL-01", "total_clears");

        var defRepositoryMock = new Mock<IAchievementDefRepository>();
        defRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AchievementDef> { def });

        var progressRepositoryMock = new Mock<IAchievementProgressRepository>();
        progressRepositoryMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(new List<AchievementProgress>
        {
            // Already at the maxed-out level (thresholds top out at 5).
            new() { User = new User { Id = userId }, Achievement = new AchievementDef { Id = def.Id }, Value = 5 }
        });

        var calculatorMock = new Mock<IAchievementMetricCalculator>();
        calculatorMock.Setup(c => c.CalculateAsync(userId)).ReturnsAsync(9);

        var calculatorProviderMock = new Mock<IAchievementMetricCalculatorProvider>();
        calculatorProviderMock.Setup(p => p.TryGet("total_clears")).Returns(calculatorMock.Object);

        var service = new AchievementEvaluationService(defRepositoryMock.Object, progressRepositoryMock.Object,
            calculatorProviderMock.Object, new Mock<ILogger<AchievementEvaluationService>>().Object);

        var changed = (await service.EvaluateForUserAsync(userId)).ToList();

        Assert.Single(changed);
        Assert.False(changed[0].LeveledUp, "5->9 is still level 5 (maxed) against thresholds {1,2,3,4,5}.");
    }

    [Fact]
    public async Task EvaluateForUserAsync_SkipsUpsert_WhenCalculatedValueMatchesStored()
    {
        var userId = Guid.NewGuid();
        var def = BuildDef("VOL-01", "total_clears");

        var defRepositoryMock = new Mock<IAchievementDefRepository>();
        defRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AchievementDef> { def });

        var progressRepositoryMock = new Mock<IAchievementProgressRepository>();
        progressRepositoryMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(new List<AchievementProgress>
        {
            new() { User = new User { Id = userId }, Achievement = new AchievementDef { Id = def.Id }, Value = 7 }
        });

        var calculatorMock = new Mock<IAchievementMetricCalculator>();
        calculatorMock.Setup(c => c.CalculateAsync(userId)).ReturnsAsync(7);

        var calculatorProviderMock = new Mock<IAchievementMetricCalculatorProvider>();
        calculatorProviderMock.Setup(p => p.TryGet("total_clears")).Returns(calculatorMock.Object);

        var service = new AchievementEvaluationService(defRepositoryMock.Object, progressRepositoryMock.Object,
            calculatorProviderMock.Object, new Mock<ILogger<AchievementEvaluationService>>().Object);

        var changed = await service.EvaluateForUserAsync(userId);

        Assert.Empty(changed);
        progressRepositoryMock.Verify(
            r => r.UpsertAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(), null, null), Times.Never);
    }

    [Fact]
    public async Task EvaluateForUserAsync_TreatsMissingProgressRow_AsZero()
    {
        var userId = Guid.NewGuid();
        var def = BuildDef("VOL-01", "total_clears");

        var defRepositoryMock = new Mock<IAchievementDefRepository>();
        defRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AchievementDef> { def });

        var progressRepositoryMock = new Mock<IAchievementProgressRepository>();
        progressRepositoryMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(new List<AchievementProgress>());

        var calculatorMock = new Mock<IAchievementMetricCalculator>();
        calculatorMock.Setup(c => c.CalculateAsync(userId)).ReturnsAsync(1);

        var calculatorProviderMock = new Mock<IAchievementMetricCalculatorProvider>();
        calculatorProviderMock.Setup(p => p.TryGet("total_clears")).Returns(calculatorMock.Object);

        var service = new AchievementEvaluationService(defRepositoryMock.Object, progressRepositoryMock.Object,
            calculatorProviderMock.Object, new Mock<ILogger<AchievementEvaluationService>>().Object);

        var changed = (await service.EvaluateForUserAsync(userId)).ToList();

        Assert.Single(changed);
        progressRepositoryMock.Verify(r => r.UpsertAsync(userId, def.Id, 1, null, null), Times.Once);
    }

    [Fact]
    public async Task EvaluateForUserAsync_SkipsAchievement_WhenNoCalculatorIsRegisteredForItsMetric()
    {
        var userId = Guid.NewGuid();
        var def = BuildDef("XXX-99", "not_implemented_yet");

        var defRepositoryMock = new Mock<IAchievementDefRepository>();
        defRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AchievementDef> { def });

        var progressRepositoryMock = new Mock<IAchievementProgressRepository>();
        progressRepositoryMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(new List<AchievementProgress>());

        var calculatorProviderMock = new Mock<IAchievementMetricCalculatorProvider>();
        calculatorProviderMock.Setup(p => p.TryGet("not_implemented_yet"))
            .Returns((IAchievementMetricCalculator?)null);

        var service = new AchievementEvaluationService(defRepositoryMock.Object, progressRepositoryMock.Object,
            calculatorProviderMock.Object, new Mock<ILogger<AchievementEvaluationService>>().Object);

        var changed = await service.EvaluateForUserAsync(userId);

        Assert.Empty(changed);
        progressRepositoryMock.Verify(
            r => r.UpsertAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(), null, null), Times.Never);
    }

    [Fact]
    public async Task EvaluateForUserAsync_IsIdempotent_OnConsecutiveCallsWithNoNewActivity()
    {
        var userId = Guid.NewGuid();
        var def = BuildDef("VOL-01", "total_clears");

        var storedValue = 0L;

        var defRepositoryMock = new Mock<IAchievementDefRepository>();
        defRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AchievementDef> { def });

        var progressRepositoryMock = new Mock<IAchievementProgressRepository>();
        progressRepositoryMock.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(() => storedValue == 0
                ? new List<AchievementProgress>()
                : new List<AchievementProgress>
                {
                    new() { User = new User { Id = userId }, Achievement = new AchievementDef { Id = def.Id }, Value = storedValue }
                });
        progressRepositoryMock
            .Setup(r => r.UpsertAsync(userId, def.Id, It.IsAny<long>(), null, null))
            .Callback<Guid, Guid, long, Npgsql.NpgsqlConnection?, Npgsql.NpgsqlTransaction?>((_, _, v, _, _) => storedValue = v)
            .Returns(Task.CompletedTask);

        var calculatorMock = new Mock<IAchievementMetricCalculator>();
        calculatorMock.Setup(c => c.CalculateAsync(userId)).ReturnsAsync(5);

        var calculatorProviderMock = new Mock<IAchievementMetricCalculatorProvider>();
        calculatorProviderMock.Setup(p => p.TryGet("total_clears")).Returns(calculatorMock.Object);

        var service = new AchievementEvaluationService(defRepositoryMock.Object, progressRepositoryMock.Object,
            calculatorProviderMock.Object, new Mock<ILogger<AchievementEvaluationService>>().Object);

        var firstRun = (await service.EvaluateForUserAsync(userId)).ToList();
        var secondRun = (await service.EvaluateForUserAsync(userId)).ToList();

        Assert.Single(firstRun);
        Assert.Empty(secondRun);
        progressRepositoryMock.Verify(r => r.UpsertAsync(userId, def.Id, 5, null, null), Times.Once);
    }
}
