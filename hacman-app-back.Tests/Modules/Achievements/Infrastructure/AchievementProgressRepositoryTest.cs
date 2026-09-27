#nullable enable
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using hacman_app_back.Modules.Achievements.Infrastructure;
using hacman_app_back.Tests.repository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace hacman_app_back.Tests.Modules.Achievements.Infrastructure;

/// <summary>
/// Runs against a real Postgres instance (see <see cref="RepositoryTestBase"/>) — requires
/// repo/docs/hacman/infra/001_achievements_schema.sql to already be applied to that database
/// (schema handoff only, never applied by this codebase's own tooling; see CLAUDE.md §0.8).
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class AchievementProgressRepositoryTest : RepositoryTestBase
{
    private AchievementProgressRepository _repository;
    private string _connectionString;

    [TestInitialize]
    public void Setup()
    {
        _connectionString = GetConnectionString();
        _repository =
            new AchievementProgressRepository(_connectionString, NullLogger<AchievementProgressRepository>.Instance);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var deleteProgressCmd = new NpgsqlCommand(
            "DELETE FROM achievement_progress WHERE user_id IN (SELECT id FROM users WHERE external_auth_id LIKE 'test_%')",
            conn);
        await deleteProgressCmd.ExecuteNonQueryAsync();

        await using var deleteDefsCmd =
            new NpgsqlCommand("DELETE FROM achievement_def WHERE code LIKE 'TEST-%'", conn);
        await deleteDefsCmd.ExecuteNonQueryAsync();

        await using var deleteUsersCmd =
            new NpgsqlCommand("DELETE FROM users WHERE external_auth_id LIKE 'test_%'", conn);
        await deleteUsersCmd.ExecuteNonQueryAsync();
    }

    [TestMethod]
    public async Task UpsertAsync_InsertsNewRow_WhenNoneExists()
    {
        var userId = await CreateTestUser();
        var achievementDefId = await InsertTestAchievementDef();

        await _repository.UpsertAsync(userId, achievementDefId, 5);

        var progress = (await _repository.GetByUserIdAsync(userId)).ToList();
        Assert.HasCount(1, progress);
        Assert.AreEqual(achievementDefId, progress[0].Achievement.Id);
        Assert.AreEqual(5L, progress[0].Value);
    }

    [TestMethod]
    public async Task UpsertAsync_UpdatesValue_WhenRowAlreadyExists()
    {
        var userId = await CreateTestUser();
        var achievementDefId = await InsertTestAchievementDef();

        await _repository.UpsertAsync(userId, achievementDefId, 3);
        await _repository.UpsertAsync(userId, achievementDefId, 8);

        var progress = (await _repository.GetByUserIdAsync(userId)).ToList();
        Assert.HasCount(1, progress, "Upsert on the same (user, achievement) pair must not create a second row.");
        Assert.AreEqual(8L, progress[0].Value);
    }

    [TestMethod]
    public async Task UpsertAsync_WorksWithinCallerManagedConnectionAndTransaction()
    {
        var userId = await CreateTestUser();
        var achievementDefId = await InsertTestAchievementDef();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var transaction = await conn.BeginTransactionAsync();

        await _repository.UpsertAsync(userId, achievementDefId, 42, conn, transaction);
        await transaction.CommitAsync();

        Assert.AreEqual(ConnectionState.Open, conn.State,
            "UpsertAsync must not dispose a connection it didn't open itself.");

        var progress = (await _repository.GetByUserIdAsync(userId)).ToList();
        Assert.HasCount(1, progress);
        Assert.AreEqual(42L, progress[0].Value);
    }

    [TestMethod]
    public async Task GetByUserIdAsync_ReturnsEmpty_WhenUserHasNoProgress()
    {
        var userId = await CreateTestUser();

        var progress = await _repository.GetByUserIdAsync(userId);

        Assert.IsEmpty(progress);
    }

    private async Task<Guid> InsertTestAchievementDef()
    {
        var id = Guid.NewGuid();
        var code = $"TEST-{Guid.NewGuid():N}"[..10];
        var metric = $"test_metric_{Guid.NewGuid():N}";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO achievement_def (id, code, category, name, description, icon_url, metric, thresholds, created_at)
            VALUES (@id, @code, 'COLLECTION'::achievement_category_enum, @name, @description, NULL, @metric, @thresholds, @createdAt)",
            conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@name", "Test Achievement");
        cmd.Parameters.AddWithValue("@description", "A test achievement.");
        cmd.Parameters.AddWithValue("@metric", metric);
        cmd.Parameters.AddWithValue("@thresholds", new[] { 1, 2, 3, 4, 5 });
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    private async Task<Guid> CreateTestUser()
    {
        var userId = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO users (id, external_auth_id, username, email, created_at, updated_at)
            VALUES (@id, @externalAuthId, @username, @email, @createdAt, @updatedAt)", conn);
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@externalAuthId", $"test_{Guid.NewGuid()}");
        cmd.Parameters.AddWithValue("@username", $"testuser_{Guid.NewGuid():N}");
        cmd.Parameters.AddWithValue("@email", $"test_{Guid.NewGuid():N}@example.com");
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync();
        return userId;
    }
}
