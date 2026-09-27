using hacman_app_back.Modules.GalleryCatalog.Domain;
using hacman_app_back.Modules.PlayerProgress.Domain;
using Npgsql;

namespace hacman_app_back.Modules.PlayerProgress.Infrastructure;

/// <summary>
/// ADO.NET repository for user-gallery associations and related score data.
/// </summary>
public class UserGalleryRepository : IUserGalleryRepository
{
    private readonly string _connectionString;
    private readonly ILogger<UserGalleryRepository> _logger;

    public UserGalleryRepository(string connectionString, ILogger<UserGalleryRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UserGallery> CreateAsync(UserGallery userGallery,
        NpgsqlConnection? connection = null, NpgsqlTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO user_galleries (id, user_id, gallery_id, is_unlocked, unlocked_at, best_time_ms, best_time_at, created_at, updated_at)
            VALUES (@id, @userId, @galleryId, @isUnlocked, @unlockedAt, @bestTimeMs, @bestTimeAt, @createdAt, @updatedAt)
            RETURNING id, user_id, gallery_id, is_unlocked, unlocked_at, best_time_ms, best_time_at, created_at, updated_at";

        _logger.LogInformation("CreateAsync: userId={UserId}, galleryId={GalleryId}",
            userGallery.UserId, userGallery.GalleryId);

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
            cmd.Parameters.AddWithValue("@id", userGallery.Id);
            cmd.Parameters.AddWithValue("@userId", userGallery.UserId);
            cmd.Parameters.AddWithValue("@galleryId", userGallery.GalleryId);
            cmd.Parameters.AddWithValue("@isUnlocked", userGallery.IsUnlocked);
            cmd.Parameters.AddWithValue("@unlockedAt", (object?)userGallery.UnlockedAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@bestTimeMs", (object?)userGallery.BestTimeMs ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@bestTimeAt", (object?)userGallery.BestTimeAt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@createdAt", userGallery.CreatedAt);
            cmd.Parameters.AddWithValue("@updatedAt", userGallery.UpdatedAt);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                // Gallery is left null here: creating an association doesn't join galleries,
                // so there's no full Gallery object to populate (see GetAllByUserIdAsync/GetByGalleryIdAsync for that).
                return new UserGallery
                {
                    Id = reader.GetGuid(0),
                    UserId = reader.GetGuid(1),
                    GalleryId = reader.GetGuid(2),
                    IsUnlocked = reader.GetBoolean(3),
                    UnlockedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    BestTimeMs = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                    BestTimeAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    CreatedAt = reader.GetDateTime(7),
                    UpdatedAt = reader.GetDateTime(8)
                };
            }

            throw new InvalidOperationException("Failed to create user gallery");
        }
        finally
        {
            if (shouldDispose && conn != null)
                await conn.DisposeAsync();
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<UserGallery>> GetAllByUserIdAsync(Guid userId)
    {
        const string sql = @"
        SELECT
            ug.id                         AS user_gallery_id,
            ug.user_id,
            ug.is_unlocked,
            ug.unlocked_at,
            ug.best_time_ms,
            ug.best_time_at,
            ug.created_at                 AS user_gallery_created_at,
            ug.updated_at                 AS user_gallery_updated_at,
            g.id                          AS gallery_guid,
            g.title                       AS gallery_title,
            g.description                 AS gallery_description,
            g.image_count                 AS gallery_image_count,
            g.created_at                  AS gallery_created_at,
            g.gallery_id                  AS gallery_number,
            g.theme                       AS gallery_theme,
            g.type                        AS gallery_type,
            g.subtype                     AS gallery_subtype,
            g.level                       AS gallery_level
        FROM user_galleries ug
        INNER JOIN galleries g ON g.id = ug.gallery_id
        WHERE ug.user_id = @userId
        ORDER BY g.gallery_id;";

        _logger.LogDebug("GetAllByUserIdAsync: userId={UserId}", userId);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);

        var result = new List<UserGallery>();
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
            result.Add(MapUserGallery(reader));

        return result;
    }

    /// <inheritdoc/>
    public async Task<UserGallery?> GetByGalleryIdAsync(Guid galleryId)
    {
        const string sql = @"
        SELECT
            ug.id                         AS user_gallery_id,
            ug.user_id,
            ug.is_unlocked,
            ug.unlocked_at,
            ug.best_time_ms,
            ug.best_time_at,
            ug.created_at                 AS user_gallery_created_at,
            ug.updated_at                 AS user_gallery_updated_at,
            g.id                          AS gallery_guid,
            g.title                       AS gallery_title,
            g.description                 AS gallery_description,
            g.image_count                 AS gallery_image_count,
            g.created_at                  AS gallery_created_at,
            g.gallery_id                  AS gallery_number,
            g.theme                       AS gallery_theme,
            g.type                        AS gallery_type,
            g.subtype                     AS gallery_subtype,
            g.level                       AS gallery_level
        FROM user_galleries ug
        INNER JOIN galleries g ON g.id = ug.gallery_id
        WHERE g.id = @galleryId
        LIMIT 1;";

        _logger.LogDebug("GetByGalleryIdAsync: galleryId={GalleryId}", galleryId);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@galleryId", galleryId);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
            return MapUserGallery(reader);

        return null;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateBestTimeIfBetterAsync(Guid userId, Guid galleryId, long timeMs, DateTime achievedAt,
        NpgsqlConnection? connection = null, NpgsqlTransaction? transaction = null)
    {
        const string sql = @"
            UPDATE user_galleries
            SET best_time_ms = @timeMs,
                best_time_at = @achievedAt,
                is_unlocked = true,
                unlocked_at = COALESCE(unlocked_at, @achievedAt),
                updated_at = @achievedAt
            WHERE user_id = @userId AND gallery_id = @galleryId
              AND (best_time_ms IS NULL OR best_time_ms > @timeMs)";

        _logger.LogInformation(
            "UpdateBestTimeIfBetterAsync: userId={UserId}, galleryId={GalleryId}, timeMs={TimeMs}",
            userId, galleryId, timeMs);

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
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@galleryId", galleryId);
            cmd.Parameters.AddWithValue("@timeMs", timeMs);
            cmd.Parameters.AddWithValue("@achievedAt", achievedAt);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                _logger.LogDebug(
                    "UpdateBestTimeIfBetterAsync: no update for userId={UserId}, galleryId={GalleryId} — either not a new best, or no association exists",
                    userId, galleryId);
            }

            return rowsAffected > 0;
        }
        finally
        {
            if (shouldDispose && conn != null)
                await conn.DisposeAsync();
        }
    }

