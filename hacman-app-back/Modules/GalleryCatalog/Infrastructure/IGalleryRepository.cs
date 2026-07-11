using Npgsql;

namespace hacman_app_back.Modules.GalleryCatalog.Infrastructure;

public interface IGalleryRepository
{
    Task<List<Guid>> GetAllGalleryIdsAsync(NpgsqlConnection? connection = null, NpgsqlTransaction? transaction = null);
}