using hacman_app_back.Modules.PlayerProgress.Domain;
using hacman_app_back.Modules.PlayerProgress.Infrastructure;

namespace hacman_app_back.Modules.PlayerProgress.Application;

public class UserGalleryProvider : IUserGalleryProvider
{
    private readonly IUserGalleryRepository _repository;

    public UserGalleryProvider(IUserGalleryRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IEnumerable<UserGallery>> GetAllByUserIdAsync(Guid userId) =>
        await _repository.GetAllByUserIdAsync(userId);

    public async Task<UserGallery?> GetByGalleryIdAsync(Guid galleryId) =>
        await _repository.GetByGalleryIdAsync(galleryId);
}