    private static UserGallery MapUserGallery(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(reader.GetOrdinal("user_gallery_id")),
        UserId = reader.GetGuid(reader.GetOrdinal("user_id")),
        GalleryId = reader.GetGuid(reader.GetOrdinal("gallery_guid")),
        IsUnlocked = reader.GetBoolean(reader.GetOrdinal("is_unlocked")),
        UnlockedAt = reader.IsDBNull(reader.GetOrdinal("unlocked_at"))
            ? null
            : reader.GetDateTime(reader.GetOrdinal("unlocked_at")),
        BestTimeMs = reader.IsDBNull(reader.GetOrdinal("best_time_ms"))
            ? null
            : reader.GetInt64(reader.GetOrdinal("best_time_ms")),
        BestTimeAt = reader.IsDBNull(reader.GetOrdinal("best_time_at"))
            ? null
            : reader.GetDateTime(reader.GetOrdinal("best_time_at")),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("user_gallery_created_at")),
        UpdatedAt = reader.GetDateTime(reader.GetOrdinal("user_gallery_updated_at")),
        Gallery = new Gallery
        {
            Id = reader.GetGuid(reader.GetOrdinal("gallery_guid")),
            Title = reader.GetString(reader.GetOrdinal("gallery_title")),
            Description = reader.IsDBNull(reader.GetOrdinal("gallery_description"))
                ? null
                : reader.GetString(reader.GetOrdinal("gallery_description")),
            ImageCount = reader.GetInt32(reader.GetOrdinal("gallery_image_count")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("gallery_created_at")),
            GalleryId = reader.GetInt32(reader.GetOrdinal("gallery_number")),
            Theme = new LookupValue { Name = reader.GetString(reader.GetOrdinal("gallery_theme")) },
            Type = new LookupValue { Name = reader.GetString(reader.GetOrdinal("gallery_type")) },
            Subtype = new LookupValue { Name = reader.GetString(reader.GetOrdinal("gallery_subtype")) },
            Level = reader.GetInt32(reader.GetOrdinal("gallery_level"))
        }
    };
}