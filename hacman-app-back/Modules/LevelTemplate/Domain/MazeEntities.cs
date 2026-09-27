using System.Text.Json;
using System.Text.Json.Serialization;

namespace hacman_app_back.Modules.LevelTemplate.Domain;

public class MazeEntities
{
    [JsonPropertyName("dots")] public MazeDots Dots { get; set; } = new();

    [JsonPropertyName("enemies")] public List<MazeEnemy> Enemies { get; set; } = new();

    [JsonPropertyName("buffs")] public List<MazeItem> Buffs { get; set; } = new();

    [JsonPropertyName("debuffs")] public List<MazeItem> Debuffs { get; set; } = new();
}

public class MazeDots
{
    [JsonPropertyName("static")] public List<short[]> Static { get; set; } = new();

    [JsonPropertyName("random_count")] public short RandomCount { get; set; } = 0;
}

public class MazeEnemy
{
    [JsonPropertyName("type")] public string Type { get; set; } = default!;

    [JsonPropertyName("id")] public string Id { get; set; } = default!;

    [JsonPropertyName("style")] public string Style { get; set; } = default!;

    [JsonPropertyName("positions")] public List<short[]> Positions { get; set; } = new();

    [JsonPropertyName("random_count")] public short RandomCount { get; set; } = 0;

    [JsonPropertyName("notes")] public string? Notes { get; set; }
}

public class MazeItem
{
    [JsonPropertyName("type")] public string Type { get; set; } = default!;

    [JsonPropertyName("id")] public string Id { get; set; } = default!;

    [JsonPropertyName("style")] public string Style { get; set; } = default!;

    [JsonPropertyName("positions")] public List<short[]> Positions { get; set; } = new();

    [JsonPropertyName("random_count")] public short RandomCount { get; set; } = 0;

    [JsonPropertyName("effect")] public JsonElement Effect { get; set; }

    [JsonPropertyName("rules")] public MazeItemRules Rules { get; set; } = default!;

    [JsonPropertyName("notes")] public string? Notes { get; set; }
}

public class MazeItemRules
{
    [JsonPropertyName("pickupBy")] public List<string> PickupBy { get; set; } = new();

    [JsonPropertyName("destroyOnTouch")] public bool DestroyOnTouch { get; set; } = true;
}