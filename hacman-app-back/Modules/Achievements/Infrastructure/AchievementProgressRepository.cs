using hacman_app_back.Modules.Achievements.Domain;
using hacman_app_back.Modules.Identity.Domain;
using Npgsql;

namespace hacman_app_back.Modules.Achievements.Infrastructure;

/// <summary>
/// ADO.NET repository for per-user achievement progress counters.
/// </summary>
public class AchievementProgressRepository : IAchievementProgressRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AchievementProgressRepository> _logger;

    public AchievementProgressRepository(string connectionString, ILogger<AchievementProgressRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<AchievementProgress>> GetByUserIdAsync(Guid userId)
    {
        const string sql = @"
            SELECT achievement_id, value, updated_at
            FROM achievement_progress
            WHERE user_id = @userId;";

        _logger.LogInformation("GetByUserIdAsync: userId={UserId}", userId);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);

        var result = new List<AchievementProgress>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new AchievementProgress
            {
                User = new User { Id = userId },
                Achievement = new AchievementDef { Id = reader.GetGuid(reader.GetOrdinal("achievement_id")) },
                Value = reader.GetInt64(reader.GetOrdinal("value")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updated_at"))
            });
        }

        _logger.LogInformation("GetByUserIdAsync: userId={UserId}, returned {Count} rows", userId, result.Count);
        return result;
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(Guid userId, Guid achievementDefId, long value,
        NpgsqlConnection? connection = null, NpgsqlTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO achievement_progress (id, user_id, achievement_id, value, updated_at)
            VALUES (@id, @userId, @achievementId, @value, @updatedAt)
            ON CONFLICT (user_id, achievement_id)
            DO UPDATE SET value = EXCLUDED.value, updated_at = EXCLUDED.updated_at;";

        _logger.LogInformation(
            "UpsertAsync: userId={UserId}, achievementId={AchievementId}, value={Value}",
            userId, achievementDefId, value);

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
            cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@achievementId", achievementDefId);
            cmd.Parameters.AddWithValue("@value", value);
            cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldDispose)
                await conn.DisposeAsync();
        }
    }
}
