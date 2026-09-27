using hacman_app_back.Modules.GalleryCatalog.Domain;
using hacman_app_back.Modules.Identity.Domain;
using hacman_app_back.Modules.LevelTemplate.Domain;

namespace hacman_app_back.Modules.Scoring.Domain;

public class LevelScore
{
    public virtual Guid Id { get; set; } = Guid.NewGuid();
    public virtual Gallery Gallery { get; set; } = default!;
    public virtual User User { get; set; } = default!;
    public virtual Difficulty Difficulty { get; set; } = Difficulty.MEDIUM;
    public virtual int Position { get; set; }
    public virtual long? TimeMs { get; set; }
    public virtual DateTime? TimeAt { get; set; }

    /// <summary>
    /// Gameplay-telemetry counters (M7-1/M7-2), client-reported and opt-in — all default to 0
    /// for any caller that doesn't send them. Feed the COMBAT/POWER_UPS/SURVIVAL achievement
    /// categories (ROADMAP.md M7); unrelated to score ranking itself.
    /// </summary>
    public virtual int EnemiesDefeated { get; set; }
    public virtual int BuffsCollected { get; set; }
    public virtual int HitsTaken { get; set; }
}