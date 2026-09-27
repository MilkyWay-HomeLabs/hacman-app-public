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
public class UserGalleryRepositoryTest : RepositoryTestBase
{
    private UserGalleryRepository _repository;
    private string _connectionString;

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
    public async Task CreateAsync_CreatesUserGallerySuccessfully()
    {
        var testUserId = await CreateTestUser();
        var testGalleryId = await GetFirstGalleryId();
        var userGallery = new UserGallery
        {
            Id = Guid.NewGuid(),
            UserId = testUserId,
            GalleryId = testGalleryId,
            IsUnlocked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _repository.CreateAsync(userGallery);

        Assert.IsNotNull(result);
        Assert.AreEqual(userGallery.Id, result.Id);
        Assert.AreEqual(userGallery.UserId, result.UserId);
        Assert.AreEqual(userGallery.GalleryId, result.GalleryId);
        Assert.IsFalse(result.IsUnlocked);
        Assert.IsNull(result.UnlockedAt);
        Assert.IsNull(result.BestTimeMs);
        Assert.IsNull(result.BestTimeAt);
    }

    [TestMethod]
    public async Task CreateAsync_SetsTimestampsCorrectly()
    {
        var testUserId = await CreateTestUser();
        var testGalleryId = await GetFirstGalleryId();
        var beforeCreation = DateTime.UtcNow.AddSeconds(-1);
        var userGallery = new UserGallery
        {
            Id = Guid.NewGuid(),
            UserId = testUserId,
            GalleryId = testGalleryId,
            IsUnlocked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _repository.CreateAsync(userGallery);
        var afterCreation = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(result.CreatedAt >= beforeCreation);
        Assert.IsTrue(result.CreatedAt <= afterCreation);
        Assert.IsTrue(result.UpdatedAt >= beforeCreation);
        Assert.IsTrue(result.UpdatedAt <= afterCreation);
    }

    [TestMethod]
    public async Task CreateAsync_CreatesUnlockedGalleryWithOptionalFields()
    {
        var testUserId = await CreateTestUser();
        var testGalleryId = await GetFirstGalleryId();
        var now = DateTime.UtcNow;
        var userGallery = new UserGallery
        {
            Id = Guid.NewGuid(),
            UserId = testUserId,
            GalleryId = testGalleryId,
            IsUnlocked = true,
            UnlockedAt = now,
            BestTimeMs = 5000,
            BestTimeAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        var result = await _repository.CreateAsync(userGallery);

        Assert.IsTrue(result.IsUnlocked);
        Assert.IsNotNull(result.UnlockedAt);
        Assert.AreEqual(5000L, result.BestTimeMs);
        Assert.IsNotNull(result.BestTimeAt);
    }

    [TestMethod]
    public async Task GetByGalleryIdAsync_ReturnsAssociation_WhenExists()
    {
        var testUserId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await _repository.CreateAsync(new UserGallery
        {
            Id = Guid.NewGuid(),
            UserId = testUserId,
            GalleryId = galleryId,
            IsUnlocked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        // Note: the query behind this method is LIMIT 1 with no ORDER BY and no user filter,
        // so it may return any association for this gallery, not necessarily the one just
        // created above (every gallery already has at least one seeded association). What's
        // guaranteed by the contract — and what this test asserts — is that the returned
        // record actually belongs to the requested gallery and has its Gallery joined in.
        var result = await _repository.GetByGalleryIdAsync(galleryId);

        Assert.IsNotNull(result);
        Assert.AreEqual(galleryId, result.GalleryId);
        Assert.IsNotNull(result.Gallery);
        Assert.AreEqual(galleryId, result.Gallery.Id);
        Assert.IsFalse(string.IsNullOrEmpty(result.Gallery.Title));
    }

    [TestMethod]
    public async Task GetByGalleryIdAsync_ReturnsNull_WhenGalleryDoesNotExist()
    {
        var result = await _repository.GetByGalleryIdAsync(Guid.NewGuid());

        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task UpdateBestTimeIfBetterAsync_UpdatesRow_WhenNoExistingBestTime()
    {
        var testUserId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        await _repository.CreateAsync(new UserGallery
        {
            Id = Guid.NewGuid(),
            UserId = testUserId,
            GalleryId = galleryId,
            IsUnlocked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        var achievedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateBestTimeIfBetterAsync(testUserId, galleryId, 5000, achievedAt);

        Assert.IsTrue(updated);
        var associations = (await _repository.GetAllByUserIdAsync(testUserId)).ToList();
        var association = associations.Single(a => a.GalleryId == galleryId);
        Assert.AreEqual(5000L, association.BestTimeMs);
        Assert.IsTrue(association.IsUnlocked);
        Assert.IsNotNull(association.UnlockedAt);
    }

    [TestMethod]
    public async Task UpdateBestTimeIfBetterAsync_UpdatesRow_WhenNewTimeIsBetterThanExisting()
    {
        var testUserId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var now = DateTime.UtcNow;
        await _repository.CreateAsync(new UserGallery
        {
            Id = Guid.NewGuid(),
            UserId = testUserId,
            GalleryId = galleryId,
            IsUnlocked = true,
            UnlockedAt = now,
            BestTimeMs = 5000,
            BestTimeAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });

        var updated = await _repository.UpdateBestTimeIfBetterAsync(testUserId, galleryId, 3000, DateTime.UtcNow);

        Assert.IsTrue(updated);
        var associations = (await _repository.GetAllByUserIdAsync(testUserId)).ToList();
        var association = associations.Single(a => a.GalleryId == galleryId);
        Assert.AreEqual(3000L, association.BestTimeMs);
    }

    [TestMethod]
    public async Task UpdateBestTimeIfBetterAsync_DoesNotUpdateRow_WhenNewTimeIsWorseThanExisting()
    {
        var testUserId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();
        var now = DateTime.UtcNow;
        await _repository.CreateAsync(new UserGallery
        {
            Id = Guid.NewGuid(),
            UserId = testUserId,
            GalleryId = galleryId,
            IsUnlocked = true,
            UnlockedAt = now,
            BestTimeMs = 1000,
            BestTimeAt = now,
            CreatedAt = now,
            UpdatedAt = now
        });

        var updated = await _repository.UpdateBestTimeIfBetterAsync(testUserId, galleryId, 2000, DateTime.UtcNow);

        Assert.IsFalse(updated);
        var associations = (await _repository.GetAllByUserIdAsync(testUserId)).ToList();
        var association = associations.Single(a => a.GalleryId == galleryId);
        Assert.AreEqual(1000L, association.BestTimeMs);
    }

    [TestMethod]
    public async Task UpdateBestTimeIfBetterAsync_ReturnsFalse_WhenNoAssociationExists()
    {
        var testUserId = await CreateTestUser();
        var galleryId = await GetFirstGalleryId();

        var updated = await _repository.UpdateBestTimeIfBetterAsync(testUserId, galleryId, 1000, DateTime.UtcNow);

        Assert.IsFalse(updated);
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