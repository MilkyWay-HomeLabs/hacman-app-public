namespace hacman_app_front.Domain;

using System.Text.Json.Serialization;

public class Gallery
{
    [JsonPropertyName("id")] public virtual Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("title")] public virtual string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")] public virtual string? Description { get; set; }

    [JsonPropertyName("imageCount")] public virtual int ImageCount { get; set; } = 10;

    [JsonPropertyName("galleryId")] public virtual int GalleryId { get; set; }

    [JsonPropertyName("theme")] public virtual LookupValue Theme { get; set; } = default!;

    [JsonPropertyName("type")] public virtual LookupValue Type { get; set; } = default!;

    [JsonPropertyName("subtype")] public virtual LookupValue Subtype { get; set; } = default!;

    [JsonPropertyName("level")] public virtual int Level { get; set; }

    [JsonPropertyName("createdAt")] public virtual DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}