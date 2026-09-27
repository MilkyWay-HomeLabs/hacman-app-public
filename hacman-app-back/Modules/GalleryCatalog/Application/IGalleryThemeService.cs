namespace hacman_app_back.Modules.GalleryCatalog.Application;

public interface IGalleryThemeService
{
    Task<IEnumerable<string>> GetAllAsync();
}