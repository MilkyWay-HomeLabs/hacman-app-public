using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hacman_app_back.Modules.LevelTemplate.Application;
using hacman_app_back.Modules.LevelTemplate.Domain;
using hacman_app_back.Modules.LevelTemplate.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace hacman_app_back.Tests.Modules.LevelTemplate.Application;

public class MazeLevelProviderTest
{
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRepositoryIsNull()
    {
        // Arrange
        IMazeLevelRepository repository = null!;
        var loggerMock = new Mock<ILogger<MazeLevelProvider>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MazeLevelProvider(repository, loggerMock.Object));
    }

    [Fact]
    public async Task GetMazeLevelsByGalleryId_ReturnsMazeLevelsFromRepository()
    {
        // Arrange
        var galleryId = new Guid("5250215a-521f-4a8b-aba9-c8325cf47615");

        var mazeLevels = new List<MazeLevel>
        {
            new()
            {
                Id = 1,
                GalleryId = galleryId,
                Difficulty = Difficulty.EASY,
                Width = 20,
                Height = 20,
                RandomSeed = 12345,
                PlacementAllowedOn = PlacementSurface.road,
                PlacementAvoidAdjWalls = true,
                Cells = new[] { new short[20] },
                PlayerStartX = 1,
                PlayerStartY = 1,
                PlayerDirection = MazeDirection.right,
                Entities = new MazeEntities(),
                Notes = "Test maze",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        var repositoryMock = new Mock<IMazeLevelRepository>();
        repositoryMock
            .Setup(repo => repo.GetMazeLevelsByGalleryId(galleryId))
            .ReturnsAsync(mazeLevels);

        var loggerMock = new Mock<ILogger<MazeLevelProvider>>();
        var provider = new MazeLevelProvider(repositoryMock.Object, loggerMock.Object);

        // Act
        var result = await provider.GetMazeLevelsByGalleryId(galleryId);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);
        Assert.Equal(1, resultList[0].Id);
        Assert.Equal(galleryId, resultList[0].GalleryId);
        Assert.Equal("EASY", resultList[0].Difficulty);
        Assert.Equal(20, resultList[0].Width);
        Assert.Equal(20, resultList[0].Height);
        repositoryMock.Verify(repo => repo.GetMazeLevelsByGalleryId(galleryId), Times.Once);
    }

    [Fact]
    public async Task GetMazeLevelsByGalleryId_ReturnsEmptyList_WhenGalleryIdIsNull()
    {
        // Arrange
        var repositoryMock = new Mock<IMazeLevelRepository>();
        var loggerMock = new Mock<ILogger<MazeLevelProvider>>();
        var provider = new MazeLevelProvider(repositoryMock.Object, loggerMock.Object);

        // Act
        var result = await provider.GetMazeLevelsByGalleryId(null);

        // Assert
        Assert.Empty(result);
        repositoryMock.Verify(repo => repo.GetMazeLevelsByGalleryId(It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task GetMazeLevelsByGalleryId_ReturnsEmptyList_WhenRepositoryReturnsEmpty()
    {
        // Arrange
        var galleryId = Guid.NewGuid();
        var repositoryMock = new Mock<IMazeLevelRepository>();
        repositoryMock
            .Setup(repo => repo.GetMazeLevelsByGalleryId(galleryId))
            .ReturnsAsync(new List<MazeLevel>());

        var loggerMock = new Mock<ILogger<MazeLevelProvider>>();
        var provider = new MazeLevelProvider(repositoryMock.Object, loggerMock.Object);

        // Act
        var result = await provider.GetMazeLevelsByGalleryId(galleryId);

        // Assert
        Assert.Empty(result);
        repositoryMock.Verify(repo => repo.GetMazeLevelsByGalleryId(galleryId), Times.Once);
    }

    [Fact]
    public async Task GetMazeLevelsByGalleryId_MapsAllFieldsCorrectly()
    {
        // Arrange
        var galleryId = new Guid("5250215a-521f-4a8b-aba9-c8325cf47615");
        var createdAt = DateTimeOffset.Parse("2026-06-26 17:38:26.618245 +00:00");
        var updatedAt = DateTimeOffset.Parse("2026-06-26 17:38:26.618245 +00:00");

        var entities = new MazeEntities
        {
            Dots = new MazeDots
            {
                Static = new List<short[]> { new short[] { 2, 1 }, new short[] { 3, 1 } },
                RandomCount = 0
            },
            Enemies = new List<MazeEnemy>
            {
                new() { Id = "random_walker", Type = "enemy", Positions = new List<short[]> { new short[] { 10, 2 } } }
            },
            Buffs = new List<MazeItem>
            {
                new() { Id = "speed_up", Type = "buff", Positions = new List<short[]> { new short[] { 5, 10 } } }
            },
            Debuffs = new List<MazeItem>
            {
                new() { Id = "poison", Type = "debuff", Positions = new List<short[]> { new short[] { 3, 9 } } }
            }
        };

        var mazeLevels = new List<MazeLevel>
        {
            new()
            {
                Id = 1,
                GalleryId = galleryId,
                Difficulty = Difficulty.EASY,
                Width = 20,
                Height = 20,
                RandomSeed = 1,
                PlacementAllowedOn = PlacementSurface.road,
                PlacementAvoidAdjWalls = false,
                Cells = new[] { new short[20] },
                PlayerStartX = 10,
                PlayerStartY = 9,
                PlayerDirection = MazeDirection.right,
                Entities = entities,
                Notes = null,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            }
        };

        var repositoryMock = new Mock<IMazeLevelRepository>();
        repositoryMock
            .Setup(repo => repo.GetMazeLevelsByGalleryId(galleryId))
            .ReturnsAsync(mazeLevels);

        var loggerMock = new Mock<ILogger<MazeLevelProvider>>();
        var provider = new MazeLevelProvider(repositoryMock.Object, loggerMock.Object);

        // Act
        var result = await provider.GetMazeLevelsByGalleryId(galleryId);
        var dto = result.First();

        // Assert
        Assert.Equal(1, dto.Id);
        Assert.Equal(galleryId, dto.GalleryId);
        Assert.Equal("EASY", dto.Difficulty);
        Assert.Equal(20, dto.Width);
        Assert.Equal(20, dto.Height);
        Assert.Equal(1, dto.RandomSeed);
        Assert.Equal("road", dto.PlacementAllowedOn);
        Assert.False(dto.PlacementAvoidAdjWalls);
        Assert.Equal(10, dto.PlayerStartX);
        Assert.Equal(9, dto.PlayerStartY);
        Assert.Equal("right", dto.PlayerDirection);
        Assert.Null(dto.Notes);
        Assert.Equal(createdAt, dto.CreatedAt);
        Assert.Equal(updatedAt, dto.UpdatedAt);
        Assert.NotNull(dto.Dots);
        Assert.NotNull(dto.Enemies);
        Assert.NotNull(dto.Buffs);
        Assert.NotNull(dto.Debuffs);
    }
}