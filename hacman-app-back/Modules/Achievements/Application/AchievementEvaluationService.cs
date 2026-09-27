using hacman_app_back.Modules.Achievements.Domain;
using hacman_app_back.Modules.Achievements.Infrastructure;

namespace hacman_app_back.Modules.Achievements.Application;

public class AchievementEvaluationService : IAchievementEvaluationService
{
    private readonly IAchievementDefRepository _defRepository;
    private readonly IAchievementProgressRepository _progressRepository;
    private readonly IAchievementMetricCalculatorProvider _calculatorProvider;
    private readonly ILogger<AchievementEvaluationService> _logger;

    public AchievementEvaluationService(
        IAchievementDefRepository defRepository,
        IAchievementProgressRepository progressRepository,
        IAchievementMetricCalculatorProvider calculatorProvider,
        ILogger<AchievementEvaluationService> logger)
    {
        _defRepository = defRepository ?? throw new ArgumentNullException(nameof(defRepository));
        _progressRepository = progressRepository ?? throw new ArgumentNullException(nameof(progressRepository));
        _calculatorProvider = calculatorProvider ?? throw new ArgumentNullException(nameof(calculatorProvider));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<AchievementProgressChange>> EvaluateForUserAsync(Guid userId)
    {
        _logger.LogInformation("EvaluateForUserAsync: userId={UserId}", userId);

        var defs = (await _defRepository.GetAllAsync()).ToList();
        var currentByAchievementId = (await _progressRepository.GetByUserIdAsync(userId))
            .ToDictionary(p => p.Achievement.Id, p => p.Value);

        var changed = new List<AchievementProgressChange>();

        foreach (var def in defs)
        {
            var calculator = _calculatorProvider.TryGet(def.Metric);
            if (calculator is null)
                continue;

            var newValue = await calculator.CalculateAsync(userId);
            var oldValue = currentByAchievementId.GetValueOrDefault(def.Id, 0L);

            if (newValue == oldValue)
                continue;

            await _progressRepository.UpsertAsync(userId, def.Id, newValue);
            changed.Add(new AchievementProgressChange { Def = def, OldValue = oldValue, NewValue = newValue });
        }

        _logger.LogInformation("EvaluateForUserAsync: userId={UserId}, changed={ChangedCount}/{TotalCount}",
            userId, changed.Count, defs.Count);

        return changed;
    }
}
