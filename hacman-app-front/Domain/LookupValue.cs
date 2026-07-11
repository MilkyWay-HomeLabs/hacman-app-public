using System.Text.Json.Serialization;

namespace hacman_app_front.Domain;

public class LookupValue
{
    [JsonPropertyName("id")] public virtual Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("name")] public virtual string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")] public virtual string? Description { get; set; }
}