namespace hacman_app_front.Domain;

using System.Text.Json.Serialization;

public sealed class UserResponse
{
    [JsonPropertyName("id")] public Guid Id { get; init; }

    [JsonPropertyName("externalAuthId")] public string? ExternalAuthId { get; init; } = string.Empty;

    [JsonPropertyName("username")] public string? Username { get; init; }

    [JsonPropertyName("email")] public string? Email { get; init; }

    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")] public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}