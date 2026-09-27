using Bunit;
using hacman_app_front.Components.Layout;
using hacman_app_front.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace hacman_app_front.Tests.Components;

/// <summary>
/// Guards the accessToken-capture fix: HacmanApiClientFactory.ResolveAccessToken() can only
/// read the cookie while HttpContext is still available (this render), and its side effect of
/// seeding UserSessionState.AccessToken must not depend on which bootstrap path
/// UserBootstrapService happens to take. Before this fix, the localStorage fast-path skipped
/// every backend call, so AccessToken was never seeded whenever a cached user/galleries pair
/// was already present -- breaking any later page (e.g. Achievements) that's the first to build
/// its own HttpClient afterwards, once HttpContext is gone for the rest of the circuit.
/// </summary>
public class MainLayoutTest : BunitContext
{
    private readonly UserSessionState _sessionState = new();

    public MainLayoutTest()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Cookie"] = "accessToken=test-token-value";
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var apiClientFactory = new HacmanApiClientFactory(
            accessor, _sessionState, NullLogger<HacmanApiClientFactory>.Instance,
            new Uri("http://localhost:9999"));

        // A cached user + cached galleries makes UserBootstrapService take the localStorage
        // fast-path, which never touches IUserApiService/IUserGalleryApiService -- exactly the
        // scenario that used to leave AccessToken unseeded.
        var localStorage = new Mock<ILocalStorageService>();
        localStorage.Setup(l => l.GetItemAsync("hacmanUser"))
            .ReturnsAsync("""{"id":"11111111-1111-1111-1111-111111111111","username":"agent"}""");
        localStorage.Setup(l => l.GetItemAsync("hacmanUserGalleries")).ReturnsAsync("[]");

        var bootstrapService = new UserBootstrapService(
            Mock.Of<IUserApiService>(),
            Mock.Of<IUserGalleryApiService>(),
            localStorage.Object,
            new UserGalleriesState(),
            _sessionState,
            NullLogger<UserBootstrapService>.Instance);

        Services.AddSingleton(_sessionState);
        Services.AddSingleton(apiClientFactory);
        Services.AddSingleton(bootstrapService);
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
    }

    [Fact]
    public async Task AccessToken_IsSeededFromTheCookie_EvenWhenTheBootstrapTakesTheLocalStorageFastPath()
    {
        var cut = Render<MainLayout>();

        await cut.InvokeAsync(() => { });
        cut.WaitForState(() => _sessionState.AccessToken == "test-token-value", TimeSpan.FromSeconds(2));

        Assert.Equal("test-token-value", _sessionState.AccessToken);
    }
}
