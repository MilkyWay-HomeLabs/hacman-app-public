using hacman_app_front.Domain;

namespace hacman_app_front.Services;

public interface IAchievementsApiService
{
    /// <summary>
    /// The full 100-achievement catalogue joined with the current user's progress, exactly
    /// as GET /api/v1/achievements/me returns it (the join happens server-side; this
    /// service makes a single call, never two + a client-side join).
    /// </summary>
    Task<List<AchievementProgressEntry>> GetMyProgress();
}
