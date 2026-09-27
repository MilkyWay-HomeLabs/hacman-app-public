#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hacman_app_back.Modules.GalleryCatalog.Infrastructure;
using hacman_app_back.Modules.Identity.Application;
using hacman_app_back.Modules.Identity.Infrastructure;
using hacman_app_back.Modules.PlayerProgress.Domain;
using hacman_app_back.Modules.PlayerProgress.Infrastructure;
using hacman_app_back.SharedKernel;
using hacman_app_back.Tests.repository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace hacman_app_back.Tests.Modules.Identity.Application;

/// <summary>
/// End-to-end coverage for UserUpdater.CreateNewUserData against a real database, using the
/// real IUnitOfWork/UserRepository/GalleryRepository/UserGalleryRepository — no mocks. This is
/// the only test that actually exercises the cross-aggregate transactional guarantee (User +
/// N UserGallery rows created atomically) that IUnitOfWork was introduced for.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class UserUpdaterIntegrationTest : RepositoryTestBase
{
    private string _connectionString;
    private UserRepository _userRepository;
    private GalleryRepository _galleryRepository;
    private UserGalleryRepository _userGalleryRepository;

    [TestInitialize]
    public void Setup()
    {
        _connectionString = GetConnectionString();
        _userRepository = new UserRepository(_connectionString, NullLogger<UserRepository>.Instance);
        _galleryRepository = new GalleryRepository(_connectionString, NullLogger<GalleryRepository>.Instance);
        _userGalleryRepository =
            new UserGalleryRepository(_connectionString, NullLogger<UserGalleryRepository>.Instance);
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
    public async Task CreateNewUserData_PersistsUserAndAllUserGalleries_WhenSuccessful()
    {
        var updater = new UserUpdater(_userRepository, _galleryRepository, _userGalleryRepository,
            () => new UnitOfWork(_connectionString));
        var externalAuthId = $"test_{Guid.NewGuid()}";
        var expectedGalleryCount = (await _galleryRepository.GetAllGalleryIdsAsync()).Count;

        var user = await updater.CreateNewUserData(externalAuthId, $"testuser_{Guid.NewGuid():N}",
            $"test_{Guid.NewGuid():N}@example.com");

        var persistedUser = await _userRepository.GetByIdAsync(user.Id);
        Assert.IsNotNull(persistedUser);
        Assert.AreEqual(externalAuthId, persistedUser.ExternalAuthId);

        var userGalleries = (await _userGalleryRepository.GetAllByUserIdAsync(user.Id)).ToList();
        Assert.HasCount(expectedGalleryCount, userGalleries,
            "Expected one UserGallery row per existing gallery.");
        Assert.IsTrue(userGalleries.All(ug => !ug.IsUnlocked),
            "Newly seeded UserGallery rows should start locked.");
    }

    [TestMethod]
    public async Task CreateNewUserData_RollsBackEverything_WhenUserGalleryCreationFailsPartway()
    {
        const int failAfter = 3;
        var failingUserGalleryRepository = new FailAfterNCallsUserGalleryRepository(_userGalleryRepository, failAfter);
        var updater = new UserUpdater(_userRepository, _galleryRepository, failingUserGalleryRepository,
            () => new UnitOfWork(_connectionString));
        var externalAuthId = $"test_{Guid.NewGuid()}";

        try
        {
            await updater.CreateNewUserData(externalAuthId, $"testuser_{Guid.NewGuid():N}",
                $"test_{Guid.NewGuid():N}@example.com");
            Assert.Fail("Expected InvalidOperationException was not thrown.");
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        Assert.IsGreaterThan(failAfter,
failingUserGalleryRepository.CallCount, "The failure should only trigger after some real inserts already happened, otherwise this test proves nothing about rollback.");

        var persistedUser = await _userRepository.GetByExternalAuthIdAsync(externalAuthId);
        Assert.IsNull(persistedUser,
            "The User row must be rolled back along with the partially-created UserGallery rows — that's the whole point of sharing one IUnitOfWork across both inserts.");
    }

    /// <summary>
    /// Wraps a real IUserGalleryRepository so the first few CreateAsync calls actually hit the
    /// database (proving there's something to roll back), then starts throwing — simulating a
    /// failure partway through UserUpdater.CreateUserGalleriesAsync's loop.
    /// </summary>
    private class FailAfterNCallsUserGalleryRepository(IUserGalleryRepository inner, int failAfter)
        : IUserGalleryRepository
    {
        public int CallCount { get; private set; }

        public async Task<UserGallery> CreateAsync(UserGallery userGallery,
            NpgsqlConnection? connection = null, NpgsqlTransaction? transaction = null)
        {
            CallCount++;
            if (CallCount > failAfter)
                throw new InvalidOperationException("Simulated failure for rollback test.");

            return await inner.CreateAsync(userGallery, connection, transaction);
        }

        public Task<IEnumerable<UserGallery>> GetAllByUserIdAsync(Guid userId) => inner.GetAllByUserIdAsync(userId);

        public Task<UserGallery?> GetByGalleryIdAsync(Guid galleryId) => inner.GetByGalleryIdAsync(galleryId);

        public Task<bool> UpdateBestTimeIfBetterAsync(Guid userId, Guid galleryId, long timeMs, DateTime achievedAt,
            NpgsqlConnection? connection = null, NpgsqlTransaction? transaction = null) =>
            inner.UpdateBestTimeIfBetterAsync(userId, galleryId, timeMs, achievedAt, connection, transaction);
    }
}