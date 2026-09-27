namespace hacman_app_back.Modules.Achievements.Infrastructure;

public interface IAchievementMetricCalculatorProvider
{
    /// <summary>The calculator for a metric, or null if this module can't compute it yet.</summary>
    IAchievementMetricCalculator? TryGet(string metric);
}
