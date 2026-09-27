using System.Text.Json.Serialization;

namespace hacman_app_back.Modules.Achievements.Infrastructure.Nebula;

/// <summary>One rung of an achievement's threshold ladder, as Nebula's catalog contract expects it.</summary>
public class NebulaCatalogLevelDto
{
    [JsonPropertyName("level")] public int Level { get; set; }

    [JsonPropertyName("value")] public int Value { get; set; }
}
