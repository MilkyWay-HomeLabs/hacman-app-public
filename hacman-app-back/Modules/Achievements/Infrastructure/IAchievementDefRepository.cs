using hacman_app_back.Modules.Achievements.Domain;

namespace hacman_app_back.Modules.Achievements.Infrastructure;

public interface IAchievementDefRepository
{
    /// <summary>The full achievement catalogue (100 rows, 10 per category), ordered by category then code.</summary>
    Task<IEnumerable<AchievementDef>> GetAllAsync();
}
