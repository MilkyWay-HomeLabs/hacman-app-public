namespace hacman_app_back.Modules.Achievements.Infrastructure.Nebula;

/// <summary>
/// Non-blocking entry point for scheduling Nebula work off the request thread. Enqueue
/// calls return immediately; the actual HTTP work happens on a dedicated background
/// consumer (<see cref="NebulaAchievementDispatcher"/>), one item at a time.
/// </summary>
public interface INebulaAchievementDispatcher
{
    /// <summary>Schedules a full catalog sync (all achievement_def rows).</summary>
    void EnqueueCatalogSync();

    /// <summary>
    /// Schedules a progress push for one (user, achievement) pair. Deliberately takes only
    /// the ids, not a value — the consumer re-reads the current value from the database at
    /// execution time (CLAUDE.md §4.4 / STATUS.md §4.3: never carry a just-computed value
    /// forward, to avoid reporting a stale value if two writes race).
    /// </summary>
    void EnqueueProgressReport(Guid userId, Guid achievementDefId);
}
