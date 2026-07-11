using hacman_app_back.Modules.Scoring.Api;

namespace hacman_app_back.Modules.Scoring.Application;

public interface ILevelScoreService
{
    Task<IEnumerable<ScoreEntryDto>> GetScores(Guid galleryId, string? difficulty, int pageNumber, int pageSize);
    Task<IEnumerable<ScoreEntryDto>> GetScoresByUserId(Guid userId, Guid? galleryId = null, string? difficulty = null);

    /// <summary>
    /// Records a new score for a user, and updates their cached best time for that gallery if this
    /// is a new record. Throws <see cref="ArgumentException"/> if difficulty isn't a valid Difficulty value.
    /// </summary>
    Task<ScoreEntryDto> CreateScore(Guid userId, string username, Guid galleryId, string difficulty, long timeMs);
}