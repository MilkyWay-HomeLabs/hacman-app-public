using hacman_app_back.Modules.GalleryCatalog.Infrastructure;

namespace hacman_app_back.Modules.GalleryCatalog.Application;

/// <summary>
/// Service class responsible for managing gallery themes.
/// </summary>
public class GalleryThemeService : IGalleryThemeService
{
    private readonly IGalleryThemeRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GalleryThemeService"/> class.
    /// </summary>
    /// <param name="repository">The repository used to access gallery theme data.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when the repository is null.</exception>
    public GalleryThemeService(IGalleryThemeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Retrieves all gallery themes asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable of gallery theme strings.</returns>
    public Task<IEnumerable<string>> GetAllAsync() => _repository.GetAllAsync();
}