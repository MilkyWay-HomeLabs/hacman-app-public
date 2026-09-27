namespace hacman_app_back.Modules.Achievements.Infrastructure;

/// <summary>
/// Computes one achievement's raw counter value for a user, straight from level_scores /
/// user_galleries / galleries (STATUS.md §5 scope boundary) — no hacman-game-app payload
/// change needed for any metric implemented this way.
/// </summary>
public interface IAchievementMetricCalculator
{
    /// <summary>The achievement_def.metric this calculator knows how to compute.</summary>
    string Metric { get; }

    Task<long> CalculateAsync(Guid userId);
}
