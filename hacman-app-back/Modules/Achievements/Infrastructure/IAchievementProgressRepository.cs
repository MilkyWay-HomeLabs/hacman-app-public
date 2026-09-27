using hacman_app_back.Modules.Achievements.Domain;
using Npgsql;

namespace hacman_app_back.Modules.Achievements.Infrastructure;

public interface IAchievementProgressRepository
{
    /// <summary>
    /// All progress rows for a user. A missing (user, achievement) pair means the counter
    /// has never moved — callers default it to 0, this method never synthesizes zero rows.
    /// Returned <see cref="AchievementProgress.Achievement"/> is a partial reference
    /// (Id only, matching the partial-hydration pattern <c>LevelScoreRepository</c> already
    /// uses elsewhere) — join against the full catalogue for name/thresholds/etc.
    /// </summary>
    Task<IEnumerable<AchievementProgress>> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Upserts a user's raw counter for one achievement. Not yet called by any Application
    /// service — this is the write path the evaluation engine (ROADMAP.md M3) will use once
    /// it exists; building it alongside the read path now keeps M3's own diff to just the
    /// evaluation logic, not repository plumbing.
    /// </summary>
    Task UpsertAsync(Guid userId, Guid achievementDefId, long value,
        NpgsqlConnection? connection = null, NpgsqlTransaction? transaction = null);
}
