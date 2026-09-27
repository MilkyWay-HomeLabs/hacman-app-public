using hacman_app_back.Modules.Achievements.Domain;

namespace hacman_app_back.Modules.Achievements.Application;

/// <summary>
/// One achievement whose stored <c>achievement_progress.value</c> changed during an
/// <see cref="IAchievementEvaluationService.EvaluateForUserAsync"/> pass. Carries both the
/// old and new raw value so a caller can derive whether the change also crossed a level
/// (M7-3: a level-up, not just any progress tick, is what a "newly unlocked" UI signal
/// should react to).
/// </summary>
public sealed class AchievementProgressChange
{
    public required AchievementDef Def { get; init; }
    public required long OldValue { get; init; }
    public required long NewValue { get; init; }

    public int OldLevel => AchievementLevelCalculator.LevelFor(OldValue, Def.Thresholds);
    public int NewLevel => AchievementLevelCalculator.LevelFor(NewValue, Def.Thresholds);

    /// <summary>True when this change crossed at least one new threshold.</summary>
    public bool LeveledUp => NewLevel > OldLevel;
}
