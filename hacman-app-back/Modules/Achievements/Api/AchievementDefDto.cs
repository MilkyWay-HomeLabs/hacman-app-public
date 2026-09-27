using System.Text.Json.Serialization;

namespace hacman_app_back.Modules.Achievements.Api;

public class AchievementDefDto
{
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;

    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;

    [JsonPropertyName("iconUrl")] public string? IconUrl { get; set; }

    [JsonPropertyName("thresholds")] public int[] Thresholds { get; set; } = Array.Empty<int>();
}
