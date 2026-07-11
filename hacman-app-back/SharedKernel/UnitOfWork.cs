using Npgsql;

namespace hacman_app_back.SharedKernel;

/// <inheritdoc cref="IUnitOfWork"/>
public class UnitOfWork : IUnitOfWork
{
    private readonly string _connectionString;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public UnitOfWork(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public NpgsqlConnection Connection =>
        _connection ?? throw new InvalidOperationException("UnitOfWork not started. Call BeginAsync first.");

    public NpgsqlTransaction Transaction =>
        _transaction ?? throw new InvalidOperationException("UnitOfWork not started. Call BeginAsync first.");

    public async Task BeginAsync()
    {
        if (_connection != null)
            throw new InvalidOperationException("UnitOfWork already started.");

        _connection = new NpgsqlConnection(_connectionString);
        await _connection.OpenAsync();
        _transaction = await _connection.BeginTransactionAsync();
    }

    public Task CommitAsync() => Transaction.CommitAsync();

    public Task RollbackAsync() => Transaction.RollbackAsync();

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
            await _transaction.DisposeAsync();

        if (_connection != null)
            await _connection.DisposeAsync();
    }
}