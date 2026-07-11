using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using hacman_app_back.Modules.LevelTemplate.Infrastructure;
using hacman_app_back.Tests.repository;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Npgsql;

namespace hacman_app_back.Tests.Modules.LevelTemplate.Infrastructure;

[TestClass]
[TestCategory("Integration")]
public class MazeLevelRepositoryTest : RepositoryTestBase
{
    private string _connectionString;
    private MazeLevelRepository _repository;

    [TestInitialize]
    public void Setup()
    {
        _connectionString = GetConnectionString();
        _repository = new MazeLevelRepository(_connectionString, NullLogger<MazeLevelRepository>.Instance);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Delete maze_level fixtures first (covers any inserted against a real gallery historically).
        await using (var mazeCmd = new NpgsqlCommand(
                         "DELETE FROM maze_levels WHERE notes = 'integration-test-fixture'", conn))
        {
            await mazeCmd.ExecuteNonQueryAsync();
        }

        // Delete throwaway test galleries (ON DELETE CASCADE removes their maze_levels too).
        await using (var galleryCmd = new NpgsqlCommand(
                         "DELETE FROM galleries WHERE title = 'integration-test-fixture-gallery'", conn))
        {
            await galleryCmd.ExecuteNonQueryAsync();
        }
    }

    [TestMethod]
    public async Task GetMazeLevelsByGalleryId_ReturnsAllForGalleryId()
    {
        // Use any gallery and verify the query returns data for it
        var galleryId = await GetAnyGalleryId();
        var mazeLevels = await _repository.GetMazeLevelsByGalleryId(galleryId);

        Assert.IsNotNull(mazeLevels);
        // Should return whatever seed data exists for this gallery (typically 3, but could vary)
        var list = mazeLevels.ToList();
        Assert.IsNotEmpty(list, "Expected at least some maze levels for the selected gallery");
        Assert.IsTrue(list.All(level => level.Id > 0 && level.GalleryId == galleryId));
    }


    [TestMethod]
    public async Task GetMazeLevelsByGalleryId_NotExisting_ReturnsEmptyList()
    {
        var galleryId = new Guid("00000000-0000-0000-0000-c8325cf40000");
        var mazeLevels = await _repository.GetMazeLevelsByGalleryId(galleryId);

        Assert.IsNotNull(mazeLevels);
        Assert.HasCount(0, mazeLevels);
    }

    [TestMethod]
    public async Task GetMazeLevelsByGalleryId_LogsWarning_ButStillReturnsLevel_WhenPlayerStartIsOutOfBounds()
    {
        // width/height are DB-CHECK-constrained (5-256), but nothing in the schema stops
        // player_start_x/y from landing outside that box — that's exactly the invariant
        // MazeLevel.Validate() exists to catch on read. This proves the repository logs a
        // warning instead of throwing or silently dropping the malformed row.

        // Create our own throwaway gallery to host the malformed level. Every real seed gallery
        // now carries a full EASY/MEDIUM/HARD maze_levels set, so inserting another level into one
        // would violate the UNIQUE(gallery_id, difficulty) constraint (a 23505 duplicate-key
        // failure). A fresh gallery has empty (gallery_id, difficulty) slots and no competing
        // seed levels, keeping the Times.Once warning assertion below exact. Both the gallery and
        // its maze_levels are removed in TestCleanup (the latter via ON DELETE CASCADE).
        var galleryId = await CreateTestGallery();
        await InsertMalformedMazeLevel(galleryId);

        var loggerMock = new Mock<ILogger<MazeLevelRepository>>();
        var repository = new MazeLevelRepository(_connectionString, loggerMock.Object);

        var mazeLevels = (await repository.GetMazeLevelsByGalleryId(galleryId)).ToList();

        // Should contain at least our test fixture (may also contain seed data)
        Assert.IsGreaterThanOrEqualTo(1, mazeLevels.Count, "Expected at least 1 maze level (our test fixture)");
        Assert.IsTrue(mazeLevels.Any(ml => ml.GalleryId == galleryId), "Expected to find our gallery");

        loggerMock.Verify(
            l => l.Log(
                It.Is<LogLevel>(level => level == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("PlayerStartX")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    private async Task<Guid> GetAnyGalleryId()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT id FROM galleries LIMIT 1", conn);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<Guid> CreateTestGallery()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        // Reuse an existing gallery's enum values (theme/type/subtype) so we don't hardcode enum
        // labels that could drift; image_count/gallery_id/level all have DB defaults.
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO galleries (title, theme, type, subtype)
              SELECT 'integration-test-fixture-gallery', theme, type, subtype FROM galleries LIMIT 1
              RETURNING id",
            conn);
        var result = await cmd.ExecuteScalarAsync();
        Assert.IsNotNull(result, "Expected at least one existing gallery to copy enum values from");
        return (Guid)result;
    }

    private async Task InsertMalformedMazeLevel(Guid galleryId)
    {
        const short width = 10;
        const short height = 10;
        var validCells = new short[height][];
        for (var y = 0; y < height; y++)
            validCells[y] = new short[width];
        var cellsJson = JsonSerializer.Serialize(validCells);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Use HARD difficulty to minimize collision with seed data (most seed uses EASY/MEDIUM)
        // Delete any existing test fixture first to be idempotent
        await using var deleteCmd = new NpgsqlCommand(
            "DELETE FROM maze_levels WHERE gallery_id = @galleryId AND difficulty = 'HARD'::difficulty AND notes = 'integration-test-fixture'",
            conn);
        deleteCmd.Parameters.AddWithValue("@galleryId", galleryId);
        await deleteCmd.ExecuteNonQueryAsync();

        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO maze_levels (gallery_id, difficulty, width, height, cells, player_start_x, player_start_y, notes)
            VALUES (@galleryId, 'HARD'::difficulty, @width, @height, @cells::jsonb, @playerStartX, @playerStartY, @notes)",
            conn);
        cmd.Parameters.AddWithValue("@galleryId", galleryId);
        cmd.Parameters.AddWithValue("@width", width);
        cmd.Parameters.AddWithValue("@height", height);
        cmd.Parameters.AddWithValue("@cells", cellsJson);
        cmd.Parameters.AddWithValue("@playerStartX", (short)(width + 5)); // out of bounds on purpose
        cmd.Parameters.AddWithValue("@playerStartY", (short)0);
        cmd.Parameters.AddWithValue("@notes", "integration-test-fixture");
        await cmd.ExecuteNonQueryAsync();
    }
}