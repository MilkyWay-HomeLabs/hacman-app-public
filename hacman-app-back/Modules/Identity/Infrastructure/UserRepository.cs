using hacman_app_back.Modules.Identity.Domain;
using Npgsql;

namespace hacman_app_back.Modules.Identity.Infrastructure;

/// <summary>
/// ADO.NET repository for user records.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly string _connectionString;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(string connectionString, ILogger<UserRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<User?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT id, users.external_auth_id, username, email, created_at, updated_at
            FROM users
            WHERE id = @id";

        _logger.LogDebug("GetByIdAsync: id={Id}", id);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
            return MapUser(reader);

        return null;
    }

    /// <inheritdoc/>
    public async Task<User?> GetByExternalAuthIdAsync(string externalAuthId)
    {
        const string sql = @"
            SELECT id, users.external_auth_id, username, email, created_at, updated_at
            FROM users
            WHERE external_auth_id = @externalAuthId";

        _logger.LogDebug("GetByExternalAuthIdAsync: externalAuthId={ExternalAuthId}", externalAuthId);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@externalAuthId", externalAuthId);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
            return MapUser(reader);

        return null;
    }

    /// <inheritdoc/>
    public async Task<User> CreateAsync(User user, NpgsqlConnection? connection = null,
        NpgsqlTransaction? transaction = null)
    {
        const string sql = @"
        INSERT INTO users (id, external_auth_id, username, email, created_at, updated_at)
        VALUES (@id, @externalAuthId, @username, @email, @createdAt, @updatedAt)
        RETURNING id, external_auth_id, username, email, created_at, updated_at";

        _logger.LogInformation("CreateAsync: externalAuthId={ExternalAuthId}", user.ExternalAuthId);

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
            cmd.Parameters.AddWithValue("@id", user.Id);
            cmd.Parameters.AddWithValue("@externalAuthId", (object?)user.ExternalAuthId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@username", (object?)user.Username ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@email", (object?)user.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@createdAt", user.CreatedAt);
            cmd.Parameters.AddWithValue("@updatedAt", user.UpdatedAt);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
                return MapUser(reader);

            throw new InvalidOperationException("Failed to create user");
        }
        finally
        {
            if (shouldDispose && conn != null)
                await conn.DisposeAsync();
        }
    }

    private static User MapUser(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        ExternalAuthId = reader.IsDBNull(1) ? null : reader.GetString(1),
        Username = reader.IsDBNull(2) ? null : reader.GetString(2),
        Email = reader.IsDBNull(3) ? null : reader.GetString(3),
        CreatedAt = reader.GetDateTime(4),
        UpdatedAt = reader.GetDateTime(5)
    };
}