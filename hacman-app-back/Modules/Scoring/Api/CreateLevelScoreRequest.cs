namespace hacman_app_back.Modules.Scoring.Api;

public class CreateLevelScoreRequest
{
    public required Guid GalleryId { get; set; }
    public required string Difficulty { get; set; }
    public required long TimeMs { get; set; }

    /// <summary>
    /// Opt-in gameplay-telemetry counters (M7-1/M7-2). Optional so existing callers are
    /// unaffected; hacman-game-app always sends concrete numbers, defaulted to 0 there.
    /// </summary>
    public int EnemiesDefeated { get; set; }
    public int BuffsCollected { get; set; }
    public int HitsTaken { get; set; }
}