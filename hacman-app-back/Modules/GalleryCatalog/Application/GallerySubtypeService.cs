using hacman_app_back.Modules.GalleryCatalog.Infrastructure;

namespace hacman_app_back.Modules.GalleryCatalog.Application;

/// <summary>
/// Service class responsible for managing subtypes.
/// </summary>
public class GallerySubtypeService : IGallerySubtypeService
{
    private readonly IGallerySubtypeRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GallerySubtypeService"/> class.
    /// </summary>
    /// <param name="repository">The repository used to access subtype data.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when the repository is null.</exception>
    public GallerySubtypeService(IGallerySubtypeRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Retrieves all subtypes asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains an enumerable of subtype strings.</returns>
    public Task<IEnumerable<string>> GetAllAsync() => _repository.GetAllAsync();
}