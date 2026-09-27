namespace hacman_app_back.Modules.Achievements.Domain;

/// <summary>
/// One row of the static, platform-wide achievement catalogue (100 rows, 10 per
/// <see cref="AchievementCategory"/>). <see cref="Code"/> doubles as Nebula's
/// <c>externalId</c> when the catalogue is pushed via the achievements-ingestion
/// contract (ROADMAP.md M4) — it is never renamed once shipped.
/// </summary>
public class AchievementDef
{
    public virtual Guid Id { get; set; } = Guid.NewGuid();
    public virtual string Code { get; set; } = default!;
    public virtual AchievementCategory Category { get; set; }
    public virtual string Name { get; set; } = default!;
    public virtual string Description { get; set; } = default!;
    public virtual string? IconUrl { get; set; }
    public virtual string Metric { get; set; } = default!;

    /// <summary>The 5-level threshold ladder this achievement's raw counter is measured against.</summary>
    public virtual int[] Thresholds { get; set; } = Array.Empty<int>();

    public virtual DateTime CreatedAt { get; set; }
}
