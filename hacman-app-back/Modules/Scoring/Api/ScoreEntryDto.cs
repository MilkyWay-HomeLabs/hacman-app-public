using System.Text.Json.Serialization;
using hacman_app_back.Modules.Achievements.Api;

namespace hacman_app_back.Modules.Scoring.Api;

public class ScoreEntryDto
{
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;

    [JsonPropertyName("timeMs")] public long TimeMs { get; set; }

    [JsonPropertyName("achievedAt")] public DateTime AchievedAt { get; set; }

    [JsonPropertyName("difficulty")] public string Difficulty { get; set; } = string.Empty;

    /// <summary>
    /// Achievements that leveled up as a direct result of this score submission (M7-3).
    /// Empty on every submission that didn't cross a new threshold — not an error, and not
    /// distinguishable from achievement evaluation having failed (CLAUDE.md §4's resilience
    /// principle: a broken evaluation must never fail the submission, so it degrades to "no
    /// unlocks reported" the same way a genuinely quiet submission does).
    /// </summary>
    [JsonPropertyName("newlyUnlockedAchievements")]
    public List<UnlockedAchievementDto> NewlyUnlockedAchievements { get; set; } = new();
}