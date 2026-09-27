namespace hacman_app_back.Modules.GalleryCatalog.Infrastructure;

public interface IGallerySubtypeRepository
{
    Task<IEnumerable<string>> GetAllAsync();
}