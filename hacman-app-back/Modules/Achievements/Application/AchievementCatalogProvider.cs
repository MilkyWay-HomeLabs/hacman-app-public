using hacman_app_back.Modules.Achievements.Api;
using hacman_app_back.Modules.Achievements.Domain;
using hacman_app_back.Modules.Achievements.Infrastructure;

namespace hacman_app_back.Modules.Achievements.Application;

public class AchievementCatalogProvider : IAchievementCatalogProvider
{
    private readonly IAchievementDefRepository _repository;
    private readonly ILogger<AchievementCatalogProvider> _logger;

    public AchievementCatalogProvider(IAchievementDefRepository repository, ILogger<AchievementCatalogProvider> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<AchievementDefDto>> GetCatalogAsync()
    {
        _logger.LogInformation("GetCatalogAsync: fetching the achievement catalogue");

        var defs = await _repository.GetAllAsync();
        var result = defs.Select(MapToDto).ToList();

        _logger.LogInformation("GetCatalogAsync: returned {Count} achievement definitions", result.Count);
        return result;
    }

    private static AchievementDefDto MapToDto(AchievementDef def) => new()
    {
        Code = def.Code,
        Category = def.Category.ToString(),
        Name = def.Name,
        Description = def.Description,
        IconUrl = def.IconUrl,
        Thresholds = def.Thresholds
    };
}
