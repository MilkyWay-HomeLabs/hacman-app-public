#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using hacman_app_back.Modules.Achievements.Domain;
using hacman_app_back.Modules.Achievements.Infrastructure;
using hacman_app_back.Modules.Achievements.Infrastructure.Nebula;
using hacman_app_back.Modules.Identity.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace hacman_app_back.Tests.Modules.Achievements.Infrastructure.Nebula;

/// <summary>
/// The dispatcher runs its work on a background loop (the single-thread-executor
/// equivalent, see its own doc comment) — these tests start it for real and wait on a
/// signal set by a mocked <see cref="INebulaAchievementClient"/> call, rather than
/// asserting on internal state, since the whole point is that work happens off the
/// caller's thread.
/// </summary>
public class NebulaAchievementDispatcherTest
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static ServiceProvider BuildServices(
        Mock<IAchievementDefRepository> defRepositoryMock,
        Mock<IAchievementProgressRepository> progressRepositoryMock,
        Mock<INebulaAchievementClient> clientMock)
    {
        var services = new ServiceCollection();
        services.AddSingleton(defRepositoryMock.Object);
        services.AddSingleton(progressRepositoryMock.Object);
        services.AddSingleton(clientMock.Object);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task StartAsync_TriggersACatalogSync_OnceOnBoot()
    {
        var defRepositoryMock = new Mock<IAchievementDefRepository>();
        defRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AchievementDef>());
        var progressRepositoryMock = new Mock<IAchievementProgressRepository>();

        var tcs = new TaskCompletionSource();
        var clientMock = new Mock<INebulaAchievementClient>();
        clientMock
            .Setup(c => c.SyncCatalogAsync(It.IsAny<IReadOnlyList<AchievementDef>>(), It.IsAny<CancellationToken>()))
            .Callback(() => tcs.TrySetResult())
            .Returns(Task.CompletedTask);

        await using var provider = BuildServices(defRepositoryMock, progressRepositoryMock, clientMock);
        var dispatcher = new NebulaAchievementDispatcher(provider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<NebulaAchievementDispatcher>>());

        await dispatcher.StartAsync(CancellationToken.None);
        try
        {
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(Timeout));
            Assert.Same(tcs.Task, completed);
            clientMock.Verify(
                c => c.SyncCatalogAsync(It.IsAny<IReadOnlyList<AchievementDef>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task EnqueueProgressReport_ReReadsTheCurrentValueFromTheRepository_NotAStaleOne()
    {
        var achievementId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var def = new AchievementDef
        {
            Id = achievementId, Code = "VOL-01", Name = "Dedicated", Description = "d",
            Category = AchievementCategory.VOLUME, Metric = "total_clears",
            Thresholds = new[] { 1, 2, 3, 4, 5 }
        };

        var defRepositoryMock = new Mock<IAchievementDefRepository>();
        defRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AchievementDef> { def });

        var progressRepositoryMock = new Mock<IAchievementProgressRepository>();
        progressRepositoryMock.Setup(r => r.GetByUserIdAsync(userId)).ReturnsAsync(new List<AchievementProgress>
        {
            new()
            {
                User = new User { Id = userId }, Achievement = new AchievementDef { Id = achievementId }, Value = 42
            }
        });

        var tcs = new TaskCompletionSource();
        long? reportedValue = null;
        int? reportedLevel = null;
        var clientMock = new Mock<INebulaAchievementClient>();
        clientMock
            .Setup(c => c.SyncCatalogAsync(It.IsAny<IReadOnlyList<AchievementDef>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        clientMock
            .Setup(c => c.ReportProgressAsync(userId, It.IsAny<AchievementDef>(), It.IsAny<long>(), It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, AchievementDef, long, int, CancellationToken>((_, _, value, level, _) =>
            {
                reportedValue = value;
                reportedLevel = level;
                tcs.TrySetResult();
            })
            .Returns(Task.CompletedTask);

        await using var provider = BuildServices(defRepositoryMock, progressRepositoryMock, clientMock);
        var dispatcher = new NebulaAchievementDispatcher(provider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<NebulaAchievementDispatcher>>());

        await dispatcher.StartAsync(CancellationToken.None);
        try
        {
            // The caller (LevelScoreService) never passes a value here — only the ids.
            dispatcher.EnqueueProgressReport(userId, achievementId);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(Timeout));
            Assert.Same(tcs.Task, completed);
            Assert.Equal(42L, reportedValue);
            Assert.Equal(5, reportedLevel); // LevelFor(42, {1,2,3,4,5}) == 5 (maxed out)
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task EnqueueProgressReport_SkipsSilently_WhenAchievementDefNoLongerExists()
    {
        var userId = Guid.NewGuid();
        var unknownAchievementId = Guid.NewGuid();

        var defRepositoryMock = new Mock<IAchievementDefRepository>();
        defRepositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<AchievementDef>());
        var progressRepositoryMock = new Mock<IAchievementProgressRepository>();

        var catalogSyncSeen = new TaskCompletionSource();
        var clientMock = new Mock<INebulaAchievementClient>();
        clientMock
            .Setup(c => c.SyncCatalogAsync(It.IsAny<IReadOnlyList<AchievementDef>>(), It.IsAny<CancellationToken>()))
            .Callback(() => catalogSyncSeen.TrySetResult())
            .Returns(Task.CompletedTask);

        await using var provider = BuildServices(defRepositoryMock, progressRepositoryMock, clientMock);
        var dispatcher = new NebulaAchievementDispatcher(provider.GetRequiredService<IServiceScopeFactory>(),
            Mock.Of<ILogger<NebulaAchievementDispatcher>>());

        await dispatcher.StartAsync(CancellationToken.None);
        try
        {
            await Task.WhenAny(catalogSyncSeen.Task, Task.Delay(Timeout));
            dispatcher.EnqueueProgressReport(userId, unknownAchievementId);

            // Give the background loop a chance to process it — there is nothing to await
            // on here since the point is that nothing happens.
            await Task.Delay(TimeSpan.FromMilliseconds(200));

            clientMock.Verify(
                c => c.ReportProgressAsync(It.IsAny<Guid>(), It.IsAny<AchievementDef>(), It.IsAny<long>(),
                    It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }
}
