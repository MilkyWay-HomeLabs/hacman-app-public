using System;
using System.Linq;
using System.Threading.Tasks;
using hacman_app_back.Modules.PlayerProgress.Domain;
using hacman_app_back.Modules.PlayerProgress.Infrastructure;
using hacman_app_back.Tests.repository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace hacman_app_back.Tests.Modules.PlayerProgress.Infrastructure;

[TestClass]
[TestCategory("Integration")]
public class UserGalleryRepositoryGetAllTest : RepositoryTestBase
{
    private string _connectionString;
    private UserGalleryRepository _repository;

    [TestInitialize]
    public void Setup()
    {
        _connectionString = GetConnectionString();
        _repository = new UserGalleryRepository(_connectionString, NullLogger<UserGalleryRepository>.Instance);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var deleteUserGalleriesCmd = new NpgsqlCommand(
            "DELETE FROM user_galleries WHERE user_id IN (SELECT id FROM users WHERE external_auth_id LIKE 'test_%')",
            conn);
        await deleteUserGalleriesCmd.ExecuteNonQueryAsync();

        await using var deleteUsersCmd = new NpgsqlCommand(
            "DELETE FROM users WHERE external_auth_id LIKE 'test_%'",
            conn);
        await deleteUsersCmd.ExecuteNonQueryAsync();
    }

    [TestMethod]
    public async Task GetAllByUserIdAsync_ReturnsCreatedAssociation()
    {
        var testUserId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var created = await _repository.CreateAsync(new UserGallery
        {
            Id = Guid.NewGuid(),
            UserId = testUserId,
            GalleryId = galleryId,
            IsUnlocked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var values = (await _repository.GetAllByUserIdAsync(testUserId)).ToList();

        Assert.HasCount(1, values);
        var sample = values[0];
        Assert.AreEqual(created.Id, sample.Id);
        Assert.AreEqual(testUserId, sample.UserId);
        if (sample.Gallery != null) Assert.AreEqual(galleryId, sample.Gallery.Id);
        Assert.IsFalse(sample.IsUnlocked);
        Assert.IsNull(sample.UnlockedAt);
        Assert.IsNull(sample.BestTimeMs);
        Assert.IsNull(sample.BestTimeAt);
    }

    [TestMethod]
    public async Task GetAllByUserIdAsync_ReturnsEmpty_WhenUserHasNoAssociations()
    {
        var testUserId = await CreateTestUser();

        var values = (await _repository.GetAllByUserIdAsync(testUserId)).ToList();

        Assert.IsEmpty(values);
    }

    [TestMethod]
    public async Task GetAllByUserIdAsync_ThrowsWithInvalidConnectionString()
    {
        var repo = new UserGalleryRepository("Host=invalid;Database=invalid;Username=invalid;Password=invalid",
            NullLogger<UserGalleryRepository>.Instance);
        try
        {
            await repo.GetAllByUserIdAsync(Guid.NewGuid());
            Assert.Fail("Expected NpgsqlException was not thrown.");
        }
        catch (NpgsqlException)
        {
            // expected
        }
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

    private async Task<Guid> GetFirstGalleryId()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT id FROM galleries LIMIT 1", conn);
        var result = await cmd.ExecuteScalarAsync();
        return (Guid)result!;
    }
}