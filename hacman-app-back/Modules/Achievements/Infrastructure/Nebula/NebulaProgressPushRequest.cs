using System.Text.Json.Serialization;

namespace hacman_app_back.Modules.Achievements.Infrastructure.Nebula;

public class NebulaProgressPushRequest
{
    [JsonPropertyName("entries")] public List<NebulaProgressEntryDto> Entries { get; set; } = [];
}
