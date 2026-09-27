using System.Text.Json.Serialization;

namespace hacman_app_back.Modules.LevelTemplate.Api;

public class MazeLevelDto
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("galleryId")] public Guid GalleryId { get; set; }
    [JsonPropertyName("difficulty")] public string Difficulty { get; set; } = string.Empty;
    [JsonPropertyName("width")] public short Width { get; set; }
    [JsonPropertyName("height")] public short Height { get; set; }
    [JsonPropertyName("randomSeed")] public int? RandomSeed { get; set; }

    [JsonPropertyName("placementAllowedOn")]
    public string PlacementAllowedOn { get; set; } = string.Empty;

    [JsonPropertyName("placementAvoidAdjWalls")]
    public bool PlacementAvoidAdjWalls { get; set; }

    [JsonPropertyName("cells")] public short[][]? Cells { get; set; }
    [JsonPropertyName("playerStartX")] public short PlayerStartX { get; set; }
    [JsonPropertyName("playerStartY")] public short PlayerStartY { get; set; }
    [JsonPropertyName("playerDirection")] public string PlayerDirection { get; set; } = string.Empty;
    [JsonPropertyName("dots")] public object? Dots { get; set; }
    [JsonPropertyName("enemies")] public object? Enemies { get; set; }
    [JsonPropertyName("buffs")] public object? Buffs { get; set; }
    [JsonPropertyName("debuffs")] public object? Debuffs { get; set; }
    [JsonPropertyName("notes")] public string? Notes { get; set; }
    [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; set; }
    [JsonPropertyName("updatedAt")] public DateTimeOffset UpdatedAt { get; set; }
}