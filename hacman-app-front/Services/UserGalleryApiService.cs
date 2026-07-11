using System.Text.Json;
using hacman_app_front.Domain;

namespace hacman_app_front.Services;

public class UserGalleryApiService : IUserGalleryApiService
{
    private readonly HttpClient _http;
    private readonly ILogger<UserGalleryApiService> _logger;


    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public UserGalleryApiService(HttpClient http, ILogger<UserGalleryApiService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<UserGallery>> GetUserGalleries(Guid userId)
    {
        try
        {
            _logger.LogInformation("===== GetUserGalleries: START (userId={UserId}) =====", userId);
            using var response = await _http.GetAsync($"v1/user-galleries?userId={userId}");
            var body = await response.Content.ReadAsStringAsync();

            // log raw body to console/log output
            _logger.LogInformation("GetUserGalleries: HTTP {StatusCode}. Content-Length: {Length}. Raw body: {Body}",
                (int)response.StatusCode, body.Length, body);
            // or, if you literally want console:
            // Console.WriteLine($"GetUserGalleries: HTTP {(int)response.StatusCode}. Raw body: {body}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetUserGalleries: HTTP {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
                response.EnsureSuccessStatusCode();
            }

            try
            {
                var result = JsonSerializer.Deserialize<List<UserGallery>>(body, JsonOptions);
                _logger.LogInformation("GetUserGalleries: Successfully deserialized {Count} galleries",
                    result?.Count ?? 0);
                _logger.LogInformation("===== GetUserGalleries: END (found {Count} galleries) =====",
                    result?.Count ?? 0);
                return result ?? new List<UserGallery>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "GetUserGalleries: JSON mismatch. Body: {Body}", body);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "===== GetUserGalleries: EXCEPTION =====");
            throw;
        }
    }
}