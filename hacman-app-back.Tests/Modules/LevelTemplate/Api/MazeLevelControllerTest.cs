using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using hacman_app_back.Modules.LevelTemplate.Api;
using hacman_app_back.Modules.LevelTemplate.Application;
using hacman_app_back.Modules.LevelTemplate.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace hacman_app_back.Tests.Modules.LevelTemplate.Api;

public class MazeLevelControllerTest
{
    private static MazeLevelController BuildController(Mock<IMazeLevelProvider> providerMock)
        => new(providerMock.Object, new Mock<ILogger<MazeLevelController>>().Object);

    [Fact]
    public async Task GetMazeLevelsByGalleryId_ReturnsOkWithLevels_WhenProviderSucceeds()
    {
        var galleryId = Guid.NewGuid();
        MazeLevelDto mazeLevelDto = new MazeLevelDto()
        {
            Id = 1,
            GalleryId = galleryId,
            Difficulty = Difficulty.EASY.ToString(),
            Width = 20,
            Height = 20,
            RandomSeed = 12345,
            PlacementAllowedOn = PlacementSurface.road.ToString(),
            PlacementAvoidAdjWalls = true,
            Cells = new[] { new short[20] },
            PlayerStartX = 1,
            PlayerStartY = 1,
            PlayerDirection = MazeDirection.right.ToString(),
            Dots = new MazeDots(),
            Enemies = null,
            Buffs = null,
            Debuffs = null,
            Notes = "Test maze",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var expectedLevels = new List<MazeLevelDto> { mazeLevelDto };

        var providerMock = new Mock<IMazeLevelProvider>();
        providerMock.Setup(p => p.GetMazeLevelsByGalleryId(galleryId)).ReturnsAsync(expectedLevels);

        var result = await BuildController(providerMock).GetMazeLevelsByGalleryId(galleryId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expectedLevels, result.Value);
    }

    [Fact]
    public async Task GetMazeLevelsByGalleryId_ReturnsOkWithEmptyList_WhenNoLevelsExist()
    {
        var galleryId = Guid.NewGuid();
        var emptyList = new List<MazeLevelDto>();

        var providerMock = new Mock<IMazeLevelProvider>();
        providerMock.Setup(p => p.GetMazeLevelsByGalleryId(galleryId)).ReturnsAsync(emptyList);

        var result = await BuildController(providerMock).GetMazeLevelsByGalleryId(galleryId) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(emptyList, result.Value);
    }

    [Fact]
    public async Task GetMazeLevelsByGalleryId_Returns500_WhenProviderThrowsException()
    {
        var galleryId = Guid.NewGuid();

        var providerMock = new Mock<IMazeLevelProvider>();
        providerMock.Setup(p => p.GetMazeLevelsByGalleryId(galleryId)).ThrowsAsync(new Exception("db error"));

        var result = await BuildController(providerMock).GetMazeLevelsByGalleryId(galleryId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
        Assert.Equal("An unexpected error occurred.", result.Value);
    }
}