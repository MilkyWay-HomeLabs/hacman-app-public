using hacman_app_back.Modules.Identity.Domain;

namespace hacman_app_back.Modules.Achievements.Domain;

/// <summary>
/// A user's raw progress counter for one achievement. Only <see cref="Value"/> is stored —
/// there is no stored level/stars column; level is always derived on read via
/// <see cref="AchievementLevelCalculator"/> against the achievement's own thresholds (see
/// CLAUDE.md §3 for why: a stored derived column can drift after a threshold re-tune,
/// deriving on read cannot). No row exists for a (user, achievement) pair until the
/// counter first moves off zero.
/// </summary>
public class AchievementProgress
{
    public virtual Guid Id { get; set; } = Guid.NewGuid();
    public virtual User User { get; set; } = default!;
    public virtual AchievementDef Achievement { get; set; } = default!;
    public virtual long Value { get; set; }
    public virtual DateTime UpdatedAt { get; set; }
}
