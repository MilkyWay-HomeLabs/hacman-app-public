using hacman_app_back.Modules.Achievements.Domain;
using Npgsql;

namespace hacman_app_back.Modules.Achievements.Infrastructure;

/// <summary>
/// ADO.NET repository for the achievement catalogue. <c>category</c> is stored as a
/// Postgres enum (<c>achievement_category_enum</c>); like the existing <c>difficulty</c>
/// column elsewhere in this codebase (see <c>Modules/Scoring/Infrastructure/LevelScoreRepository</c>),
/// it is read/written as plain text rather than via a mapped Npgsql enum type, matching
/// this project's connection-string-only setup (no <c>NpgsqlDataSourceBuilder</c>).
/// </summary>
public class AchievementDefRepository : IAchievementDefRepository
{
    private readonly string _connectionString;
    private readonly ILogger<AchievementDefRepository> _logger;

    public AchievementDefRepository(string connectionString, ILogger<AchievementDefRepository> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<AchievementDef>> GetAllAsync()
    {
        const string sql = @"
            SELECT id, code, category, name, description, icon_url, metric, thresholds, created_at
            FROM achievement_def
            ORDER BY category, code;";

        _logger.LogInformation("GetAllAsync: fetching the achievement catalogue");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);

        var result = new List<AchievementDef>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(MapAchievementDef(reader));

        _logger.LogInformation("GetAllAsync: returned {Count} achievement definitions", result.Count);
        return result;
    }

    private static AchievementDef MapAchievementDef(NpgsqlDataReader reader)
    {
        return new AchievementDef
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            Code = reader.GetString(reader.GetOrdinal("code")),
            Category = Enum.Parse<AchievementCategory>(reader.GetString(reader.GetOrdinal("category"))),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Description = reader.GetString(reader.GetOrdinal("description")),
            IconUrl = reader.IsDBNull(reader.GetOrdinal("icon_url"))
                ? null
                : reader.GetString(reader.GetOrdinal("icon_url")),
            Metric = reader.GetString(reader.GetOrdinal("metric")),
            Thresholds = reader.GetFieldValue<int[]>(reader.GetOrdinal("thresholds")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
        };
    }
}
