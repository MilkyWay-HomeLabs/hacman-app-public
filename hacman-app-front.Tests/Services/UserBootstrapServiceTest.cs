using hacman_app_front.Domain;
using hacman_app_front.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace hacman_app_front.Tests.Services;

public class UserBootstrapServiceTest
{
    private const string HacmanUserKey = "hacmanUser";
    private const string HacmanUserGalleriesKey = "hacmanUserGalleries";

    private readonly Mock<IUserApiService> _userApi = new();
    private readonly Mock<IUserGalleryApiService> _galleryApi = new();
    private readonly Mock<ILocalStorageService> _localStorage = new();
    private readonly UserGalleriesState _galleriesState = new();
    private readonly UserSessionState _sessionState = new();

    private UserBootstrapService CreateService() => new(
        _userApi.Object, _galleryApi.Object, _localStorage.Object,
        _galleriesState, _sessionState, NullLogger<UserBootstrapService>.Instance);

    private void SetCache(string key, string? value) =>
        _localStorage.Setup(s => s.GetItemAsync(key)).ReturnsAsync(value);

    private static string UserJson(Guid id, string username = "neo") =>
        $$$"""{"id":"{{{id}}}","username":"{{{username}}}","externalAuthId":"ext-{{{username}}}","email":"{{{username}}}@test.local"}""";

    private static string GalleriesJson(params Guid[] galleryIds)
    {
        var items = galleryIds.Select(gid =>
            $$$"""{"id":"{{{Guid.NewGuid()}}}","userId":"{{{Guid.NewGuid()}}}","isUnlocked":false,"gallery":{"id":"{{{gid}}}"}}""");
        return "[" + string.Join(",", items) + "]";
    }

    private static string BootstrapPayload(Guid? hacmanId, string? login = null, string? username = "neo")
    {
        var hacman = hacmanId is { } id
            ? $$$"""{"id":"{{{id}}}","externalAuthId":"ext","username":"{{{username}}}","email":"n@test.local"}"""
            : "null";
        var loginJson = login is null ? "null" : $"\"{login}\"";
        return $$$"""{"login":{{{loginJson}}},"email":"n@test.local","hacmanUser":{{{hacman}}}}""";
    }

    // ---------- Path 1: localStorage cache ----------

