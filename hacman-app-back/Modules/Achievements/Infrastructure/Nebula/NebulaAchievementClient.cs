using System.Net.Http.Headers;
using System.Net.Http.Json;
using hacman_app_back.Modules.Achievements.Domain;

namespace hacman_app_back.Modules.Achievements.Infrastructure.Nebula;

/// <summary>
/// HTTP adapter for Nebula's achievements-ingestion contract (CLAUDE.md §4). Mirrors
/// Robak's reference adapter (STATUS.md §4.3) in spirit: every call swallows its own
/// exceptions and no-ops when not configured (no <c>HttpClient.BaseAddress</c>, or no
/// bearer token) — a down or unconfigured Nebula must never affect a player-facing
/// request. Callers are expected to invoke this off the request thread (see
/// <see cref="NebulaAchievementDispatcher"/>), not directly from a controller/service.
/// </summary>
public class NebulaAchievementClient : INebulaAchievementClient
{
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly ILogger<NebulaAchievementClient> _logger;

    public NebulaAchievementClient(HttpClient httpClient, string? token, ILogger<NebulaAchievementClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _token = token ?? string.Empty;
        _logger = logger;
    }

    private bool IsConfigured => _httpClient.BaseAddress != null && !string.IsNullOrWhiteSpace(_token);

    /// <inheritdoc/>
    public async Task SyncCatalogAsync(IReadOnlyList<AchievementDef> achievements,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || achievements.Count == 0)
        {
            LogSkippedIfUnconfigured("catalog sync");
            return;
        }

        try
        {
            var request = new NebulaCatalogSyncRequest { Achievements = achievements.Select(ToCatalogDto).ToList() };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/v1/achievements/catalog")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.IsSuccessStatusCode)
                _logger.LogInformation("Nebula catalog sync: pushed {Count} achievements", request.Achievements.Count);
            else
                _logger.LogWarning("Nebula catalog sync failed: {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nebula catalog sync failed (non-fatal)");
        }
    }

    /// <inheritdoc/>
    public async Task ReportProgressAsync(Guid userId, AchievementDef achievement, long value, int level,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            LogSkippedIfUnconfigured("progress report");
            return;
        }

        try
        {
            var entry = new NebulaProgressEntryDto
            {
                UserId = userId,
                ExternalId = achievement.Code,
                Value = value,
                Level = level,
                Progress = ProgressBasisPoints(value, achievement.Thresholds)
            };
            var request = new NebulaProgressPushRequest { Entries = [entry] };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/v1/achievements/progress")
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nebula progress report failed for externalId={ExternalId}: {StatusCode}",
                    achievement.Code, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nebula progress report failed (non-fatal) for externalId={ExternalId}",
                achievement.Code);
        }
    }

    /// <summary>0-10000, two implied decimal places, per STATUS.md §3's <c>progress</c> convention.</summary>
    private static int ProgressBasisPoints(long value, IReadOnlyList<int> thresholds)
    {
        var maxThreshold = thresholds.Count == 0 ? 0 : thresholds[^1];
        if (maxThreshold <= 0)
            return 0;

        var fraction = Math.Min(1.0, value / (double)maxThreshold);
        return (int)Math.Round(fraction * 10000);
    }

    private static NebulaCatalogAchievementDto ToCatalogDto(AchievementDef def) => new()
    {
        ExternalId = def.Code,
        Name = def.Name,
        Description = def.Description,
        IconUrl = def.IconUrl,
        MinValue = 0,
        MaxValue = def.Thresholds.Length == 0 ? 0 : def.Thresholds[^1],
        Levels = def.Thresholds
            .Select((threshold, index) => new NebulaCatalogLevelDto { Level = index + 1, Value = threshold })
            .ToList()
    };

    private void LogSkippedIfUnconfigured(string what)
    {
        if (!IsConfigured)
            _logger.LogDebug("Nebula {What} skipped: base URL/token not configured", what);
    }
}
