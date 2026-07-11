using hacman_app_back.Modules.GalleryCatalog.Domain;

namespace hacman_app_back.Modules.PlayerProgress.Domain;

public class UserGallery
{
    public virtual Guid Id { get; set; }
    public virtual Guid UserId { get; set; }

    public virtual Guid GalleryId { get; set; }

    // Populated on read (joined with galleries); left null when only creating a new association.
    public virtual Gallery? Gallery { get; set; }
    public virtual bool IsUnlocked { get; set; }
    public virtual DateTime? UnlockedAt { get; set; }
    public virtual long? BestTimeMs { get; set; }
    public virtual DateTime? BestTimeAt { get; set; }
    public virtual DateTime CreatedAt { get; set; }
    public virtual DateTime UpdatedAt { get; set; }
}