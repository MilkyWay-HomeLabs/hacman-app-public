using hacman_app_front.Domain;

namespace hacman_app_front.Services;

public interface IUserGalleryApiService
{
    Task<List<UserGallery>> GetUserGalleries(Guid userId);
}