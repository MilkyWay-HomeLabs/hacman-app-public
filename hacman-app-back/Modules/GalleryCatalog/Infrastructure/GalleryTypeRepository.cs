using Microsoft.Extensions.Logging;
using Npgsql;

namespace hacman_app_back.Modules.GalleryCatalog.Infrastructure;

/// <summary>
/// Repository class responsible for accessing type data from the database.
/// </summary>
public class GalleryTypeRepository : IGalleryTypeRepository
{
    private readonly string _connectionString;
    private readonly ILogger<GalleryTypeRepository> _logger;

    public GalleryTypeRepository(string connectionString, ILogger<GalleryTypeRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all gallery types from the <c>gallery_type_enum</c> PostgreSQL enum.
    /// </summary>
    /// <returns>An enumerable of type name strings ordered by enum sort order.</returns>
    public async Task<IEnumerable<string>> GetAllAsync()
    {
        const string sql =
            "SELECT enumlabel FROM pg_enum WHERE enumtypid = 'gallery_type_enum'::regtype ORDER BY enumsortorder;";

        _logger.LogDebug("GetAllAsync: fetching gallery types");

        var result = new List<string>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            result.Add(reader.GetString(0));

        return result;
    }
}