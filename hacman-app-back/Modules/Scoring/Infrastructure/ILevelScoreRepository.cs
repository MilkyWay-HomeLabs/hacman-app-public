using hacman_app_back.Modules.Scoring.Domain;
using Npgsql;

namespace hacman_app_back.Modules.Scoring.Infrastructure;

public interface ILevelScoreRepository
{
    Task<IEnumerable<LevelScore>> GetScoresAsync(Guid galleryId, string? difficulty, int pageNumber, int pageSize);

    Task<IEnumerable<LevelScore>>
        GetScoresByUserIdAsync(Guid userId, Guid? galleryId = null, string? difficulty = null);

    /// <summary>Returns the top-ten scores for a gallery ordered by best_time_ms ascending.</summary>
    Task<IEnumerable<LevelScore>> GetTopTenScoresForGalleryAsync(Guid galleryId);

    /// <summary>Persists a new level score record, optionally within a supplied connection/transaction.</summary>
    Task<LevelScore> CreateAsync(LevelScore levelScore,
        NpgsqlConnection? connection = null, NpgsqlTransaction? transaction = null);
}