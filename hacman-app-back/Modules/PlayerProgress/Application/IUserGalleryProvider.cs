using hacman_app_back.Modules.PlayerProgress.Domain;

namespace hacman_app_back.Modules.PlayerProgress.Application;

public interface IUserGalleryProvider
{
    Task<IEnumerable<UserGallery>> GetAllByUserIdAsync(Guid userId);
    Task<UserGallery?> GetByGalleryIdAsync(Guid galleryId);
}