using Npgsql;

namespace hacman_app_back.SharedKernel;

/// <summary>
/// Wraps a single Npgsql connection + transaction shared across multiple repository calls,
/// so cross-aggregate operations (e.g. creating a User plus its UserGallery rows) don't have
/// to hand-roll connection/transaction plumbing themselves.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    /// <summary>The connection for this unit of work. Throws if <see cref="BeginAsync"/> hasn't been called.</summary>
    NpgsqlConnection Connection { get; }

    /// <summary>The transaction for this unit of work. Throws if <see cref="BeginAsync"/> hasn't been called.</summary>
    NpgsqlTransaction Transaction { get; }

    /// <summary>Opens the connection and starts the transaction.</summary>
    Task BeginAsync();

    /// <summary>Commits the transaction.</summary>
    Task CommitAsync();

    /// <summary>Rolls back the transaction.</summary>
    Task RollbackAsync();
}