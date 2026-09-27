using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using hacman_app_back.Modules.GalleryCatalog.Infrastructure;
using hacman_app_back.Tests.repository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace hacman_app_back.Tests.Modules.GalleryCatalog.Infrastructure;

[TestClass]
[TestCategory("Integration")]
public class GalleryRepositoryTest : RepositoryTestBase
{
    private string _connectionString;
    private GalleryRepository _repository;

    [TestInitialize]
    public void Setup()
    {
        _connectionString = GetConnectionString();
        _repository = new GalleryRepository(_connectionString, NullLogger<GalleryRepository>.Instance);
    }

    [TestMethod]
    public async Task GetAllGalleryIdsAsync_ReturnsAllGalleryIds()
    {
        var galleryIds = await _repository.GetAllGalleryIdsAsync();

        Assert.IsNotNull(galleryIds);
        Assert.HasCount(320, galleryIds);
        Assert.IsTrue(galleryIds.All(id => id != Guid.Empty));
    }

    [TestMethod]
    public async Task GetAllGalleryIdsAsync_ReturnsUniqueIds()
    {
        var galleryIds = await _repository.GetAllGalleryIdsAsync();

        var uniqueIds = new HashSet<Guid>(galleryIds);
        Assert.HasCount(galleryIds.Count, uniqueIds);
    }

    [TestMethod]
    public async Task GetAllGalleryIdsAsync_ReturnsListOfGuids()
    {
        var galleryIds = await _repository.GetAllGalleryIdsAsync();

        Assert.IsInstanceOfType(galleryIds, typeof(List<Guid>));
        foreach (var id in galleryIds)
            Assert.IsInstanceOfType(id, typeof(Guid));
    }

    [TestMethod]
    public async Task GetAllGalleryIdsAsync_WorksWithinCallerManagedConnectionAndTransaction()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var transaction = await conn.BeginTransactionAsync();

        var galleryIds = await _repository.GetAllGalleryIdsAsync(conn, transaction);

        Assert.IsNotNull(galleryIds);
        Assert.IsNotEmpty(galleryIds);
        Assert.AreEqual(ConnectionState.Open, conn.State,
            "The repository must not close a connection it didn't open itself, since UserUpdater keeps using it afterward within the same transaction.");

        await transaction.RollbackAsync();
    }
}