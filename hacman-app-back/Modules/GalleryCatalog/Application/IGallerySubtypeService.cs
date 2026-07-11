namespace hacman_app_back.Modules.GalleryCatalog.Application;

public interface IGallerySubtypeService
{
    Task<IEnumerable<string>> GetAllAsync();
}