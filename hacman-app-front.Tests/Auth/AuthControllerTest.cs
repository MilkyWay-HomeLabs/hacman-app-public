using System.Net;
using hacman_app_front.Auth;
using hacman_app_front.Tests.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace hacman_app_front.Tests.Auth;

public class AuthControllerTest
{
    private const string TokenEndpoint = "https://nebula.test.local/api/token";

    private static AuthController CreateController(
        FakeHttpMessageHandler handler, out DefaultHttpContext httpContext)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["NEBULA_TOKEN_ENDPOINT"] = TokenEndpoint,
            ["NEBULA_CLIENT_ID"] = "test-client",
            ["NEBULA_CLIENT_SECRET"] = "test-secret",
            ["ASPNETCORE_ENVIRONMENT"] = "Development",
            ["AppBasePath"] = "/hacman/app"
        }).Build();

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        httpContext = new DefaultHttpContext();
        return new AuthController(factoryMock.Object, config, NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static string SetCookieHeader(DefaultHttpContext httpContext)
        => string.Join("\n", httpContext.Response.Headers.SetCookie.ToArray());

    // --- Callback ---

    [Fact]
    public async Task Callback_ExchangesCodeForToken_SetsCookiesAndRedirects()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK,
            """{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600}""");
        var controller = CreateController(handler, out var httpContext);

        var result = await controller.Callback(code: "abc123", token: "", returnUrl: "/hacman/app/home");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/hacman/app/home", redirect.Url);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(TokenEndpoint, request.RequestUri!.ToString());
        var body = handler.RequestBodies[0]!;
        Assert.Contains("grant_type=authorization_code", body);
        Assert.Contains("code=abc123", body);
        Assert.Contains("client_id=test-client", body);
        Assert.Contains("client_secret=test-secret", body);

        var cookies = SetCookieHeader(httpContext);
        Assert.Contains("accessToken=new-access", cookies);
        Assert.Contains("refreshToken=new-refresh", cookies);
    }

    [Fact]
    public async Task Callback_UsesTokenFromQuery_WithoutCallingTokenEndpoint()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK);
        var controller = CreateController(handler, out var httpContext);

        var result = await controller.Callback(code: "", token: "direct-token", returnUrl: "");

        Assert.Empty(handler.Requests);
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/", redirect.Url);
        Assert.Contains("accessToken=direct-token", SetCookieHeader(httpContext));
    }

    [Fact]
    public async Task Callback_ReturnsBadRequest_WhenNeitherCodeNorTokenProvided()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK);
        var controller = CreateController(handler, out _);

        var result = await controller.Callback(code: "", token: "", returnUrl: "");

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Callback_PropagatesStatusCode_WhenTokenExchangeFails()
    {
        var handler = FakeHttpMessageHandler.Returning(
            HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
        var controller = CreateController(handler, out var httpContext);

        var result = await controller.Callback(code: "expired-code", token: "", returnUrl: "");

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, statusResult.StatusCode);
        Assert.DoesNotContain("accessToken", SetCookieHeader(httpContext));
    }

    // --- Refresh ---

    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenRefreshCookieIsMissing()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK);
        var controller = CreateController(handler, out _);

        var result = await controller.Refresh();

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Refresh_SetsNewAccessTokenCookie_WhenExchangeSucceeds()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK,
            """{"access_token":"rotated-access","refresh_token":"rotated-refresh","expires_in":1800}""");
        var controller = CreateController(handler, out var httpContext);
        httpContext.Request.Headers.Cookie = "refreshToken=old-refresh";

        var result = await controller.Refresh();

        Assert.IsType<OkObjectResult>(result);
        var body = handler.RequestBodies[0]!;
        Assert.Contains("grant_type=refresh_token", body);
        Assert.Contains("refresh_token=old-refresh", body);

        var cookies = SetCookieHeader(httpContext);
        Assert.Contains("accessToken=rotated-access", cookies);
        Assert.Contains("refreshToken=rotated-refresh", cookies);
    }

    [Fact]
    public async Task Refresh_Returns502_WhenResponseHasNoAccessToken()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, """{"unexpected":"shape"}""");
        var controller = CreateController(handler, out var httpContext);
        httpContext.Request.Headers.Cookie = "refreshToken=old-refresh";

        var result = await controller.Refresh();

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, statusResult.StatusCode);
    }

    [Fact]
    public async Task Refresh_DeletesAccessCookieAndPropagatesStatus_WhenExchangeFails()
    {
        var handler = FakeHttpMessageHandler.Returning(
            HttpStatusCode.Unauthorized, """{"error":"invalid_token"}""");
        var controller = CreateController(handler, out var httpContext);
        httpContext.Request.Headers.Cookie = "refreshToken=stale-refresh";

        var result = await controller.Refresh();

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(401, statusResult.StatusCode);
        // Cookies.Delete appends an expired, empty-value accessToken cookie.
        Assert.Contains("accessToken=;", SetCookieHeader(httpContext));
    }
}