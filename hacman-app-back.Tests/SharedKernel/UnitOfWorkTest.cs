using System;
using System.Threading.Tasks;
using hacman_app_back.SharedKernel;
using Xunit;

namespace hacman_app_back.Tests.SharedKernel;

/// <summary>
/// Guard-clause tests only — no database connection is ever opened here (BeginAsync itself
/// requires a reachable Postgres and is covered by the DB-backed repository test suite instead).
/// </summary>
public class UnitOfWorkTest
{
    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenConnectionStringIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new UnitOfWork(null!));
    }

    [Fact]
    public void Connection_Throws_WhenNotStarted()
    {
        var uow = new UnitOfWork("Host=unused");

        Assert.Throws<InvalidOperationException>(() => uow.Connection);
    }

    [Fact]
    public void Transaction_Throws_WhenNotStarted()
    {
        var uow = new UnitOfWork("Host=unused");

        Assert.Throws<InvalidOperationException>(() => uow.Transaction);
    }

    [Fact]
    public async Task CommitAsync_Throws_WhenNotStarted()
    {
        var uow = new UnitOfWork("Host=unused");

        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.CommitAsync());
    }

    [Fact]
    public async Task RollbackAsync_Throws_WhenNotStarted()
    {
        var uow = new UnitOfWork("Host=unused");

        await Assert.ThrowsAsync<InvalidOperationException>(() => uow.RollbackAsync());
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow_WhenNeverStarted()
    {
        var uow = new UnitOfWork("Host=unused");

        await uow.DisposeAsync();
    }
}