using System.Text.Json.Serialization;

namespace hacman_app_front.Domain;

public class ScoreEntry
{
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;

    [JsonPropertyName("timeMs")] public long TimeMs { get; set; }

    [JsonPropertyName("achievedAt")] public DateTime AchievedAt { get; set; }

    [JsonPropertyName("difficulty")] public string Difficulty { get; set; } = string.Empty;
}