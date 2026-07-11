using hacman_app_back.Modules.GalleryCatalog.Infrastructure;
using hacman_app_back.Modules.Identity.Domain;
using hacman_app_back.Modules.Identity.Infrastructure;
using hacman_app_back.Modules.PlayerProgress.Domain;
using hacman_app_back.Modules.PlayerProgress.Infrastructure;
using hacman_app_back.SharedKernel;

namespace hacman_app_back.Modules.Identity.Application;

public class UserUpdater : IUserUpdater
{
    private readonly IUserRepository _repository;
    private readonly IGalleryRepository _galleryRepository;
    private readonly IUserGalleryRepository _userGalleryRepository;
    private readonly Func<IUnitOfWork> _unitOfWorkFactory;

    public UserUpdater(
        IUserRepository repository,
        IGalleryRepository galleryRepository,
        IUserGalleryRepository userGalleryRepository,
        Func<IUnitOfWork> unitOfWorkFactory)
    {
        _repository = repository;
        _galleryRepository = galleryRepository;
        _userGalleryRepository = userGalleryRepository;
        _unitOfWorkFactory = unitOfWorkFactory;
    }

    public virtual async Task<User> CreateNewUserData(string externalAuthId, string username, string email)
    {
        var existingUser = await _repository.GetByExternalAuthIdAsync(externalAuthId);
        if (existingUser != null)
        {
            throw new InvalidOperationException("User with this external auth ID already exists");
        }

        await using var unitOfWork = _unitOfWorkFactory();
        await unitOfWork.BeginAsync();

        try
        {
            var user = await CreateUserAsync(externalAuthId, username, email, unitOfWork);
            await CreateUserGalleriesAsync(user.Id, unitOfWork);

            await unitOfWork.CommitAsync();
            return user;
        }
        catch
        {
            await unitOfWork.RollbackAsync();
            throw;
        }
    }

    private async Task<User> CreateUserAsync(string externalAuthId, string username, string email,
        IUnitOfWork unitOfWork)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalAuthId = externalAuthId,
            Username = username,
            Email = email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(user, unitOfWork.Connection, unitOfWork.Transaction);
        return user;
    }

    private async Task CreateUserGalleriesAsync(Guid userId, IUnitOfWork unitOfWork)
    {
        var galleryIds = await _galleryRepository.GetAllGalleryIdsAsync(unitOfWork.Connection, unitOfWork.Transaction);
        foreach (var galleryId in galleryIds)
        {
            var userGallery = new UserGallery
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GalleryId = galleryId,
                IsUnlocked = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _userGalleryRepository.CreateAsync(userGallery, unitOfWork.Connection, unitOfWork.Transaction);
        }
    }
}