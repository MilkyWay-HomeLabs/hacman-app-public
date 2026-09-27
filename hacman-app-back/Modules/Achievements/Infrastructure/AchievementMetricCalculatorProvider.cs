namespace hacman_app_back.Modules.Achievements.Infrastructure;

/// <inheritdoc/>
public sealed class AchievementMetricCalculatorProvider : IAchievementMetricCalculatorProvider
{
    private readonly IReadOnlyDictionary<string, IAchievementMetricCalculator> _calculators;

    public AchievementMetricCalculatorProvider(string connectionString)
    {
        if (connectionString == null) throw new ArgumentNullException(nameof(connectionString));

        _calculators = AchievementMetricSqlRegistry.Sql.ToDictionary(
            kv => kv.Key,
            IAchievementMetricCalculator (kv) => new SqlAchievementMetricCalculator(connectionString, kv.Key, kv.Value));
    }

    /// <inheritdoc/>
    public IAchievementMetricCalculator? TryGet(string metric) => _calculators.GetValueOrDefault(metric);
}
