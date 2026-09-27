namespace hacman_app_back.Modules.Achievements.Domain;

/// <summary>
/// Derives an achievement's level (0-5) and next threshold from its raw progress value.
/// Pure, stateless calculation — the level is never stored (see
/// <see cref="AchievementProgress"/>'s remarks).
/// </summary>
public static class AchievementLevelCalculator
{
    /// <summary>Every achievement's threshold ladder has exactly this many rungs.</summary>
    public const int MaxLevel = 5;

    /// <summary>
    /// How many thresholds <paramref name="value"/> has cleared: 0 (not started) through
    /// <see cref="MaxLevel"/> (maxed out). Thresholds are checked in order; the first one
    /// not yet reached stops the count.
    /// </summary>
    public static int LevelFor(long value, IReadOnlyList<int> thresholds)
    {
        ValidateThresholds(thresholds);

        var level = 0;
        for (var i = 0; i < thresholds.Count; i++)
        {
            if (value < thresholds[i])
                break;
            level = i + 1;
        }

        return level;
    }

    /// <summary>The next threshold <paramref name="value"/> must reach, or null once maxed out.</summary>
    public static int? NextThresholdFor(long value, IReadOnlyList<int> thresholds)
    {
        var level = LevelFor(value, thresholds);
        return level >= thresholds.Count ? null : thresholds[level];
    }

    private static void ValidateThresholds(IReadOnlyList<int> thresholds)
    {
        if (thresholds.Count != MaxLevel)
        {
            throw new ArgumentException(
                $"Expected exactly {MaxLevel} thresholds, got {thresholds.Count}.", nameof(thresholds));
        }
    }
}
