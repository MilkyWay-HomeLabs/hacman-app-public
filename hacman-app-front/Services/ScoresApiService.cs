using System.Net.Http.Headers;
using hacman_app_front.Domain;

namespace hacman_app_front.Services;

public class ScoresApiService : IScoresApiService
{
    private readonly HttpClient _http;
    private readonly ILogger<ScoresApiService> _logger;
    private readonly UserSessionState _sessionState;

    public ScoresApiService(HttpClient http, ILogger<ScoresApiService> logger, UserSessionState sessionState)
    {
        _http = http;
        _logger = logger;
        _sessionState = sessionState;
    }

    private static readonly string[] Difficulties = { "EASY", "MEDIUM", "HARD" };

    public async Task<List<ScoreEntry>> GetScores(Guid galleryId)
    {
        try
        {
            _logger.LogInformation(
                "[ScoresApiService] GetScores called for galleryId={GalleryId}, BaseAddress={BaseAddress}",
                galleryId, _http.BaseAddress);

            var token = _sessionState?.AccessToken;
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("[ScoresApiService] No access token available in UserSessionState");
            }

            // Fetch all three difficulty levels concurrently. Task.WhenAll preserves input order,
            // so the merged list keeps the EASY/MEDIUM/HARD ordering of the sequential version.
            var perDifficulty = await Task.WhenAll(
                Difficulties.Select(difficulty => FetchScoresForDifficultyAsync(galleryId, difficulty, token)));

            var allScores = perDifficulty.SelectMany(scores => scores).ToList();
            _logger.LogInformation("[ScoresApiService] GetScores completed with {TotalCount} scores", allScores.Count);
            return allScores;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ScoresApiService] Critical error fetching scores for gallery {GalleryId}",
                galleryId);
            // Return an empty list instead of throwing when backend data is unavailable.
            return new List<ScoreEntry>();
        }
    }

    /// <summary>
    /// Fetches one difficulty's scores. Uses a per-request <see cref="HttpRequestMessage"/> (rather than mutating
    /// the shared <c>HttpClient.DefaultRequestHeaders</c>) so concurrent calls don't race on the Authorization header.
    /// Returns an empty list on any failure so a single difficulty's error doesn't sink the others.
    /// </summary>
    private async Task<List<ScoreEntry>> FetchScoresForDifficultyAsync(Guid galleryId, string difficulty, string? token)
    {
        const int pageNumber = 1;
        const int pageSize = 10;

        try
        {
            var url =
                $"v1/level-scores?galleryId={galleryId}&difficulty={difficulty}&pageNumber={pageNumber}&pageSize={pageSize}";
            _logger.LogInformation("[ScoresApiService] Fetching scores for difficulty={Difficulty} at url={Url}",
                difficulty, url);

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "[ScoresApiService] Failed to fetch scores for difficulty={Difficulty}. Status: {StatusCode}, Body: {Body}",
                    difficulty, response.StatusCode, errorBody);
                return new List<ScoreEntry>();
            }

            var scores = await response.Content.ReadFromJsonAsync<List<ScoreEntry>>() ?? new List<ScoreEntry>();
            foreach (var score in scores)
            {
                score.Difficulty = difficulty;
            }

            _logger.LogInformation("[ScoresApiService] Received {Count} scores for difficulty={Difficulty}",
                scores.Count, difficulty);
            return scores;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[ScoresApiService] Error fetching scores for gallery {GalleryId} with difficulty {Difficulty}",
                galleryId, difficulty);
            return new List<ScoreEntry>();
        }
    }
}