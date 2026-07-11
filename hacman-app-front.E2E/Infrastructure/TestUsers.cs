using Npgsql;

namespace hacman_app_front.E2E.Infrastructure;

/// <summary>
/// Shared helpers for authenticated E2E tests that bootstrap a throwaway Hacman user against the
/// milkyway.test deployment (see <see cref="AuthenticatedStackFixture"/>). The <c>test_e2e_</c>
/// prefix matches the cleanup convention used across the backend integration tests (CLAUDE.md's
/// Integration Test Coverage section), so a row left behind by a crashed run is easy to find and
/// purge by hand.
/// </summary>
public static class TestUsers
{
    public static string NewExternalAuthId() => $"test_e2e_{Guid.NewGuid():N}";

    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("TEST_HACMAN_CONNECTIONSTRING")
        ?? throw new InvalidOperationException(
            "TEST_HACMAN_CONNECTIONSTRING is not set. Point it at the E2E deployment's Postgres "
            + "(the shared milky-test-postgres instance) before running the authenticated E2E tests, e.g. "
            + "export TEST_HACMAN_CONNECTIONSTRING=\"Host=localhost;Port=5432;Database=test_hacman;Username=hacman;Password=<your-test-db-password>\"");

    /// <summary>
    /// Deletes the user bootstrapped for <paramref name="externalAuthId"/> from the deployment's
    /// database (the shared <c>milky-test-postgres</c> instance). ON DELETE CASCADE on
    /// <c>user_galleries.user_id</c> removes the seeded gallery rows too.
    /// </summary>
    public static async Task CleanupAsync(string externalAuthId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM users WHERE external_auth_id = @externalAuthId";
        command.Parameters.AddWithValue("externalAuthId", externalAuthId);
        await command.ExecuteNonQueryAsync();
    }
}
