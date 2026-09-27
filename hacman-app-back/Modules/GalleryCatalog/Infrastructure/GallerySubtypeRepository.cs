using Npgsql;

namespace hacman_app_back.Modules.GalleryCatalog.Infrastructure;

/// <summary>
/// Repository class responsible for accessing subtype data from the database.
/// </summary>
public class GallerySubtypeRepository : IGallerySubtypeRepository
{
    private readonly string _connectionString;
    private readonly ILogger<GallerySubtypeRepository> _logger;

    public GallerySubtypeRepository(string connectionString, ILogger<GallerySubtypeRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
    }

    /// <summary>
    /// Retrieves all gallery subtypes from the <c>gallery_subtype_enum</c> PostgreSQL enum.
    /// </summary>
    /// <returns>An enumerable of subtype name strings ordered by enum sort order.</returns>
    public async Task<IEnumerable<string>> GetAllAsync()
    {
        const string sql =
            "SELECT enumlabel FROM pg_enum WHERE enumtypid = 'gallery_subtype_enum'::regtype ORDER BY enumsortorder;";

        _logger.LogDebug("GetAllAsync: fetching gallery subtypes");

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