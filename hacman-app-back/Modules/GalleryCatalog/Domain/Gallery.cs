namespace hacman_app_back.Modules.GalleryCatalog.Domain;

public class Gallery
{
    public virtual Guid Id { get; set; } = Guid.NewGuid();
    public virtual string Title { get; set; } = string.Empty;
    public virtual string? Description { get; set; }
    public virtual int ImageCount { get; set; } = 10;

    public virtual int GalleryId { get; set; }

    public virtual Guid ThemeId { get; set; }
    public virtual LookupValue Theme { get; set; } = default!;

    public virtual Guid TypeId { get; set; }
    public virtual LookupValue Type { get; set; } = default!;

    public virtual Guid SubtypeId { get; set; }
    public virtual LookupValue Subtype { get; set; } = default!;

    public virtual int Level { get; set; }
    public virtual DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}