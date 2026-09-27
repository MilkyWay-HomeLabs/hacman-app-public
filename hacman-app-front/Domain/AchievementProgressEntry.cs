using System.Text.Json.Serialization;

namespace hacman_app_front.Domain;

/// <summary>Mirrors the backend's AchievementProgressDto (GET /api/v1/achievements/me) exactly.</summary>
public class AchievementProgressEntry
{
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;

    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;

    [JsonPropertyName("iconUrl")] public string? IconUrl { get; set; }

    [JsonPropertyName("thresholds")] public int[] Thresholds { get; set; } = Array.Empty<int>();

    [JsonPropertyName("value")] public long Value { get; set; }

    /// <summary>0 (not started) through 5 (maxed out) — derived server-side, never stored.</summary>
    [JsonPropertyName("level")] public int Level { get; set; }

    [JsonPropertyName("nextThreshold")] public int? NextThreshold { get; set; }
}
