using hacman_app_back.Modules.GalleryCatalog.Domain;
using hacman_app_back.Modules.Identity.Domain;
using hacman_app_back.Modules.LevelTemplate.Domain;
using hacman_app_back.Modules.Scoring.Domain;
using Npgsql;

namespace hacman_app_back.Modules.Scoring.Infrastructure;

/// <summary>
/// ADO.NET repository for level score records.
/// </summary>
public class LevelScoreRepository : ILevelScoreRepository
{
    private readonly string _connectionString;
    private readonly ILogger<LevelScoreRepository> _logger;

    public LevelScoreRepository(string connectionString, ILogger<LevelScoreRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<LevelScore>> GetScoresAsync(Guid galleryId, string? difficulty, int pageNumber,
        int pageSize)
    {
        var offset = (pageNumber - 1) * pageSize;

        _logger.LogInformation(
            "GetScoresAsync: galleryId={GalleryId}, difficulty={Difficulty}, page={Page}/{Size}, offset={Offset}",
            galleryId, difficulty ?? "any", pageNumber, pageSize, offset);

        var sql = @"
            SELECT g.id as gallery_id, g.gallery_id as gallery_number, g.title, g.level,
                   u.id as user_id, u.username, ls.time_ms as best_time_ms, ls.difficulty, ls.time_at,
                   ROW_NUMBER() OVER (PARTITION BY g.id ORDER BY ls.time_ms ASC NULLS LAST) AS position
            FROM level_scores ls
            INNER JOIN galleries g ON g.id = ls.gallery_id
            INNER JOIN users u ON u.id = ls.user_id
            WHERE g.id = @galleryId";

        if (!string.IsNullOrEmpty(difficulty))
            sql += " AND ls.difficulty = @difficulty::difficulty";

        sql += @"
            ORDER BY ls.time_ms ASC
            LIMIT @pageSize OFFSET @offset;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@galleryId", galleryId);
        cmd.Parameters.AddWithValue("@pageSize", pageSize);
        cmd.Parameters.AddWithValue("@offset", offset);
        if (!string.IsNullOrEmpty(difficulty))
            cmd.Parameters.AddWithValue("@difficulty", difficulty);

        var result = new List<LevelScore>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(MapLevelScore(reader));

        _logger.LogInformation("GetScoresAsync: returned {Count} records for galleryId={GalleryId}", result.Count,
            galleryId);
        return result;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<LevelScore>> GetScoresByUserIdAsync(Guid userId, Guid? galleryId = null,
        string? difficulty = null)
    {
        _logger.LogInformation(
            "GetScoresByUserIdAsync: userId={UserId}, galleryId={GalleryId}, difficulty={Difficulty}",
            userId, galleryId?.ToString() ?? "any", difficulty ?? "any");

        var sql = @"
            SELECT g.id as gallery_id, g.gallery_id as gallery_number, g.title, g.level,
                   u.id as user_id, u.username, ls.time_ms as best_time_ms, ls.difficulty, ls.time_at,
                   1 as position
            FROM level_scores ls
            INNER JOIN galleries g ON g.id = ls.gallery_id
            INNER JOIN users u ON u.id = ls.user_id
            WHERE u.id = @userId";

        if (galleryId.HasValue)
            sql += " AND g.id = @galleryId";

        if (!string.IsNullOrEmpty(difficulty))
            sql += " AND ls.difficulty = @difficulty::difficulty";

        sql += " ORDER BY ls.time_ms ASC;";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);

        if (galleryId.HasValue)
            cmd.Parameters.AddWithValue("@galleryId", galleryId.Value);

        if (!string.IsNullOrEmpty(difficulty))
            cmd.Parameters.AddWithValue("@difficulty", difficulty);

        var result = new List<LevelScore>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(MapLevelScore(reader));

        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Reads from user_galleries.best_time_ms (the cached best time per user/gallery), not from
    /// level_scores — this mirrors the original query relocated here from IUserGalleryRepository
    /// as part of the DDD move to keep the Scoring context self-contained (see CLAUDE.md roadmap step 5).
    /// </remarks>
    public async Task<IEnumerable<LevelScore>> GetTopTenScoresForGalleryAsync(Guid galleryId)
    {
        const string sql = @"
            SELECT g.id as gallery_id, g.gallery_id as gallery_number, g.title, g.level,
                   u.id as user_id, u.username, ug.best_time_ms,
                   ROW_NUMBER() OVER (ORDER BY ug.best_time_ms NULLS LAST) AS position
            FROM user_galleries ug
            INNER JOIN galleries g ON g.id = ug.gallery_id
            INNER JOIN users u ON u.id = ug.user_id
            WHERE g.id = @galleryId
            AND ug.best_time_ms IS NOT NULL
            ORDER BY ug.best_time_ms
            LIMIT 10;";

        _logger.LogDebug("GetTopTenScoresForGalleryAsync: galleryId={GalleryId}", galleryId);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@galleryId", galleryId);

        var result = new List<LevelScore>();
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new LevelScore
            {
                Gallery = new Gallery
                {
                    Id = reader.GetGuid(reader.GetOrdinal("gallery_id")),
                    GalleryId = reader.GetInt32(reader.GetOrdinal("gallery_number")),
                    Title = reader.GetString(reader.GetOrdinal("title")),
                    Level = reader.GetInt32(reader.GetOrdinal("level"))
                },
                User = new User
                {
                    Id = reader.GetGuid(reader.GetOrdinal("user_id")),
                    Username = reader.GetString(reader.GetOrdinal("username"))
                },
                Position = reader.GetInt32(reader.GetOrdinal("position")),
                TimeMs = reader.IsDBNull(reader.GetOrdinal("best_time_ms"))
                    ? null
                    : reader.GetInt64(reader.GetOrdinal("best_time_ms"))
            });
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<LevelScore> CreateAsync(LevelScore levelScore,
        NpgsqlConnection? connection = null, NpgsqlTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO level_scores (id, gallery_id, user_id, difficulty, position, time_ms, time_at, created_at)
            VALUES (@id, @galleryId, @userId, @difficulty::difficulty, @position, @timeMs, @timeAt, @createdAt)
            RETURNING id, time_ms, time_at";

        _logger.LogInformation("CreateAsync: userId={UserId}, galleryId={GalleryId}, difficulty={Difficulty}",
            levelScore.User.Id, levelScore.Gallery.Id, levelScore.Difficulty);

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
            cmd.Parameters.AddWithValue("@id", levelScore.Id);
            cmd.Parameters.AddWithValue("@galleryId", levelScore.Gallery.Id);
            cmd.Parameters.AddWithValue("@userId", levelScore.User.Id);
            cmd.Parameters.AddWithValue("@difficulty", levelScore.Difficulty.ToString());
            // Position is a point-in-time leaderboard rank; it's recomputed on every read via
            // ROW_NUMBER() (see GetScoresAsync/GetTopTenScoresForGalleryAsync above), and this
            // stored column is kept in sync separately (scheduled DB job) — 0 here is an
            // intentional placeholder, not a bug.
            cmd.Parameters.AddWithValue("@position", 0);
            cmd.Parameters.AddWithValue("@timeMs", (object?)levelScore.TimeMs ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@timeAt", (object?)levelScore.TimeAt ?? DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                levelScore.TimeMs = reader.IsDBNull(1) ? null : reader.GetInt64(1);
                levelScore.TimeAt = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
                return levelScore;
            }

            throw new InvalidOperationException("Failed to create level score");
        }
        finally
        {
            if (shouldDispose && conn != null)
                await conn.DisposeAsync();
        }
    }

    private static LevelScore MapLevelScore(NpgsqlDataReader reader)
    {
        var gallery = new Gallery
        {
            Id = reader.GetGuid(reader.GetOrdinal("gallery_id")),
            GalleryId = reader.GetInt32(reader.GetOrdinal("gallery_number")),
            Title = reader.GetString(reader.GetOrdinal("title")),
            Level = reader.GetInt32(reader.GetOrdinal("level"))
        };

        var user = new User
        {
            Id = reader.GetGuid(reader.GetOrdinal("user_id")),
            Username = reader.IsDBNull(reader.GetOrdinal("username"))
                ? null
                : reader.GetString(reader.GetOrdinal("username"))
        };

        return new LevelScore
        {
            Gallery = gallery,
            User = user,
            Position = reader.GetInt32(reader.GetOrdinal("position")),
            TimeMs = reader.IsDBNull(reader.GetOrdinal("best_time_ms"))
                ? null
                : reader.GetInt64(reader.GetOrdinal("best_time_ms")),
            Difficulty = reader.IsDBNull(reader.GetOrdinal("difficulty"))
                ? Difficulty.MEDIUM
                : Enum.Parse<Difficulty>(reader.GetString(reader.GetOrdinal("difficulty"))),
            TimeAt = reader.IsDBNull(reader.GetOrdinal("time_at"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("time_at"))
        };
    }
}