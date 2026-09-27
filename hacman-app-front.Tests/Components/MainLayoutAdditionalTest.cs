using Bunit;
using hacman_app_front.Components.Layout;
using hacman_app_front.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Components;
using Moq;
using Xunit;

namespace hacman_app_front.Tests.Components;

/// <summary>
/// Covers MainLayout's JS-callback paths (ReceiveBootstrapData/ReceiveAuthError) and the
/// error-banner/redirect-loop branches of RedirectToLogin -- not exercised by MainLayoutTest,
/// which only guards the accessToken-capture fix.
/// </summary>
public class MainLayoutAdditionalTest : BunitContext
{
    private readonly UserSessionState _sessionState = new();

    public MainLayoutAdditionalTest()
    {
        var httpContext = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var apiClientFactory = new HacmanApiClientFactory(
            accessor, _sessionState, NullLogger<HacmanApiClientFactory>.Instance, new Uri("http://localhost:9999"));

        var localStorage = new Mock<ILocalStorageService>();
        localStorage.Setup(l => l.GetItemAsync(It.IsAny<string>())).ReturnsAsync((string?)null);

        var bootstrapService = new UserBootstrapService(
            Mock.Of<IUserApiService>(a => a.GetUserByExternalAuthId() == Task.FromResult<Domain.UserResponse?>(null)),
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

    private async Task<IRenderedComponent<MainLayout>> RenderReadyLayoutAsync()
    {
        var cut = Render<MainLayout>();
        await cut.InvokeAsync(() => { });
        cut.WaitForState(() => !cut.Markup.Contains("INITIALIZING SESSION"), TimeSpan.FromSeconds(2));
        return cut;
    }

    [Fact]
    public async Task ReceiveBootstrapData_SetsCurrentUser_AndRendersLoggedInBanner()
    {
        var cut = await RenderReadyLayoutAsync();
        const string payload = """{"login":"neo","hacmanUser":{"id":"11111111-1111-1111-1111-111111111111","username":"neo"}}""";

        await cut.InvokeAsync(() => cut.Instance.ReceiveBootstrapData(payload));

        cut.WaitForAssertion(() => Assert.Contains("LOGGED IN AS", cut.Markup));
        Assert.Contains("neo", cut.Markup);
    }

    [Fact]
    public async Task ReceiveBootstrapData_DoesNothing_WhenPayloadIsMalformed()
    {
        var cut = await RenderReadyLayoutAsync();

        await cut.InvokeAsync(() => cut.Instance.ReceiveBootstrapData("not-json"));

        Assert.DoesNotContain("LOGGED IN AS", cut.Markup);
    }

    [Fact]
    public async Task ReceiveAuthError_ShowsErrorBanner_WhenNoCurrentUser()
    {
        var cut = await RenderReadyLayoutAsync();

        await cut.InvokeAsync(() => cut.Instance.ReceiveAuthError("no tokens found"));

        Assert.Contains("ERROR: no tokens found", cut.Markup);
        Assert.Contains("[ LOG IN ]", cut.Markup);
    }

    [Fact]
    public async Task ClickingLogIn_RedirectsToLoginUrl_WhenNotInARedirectLoop()
    {
        var cut = await RenderReadyLayoutAsync();
        await cut.InvokeAsync(() => cut.Instance.ReceiveAuthError("no tokens found"));
        var navigation = Services.GetRequiredService<NavigationManager>();

        cut.Find("button.matrix-btn.primary.btn-sm").Click();

        Assert.Contains("nebula/app/redirect", navigation.Uri);
    }

    [Fact]
    public async Task ClickingLogIn_ShowsLoopMessage_WhenAlreadyOnNebulaPage()
    {
        var cut = await RenderReadyLayoutAsync();
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("http://localhost/nebula/app/callback");
        await cut.InvokeAsync(() => cut.Instance.ReceiveAuthError("no tokens found"));
        var uriBeforeClick = navigation.Uri;

        cut.Find("button.matrix-btn.primary.btn-sm").Click();

        Assert.Contains("Detected redirect loop", cut.Markup);
        Assert.Equal(uriBeforeClick, navigation.Uri);
    }
}