    [Fact]
    public async Task LoadUserDataAsync_ReturnsCachedUser_AndCachedGalleries_WithoutHittingApis()
    {
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        SetCache(HacmanUserKey, UserJson(userId));
        SetCache(HacmanUserGalleriesKey, GalleriesJson(galleryId));

        var result = await CreateService().LoadUserDataAsync();

        Assert.NotNull(result.User);
        Assert.Equal(userId, result.User!.Id);
        Assert.Same(result.User, _sessionState.CurrentUser);
        Assert.Single(_galleriesState.All);
        Assert.Equal(galleryId, _galleriesState.All[0].Gallery.Id);
        _userApi.Verify(a => a.GetUserByExternalAuthId(), Times.Never);
        _galleryApi.Verify(a => a.GetUserGalleries(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task LoadUserDataAsync_FetchesGalleries_WhenUserCachedButGalleriesNotCached()
    {
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        SetCache(HacmanUserKey, UserJson(userId));
        SetCache(HacmanUserGalleriesKey, null);
        _galleryApi.Setup(a => a.GetUserGalleries(userId))
            .ReturnsAsync([NewGallery(galleryId)]);

        var result = await CreateService().LoadUserDataAsync();

        Assert.Equal(userId, result.User!.Id);
        _galleryApi.Verify(a => a.GetUserGalleries(userId), Times.Once);
        Assert.Single(_galleriesState.All);
        _localStorage.Verify(s => s.SetItemAsync(HacmanUserGalleriesKey, It.IsAny<string>()), Times.Once);
        _userApi.Verify(a => a.GetUserByExternalAuthId(), Times.Never);
    }

    [Fact]
    public async Task LoadUserDataAsync_IgnoresCachedUser_WhenIdIsEmpty_AndFallsBackToServer()
    {
        SetCache(HacmanUserKey, UserJson(Guid.Empty));
        SetCache(HacmanUserGalleriesKey, null);
        _userApi.Setup(a => a.GetUserByExternalAuthId()).ReturnsAsync((UserResponse?)null);

        var result = await CreateService().LoadUserDataAsync();

        Assert.Null(result.User);
        _userApi.Verify(a => a.GetUserByExternalAuthId(), Times.Once);
    }

    // ---------- Path 2: server fetch ----------

    [Fact]
    public async Task LoadUserDataAsync_FetchesUserAndGalleriesFromServer_WhenCacheEmpty()
    {
        var userId = Guid.NewGuid();
        SetCache(HacmanUserKey, null);
        SetCache(HacmanUserGalleriesKey, null);
        _userApi.Setup(a => a.GetUserByExternalAuthId())
            .ReturnsAsync(new UserResponse { Id = userId, Username = "trinity" });
        _galleryApi.Setup(a => a.GetUserGalleries(userId))
            .ReturnsAsync([NewGallery(Guid.NewGuid())]);

        var result = await CreateService().LoadUserDataAsync();

        Assert.Equal(userId, result.User!.Id);
        Assert.Same(result.User, _sessionState.CurrentUser);
        _galleryApi.Verify(a => a.GetUserGalleries(userId), Times.Once);
        Assert.Single(_galleriesState.All);
    }

    [Fact]
    public async Task LoadUserDataAsync_ReturnsNullUser_WhenServerFetchReturnsNull()
    {
        SetCache(HacmanUserKey, null);
        _userApi.Setup(a => a.GetUserByExternalAuthId()).ReturnsAsync((UserResponse?)null);

        var result = await CreateService().LoadUserDataAsync();

        Assert.Null(result.User);
        Assert.Null(_sessionState.CurrentUser);
        _galleryApi.Verify(a => a.GetUserGalleries(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task LoadUserDataAsync_ReturnsNullUser_WhenServerFetchThrows()
    {
        SetCache(HacmanUserKey, null);
        _userApi.Setup(a => a.GetUserByExternalAuthId()).ThrowsAsync(new HttpRequestException("backend down"));

        var result = await CreateService().LoadUserDataAsync();

        Assert.Null(result.User);
        _galleryApi.Verify(a => a.GetUserGalleries(It.IsAny<Guid>()), Times.Never);
    }

    // ---------- Path 3: JS callback ----------

    [Fact]
    public async Task ApplyBootstrapPayloadAsync_BuildsUserFromHacmanUser_AndLoadsCachedGalleries()
    {
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        SetCache(HacmanUserGalleriesKey, GalleriesJson(galleryId));

        var result = await CreateService().ApplyBootstrapPayloadAsync(BootstrapPayload(userId), currentUser: null);

        Assert.NotNull(result.User);
        Assert.Equal(userId, result.User!.Id);
        Assert.True(result.ClearError);
        Assert.Same(result.User, _sessionState.CurrentUser);
        Assert.Single(_galleriesState.All);
        _galleryApi.Verify(a => a.GetUserGalleries(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ApplyBootstrapPayloadAsync_FetchesGalleriesFromServer_WhenCacheEmpty()
    {
        var userId = Guid.NewGuid();
        SetCache(HacmanUserGalleriesKey, null);
        _galleryApi.Setup(a => a.GetUserGalleries(userId))
            .ReturnsAsync([NewGallery(Guid.NewGuid())]);

        var result = await CreateService().ApplyBootstrapPayloadAsync(BootstrapPayload(userId), currentUser: null);

        Assert.Equal(userId, result.User!.Id);
        _galleryApi.Verify(a => a.GetUserGalleries(userId), Times.Once);
    }

    [Fact]
    public async Task ApplyBootstrapPayloadAsync_FallsBackToNebulaLogin_WhenHacmanUserMissing()
    {
        var result = await CreateService()
            .ApplyBootstrapPayloadAsync(BootstrapPayload(hacmanId: null, login: "agent"), currentUser: null);

        Assert.NotNull(result.User);
        Assert.Equal(Guid.Empty, result.User!.Id);
        Assert.Equal("agent", result.User.Username);
        Assert.Equal("agent", result.User.ExternalAuthId);
        // Id is empty, so no gallery lookup happens.
        _galleryApi.Verify(a => a.GetUserGalleries(It.IsAny<Guid>()), Times.Never);
        _localStorage.Verify(s => s.GetItemAsync(HacmanUserGalleriesKey), Times.Never);
    }

    [Fact]
    public async Task ApplyBootstrapPayloadAsync_ReturnsNone_WhenCurrentUserAlreadyResolved()
    {
        var existing = new UserResponse { Id = Guid.NewGuid(), Username = "neo" };

        var result = await CreateService().ApplyBootstrapPayloadAsync(BootstrapPayload(Guid.NewGuid()), existing);

        Assert.Null(result.User);
        Assert.False(result.ClearError);
        _galleryApi.Verify(a => a.GetUserGalleries(It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("""{"login":null,"email":null,"hacmanUser":null}""")]
    public async Task ApplyBootstrapPayloadAsync_ReturnsNone_WhenPayloadEmptyOrHasNoUsableData(string json)
    {
        var result = await CreateService().ApplyBootstrapPayloadAsync(json, currentUser: null);

        Assert.Null(result.User);
        Assert.Null(_sessionState.CurrentUser);
    }

    private static UserGallery NewGallery(Guid galleryId) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Gallery = new Gallery { Id = galleryId }
    };
}