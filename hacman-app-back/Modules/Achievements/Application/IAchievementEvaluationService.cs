namespace hacman_app_back.Modules.Achievements.Application;

public interface IAchievementEvaluationService
{
    /// <summary>
    /// Recomputes every achievement this module currently knows how to calculate for one
    /// user (see <c>AchievementMetricSqlRegistry</c>) and upserts <c>achievement_progress</c>
    /// for whichever ones changed. Idempotent — calling it again with no new activity upserts
    /// nothing. Returns every achievement whose stored value actually changed (old and new
    /// value included), for consumers such as the M4 Nebula progress push (every change) and
    /// the M7-3 "newly unlocked" score-submission echo (only changes where
    /// <see cref="AchievementProgressChange.LeveledUp"/>).
    /// </summary>
    Task<IEnumerable<AchievementProgressChange>> EvaluateForUserAsync(Guid userId);
}
