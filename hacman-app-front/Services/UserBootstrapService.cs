using System.Text.Json;
using hacman_app_front.Domain;

namespace hacman_app_front.Services;

/// <summary>
/// Orchestrates resolving the current user and their galleries during app startup.
///
/// Extracted from <c>MainLayout</c> so the three bootstrap paths are unit-testable in isolation:
/// <list type="number">
///   <item>localStorage cache — user/galleries already persisted by a prior session or the app.js bootstrap;</item>
///   <item>server fetch — a single backend call when the cache is empty;</item>
///   <item>JS callback — the app.js bootstrap IIFE pushes a payload back via <c>MainLayout.ReceiveBootstrapData</c>.</item>
/// </list>
///
/// The service mutates the shared <see cref="UserGalleriesState"/>/<see cref="UserSessionState"/> directly and
/// returns a <see cref="BootstrapResult"/> describing what the layout should apply to its own render state
/// (the resolved user and whether to clear a previously shown error). The layout keeps the loading flag and
/// <c>StateHasChanged</c> calls, and stays free of orchestration logic.
/// </summary>
public sealed class UserBootstrapService(
    IUserApiService userApiService,
    IUserGalleryApiService userGalleryApiService,
    ILocalStorageService localStorage,
    UserGalleriesState galleriesState,
    UserSessionState sessionState,
    ILogger<UserBootstrapService> logger)
{
    private const string HacmanUserKey = "hacmanUser";
    private const string HacmanUserGalleriesKey = "hacmanUserGalleries";

    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonSerializerOptions WriteOptions = new()
        { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Resolves the current user via the localStorage cache first, falling back to a single server-side fetch.
    /// Sets <see cref="UserGalleriesState"/>/<see cref="UserSessionState"/> as a side effect. Returns the
    /// resolved user (or null when neither path yields one — the JS callback path will complete the flow).
    /// </summary>
    public async Task<BootstrapResult> LoadUserDataAsync()
    {
        logger.LogInformation("UserBootstrapService.LoadUserDataAsync: START");

        // Fast-path: read data cached by a previous session or the JS bootstrap. This avoids a slow
        // server-to-server call when the backend is unreachable from within the container.
        var cachedUser = await ReadCachedUserAsync();
        if (cachedUser != null)
        {
            sessionState.CurrentUser = cachedUser;
            logger.LogInformation("LoadUserDataAsync: user loaded from localStorage = {Username}", cachedUser.Username);

            if (await TryLoadGalleriesFromCacheAsync())
            {
                logger.LogInformation("LoadUserDataAsync: END (via localStorage cache)");
                return new BootstrapResult(cachedUser);
            }

            logger.LogInformation("LoadUserDataAsync: no cached galleries, fetching from backend");
            await LoadGalleriesAsync(cachedUser.Id);
            logger.LogInformation("LoadUserDataAsync: END (cached user + fetched galleries)");
            return new BootstrapResult(cachedUser);
        }

        // localStorage empty — fall back to a single server-side user fetch (short HttpClient timeout,
        // so it fails fast if the backend is unreachable, and the JS bootstrap picks up from there).
        logger.LogInformation("LoadUserDataAsync: no localStorage cache, trying server-side fetch");
        UserResponse? user = null;
        try
        {
            user = await userApiService.GetUserByExternalAuthId();
            logger.LogInformation("LoadUserDataAsync: server-side user = {Username}", user?.Username);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LoadUserDataAsync: server-side user fetch failed, deferring to JS bootstrap");
        }

        if (user != null)
        {
            sessionState.CurrentUser = user;
            await LoadGalleriesAsync(user.Id);
            logger.LogInformation("LoadUserDataAsync: END (server user + galleries)");
        }
        else
        {
            logger.LogInformation("LoadUserDataAsync: END (no user, waiting for JS bootstrap)");
        }

        return new BootstrapResult(user);
    }

    /// <summary>
    /// Applies a bootstrap payload pushed from app.js (the JS-callback path). Builds the current user from the
    /// resolved Hacman user or, failing that, the Nebula login, then loads galleries (localStorage first, server
    /// fallback). Returns <see cref="BootstrapResult.User"/> = null (and no error clear) when there is nothing to
    /// apply: empty/invalid payload, a user already resolved, or no usable user data in the payload.
    /// </summary>
    public async Task<BootstrapResult> ApplyBootstrapPayloadAsync(string json, UserResponse? currentUser)
    {
        try
        {
            if (string.IsNullOrEmpty(json)) return BootstrapResult.None;
            logger.LogInformation("ApplyBootstrapPayloadAsync: payload received");

            var detail = JsonSerializer.Deserialize<BootstrapDetail>(json, ReadOptions);
            if (detail == null)
            {
                logger.LogWarning("ApplyBootstrapPayloadAsync: failed to deserialize payload");
                return BootstrapResult.None;
            }

            // If a fully resolved user is already present, nothing to do.
            if (currentUser != null && currentUser.Id != Guid.Empty)
            {
                logger.LogInformation("ApplyBootstrapPayloadAsync: currentUser already set, skipping");
                return BootstrapResult.None;
            }

            UserResponse? user;
            if (detail.HacmanUser != null && detail.HacmanUser.Id != Guid.Empty)
            {
                user = new UserResponse
                {
                    Id = detail.HacmanUser.Id,
                    ExternalAuthId = detail.HacmanUser.ExternalAuthId,
                    Username = detail.HacmanUser.Username ?? detail.Login,
                    Email = detail.HacmanUser.Email
                };
                logger.LogInformation("ApplyBootstrapPayloadAsync: user={Username} id={Id}", user.Username, user.Id);
            }
            else if (!string.IsNullOrEmpty(detail.Login))
            {
                logger.LogWarning("ApplyBootstrapPayloadAsync: hacmanUser missing, using Nebula login as fallback");
                user = new UserResponse
                {
                    Id = Guid.Empty,
                    ExternalAuthId = detail.Login,
                    Username = detail.Login,
                    Email = detail.Email
                };
            }
            else
            {
                logger.LogWarning("ApplyBootstrapPayloadAsync: no usable user data");
                return BootstrapResult.None;
            }

            sessionState.CurrentUser = user;

            // Priority 1: galleries the JS layer already fetched and cached (avoids a redundant server call and
            // works even when the Blazor server cannot reach the Hacman API directly). Priority 2: server fetch.
            if (user.Id != Guid.Empty && !await TryLoadGalleriesFromCacheAsync())
            {
                await LoadGalleriesAsync(user.Id);
            }

            return new BootstrapResult(user, ClearError: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ApplyBootstrapPayloadAsync: failed to process payload");
            return BootstrapResult.None;
        }
    }

    /// <summary>Reads and deserializes the cached Hacman user; returns null when absent, unparseable, or invalid.</summary>
    private async Task<UserResponse?> ReadCachedUserAsync()
    {
        var cachedUserJson = await localStorage.GetItemAsync(HacmanUserKey);
        if (string.IsNullOrEmpty(cachedUserJson)) return null;

        try
        {
            var user = JsonSerializer.Deserialize<UserResponse>(cachedUserJson, ReadOptions);
            return user is { Id: var id } && id != Guid.Empty ? user : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ReadCachedUserAsync: failed to deserialize cached hacmanUser");
            return null;
        }
    }

    /// <summary>Loads galleries from the localStorage cache into state. Returns true when non-empty galleries were applied.</summary>
    private async Task<bool> TryLoadGalleriesFromCacheAsync()
    {
        try
        {
            var cachedGalleriesJson = await localStorage.GetItemAsync(HacmanUserGalleriesKey);
            if (string.IsNullOrEmpty(cachedGalleriesJson)) return false;

            var galleries = JsonSerializer.Deserialize<List<UserGallery>>(cachedGalleriesJson, ReadOptions);
            if (galleries is not { Count: > 0 }) return false;

            galleriesState.Set(galleries);
            logger.LogInformation("TryLoadGalleriesFromCacheAsync: {Count} galleries loaded from localStorage",
                galleries.Count);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TryLoadGalleriesFromCacheAsync: failed to load galleries from localStorage");
            return false;
        }
    }

    /// <summary>Fetches galleries from the backend, applies them to state, and persists them to localStorage. Non-fatal on failure.</summary>
    private async Task LoadGalleriesAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            logger.LogWarning("LoadGalleriesAsync: userId is empty, skipping gallery load");
            return;
        }

        try
        {
            logger.LogInformation("LoadGalleriesAsync: START (userId={UserId})", userId);
            var galleries = await userGalleryApiService.GetUserGalleries(userId) ?? new List<UserGallery>();

            galleriesState.Set(galleries);
            logger.LogInformation("LoadGalleriesAsync: {Count} galleries set to UserGalleriesState", galleries.Count);

            try
            {
                var galleriesJson = JsonSerializer.Serialize(galleries, WriteOptions);
                await localStorage.SetItemAsync(HacmanUserGalleriesKey, galleriesJson);
                logger.LogInformation("LoadGalleriesAsync: galleries saved to localStorage");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "LoadGalleriesAsync: failed to save galleries to localStorage");
            }
        }
        catch (HttpRequestException ex)
        {
            // Non-fatal: the app works without galleries.
            logger.LogWarning("LoadGalleriesAsync: HTTP {StatusCode} - {Message}", ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LoadGalleriesAsync: unexpected error");
        }
    }

    // DTO for the bootstrap payload dispatched from app.js: { login, email, userData, hacmanUser, hacmanUserGalleries }.
    private sealed record BootstrapDetail(string? Login, string? Email, HacmanDetail? HacmanUser);

    private sealed record HacmanDetail(Guid Id, string? ExternalAuthId, string? Username, string? Email);
}

/// <summary>What the layout should apply after a bootstrap step: the resolved user and whether to clear a prior error.</summary>
public sealed record BootstrapResult(UserResponse? User, bool ClearError = false)
{
    /// <summary>Nothing to apply — the layout should leave its render state untouched.</summary>
    public static readonly BootstrapResult None = new((UserResponse?)null);
}