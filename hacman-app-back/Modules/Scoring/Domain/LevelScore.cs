using hacman_app_back.Modules.GalleryCatalog.Domain;
using hacman_app_back.Modules.Identity.Domain;
using hacman_app_back.Modules.LevelTemplate.Domain;

namespace hacman_app_back.Modules.Scoring.Domain;

public class LevelScore
{
    public virtual Guid Id { get; set; } = Guid.NewGuid();
    public virtual Gallery Gallery { get; set; } = default!;
    public virtual User User { get; set; } = default!;
    public virtual Difficulty Difficulty { get; set; } = Difficulty.MEDIUM;
    public virtual int Position { get; set; }
    public virtual long? TimeMs { get; set; }
    public virtual DateTime? TimeAt { get; set; }
}