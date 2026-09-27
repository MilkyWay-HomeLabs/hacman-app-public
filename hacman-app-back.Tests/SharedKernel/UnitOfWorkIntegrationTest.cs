using System;
using System.Data;
using System.Threading.Tasks;
using hacman_app_back.SharedKernel;
using hacman_app_back.Tests.repository;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace hacman_app_back.Tests.SharedKernel;

/// <summary>
/// Exercises UnitOfWork's real BeginAsync/CommitAsync/RollbackAsync path against a live Postgres
/// connection. UnitOfWorkTest.cs (xUnit, same namespace) covers the DB-free guard clauses only.
/// </summary>
[TestClass]
[TestCategory("Integration")]
public class UnitOfWorkIntegrationTest : RepositoryTestBase
{
    private string _connectionString;

    [TestInitialize]
    public void Setup()
    {
        _connectionString = GetConnectionString();
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
    public async Task BeginAsync_OpensConnectionAndStartsTransaction()
    {
        await using var unitOfWork = new UnitOfWork(_connectionString);

        await unitOfWork.BeginAsync();

        Assert.AreEqual(ConnectionState.Open, unitOfWork.Connection.State);
        Assert.IsNotNull(unitOfWork.Transaction);

        await unitOfWork.RollbackAsync();
    }

    [TestMethod]
    public async Task CommitAsync_PersistsChangesMadeWithinTheTransaction()
    {
        var externalAuthId = $"test_{Guid.NewGuid()}";

        await using (var unitOfWork = new UnitOfWork(_connectionString))
        {
            await unitOfWork.BeginAsync();
            await InsertTestUser(unitOfWork.Connection, unitOfWork.Transaction, externalAuthId);
            await unitOfWork.CommitAsync();
        }

        Assert.IsTrue(await UserExists(externalAuthId),
            "Committed row should be visible once the UnitOfWork has committed.");
    }

    [TestMethod]
    public async Task RollbackAsync_UndoesChangesMadeWithinTheTransaction()
    {
        var externalAuthId = $"test_{Guid.NewGuid()}";

        await using (var unitOfWork = new UnitOfWork(_connectionString))
        {
            await unitOfWork.BeginAsync();
            await InsertTestUser(unitOfWork.Connection, unitOfWork.Transaction, externalAuthId);
            await unitOfWork.RollbackAsync();
        }

        Assert.IsFalse(await UserExists(externalAuthId),
            "Rolled-back row must not exist after RollbackAsync.");
    }

    [TestMethod]
    public async Task DisposeAsync_WithoutCommitOrRollback_DoesNotPersistChanges()
    {
        // Guards against the "forgot to call CommitAsync" failure mode: disposing an
        // in-progress transaction must not silently make its writes durable.
        var externalAuthId = $"test_{Guid.NewGuid()}";

        var unitOfWork = new UnitOfWork(_connectionString);
        await unitOfWork.BeginAsync();
        await InsertTestUser(unitOfWork.Connection, unitOfWork.Transaction, externalAuthId);
        await unitOfWork.DisposeAsync();

        Assert.IsFalse(await UserExists(externalAuthId),
            "Disposing without an explicit CommitAsync must not persist pending writes.");
    }

    private static async Task InsertTestUser(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string externalAuthId)
    {
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO users (id, external_auth_id, username, email, created_at, updated_at)
            VALUES (@id, @externalAuthId, @username, @email, @createdAt, @updatedAt)",
            connection, transaction);
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@externalAuthId", externalAuthId);
        cmd.Parameters.AddWithValue("@username", $"testuser_{Guid.NewGuid():N}");
        cmd.Parameters.AddWithValue("@email", $"test_{Guid.NewGuid():N}@example.com");
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<bool> UserExists(string externalAuthId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM users WHERE external_auth_id = @externalAuthId", conn);
        cmd.Parameters.AddWithValue("@externalAuthId", externalAuthId);
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return count > 0;
    }
}