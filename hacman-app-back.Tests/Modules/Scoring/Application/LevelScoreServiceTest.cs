#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hacman_app_back.Modules.Achievements.Application;
using hacman_app_back.Modules.Achievements.Domain;
using hacman_app_back.Modules.Achievements.Infrastructure.Nebula;
using hacman_app_back.Modules.Identity.Domain;
using hacman_app_back.Modules.LevelTemplate.Domain;
using hacman_app_back.Modules.PlayerProgress.Infrastructure;
using hacman_app_back.Modules.Scoring.Application;
using hacman_app_back.Modules.Scoring.Domain;
using hacman_app_back.Modules.Scoring.Infrastructure;
using hacman_app_back.SharedKernel;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace hacman_app_back.Tests.Modules.Scoring.Application;

public class LevelScoreServiceTest
{
    private static Mock<IUnitOfWork> BuildUnitOfWorkMock()
    {
        var mock = new Mock<IUnitOfWork>();
        mock.Setup(u => u.BeginAsync()).Returns(Task.CompletedTask);
        mock.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);
        mock.Setup(u => u.RollbackAsync()).Returns(Task.CompletedTask);
        mock.Setup(u => u.DisposeAsync()).Returns(ValueTask.CompletedTask);
        return mock;
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenRepositoryIsNull()
    {
        // Arrange
        ILevelScoreRepository repository = null!;
        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        var loggerMock = new Mock<ILogger<LevelScoreService>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new LevelScoreService(repository, userGalleryRepositoryMock.Object, () => Mock.Of<IUnitOfWork>(),
                Mock.Of<IAchievementEvaluationService>(), Mock.Of<INebulaAchievementDispatcher>(),
                loggerMock.Object));
    }

    [Fact]
    public async Task GetScores_ReturnsScoresFromRepository()
    {
        // Arrange
        var galleryId = Guid.NewGuid();
        var difficulty = "Hard";
        var pageNumber = 1;
        var pageSize = 10;
        var user = new User { Id = Guid.NewGuid(), Username = "TestUser" };
        var expectedScores = new List<LevelScore>
        {
            new() { User = user, Difficulty = Difficulty.HARD, Position = 1, TimeMs = 1000, TimeAt = DateTime.UtcNow }
        };

        var repositoryMock = new Mock<ILevelScoreRepository>();
        repositoryMock
            .Setup(repo => repo.GetScoresAsync(galleryId, difficulty, pageNumber, pageSize))
            .ReturnsAsync(expectedScores);

        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        var loggerMock = new Mock<ILogger<LevelScoreService>>();
        var service = new LevelScoreService(repositoryMock.Object, userGalleryRepositoryMock.Object,
            () => Mock.Of<IUnitOfWork>(), Mock.Of<IAchievementEvaluationService>(), Mock.Of<INebulaAchievementDispatcher>(), loggerMock.Object);

        // Act
        var result = await service.GetScores(galleryId, difficulty, pageNumber, pageSize);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);
        Assert.Equal("TestUser", resultList[0].Username);
        Assert.Equal("HARD", resultList[0].Difficulty);
        Assert.Equal(1000L, resultList[0].TimeMs);
        repositoryMock.Verify(repo => repo.GetScoresAsync(galleryId, difficulty, pageNumber, pageSize), Times.Once);
    }

    [Fact]
    public async Task GetScoresByUserId_ReturnsScoresFromRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        var difficulty = "EASY";
        var user = new User { Id = userId, Username = "TestUser" };
        var expectedScores = new List<LevelScore>
        {
            new() { User = user, Position = 1, TimeMs = 2000, TimeAt = DateTime.UtcNow, Difficulty = Difficulty.EASY }
        };

        var repositoryMock = new Mock<ILevelScoreRepository>();
        repositoryMock
            .Setup(repo => repo.GetScoresByUserIdAsync(userId, galleryId, difficulty))
            .ReturnsAsync(expectedScores);

        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        var loggerMock = new Mock<ILogger<LevelScoreService>>();
        var service = new LevelScoreService(repositoryMock.Object, userGalleryRepositoryMock.Object,
            () => Mock.Of<IUnitOfWork>(), Mock.Of<IAchievementEvaluationService>(), Mock.Of<INebulaAchievementDispatcher>(), loggerMock.Object);

        // Act
        var result = await service.GetScoresByUserId(userId, galleryId, difficulty);

        // Assert
        var resultList = result.ToList();
        Assert.Single(resultList);
        Assert.Equal("TestUser", resultList[0].Username);
        Assert.Equal("EASY", resultList[0].Difficulty);
        Assert.Equal(2000L, resultList[0].TimeMs);
        repositoryMock.Verify(repo => repo.GetScoresByUserIdAsync(userId, galleryId, difficulty), Times.Once);
    }

    [Fact]
    public async Task CreateScore_PersistsScoreAndUpdatesBestTime_WhenDifficultyIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        const string username = "TestUser";
        const long timeMs = 4200;

        var repositoryMock = new Mock<ILevelScoreRepository>();
        repositoryMock
            .Setup(repo =>
                repo.CreateAsync(It.IsAny<LevelScore>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((LevelScore score, NpgsqlConnection? _, NpgsqlTransaction? _) => score);

        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        userGalleryRepositoryMock
            .Setup(repo => repo.UpdateBestTimeIfBetterAsync(userId, galleryId, timeMs, It.IsAny<DateTime>(),
                It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync(true);

        var unitOfWorkMock = BuildUnitOfWorkMock();
        var loggerMock = new Mock<ILogger<LevelScoreService>>();
        var service = new LevelScoreService(repositoryMock.Object, userGalleryRepositoryMock.Object,
            () => unitOfWorkMock.Object, Mock.Of<IAchievementEvaluationService>(), Mock.Of<INebulaAchievementDispatcher>(), loggerMock.Object);

        // Act
        var result = await service.CreateScore(userId, username, galleryId, "HARD", timeMs);

        // Assert
        Assert.Equal(username, result.Username);
        Assert.Equal("HARD", result.Difficulty);
        Assert.Equal(timeMs, result.TimeMs);
        repositoryMock.Verify(
            repo => repo.CreateAsync(It.IsAny<LevelScore>(), It.IsAny<NpgsqlConnection>(),
                It.IsAny<NpgsqlTransaction>()),
            Times.Once);
        userGalleryRepositoryMock.Verify(
            repo => repo.UpdateBestTimeIfBetterAsync(userId, galleryId, timeMs, It.IsAny<DateTime>(),
                It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()),
            Times.Once);
        unitOfWorkMock.Verify(u => u.BeginAsync(), Times.Once);
        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateScore_IncrementsScoresSubmittedCounter_OnSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();

        var repositoryMock = new Mock<ILevelScoreRepository>();
        repositoryMock
            .Setup(repo =>
                repo.CreateAsync(It.IsAny<LevelScore>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((LevelScore score, NpgsqlConnection? _, NpgsqlTransaction? _) => score);

        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        userGalleryRepositoryMock
            .Setup(repo => repo.UpdateBestTimeIfBetterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(),
                It.IsAny<DateTime>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync(true);

        var service = new LevelScoreService(repositoryMock.Object, userGalleryRepositoryMock.Object,
            () => BuildUnitOfWorkMock().Object, Mock.Of<IAchievementEvaluationService>(),
            Mock.Of<INebulaAchievementDispatcher>(), new Mock<ILogger<LevelScoreService>>().Object);

        // The counter is a process-global Prometheus collector, so assert on the delta for this label.
        var before = LevelScoreService.ScoresSubmitted.WithLabels("HARD").Value;

        // Act
        await service.CreateScore(userId, "TestUser", galleryId, "HARD", 4200);

        // Assert
        Assert.Equal(before + 1, LevelScoreService.ScoresSubmitted.WithLabels("HARD").Value);
    }

    [Fact]
    public async Task CreateScore_DoesNotIncrementCounter_WhenRepositoryThrows()
    {
        // Arrange
        var repositoryMock = new Mock<ILevelScoreRepository>();
        repositoryMock
            .Setup(repo =>
                repo.CreateAsync(It.IsAny<LevelScore>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ThrowsAsync(new InvalidOperationException("db error"));

        var service = new LevelScoreService(repositoryMock.Object, new Mock<IUserGalleryRepository>().Object,
            () => BuildUnitOfWorkMock().Object, Mock.Of<IAchievementEvaluationService>(),
            Mock.Of<INebulaAchievementDispatcher>(), new Mock<ILogger<LevelScoreService>>().Object);

        var before = LevelScoreService.ScoresSubmitted.WithLabels("EASY").Value;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateScore(Guid.NewGuid(), "TestUser", Guid.NewGuid(), "EASY", 1000));
        Assert.Equal(before, LevelScoreService.ScoresSubmitted.WithLabels("EASY").Value);
    }

    [Fact]
    public async Task CreateScore_PersistsGameplayTelemetryCounters_WhenProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        LevelScore? captured = null;

        var repositoryMock = new Mock<ILevelScoreRepository>();
        repositoryMock
            .Setup(repo =>
                repo.CreateAsync(It.IsAny<LevelScore>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .Callback<LevelScore, NpgsqlConnection?, NpgsqlTransaction?>((score, _, _) => captured = score)
            .ReturnsAsync((LevelScore score, NpgsqlConnection? _, NpgsqlTransaction? _) => score);

        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        userGalleryRepositoryMock
            .Setup(repo => repo.UpdateBestTimeIfBetterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(),
                It.IsAny<DateTime>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync(true);

        var service = new LevelScoreService(repositoryMock.Object, userGalleryRepositoryMock.Object,
            () => BuildUnitOfWorkMock().Object, Mock.Of<IAchievementEvaluationService>(),
            Mock.Of<INebulaAchievementDispatcher>(), new Mock<ILogger<LevelScoreService>>().Object);

        // Act
        await service.CreateScore(userId, "TestUser", galleryId, "HARD", 4200,
            enemiesDefeated: 3, buffsCollected: 2, hitsTaken: 1);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(3, captured!.EnemiesDefeated);
        Assert.Equal(2, captured.BuffsCollected);
        Assert.Equal(1, captured.HitsTaken);
    }

    [Fact]
    public async Task CreateScore_ClampsNegativeGameplayTelemetryCountersToZero()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        LevelScore? captured = null;

        var repositoryMock = new Mock<ILevelScoreRepository>();
        repositoryMock
            .Setup(repo =>
                repo.CreateAsync(It.IsAny<LevelScore>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .Callback<LevelScore, NpgsqlConnection?, NpgsqlTransaction?>((score, _, _) => captured = score)
            .ReturnsAsync((LevelScore score, NpgsqlConnection? _, NpgsqlTransaction? _) => score);

        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        userGalleryRepositoryMock
            .Setup(repo => repo.UpdateBestTimeIfBetterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(),
                It.IsAny<DateTime>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync(true);

        var service = new LevelScoreService(repositoryMock.Object, userGalleryRepositoryMock.Object,
            () => BuildUnitOfWorkMock().Object, Mock.Of<IAchievementEvaluationService>(),
            Mock.Of<INebulaAchievementDispatcher>(), new Mock<ILogger<LevelScoreService>>().Object);

        // Act
        await service.CreateScore(userId, "TestUser", galleryId, "HARD", 4200,
            enemiesDefeated: -5, buffsCollected: -1, hitsTaken: -2);

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(0, captured!.EnemiesDefeated);
        Assert.Equal(0, captured.BuffsCollected);
        Assert.Equal(0, captured.HitsTaken);
    }

    [Fact]
    public async Task CreateScore_ThrowsArgumentException_WhenDifficultyIsInvalid()
    {
        // Arrange
        var repositoryMock = new Mock<ILevelScoreRepository>();
        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        var loggerMock = new Mock<ILogger<LevelScoreService>>();
        var service = new LevelScoreService(repositoryMock.Object, userGalleryRepositoryMock.Object,
            () => Mock.Of<IUnitOfWork>(), Mock.Of<IAchievementEvaluationService>(), Mock.Of<INebulaAchievementDispatcher>(), loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateScore(Guid.NewGuid(), "TestUser", Guid.NewGuid(), "NOT_A_DIFFICULTY", 1000));
        repositoryMock.Verify(
            repo => repo.CreateAsync(It.IsAny<LevelScore>(), It.IsAny<NpgsqlConnection>(),
                It.IsAny<NpgsqlTransaction>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateScore_RollsBackUnitOfWork_WhenRepositoryThrows()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();

        var repositoryMock = new Mock<ILevelScoreRepository>();
        repositoryMock
            .Setup(repo =>
                repo.CreateAsync(It.IsAny<LevelScore>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ThrowsAsync(new InvalidOperationException("db error"));

        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        var unitOfWorkMock = BuildUnitOfWorkMock();
        var loggerMock = new Mock<ILogger<LevelScoreService>>();
        var service = new LevelScoreService(repositoryMock.Object, userGalleryRepositoryMock.Object,
            () => unitOfWorkMock.Object, Mock.Of<IAchievementEvaluationService>(), Mock.Of<INebulaAchievementDispatcher>(), loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateScore(userId, "TestUser", galleryId, "EASY", 1000));
        unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateScore_EvaluatesAchievements_AfterSuccessfulCommit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();

        var repositoryMock = new Mock<ILevelScoreRepository>();
        repositoryMock
            .Setup(repo =>
                repo.CreateAsync(It.IsAny<LevelScore>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((LevelScore score, NpgsqlConnection? _, NpgsqlTransaction? _) => score);

        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        userGalleryRepositoryMock
            .Setup(repo => repo.UpdateBestTimeIfBetterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(),
                It.IsAny<DateTime>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync(true);

        var evaluationServiceMock = new Mock<IAchievementEvaluationService>();
        evaluationServiceMock.Setup(s => s.EvaluateForUserAsync(userId))
            .ReturnsAsync(new List<AchievementProgressChange>());

        var service = new LevelScoreService(repositoryMock.Object, userGalleryRepositoryMock.Object,
            () => BuildUnitOfWorkMock().Object, evaluationServiceMock.Object,
            Mock.Of<INebulaAchievementDispatcher>(), new Mock<ILogger<LevelScoreService>>().Object);

        // Act
        await service.CreateScore(userId, "TestUser", galleryId, "HARD", 4200);

        // Assert
        evaluationServiceMock.Verify(s => s.EvaluateForUserAsync(userId), Times.Once);
    }

    [Fact]
    public async Task CreateScore_StillSucceeds_WhenAchievementEvaluationThrows()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();

        var repositoryMock = new Mock<ILevelScoreRepository>();
        repositoryMock
            .Setup(repo =>
                repo.CreateAsync(It.IsAny<LevelScore>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((LevelScore score, NpgsqlConnection? _, NpgsqlTransaction? _) => score);

        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        userGalleryRepositoryMock
            .Setup(repo => repo.UpdateBestTimeIfBetterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(),
                It.IsAny<DateTime>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync(true);

        var evaluationServiceMock = new Mock<IAchievementEvaluationService>();
        evaluationServiceMock.Setup(s => s.EvaluateForUserAsync(userId))
            .ThrowsAsync(new InvalidOperationException("evaluation blew up"));

        var service = new LevelScoreService(repositoryMock.Object, userGalleryRepositoryMock.Object,
            () => BuildUnitOfWorkMock().Object, evaluationServiceMock.Object,
            Mock.Of<INebulaAchievementDispatcher>(), new Mock<ILogger<LevelScoreService>>().Object);

        // Act
        var result = await service.CreateScore(userId, "TestUser", galleryId, "HARD", 4200);

        // Assert — a broken achievement evaluation must never fail an otherwise-successful
        // score submission (CLAUDE.md §4's resilience principle, applied here too).
        Assert.Equal("TestUser", result.Username);
        Assert.Equal(4200L, result.TimeMs);
    }

    [Fact]
    public async Task CreateScore_EnqueuesNebulaProgressReport_ForEveryChangedAchievement()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        var changedDef = new AchievementDef
            { Id = Guid.NewGuid(), Code = "VOL-01", Thresholds = new[] { 1, 2, 3, 4, 5 } };
        var change = new AchievementProgressChange { Def = changedDef, OldValue = 0, NewValue = 1 };

        var repositoryMock = new Mock<ILevelScoreRepository>();
        repositoryMock
            .Setup(repo =>
                repo.CreateAsync(It.IsAny<LevelScore>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((LevelScore score, NpgsqlConnection? _, NpgsqlTransaction? _) => score);

        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        userGalleryRepositoryMock
            .Setup(repo => repo.UpdateBestTimeIfBetterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(),
                It.IsAny<DateTime>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync(true);

        var evaluationServiceMock = new Mock<IAchievementEvaluationService>();
        evaluationServiceMock.Setup(s => s.EvaluateForUserAsync(userId))
            .ReturnsAsync(new List<AchievementProgressChange> { change });

        var dispatcherMock = new Mock<INebulaAchievementDispatcher>();

        var service = new LevelScoreService(repositoryMock.Object, userGalleryRepositoryMock.Object,
            () => BuildUnitOfWorkMock().Object, evaluationServiceMock.Object,
            dispatcherMock.Object, new Mock<ILogger<LevelScoreService>>().Object);

        // Act
        await service.CreateScore(userId, "TestUser", galleryId, "HARD", 4200);

        // Assert
        dispatcherMock.Verify(d => d.EnqueueProgressReport(userId, changedDef.Id), Times.Once);
    }

    [Fact]
    public async Task CreateScore_EchoesNewlyUnlockedAchievements_OnlyForChangesThatLeveledUp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        var leveledUpDef = new AchievementDef
        {
            Id = Guid.NewGuid(), Code = "VOL-01", Name = "Marathoner", Description = "Clear many levels.",
            IconUrl = "https://example/icon.svg", Thresholds = new[] { 1, 2, 3, 4, 5 }
        };
        var notLeveledUpDef = new AchievementDef
            { Id = Guid.NewGuid(), Code = "SPD-01", Thresholds = new[] { 1, 2, 3, 4, 5 } };

        var repositoryMock = new Mock<ILevelScoreRepository>();
        repositoryMock
            .Setup(repo =>
                repo.CreateAsync(It.IsAny<LevelScore>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((LevelScore score, NpgsqlConnection? _, NpgsqlTransaction? _) => score);

        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        userGalleryRepositoryMock
            .Setup(repo => repo.UpdateBestTimeIfBetterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(),
                It.IsAny<DateTime>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync(true);

        var evaluationServiceMock = new Mock<IAchievementEvaluationService>();
        evaluationServiceMock.Setup(s => s.EvaluateForUserAsync(userId))
            .ReturnsAsync(new List<AchievementProgressChange>
            {
                new() { Def = leveledUpDef, OldValue = 0, NewValue = 1 }, // crosses level 0 -> 1
                new() { Def = notLeveledUpDef, OldValue = 5, NewValue = 9 } // already maxed, no new level
            });

        var service = new LevelScoreService(repositoryMock.Object, userGalleryRepositoryMock.Object,
            () => BuildUnitOfWorkMock().Object, evaluationServiceMock.Object,
            Mock.Of<INebulaAchievementDispatcher>(), new Mock<ILogger<LevelScoreService>>().Object);

        // Act
        var result = await service.CreateScore(userId, "TestUser", galleryId, "HARD", 4200);

        // Assert
        var unlocked = Assert.Single(result.NewlyUnlockedAchievements);
        Assert.Equal("VOL-01", unlocked.Code);
        Assert.Equal("Marathoner", unlocked.Name);
        Assert.Equal("Clear many levels.", unlocked.Description);
        Assert.Equal("https://example/icon.svg", unlocked.IconUrl);
        Assert.Equal(1, unlocked.Level);
    }

    [Fact]
    public async Task CreateScore_LeavesNewlyUnlockedAchievementsEmpty_WhenAchievementEvaluationThrows()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();

        var repositoryMock = new Mock<ILevelScoreRepository>();
        repositoryMock
            .Setup(repo =>
                repo.CreateAsync(It.IsAny<LevelScore>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((LevelScore score, NpgsqlConnection? _, NpgsqlTransaction? _) => score);

        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        userGalleryRepositoryMock
            .Setup(repo => repo.UpdateBestTimeIfBetterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(),
                It.IsAny<DateTime>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync(true);

        var evaluationServiceMock = new Mock<IAchievementEvaluationService>();
        evaluationServiceMock.Setup(s => s.EvaluateForUserAsync(userId))
            .ThrowsAsync(new InvalidOperationException("evaluation blew up"));

        var service = new LevelScoreService(repositoryMock.Object, userGalleryRepositoryMock.Object,
            () => BuildUnitOfWorkMock().Object, evaluationServiceMock.Object,
            Mock.Of<INebulaAchievementDispatcher>(), new Mock<ILogger<LevelScoreService>>().Object);

        // Act
        var result = await service.CreateScore(userId, "TestUser", galleryId, "HARD", 4200);

        // Assert
        Assert.Empty(result.NewlyUnlockedAchievements);
    }

    [Fact]
    public async Task CreateScore_DoesNotEnqueueNebulaProgressReport_WhenNothingChanged()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();

        var repositoryMock = new Mock<ILevelScoreRepository>();
        repositoryMock
            .Setup(repo =>
                repo.CreateAsync(It.IsAny<LevelScore>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((LevelScore score, NpgsqlConnection? _, NpgsqlTransaction? _) => score);

        var userGalleryRepositoryMock = new Mock<IUserGalleryRepository>();
        userGalleryRepositoryMock
            .Setup(repo => repo.UpdateBestTimeIfBetterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<long>(),
                It.IsAny<DateTime>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync(true);

        var evaluationServiceMock = new Mock<IAchievementEvaluationService>();
        evaluationServiceMock.Setup(s => s.EvaluateForUserAsync(userId))
            .ReturnsAsync(new List<AchievementProgressChange>());

        var dispatcherMock = new Mock<INebulaAchievementDispatcher>();

        var service = new LevelScoreService(repositoryMock.Object, userGalleryRepositoryMock.Object,
            () => BuildUnitOfWorkMock().Object, evaluationServiceMock.Object,
            dispatcherMock.Object, new Mock<ILogger<LevelScoreService>>().Object);

        // Act
        await service.CreateScore(userId, "TestUser", galleryId, "HARD", 4200);

        // Assert
        dispatcherMock.Verify(d => d.EnqueueProgressReport(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }
}