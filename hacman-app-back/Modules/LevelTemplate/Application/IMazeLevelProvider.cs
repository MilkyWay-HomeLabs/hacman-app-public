using hacman_app_back.Modules.LevelTemplate.Api;

namespace hacman_app_back.Modules.LevelTemplate.Application;

public interface IMazeLevelProvider
{
    Task<IEnumerable<MazeLevelDto>> GetMazeLevelsByGalleryId(Guid? galleryId);
}