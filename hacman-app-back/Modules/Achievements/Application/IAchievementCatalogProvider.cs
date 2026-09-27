using hacman_app_back.Modules.Achievements.Api;

namespace hacman_app_back.Modules.Achievements.Application;

public interface IAchievementCatalogProvider
{
    /// <summary>The full 100-row achievement catalogue.</summary>
    Task<IEnumerable<AchievementDefDto>> GetCatalogAsync();
}
