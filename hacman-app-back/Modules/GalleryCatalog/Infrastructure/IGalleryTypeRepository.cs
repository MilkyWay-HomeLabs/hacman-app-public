namespace hacman_app_back.Modules.GalleryCatalog.Infrastructure;

public interface IGalleryTypeRepository
{
    Task<IEnumerable<string>> GetAllAsync();
}