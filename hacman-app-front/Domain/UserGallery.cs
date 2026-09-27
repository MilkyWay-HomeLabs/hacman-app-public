namespace hacman_app_front.Domain;

using System.Text.Json.Serialization;

public class UserGallery
{
    [JsonPropertyName("id")] public Guid Id { get; set; }

    [JsonPropertyName("userId")] public Guid UserId { get; set; }

    [JsonPropertyName("gallery")] public required Gallery Gallery { get; set; }

    [JsonPropertyName("isUnlocked")] public bool IsUnlocked { get; set; }

    [JsonPropertyName("unlockedAt")] public DateTime? UnlockedAt { get; set; }

    [JsonPropertyName("bestTimeMs")] public long? BestTimeMs { get; set; }

    [JsonPropertyName("bestTimeAt")] public DateTime? BestTimeAt { get; set; }

    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")] public DateTime UpdatedAt { get; set; }
}