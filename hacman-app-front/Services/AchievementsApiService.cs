using hacman_app_front.Domain;

namespace hacman_app_front.Services;

/// <summary>
/// Mirrors IScoresApiService's resilience style: never throws, always returns an empty
/// list on failure so a backend/Nebula hiccup degrades to "no achievements shown yet"
/// rather than a broken page. Auth is the accessToken cookie HacmanApiClientFactory
/// already attaches to every client it creates -- no manual token handling needed here.
/// </summary>
public class AchievementsApiService : IAchievementsApiService
{
    private readonly HttpClient _http;
    private readonly ILogger<AchievementsApiService> _logger;

    public AchievementsApiService(HttpClient http, ILogger<AchievementsApiService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<AchievementProgressEntry>> GetMyProgress()
    {
        try
        {
            _logger.LogInformation("[AchievementsApiService] GetMyProgress called, BaseAddress={BaseAddress}",
                _http.BaseAddress);

            using var response = await _http.GetAsync("v1/achievements/me");
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "[AchievementsApiService] GetMyProgress failed. Status: {StatusCode}, Body: {Body}",
                    response.StatusCode, errorBody);
                return new List<AchievementProgressEntry>();
            }

            var result = await response.Content.ReadFromJsonAsync<List<AchievementProgressEntry>>()
                         ?? new List<AchievementProgressEntry>();
            _logger.LogInformation("[AchievementsApiService] GetMyProgress completed with {Count} achievements",
                result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AchievementsApiService] Critical error fetching achievement progress");
            return new List<AchievementProgressEntry>();
        }
    }
}
