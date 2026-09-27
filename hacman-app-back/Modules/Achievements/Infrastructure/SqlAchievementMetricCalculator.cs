using Npgsql;

namespace hacman_app_back.Modules.Achievements.Infrastructure;

/// <summary>
/// A metric calculator backed by a single scalar SQL query taking exactly one parameter,
/// <c>@userId</c>. Every content-coverage (<c>_pct</c>-suffixed) query in
/// <see cref="AchievementMetricSqlRegistry"/> computes its denominator live, with a
/// <c>COUNT(DISTINCT ...)</c> against this environment's own tables, inside that same SQL
/// text — never a constant baked into this class (CLAUDE.md §3).
/// </summary>
public sealed class SqlAchievementMetricCalculator : IAchievementMetricCalculator
{
    private readonly string _connectionString;
    private readonly string _sql;

    public string Metric { get; }

    public SqlAchievementMetricCalculator(string connectionString, string metric, string sql)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        Metric = metric ?? throw new ArgumentNullException(nameof(metric));
        _sql = sql ?? throw new ArgumentNullException(nameof(sql));
    }

    /// <inheritdoc/>
    public async Task<long> CalculateAsync(Guid userId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(_sql, conn);
        cmd.Parameters.AddWithValue("@userId", userId);

        var result = await cmd.ExecuteScalarAsync();
        return result is null or DBNull ? 0L : Convert.ToInt64(result);
    }
}
