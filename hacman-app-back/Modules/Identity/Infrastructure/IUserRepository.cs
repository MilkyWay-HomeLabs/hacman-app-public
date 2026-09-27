using hacman_app_back.Modules.Identity.Domain;
using Npgsql;

namespace hacman_app_back.Modules.Identity.Infrastructure;

public interface IUserRepository
{
    /// <summary>Retrieves a user by their internal identifier.</summary>
    Task<User?> GetByIdAsync(Guid id);

    /// <summary>Retrieves a user by their external OAuth identifier.</summary>
    Task<User?> GetByExternalAuthIdAsync(string externalAuthId);

    /// <summary>Persists a new user record, optionally within a supplied connection/transaction.</summary>
    Task<User> CreateAsync(User user, NpgsqlConnection? connection = null, NpgsqlTransaction? transaction = null);
}