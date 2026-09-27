#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hacman_app_back.Modules.Achievements.Application;
using hacman_app_back.Modules.Achievements.Domain;
using hacman_app_back.Modules.Achievements.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace hacman_app_back.Tests.Modules.Achievements.Application;

public class AchievementCatalogProviderTest
{
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRepositoryIsNull()
    {
        IAchievementDefRepository repository = null!;
        var loggerMock = new Mock<ILogger<AchievementCatalogProvider>>();

        Assert.Throws<ArgumentNullException>(() => new AchievementCatalogProvider(repository, loggerMock.Object));
    }

    [Fact]
    public async Task GetCatalogAsync_MapsEveryDefinitionField()
    {
        var def = new AchievementDef
        {
            Id = Guid.NewGuid(),
            Code = "COL-01",
            Category = AchievementCategory.COLLECTION,
            Name = "Archivist",
            Description = "% of the archive unlocked.",
            IconUrl = null,
            Metric = "galleries_unlocked_pct",
            Thresholds = new[] { 10, 25, 50, 75, 100 }
        };

        var repositoryMock = new Mock<IAchievementDefRepository>();
        repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AchievementDef> { def });

        var provider = new AchievementCatalogProvider(repositoryMock.Object,
            new Mock<ILogger<AchievementCatalogProvider>>().Object);

        var result = (await provider.GetCatalogAsync()).ToList();

        Assert.Single(result);
        Assert.Equal("COL-01", result[0].Code);
        Assert.Equal("COLLECTION", result[0].Category);
        Assert.Equal("Archivist", result[0].Name);
        Assert.Equal(def.Description, result[0].Description);
        Assert.Null(result[0].IconUrl);
        Assert.Equal(def.Thresholds, result[0].Thresholds);
    }

    [Fact]
    public async Task GetCatalogAsync_ReturnsEmpty_WhenRepositoryHasNoRows()
    {
        var repositoryMock = new Mock<IAchievementDefRepository>();
        repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AchievementDef>());

        var provider = new AchievementCatalogProvider(repositoryMock.Object,
            new Mock<ILogger<AchievementCatalogProvider>>().Object);

        var result = await provider.GetCatalogAsync();

        Assert.Empty(result);
    }
}
