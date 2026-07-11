using hacman_app_front.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace hacman_app_front.Tests.Services;

public class HacmanApiClientFactoryTest
{
    private static readonly Uri ApiUri = new("https://api.test.local/");

    private static HacmanApiClientFactory CreateFactory(
        UserSessionState sessionState, string? requestCookieToken = null, bool trustAnyServerCertificate = false)
    {
        var accessor = new HttpContextAccessor();
        if (requestCookieToken != null)
        {
            var context = new DefaultHttpContext();
            context.Request.Headers.Cookie = $"accessToken={requestCookieToken}";
            accessor.HttpContext = context;
        }

        return new HacmanApiClientFactory(
            accessor, sessionState, NullLogger<HacmanApiClientFactory>.Instance, ApiUri,
            trustAnyServerCertificate);
    }

    [Fact]
    public void ResolveAccessToken_PrefersHttpContextCookie_AndPersistsItToSessionState()
    {
        var sessionState = new UserSessionState { AccessToken = "stale-circuit-token" };
        var factory = CreateFactory(sessionState, requestCookieToken: "fresh-cookie-token");

        var token = factory.ResolveAccessToken();

        Assert.Equal("fresh-cookie-token", token);
        Assert.Equal("fresh-cookie-token", sessionState.AccessToken);
    }

    [Fact]
    public void ResolveAccessToken_FallsBackToSessionState_WhenNoHttpContext()
    {
        var sessionState = new UserSessionState { AccessToken = "circuit-token" };
        var factory = CreateFactory(sessionState);

        var token = factory.ResolveAccessToken();

        Assert.Equal("circuit-token", token);
    }

    [Fact]
    public void ResolveAccessToken_ReturnsNull_WhenNoCookieAndNoSessionToken()
    {
        var sessionState = new UserSessionState();
        var factory = CreateFactory(sessionState);

        var token = factory.ResolveAccessToken();

        Assert.Null(token);
        Assert.Null(sessionState.AccessToken);
    }

    [Fact]
    public void CreateClient_UsesApiBaseUriAndAcceptsJson()
    {
        var factory = CreateFactory(new UserSessionState());

        using var client = factory.CreateClient();

        Assert.Equal(ApiUri, client.BaseAddress);
        Assert.Contains(client.DefaultRequestHeaders.Accept, h => h.MediaType == "application/json");
        Assert.Equal(TimeSpan.FromSeconds(30), client.Timeout);
    }

    [Fact]
    public void CreateHandler_KeepsFullCertificateValidation_ByDefault()
    {
        var factory = CreateFactory(new UserSessionState(), trustAnyServerCertificate: false);

        using var handler = factory.CreateHandler(token: null);

        Assert.Null(handler.ServerCertificateCustomValidationCallback);
    }

    [Fact]
    public void CreateHandler_RelaxesCertificateValidation_OnlyWhenOptedIn()
    {
        var factory = CreateFactory(new UserSessionState(), trustAnyServerCertificate: true);

        using var handler = factory.CreateHandler(token: null);

        Assert.NotNull(handler.ServerCertificateCustomValidationCallback);
    }
}