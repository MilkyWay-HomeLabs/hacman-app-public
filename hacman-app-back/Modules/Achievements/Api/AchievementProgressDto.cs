using System.Text.Json.Serialization;

namespace hacman_app_back.Modules.Achievements.Api;

public class AchievementProgressDto
{
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;

    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;

    [JsonPropertyName("iconUrl")] public string? IconUrl { get; set; }

    [JsonPropertyName("thresholds")] public int[] Thresholds { get; set; } = Array.Empty<int>();

    [JsonPropertyName("value")] public long Value { get; set; }

    /// <summary>0 (not started) through 5 (maxed out) — derived, never stored (see <c>AchievementProgress</c>).</summary>
    [JsonPropertyName("level")] public int Level { get; set; }

    [JsonPropertyName("nextThreshold")] public int? NextThreshold { get; set; }
}
