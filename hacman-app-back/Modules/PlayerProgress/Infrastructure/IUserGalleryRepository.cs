using hacman_app_back.Modules.PlayerProgress.Domain;
using Npgsql;

namespace hacman_app_back.Modules.PlayerProgress.Infrastructure;

public interface IUserGalleryRepository
{
    /// <summary>Persists a new user-gallery association, optionally within a supplied connection/transaction.</summary>
    Task<UserGallery> CreateAsync(UserGallery userGallery,
        NpgsqlConnection? connection = null, NpgsqlTransaction? transaction = null);

    /// <summary>Returns all gallery associations for the given user, ordered by gallery number.</summary>
    Task<IEnumerable<UserGallery>> GetAllByUserIdAsync(Guid userId);

    /// <summary>Returns the user-gallery record for a specific gallery, or null if none exists.</summary>
    Task<UserGallery?> GetByGalleryIdAsync(Guid galleryId);

    /// <summary>
    /// Updates the cached best time for a user's gallery association if the given time is better
    /// (lower) than what's currently stored, or if none is stored yet, and marks it unlocked.
    /// Returns true if a row was actually updated (i.e. this was a new best).
    /// </summary>
    Task<bool> UpdateBestTimeIfBetterAsync(Guid userId, Guid galleryId, long timeMs, DateTime achievedAt,
        NpgsqlConnection? connection = null, NpgsqlTransaction? transaction = null);
}