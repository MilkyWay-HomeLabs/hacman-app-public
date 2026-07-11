using System.Net;
using hacman_app_front.Domain;

namespace hacman_app_front.Services;

public class UserApiService : IUserApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserApiService> _logger;

    public UserApiService(HttpClient httpClient, ILogger<UserApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<UserResponse?> GetUserByExternalAuthId()
    {
        var url = "v1/users/external-auth-id";
        _logger.LogInformation("Requesting GET {Url}", url);

        var response = await _httpClient.GetAsync(url);

        _logger.LogInformation("Response from {Url}: {StatusCode}", url, response.StatusCode);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null; // User does not exist yet.
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Unauthorized access to {Url}. Response body: {Body}", url, body);
            throw new HttpRequestException($"Unauthorized: {body}", null, HttpStatusCode.Unauthorized);
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserResponse>();
    }

    public async Task<UserResponse?> CreateNewUser(string userName)
    {
        var url = "v1/users";
        try
        {
            _logger.LogInformation("Requesting POST {Url} for user {UserName}", url, userName);
            var requestBody = new { Username = userName };
            var response = await _httpClient.PostAsJsonAsync(url, requestBody);

            _logger.LogInformation("Response from {Url}: {StatusCode}", url, response.StatusCode);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Unauthorized POST to {Url}. Response body: {Body}", url, body);
                throw new HttpRequestException($"Unauthorized: {body}");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserResponse>();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP Request error during CreateNewUser for {Url}", url);
            throw;
        }
    }
}