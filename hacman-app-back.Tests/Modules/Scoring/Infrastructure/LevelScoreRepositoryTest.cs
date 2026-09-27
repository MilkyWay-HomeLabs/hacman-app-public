using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using hacman_app_back.Modules.GalleryCatalog.Domain;
using hacman_app_back.Modules.Identity.Domain;
using hacman_app_back.Modules.LevelTemplate.Domain;
using hacman_app_back.Modules.Scoring.Domain;
using hacman_app_back.Modules.Scoring.Infrastructure;
using hacman_app_back.Tests.repository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace hacman_app_back.Tests.Modules.Scoring.Infrastructure;

[TestClass]
[TestCategory("Integration")]
public class LevelScoreRepositoryTest : RepositoryTestBase
{
    private LevelScoreRepository _repository;
    private string _connectionString;

    [TestInitialize]
    public void Setup()
    {
        _connectionString = GetConnectionString();
        _repository = new LevelScoreRepository(_connectionString, NullLogger<LevelScoreRepository>.Instance);
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
    public async Task GetTopTenScoresForGalleryAsync_ReturnsFiveOrderedWithPositions()
    {
        var galleryId = await GetFirstGalleryId();
        var insertedTimes = new List<long> { 5000L, 3000L, 7000L, 1000L, 2000L };
        var userIds = new List<Guid>();
        var testUsernames = new List<string>();

        foreach (var time in insertedTimes)
        {
            var userId = await CreateTestUser();
            userIds.Add(userId);
            testUsernames.Add(await GetUsername(userId));

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO user_galleries
                    (id, user_id, gallery_id, is_unlocked, unlocked_at, best_time_ms, best_time_at, created_at, updated_at)
                VALUES
                    (@id, @userId, @galleryId, @isUnlocked, @unlockedAt, @bestTimeMs, @bestTimeAt, @createdAt, @updatedAt)",
                conn);
            cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@galleryId", galleryId);
            cmd.Parameters.AddWithValue("@isUnlocked", true);
            cmd.Parameters.AddWithValue("@unlockedAt", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@bestTimeMs", time);
            cmd.Parameters.AddWithValue("@bestTimeAt", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
        }

        var scores = (await _repository.GetTopTenScoresForGalleryAsync(galleryId)).ToList();

        var testScores = scores.Where(s => testUsernames.Contains(s.User.Username)).OrderBy(s => s.TimeMs).ToList();
        Assert.HasCount(insertedTimes.Count, testScores,
            "Not all inserted test entries appeared in the top results.");

        var expectedOrdered = insertedTimes.OrderBy(t => t).ToList();
        for (var i = 0; i < expectedOrdered.Count; i++)
        {
            Assert.AreEqual(expectedOrdered[i], testScores[i].TimeMs);
            if (i > 0)
                Assert.IsGreaterThan(testScores[i - 1].Position, testScores[i].Position,
                    "Positions are not strictly increasing for test users.");
            Assert.AreEqual(galleryId, testScores[i].Gallery.Id);
        }
    }

    [TestMethod]
    public async Task GetTopTenScoresForGalleryAsync_IgnoresNullBestTimes()
    {
        var galleryId = await GetFirstGalleryId();
        var userWithValue = await CreateTestUser();
        var userNull1 = await CreateTestUser();
        var userNull2 = await CreateTestUser();
        var usernameWithValue = await GetUsername(userWithValue);

        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();

            foreach (var nullUserId in new[] { userNull1, userNull2 })
            {
                await using var cmd = new NpgsqlCommand(@"
                    INSERT INTO user_galleries
                        (id, user_id, gallery_id, is_unlocked, unlocked_at, best_time_ms, best_time_at, created_at, updated_at)
                    VALUES (@id, @userId, @galleryId, true, NULL, NULL, NULL, @ts, @ts)", conn);
                cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
                cmd.Parameters.AddWithValue("@userId", nullUserId);
                cmd.Parameters.AddWithValue("@galleryId", galleryId);
                cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow);
                await cmd.ExecuteNonQueryAsync();
            }

            await using var cmdWithValue = new NpgsqlCommand(@"
                INSERT INTO user_galleries
                    (id, user_id, gallery_id, is_unlocked, unlocked_at, best_time_ms, best_time_at, created_at, updated_at)
                VALUES (@id, @userId, @galleryId, true, @ts, 12345, @ts, @ts, @ts)", conn);
            cmdWithValue.Parameters.AddWithValue("@id", Guid.NewGuid());
            cmdWithValue.Parameters.AddWithValue("@userId", userWithValue);
            cmdWithValue.Parameters.AddWithValue("@galleryId", galleryId);
            cmdWithValue.Parameters.AddWithValue("@ts", DateTime.UtcNow);
            await cmdWithValue.ExecuteNonQueryAsync();
        }

        var scores = (await _repository.GetTopTenScoresForGalleryAsync(galleryId)).ToList();

        var myScore = scores.FirstOrDefault(s => s.User.Username == usernameWithValue);
        Assert.IsNotNull(myScore, "Inserted non-null best_time_ms record was not present in the returned results.");
        Assert.AreEqual(12345L, myScore!.TimeMs);
        Assert.IsGreaterThan(0, myScore.Position);
    }

    [TestMethod]
    public async Task GetScoresAsync_ReturnsScoresForGallery_OrderedByTimeAscending()
    {
        var galleryId = await GetFirstGalleryId();
        var insertedTimes = new List<long> { 4000L, 1000L, 3000L, 2000L };
        var testUsernames = new List<string>();

        var position = 1;
        foreach (var time in insertedTimes)
        {
            var userId = await CreateTestUser();
            testUsernames.Add(await GetUsername(userId));
            await InsertLevelScore(userId, galleryId, time, "MEDIUM", position++);
        }

        var scores = (await _repository.GetScoresAsync(galleryId, null, 1, 100))
            .Where(s => testUsernames.Contains(s.User.Username))
            .ToList();

        Assert.HasCount(insertedTimes.Count, scores,
            "Not all inserted test scores appeared in the results.");

        var expectedOrdered = insertedTimes.OrderBy(t => t).ToList();
        for (var i = 0; i < expectedOrdered.Count; i++)
        {
            Assert.AreEqual(expectedOrdered[i], scores[i].TimeMs);
            Assert.AreEqual(galleryId, scores[i].Gallery.Id);
        }
    }

    [TestMethod]
    public async Task GetScoresAsync_FiltersByDifficulty_WhenProvided()
    {
        var galleryId = await GetFirstGalleryId();
        var easyUserId = await CreateTestUser();
        var easyUsername = await GetUsername(easyUserId);
        var hardUserId = await CreateTestUser();
        var hardUsername = await GetUsername(hardUserId);

        await InsertLevelScore(easyUserId, galleryId, 1000, "EASY", 1);
        await InsertLevelScore(hardUserId, galleryId, 2000, "HARD", 1);

        var scores = (await _repository.GetScoresAsync(galleryId, "EASY", 1, 100)).ToList();

        Assert.IsTrue(scores.Any(s => s.User.Username == easyUsername),
            "Expected EASY-difficulty score for the test user was missing.");
        Assert.IsFalse(scores.Any(s => s.User.Username == hardUsername),
            "HARD-difficulty score leaked through the EASY filter.");
    }

    [TestMethod]
    public async Task GetScoresByUserIdAsync_ReturnsScoresForUser()
    {
        var galleryId = await GetFirstGalleryId();
        var userId = await CreateTestUser();
        await InsertLevelScore(userId, galleryId, 1500, "MEDIUM", 1);

        var scores = (await _repository.GetScoresByUserIdAsync(userId)).ToList();

        Assert.HasCount(1, scores);
        Assert.AreEqual(1500L, scores[0].TimeMs);
        Assert.AreEqual(galleryId, scores[0].Gallery.Id);
    }

    [TestMethod]
    public async Task GetScoresByUserIdAsync_FiltersByGalleryId_WhenProvided()
    {
        var galleryId = await GetFirstGalleryId();
        var otherGalleryId = await GetGalleryIdAt(1);
        var userId = await CreateTestUser();
        await InsertLevelScore(userId, galleryId, 1000, "MEDIUM", 1);
        await InsertLevelScore(userId, otherGalleryId, 2000, "MEDIUM", 1);

        var scores = (await _repository.GetScoresByUserIdAsync(userId, galleryId)).ToList();

        Assert.HasCount(1, scores);
        Assert.AreEqual(galleryId, scores[0].Gallery.Id);
    }

    [TestMethod]
    public async Task CreateAsync_PersistsLevelScoreAndReturnsItWithGeneratedFields()
    {
        var galleryId = await GetFirstGalleryId();
        var userId = await CreateTestUser();
        var achievedAt = DateTime.UtcNow;
        var levelScore = new LevelScore
        {
            Id = Guid.NewGuid(),
            Gallery = new Gallery { Id = galleryId },
            User = new User { Id = userId },
            Difficulty = Difficulty.HARD,
            TimeMs = 6500,
            TimeAt = achievedAt
        };

        var result = await _repository.CreateAsync(levelScore);

        Assert.AreEqual(levelScore.Id, result.Id);
        Assert.AreEqual(6500L, result.TimeMs);
        Assert.IsNotNull(result.TimeAt);

        var persisted = (await _repository.GetScoresByUserIdAsync(userId)).ToList();
        Assert.HasCount(1, persisted);
        Assert.AreEqual(galleryId, persisted[0].Gallery.Id);
        Assert.AreEqual(6500L, persisted[0].TimeMs);
        Assert.AreEqual(Difficulty.HARD, persisted[0].Difficulty);
    }

    [TestMethod]
    public async Task CreateAsync_WorksWithinCallerManagedConnectionAndTransaction()
    {
        var galleryId = await GetFirstGalleryId();
        var userId = await CreateTestUser();
        var levelScore = new LevelScore
        {
            Id = Guid.NewGuid(),
            Gallery = new Gallery { Id = galleryId },
            User = new User { Id = userId },
            Difficulty = Difficulty.EASY,
            TimeMs = 1234,
            TimeAt = DateTime.UtcNow
        };

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var transaction = await conn.BeginTransactionAsync();

        var result = await _repository.CreateAsync(levelScore, conn, transaction);
        await transaction.CommitAsync();

        Assert.AreEqual(ConnectionState.Open, conn.State,
            "CreateAsync must not dispose a connection it didn't open itself.");
        Assert.AreEqual(1234L, result.TimeMs);

        var persisted = (await _repository.GetScoresByUserIdAsync(userId)).ToList();
        Assert.HasCount(1, persisted);
        Assert.AreEqual(1234L, persisted[0].TimeMs);
    }

    [TestMethod]
    public async Task CreateAsync_PersistsGameplayTelemetryCounters()
    {
        var galleryId = await GetFirstGalleryId();
        var userId = await CreateTestUser();
        var levelScore = new LevelScore
        {
            Id = Guid.NewGuid(),
            Gallery = new Gallery { Id = galleryId },
            User = new User { Id = userId },
            Difficulty = Difficulty.HARD,
            TimeMs = 5000,
            TimeAt = DateTime.UtcNow,
            EnemiesDefeated = 4,
            BuffsCollected = 2,
            HitsTaken = 1
        };

        await _repository.CreateAsync(levelScore);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT enemies_defeated, buffs_collected, hits_taken FROM level_scores WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", levelScore.Id);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync());
        Assert.AreEqual(4, reader.GetInt32(0));
        Assert.AreEqual(2, reader.GetInt32(1));
        Assert.AreEqual(1, reader.GetInt32(2));
    }

    [TestMethod]
    public async Task CreateAsync_DefaultsGameplayTelemetryCountersToZero_WhenOmitted()
    {
        var galleryId = await GetFirstGalleryId();
        var userId = await CreateTestUser();
        var levelScore = new LevelScore
        {
            Id = Guid.NewGuid(),
            Gallery = new Gallery { Id = galleryId },
            User = new User { Id = userId },
            Difficulty = Difficulty.EASY,
            TimeMs = 3000,
            TimeAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(levelScore);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT enemies_defeated, buffs_collected, hits_taken FROM level_scores WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", levelScore.Id);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.IsTrue(await reader.ReadAsync());
        Assert.AreEqual(0, reader.GetInt32(0));
        Assert.AreEqual(0, reader.GetInt32(1));
        Assert.AreEqual(0, reader.GetInt32(2));
    }

    private async Task InsertLevelScore(Guid userId, Guid galleryId, long? timeMs, string difficulty, int position)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO level_scores (id, gallery_id, user_id, difficulty, position, time_ms, time_at, created_at)
            VALUES (@id, @galleryId, @userId, @difficulty::difficulty, @position, @timeMs, @timeAt, @createdAt)",
            conn);
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@galleryId", galleryId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@difficulty", difficulty);
        cmd.Parameters.AddWithValue("@position", position);
        cmd.Parameters.AddWithValue("@timeMs", (object?)timeMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@timeAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<Guid> GetGalleryIdAt(int offset)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd =
            new NpgsqlCommand("SELECT id FROM galleries ORDER BY gallery_id LIMIT 1 OFFSET @offset", conn);
        cmd.Parameters.AddWithValue("@offset", offset);
        var result = await cmd.ExecuteScalarAsync();
        return (Guid)result!;
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

    private async Task<string> GetUsername(Guid userId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT username FROM users WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("@id", userId);
        var result = await cmd.ExecuteScalarAsync();
        return (string)result!;
    }
}