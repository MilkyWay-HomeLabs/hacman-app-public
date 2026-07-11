using System;
using System.Threading.Tasks;
using hacman_app_back.Modules.Identity.Domain;
using hacman_app_back.Modules.Identity.Infrastructure;
using hacman_app_back.Tests.repository;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace hacman_app_back.Tests.Modules.Identity.Infrastructure;

[TestClass]
[TestCategory("Integration")]
public class UserRepositoryTest : RepositoryTestBase
{
    private const string TestSchema = "public";
    private string _connectionString;
    private IUserRepository _userRepository;

    [TestInitialize]
    public void SetUp()
    {
        var cs = GetConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
            Assert.Fail("Connection string not found.");

        _connectionString = cs + $";Search Path={TestSchema}";
        _userRepository = new UserRepository(_connectionString, NullLogger<UserRepository>.Instance);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM users WHERE external_auth_id LIKE 'test_%'", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    [TestMethod]
    public async Task GetByIdAsync_ReturnsUser_WhenUserExists()
    {
        var created = await CreateAndPersistTestUser();

        var user = await _userRepository.GetByIdAsync(created.Id);

        Assert.IsNotNull(user);
        Assert.AreEqual(created.Id, user.Id);
        Assert.AreEqual(created.Username, user.Username);
        Assert.AreEqual(created.Email, user.Email);
    }

    [TestMethod]
    public async Task GetByIdAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        var nonExistentId = Guid.NewGuid();

        var user = await _userRepository.GetByIdAsync(nonExistentId);

        Assert.IsNull(user);
    }

    [TestMethod]
    public async Task GetByIdAsync_ReturnsUserWithExternalAuthId_WhenSet()
    {
        var created = await CreateAndPersistTestUser();

        var user = await _userRepository.GetByIdAsync(created.Id);

        Assert.IsNotNull(user);
        Assert.AreEqual(created.ExternalAuthId, user.ExternalAuthId);
        Assert.AreEqual(created.Username, user.Username);
        Assert.AreEqual(created.Email, user.Email);
    }

    [TestMethod]
    public async Task GetByExternalAuthIdAsync_ReturnsUser_WhenUserExists()
    {
        var created = await CreateAndPersistTestUser();

        var user = await _userRepository.GetByExternalAuthIdAsync(created.ExternalAuthId);

        Assert.IsNotNull(user);
        Assert.AreEqual(created.Id, user.Id);
        Assert.AreEqual(created.ExternalAuthId, user.ExternalAuthId);
        Assert.AreEqual(created.Username, user.Username);
        Assert.AreEqual(created.Email, user.Email);
    }

    [TestMethod]
    public async Task GetByExternalAuthIdAsync_ReturnsNull_WhenUserDoesNotExist()
    {
        var nonExistentId = "non_existing_id";

        var user = await _userRepository.GetByExternalAuthIdAsync(nonExistentId);

        Assert.IsNull(user);
    }

    [TestMethod]
    public async Task CreateAsync_CreatesUserSuccessfully()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalAuthId = $"test_{Guid.NewGuid()}",
            Username = $"testuser_{Guid.NewGuid():N}",
            Email = $"test_{Guid.NewGuid():N}@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userRepository.CreateAsync(user);

        Assert.IsNotNull(result);
        Assert.AreEqual(user.Id, result.Id);
        Assert.AreEqual(user.ExternalAuthId, result.ExternalAuthId);
        Assert.AreEqual(user.Username, result.Username);
        Assert.AreEqual(user.Email, result.Email);
    }

    [TestMethod]
    public async Task CreateAsync_PersistsUser_RetrievableViaGetByIdAsync()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalAuthId = $"test_{Guid.NewGuid()}",
            Username = $"testuser_{Guid.NewGuid():N}",
            Email = $"test_{Guid.NewGuid():N}@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(user);
        var fetched = await _userRepository.GetByIdAsync(user.Id);

        Assert.IsNotNull(fetched);
        Assert.AreEqual(user.ExternalAuthId, fetched.ExternalAuthId);
        Assert.AreEqual(user.Username, fetched.Username);
        Assert.AreEqual(user.Email, fetched.Email);
    }

    [TestMethod]
    public async Task CreateAsync_SetsTimestampsCorrectly()
    {
        var beforeCreation = DateTime.UtcNow.AddSeconds(-1);
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalAuthId = $"test_{Guid.NewGuid()}",
            Username = $"testuser_{Guid.NewGuid():N}",
            Email = $"test_{Guid.NewGuid():N}@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var result = await _userRepository.CreateAsync(user);
        var afterCreation = DateTime.UtcNow.AddSeconds(1);

        Assert.IsTrue(result.CreatedAt >= beforeCreation);
        Assert.IsTrue(result.CreatedAt <= afterCreation);
        Assert.IsTrue(result.UpdatedAt >= beforeCreation);
        Assert.IsTrue(result.UpdatedAt <= afterCreation);
    }

    private async Task<User> CreateAndPersistTestUser()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalAuthId = $"test_{Guid.NewGuid()}",
            Username = $"testuser_{Guid.NewGuid():N}",
            Email = $"test_{Guid.NewGuid():N}@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await _userRepository.CreateAsync(user);
    }
}