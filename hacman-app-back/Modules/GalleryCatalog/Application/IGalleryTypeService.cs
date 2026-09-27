namespace hacman_app_back.Modules.GalleryCatalog.Application;

public interface IGalleryTypeService
{
    Task<IEnumerable<string>> GetAllAsync();
}