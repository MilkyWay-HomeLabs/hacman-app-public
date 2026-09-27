using hacman_app_back.Modules.Achievements.Api;
using hacman_app_back.Modules.Achievements.Domain;
using hacman_app_back.Modules.Achievements.Infrastructure;

namespace hacman_app_back.Modules.Achievements.Application;

public class AchievementProgressService : IAchievementProgressService
{
    private readonly IAchievementDefRepository _defRepository;
    private readonly IAchievementProgressRepository _progressRepository;
    private readonly ILogger<AchievementProgressService> _logger;

    public AchievementProgressService(
        IAchievementDefRepository defRepository,
        IAchievementProgressRepository progressRepository,
        ILogger<AchievementProgressService> logger)
    {
        _defRepository = defRepository ?? throw new ArgumentNullException(nameof(defRepository));
        _progressRepository = progressRepository ?? throw new ArgumentNullException(nameof(progressRepository));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<AchievementProgressDto>> GetUserProgressAsync(Guid userId)
    {
        _logger.LogInformation("GetUserProgressAsync: userId={UserId}", userId);

        var defs = await _defRepository.GetAllAsync();
        var progressByAchievementId = (await _progressRepository.GetByUserIdAsync(userId))
            .ToDictionary(p => p.Achievement.Id, p => p.Value);

        var result = defs.Select(def => MapToDto(def, progressByAchievementId.GetValueOrDefault(def.Id, 0L)))
            .ToList();

        _logger.LogInformation("GetUserProgressAsync: userId={UserId}, returned {Count} entries", userId,
            result.Count);
        return result;
    }

    private static AchievementProgressDto MapToDto(AchievementDef def, long value) => new()
    {
        Code = def.Code,
        Category = def.Category.ToString(),
        Name = def.Name,
        Description = def.Description,
        IconUrl = def.IconUrl,
        Thresholds = def.Thresholds,
        Value = value,
        Level = AchievementLevelCalculator.LevelFor(value, def.Thresholds),
        NextThreshold = AchievementLevelCalculator.NextThresholdFor(value, def.Thresholds)
    };
}
