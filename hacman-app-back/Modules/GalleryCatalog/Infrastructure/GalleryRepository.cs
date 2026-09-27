using Npgsql;

namespace hacman_app_back.Modules.GalleryCatalog.Infrastructure;

/// <summary>
/// ADO.NET repository for gallery records.
/// </summary>
public class GalleryRepository : IGalleryRepository
{
    private readonly string _connectionString;
    private readonly ILogger<GalleryRepository> _logger;

    public GalleryRepository(string connectionString, ILogger<GalleryRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
    }

    /// <summary>
    /// Returns all gallery identifiers. Accepts an existing connection and transaction to participate in a
    /// caller-managed unit of work.
    /// </summary>
    public async Task<List<Guid>> GetAllGalleryIdsAsync(NpgsqlConnection? connection = null,
        NpgsqlTransaction? transaction = null)
    {
        const string sql = "SELECT id FROM galleries";
        _logger.LogDebug("GetAllGalleryIdsAsync: fetching all gallery ids");

        var conn = connection;
        var shouldDispose = false;
        if (conn == null)
        {
            conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            shouldDispose = true;
        }

        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn, transaction);
            await using var reader = await cmd.ExecuteReaderAsync();

            var galleryIds = new List<Guid>();
            while (await reader.ReadAsync())
                galleryIds.Add(reader.GetGuid(0));

            _logger.LogDebug("GetAllGalleryIdsAsync: returned {Count} ids", galleryIds.Count);
            return galleryIds;
        }
        finally
        {
            if (shouldDispose && conn != null)
                await conn.DisposeAsync();
        }
    }
}