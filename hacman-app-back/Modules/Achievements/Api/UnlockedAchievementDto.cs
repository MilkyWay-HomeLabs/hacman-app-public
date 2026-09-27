using System.Text.Json.Serialization;

namespace hacman_app_back.Modules.Achievements.Api;

/// <summary>
/// One achievement that just leveled up, echoed on the score-submission response
/// (ROADMAP.md M7-3) so the game client can show an "Achievement Unlocked" toast without a
/// separate round-trip to <c>GET /api/v1/achievements/me</c>.
/// </summary>
public class UnlockedAchievementDto
{
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;

    [JsonPropertyName("iconUrl")] public string? IconUrl { get; set; }

    /// <summary>The level (1-5) just reached, not the achievement's maximum.</summary>
    [JsonPropertyName("level")] public int Level { get; set; }
}
