using hacman_app_back.Modules.LevelTemplate.Domain;

namespace hacman_app_back.Modules.LevelTemplate.Infrastructure;

public interface IMazeLevelRepository
{
    Task<IEnumerable<MazeLevel>> GetMazeLevelsByGalleryId(Guid? galleryId);
}