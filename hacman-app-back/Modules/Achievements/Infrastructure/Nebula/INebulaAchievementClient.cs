using hacman_app_back.Modules.Achievements.Domain;

namespace hacman_app_back.Modules.Achievements.Infrastructure.Nebula;

/// <summary>
/// Pushes Hacman's achievement catalogue and per-user progress into Nebula's
/// achievements-ingestion contract (CLAUDE.md §4, STATUS.md §3). Every implementation must
/// swallow its own exceptions and no-op when not configured — Nebula's availability must
/// never affect a player-facing request.
/// </summary>
public interface INebulaAchievementClient
{
    Task SyncCatalogAsync(IReadOnlyList<AchievementDef> achievements, CancellationToken cancellationToken = default);

    Task ReportProgressAsync(Guid userId, AchievementDef achievement, long value, int level,
        CancellationToken cancellationToken = default);
}
