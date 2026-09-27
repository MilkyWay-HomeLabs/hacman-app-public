using System.Threading.Channels;
using hacman_app_back.Modules.Achievements.Domain;

namespace hacman_app_back.Modules.Achievements.Infrastructure.Nebula;

/// <summary>
/// Drains a single-reader channel of Nebula work items sequentially, one at a time, on a
/// dedicated background loop — the .NET-idiomatic equivalent of Robak's single-thread
/// executor (STATUS.md §4.3). Registered as a hosted service, so it starts once per process
/// and its <see cref="ExecuteAsync"/> loop self-schedules the "once per boot" catalog sync
/// (M4-2) before entering the consume loop.
/// </summary>
public sealed class NebulaAchievementDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<NebulaAchievementDispatcher> logger) : BackgroundService, INebulaAchievementDispatcher
{
    private readonly Channel<NebulaWorkItem> _channel =
        Channel.CreateUnbounded<NebulaWorkItem>(new UnboundedChannelOptions { SingleReader = true });

    /// <inheritdoc/>
    public void EnqueueCatalogSync() => _channel.Writer.TryWrite(new CatalogSyncWorkItem());

    /// <inheritdoc/>
    public void EnqueueProgressReport(Guid userId, Guid achievementDefId) =>
        _channel.Writer.TryWrite(new ProgressReportWorkItem(userId, achievementDefId));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        EnqueueCatalogSync();

        await foreach (var item in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(item, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Nebula dispatcher: work item failed (non-fatal)");
            }
        }
    }

    private async Task ProcessAsync(NebulaWorkItem item, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<INebulaAchievementClient>();

        switch (item)
        {
            case CatalogSyncWorkItem:
                var defRepository = scope.ServiceProvider.GetRequiredService<IAchievementDefRepository>();
                var defs = (await defRepository.GetAllAsync()).ToList();
                await client.SyncCatalogAsync(defs, cancellationToken);
                break;

            case ProgressReportWorkItem(var userId, var achievementDefId):
                await ReportProgressAsync(scope.ServiceProvider, client, userId, achievementDefId, cancellationToken);
                break;
        }
    }

    private static async Task ReportProgressAsync(IServiceProvider services, INebulaAchievementClient client,
        Guid userId, Guid achievementDefId, CancellationToken cancellationToken)
    {
        var defRepository = services.GetRequiredService<IAchievementDefRepository>();
        var progressRepository = services.GetRequiredService<IAchievementProgressRepository>();

        // Re-read post-commit, per CLAUDE.md §4.4 — never carry the just-computed value forward.
        var def = (await defRepository.GetAllAsync()).FirstOrDefault(d => d.Id == achievementDefId);
        if (def is null)
            return;

        var progress = (await progressRepository.GetByUserIdAsync(userId))
            .FirstOrDefault(p => p.Achievement.Id == achievementDefId);
        var value = progress?.Value ?? 0L;
        var level = AchievementLevelCalculator.LevelFor(value, def.Thresholds);

        await client.ReportProgressAsync(userId, def, value, level, cancellationToken);
    }

    private abstract record NebulaWorkItem;

    private sealed record CatalogSyncWorkItem : NebulaWorkItem;

    private sealed record ProgressReportWorkItem(Guid UserId, Guid AchievementDefId) : NebulaWorkItem;
}
