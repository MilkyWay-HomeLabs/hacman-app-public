using System.Text.Json.Serialization;

namespace hacman_app_back.Modules.Achievements.Infrastructure.Nebula;

public class NebulaCatalogSyncRequest
{
    [JsonPropertyName("achievements")] public List<NebulaCatalogAchievementDto> Achievements { get; set; } = [];
}
