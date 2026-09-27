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

public class AchievementProgressServiceTest
{
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenDefRepositoryIsNull()
    {
        IAchievementDefRepository defRepository = null!;
        var progressRepositoryMock = new Mock<IAchievementProgressRepository>();
        var loggerMock = new Mock<ILogger<AchievementProgressService>>();

        Assert.Throws<ArgumentNullException>(() =>
            new AchievementProgressService(defRepository, progressRepositoryMock.Object, loggerMock.Object));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenProgressRepositoryIsNull()
    {
        var defRepositoryMock = new Mock<IAchievementDefRepository>();
        IAchievementProgressRepository progressRepository = null!;
        var loggerMock = new Mock<ILogger<AchievementProgressService>>();

        Assert.Throws<ArgumentNullException>(() =>
            new AchievementProgressService(defRepositoryMock.Object, progressRepository, loggerMock.Object));
    }

    [Fact]
    public async Task GetUserProgressAsync_DefaultsToZeroValueAndLevel_WhenUserHasNoProgressRow()
    {
        var userId = Guid.NewGuid();
        var def = new AchievementDef
        {
            Id = Guid.NewGuid(),
            Code = "COL-01",
            Category = AchievementCategory.COLLECTION,
            Name = "Archivist",
            Description = "% of the archive unlocked.",
            Metric = "galleries_unlocked_pct",
            Thresholds = new[] { 10, 25, 50, 75, 100 }
        };

        var defRepositoryMock = new Mock<IAchievementDefRepository>();
        defRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AchievementDef> { def });

        var progressRepositoryMock = new Mock<IAchievementProgressRepository>();
        progressRepositoryMock.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new List<AchievementProgress>());

        var service = new AchievementProgressService(defRepositoryMock.Object, progressRepositoryMock.Object,
            new Mock<ILogger<AchievementProgressService>>().Object);

        var result = (await service.GetUserProgressAsync(userId)).ToList();

        Assert.Single(result);
        Assert.Equal("COL-01", result[0].Code);
        Assert.Equal(0L, result[0].Value);
        Assert.Equal(0, result[0].Level);
        Assert.Equal(10, result[0].NextThreshold);
    }

    [Fact]
    public async Task GetUserProgressAsync_JoinsExistingProgress_AndDerivesLevel()
    {
        var userId = Guid.NewGuid();
        var def = new AchievementDef
        {
            Id = Guid.NewGuid(),
            Code = "COL-01",
            Category = AchievementCategory.COLLECTION,
            Name = "Archivist",
            Description = "% of the archive unlocked.",
            Metric = "galleries_unlocked_pct",
            Thresholds = new[] { 10, 25, 50, 75, 100 }
        };

        var defRepositoryMock = new Mock<IAchievementDefRepository>();
        defRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AchievementDef> { def });

        var progressRepositoryMock = new Mock<IAchievementProgressRepository>();
        progressRepositoryMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(new List<AchievementProgress>
        {
            new()
            {
                User = new User { Id = userId },
                Achievement = new AchievementDef { Id = def.Id },
                Value = 60,
                UpdatedAt = DateTime.UtcNow
            }
        });

        var service = new AchievementProgressService(defRepositoryMock.Object, progressRepositoryMock.Object,
            new Mock<ILogger<AchievementProgressService>>().Object);

        var result = (await service.GetUserProgressAsync(userId)).ToList();

        Assert.Single(result);
        Assert.Equal(60L, result[0].Value);
        Assert.Equal(3, result[0].Level);
        Assert.Equal(75, result[0].NextThreshold);
    }

    [Fact]
    public async Task GetUserProgressAsync_ReturnsEmpty_WhenCatalogHasNoRows()
    {
        var userId = Guid.NewGuid();
        var defRepositoryMock = new Mock<IAchievementDefRepository>();
        defRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AchievementDef>());

        var progressRepositoryMock = new Mock<IAchievementProgressRepository>();
        progressRepositoryMock.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new List<AchievementProgress>());

        var service = new AchievementProgressService(defRepositoryMock.Object, progressRepositoryMock.Object,
            new Mock<ILogger<AchievementProgressService>>().Object);

        var result = await service.GetUserProgressAsync(userId);

        Assert.Empty(result);
    }
}
