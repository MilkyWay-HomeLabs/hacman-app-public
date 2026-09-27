using hacman_app_back.Modules.Achievements.Api;

namespace hacman_app_back.Modules.Achievements.Application;

public interface IAchievementProgressService
{
    /// <summary>
    /// Every achievement in the catalogue, joined with this user's progress (defaulted to
    /// value 0 / level 0 for achievements they haven't touched yet).
    /// </summary>
    Task<IEnumerable<AchievementProgressDto>> GetUserProgressAsync(Guid userId);
}
