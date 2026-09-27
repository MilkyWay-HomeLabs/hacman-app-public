using System.Text.Json.Serialization;

namespace hacman_app_back.Modules.Achievements.Infrastructure.Nebula;

/// <summary>
/// One achievement definition as Nebula's <c>POST /api/v1/achievements/catalog</c> contract
/// expects it (STATUS.md §3). <see cref="ExternalId"/> is Hacman's own <c>achievement_def.code</c>
/// — an opaque string to Nebula, never renamed once shipped. <c>source</c> is deliberately
/// absent: Nebula derives it from the bearer token's identity (CLAUDE.md §4.2).
/// </summary>
public class NebulaCatalogAchievementDto
{
    [JsonPropertyName("externalId")] public string ExternalId { get; set; } = string.Empty;

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;

    [JsonPropertyName("iconUrl")] public string? IconUrl { get; set; }

    [JsonPropertyName("minValue")] public int MinValue { get; set; }

    [JsonPropertyName("maxValue")] public int MaxValue { get; set; }

    [JsonPropertyName("levels")] public List<NebulaCatalogLevelDto> Levels { get; set; } = [];
}
