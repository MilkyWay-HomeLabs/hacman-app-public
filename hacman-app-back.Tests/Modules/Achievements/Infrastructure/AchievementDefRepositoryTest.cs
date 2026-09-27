#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using hacman_app_back.Modules.Achievements.Domain;
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
/// Test rows use a 'TEST-' code prefix so they never collide with the real catalogue and are
/// removed in cleanup, mirroring the 'test_%' external_auth_id convention used elsewhere.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class AchievementDefRepositoryTest : RepositoryTestBase
{
    private AchievementDefRepository _repository;
    private string _connectionString;

    [TestInitialize]
    public void Setup()
    {
        _connectionString = GetConnectionString();
        _repository = new AchievementDefRepository(_connectionString, NullLogger<AchievementDefRepository>.Instance);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM achievement_def WHERE code LIKE 'TEST-%'", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    [TestMethod]
    public async Task GetAllAsync_ReturnsInsertedDefinition_WithAllFieldsMapped()
    {
        var code = $"TEST-{Guid.NewGuid():N}"[..10];
        var metric = $"test_metric_{Guid.NewGuid():N}";
        var id = await InsertAchievementDef(code, "COLLECTION", "Test Achievement",
            "A test achievement.", null, metric, new[] { 1, 2, 3, 4, 5 });

        var defs = (await _repository.GetAllAsync()).ToList();
        var found = defs.SingleOrDefault(d => d.Id == id);

        Assert.IsNotNull(found, "Inserted achievement_def row was not returned by GetAllAsync.");
        Assert.AreEqual(code, found.Code);
        Assert.AreEqual(AchievementCategory.COLLECTION, found.Category);
        Assert.AreEqual("Test Achievement", found.Name);
        Assert.AreEqual("A test achievement.", found.Description);
        Assert.IsNull(found.IconUrl);
        Assert.AreEqual(metric, found.Metric);
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, found.Thresholds);
    }

    [TestMethod]
    public async Task GetAllAsync_MapsNonNullIconUrl()
    {
        var code = $"TEST-{Guid.NewGuid():N}"[..10];
        var metric = $"test_metric_{Guid.NewGuid():N}";
        var id = await InsertAchievementDef(code, "SPEED", "Test Icon Achievement",
            "Has an icon.", "https://milkyway.test/resources/hacman/icons/test.png", metric,
            new[] { 5, 10, 20, 40, 80 });

        var defs = (await _repository.GetAllAsync()).ToList();
        var found = defs.SingleOrDefault(d => d.Id == id);

        Assert.IsNotNull(found);
        Assert.AreEqual("https://milkyway.test/resources/hacman/icons/test.png", found.IconUrl);
    }

    private async Task<Guid> InsertAchievementDef(string code, string category, string name, string description,
        string? iconUrl, string metric, int[] thresholds)
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO achievement_def (id, code, category, name, description, icon_url, metric, thresholds, created_at)
            VALUES (@id, @code, @category::achievement_category_enum, @name, @description, @iconUrl, @metric, @thresholds, @createdAt)",
            conn);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.Parameters.AddWithValue("@category", category);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@description", description);
        cmd.Parameters.AddWithValue("@iconUrl", (object?)iconUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@metric", metric);
        cmd.Parameters.AddWithValue("@thresholds", thresholds);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync();
        return id;
    }
}
