namespace hacman_app_back.Modules.GalleryCatalog.Infrastructure;

public interface IGalleryThemeRepository
{
    Task<IEnumerable<string>> GetAllAsync();
}