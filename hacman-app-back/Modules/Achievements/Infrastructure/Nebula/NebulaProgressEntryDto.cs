using System.Text.Json.Serialization;

namespace hacman_app_back.Modules.Achievements.Infrastructure.Nebula;

/// <summary>
/// One progress report as Nebula's <c>POST /api/v1/achievements/progress</c> contract
/// expects it (STATUS.md §3). <see cref="Progress"/> is 0-10000 — a percentage with two
/// implied decimal places, matching Nebula's own <c>user_achievements.progress</c>
/// convention, computed as <c>round(min(1, value / lastThreshold) * 10000)</c>.
/// <see cref="UserId"/> is sent as Hacman's own <c>users.id</c> (a UUID) — STATUS.md §3
/// doesn't pin down the wire type; this is the platform-wide identity Hacman actually has.
/// If Nebula expects something else for this source, that surfaces as a per-entry
/// rejection, not a crash, since every Nebula call here swallows its own exceptions.
/// </summary>
public class NebulaProgressEntryDto
{
    [JsonPropertyName("userId")] public Guid UserId { get; set; }

    [JsonPropertyName("externalId")] public string ExternalId { get; set; } = string.Empty;

    [JsonPropertyName("value")] public long Value { get; set; }

    [JsonPropertyName("level")] public int Level { get; set; }

    [JsonPropertyName("progress")] public int Progress { get; set; }
}
