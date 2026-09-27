using hacman_app_front.Domain;

namespace hacman_app_front.Services;

public interface IScoresApiService
{
    Task<List<ScoreEntry>> GetScores(Guid galleryId);
}