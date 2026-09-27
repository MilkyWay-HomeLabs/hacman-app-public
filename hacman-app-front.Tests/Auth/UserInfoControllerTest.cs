using System.Text.Json;
using hacman_app_front.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace hacman_app_front.Tests.Auth;

public class UserInfoControllerTest
{
    private static UserInfoController CreateController(string? accessToken = null)
    {
        var httpContext = new DefaultHttpContext();
        if (accessToken != null)
        {
            httpContext.Request.Headers.Cookie = $"accessToken={accessToken}";
        }

        return new UserInfoController(NullLogger<UserInfoController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    /// <summary>
    /// Builds an unsigned JWT-shaped token (header.payload.fakesignature) — the controller only
    /// base64url-decodes the payload, it never validates the signature.
    /// </summary>
    private static string CreateJwt(object payload)
    {
        static string B64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var header = B64Url("""{"alg":"HS256","typ":"JWT"}"""u8.ToArray());
        var body = B64Url(JsonSerializer.SerializeToUtf8Bytes(payload));
        return $"{header}.{body}.fakesignature";
    }

    // --- Bootstrap ---

    [Fact]
    public async Task Bootstrap_ReturnsUnauthorized_WhenAccessTokenCookieIsMissing()
    {
        var controller = CreateController();

        var result = await controller.Bootstrap();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Bootstrap_Returns502_WhenTokenIsMalformed()
    {
        var controller = CreateController(accessToken: "not-a-jwt-at-all");

        var result = await controller.Bootstrap();

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, statusResult.StatusCode);
    }

    [Fact]
    public async Task Bootstrap_ReturnsNebulaUserData_WhenTokenIsValid()
    {
        var token = CreateJwt(new
        {
            sub = "1000011",
            email = "neo@zion.io",
            preferred_username = "neo",
            name = "Neo Anderson"
        });
        var controller = CreateController(accessToken: token);

        var result = await controller.Bootstrap();

        var ok = Assert.IsType<OkObjectResult>(result);
        var bootstrap = Assert.IsType<BootstrapResponse>(ok.Value);
        Assert.NotNull(bootstrap.UserData);
        Assert.Equal(1000011L, bootstrap.UserData.Id);
        Assert.Equal("neo", bootstrap.UserData.Login);
        Assert.Equal("Neo Anderson", bootstrap.UserData.UserName);
        Assert.Equal("neo@zion.io", bootstrap.UserData.Email);
        // Hacman user/galleries are resolved client-side by app.js — bootstrap always returns null here.
        Assert.Null(bootstrap.HacmanData);
    }

    [Fact]
    public async Task Bootstrap_FallsBackToEmailPrefixForLogin_WhenNoUsernameClaimsPresent()
    {
        var token = CreateJwt(new { sub = "1000012", email = "trinity@zion.io" });
        var controller = CreateController(accessToken: token);

        var result = await controller.Bootstrap();

        var ok = Assert.IsType<OkObjectResult>(result);
        var bootstrap = Assert.IsType<BootstrapResponse>(ok.Value);
        Assert.Equal("trinity", bootstrap.UserData!.Login);
    }

    [Fact]
    public async Task Bootstrap_LeavesNumericIdNull_WhenSubIsCompositeAuthIdEmailFormat()
    {
        // Nebula's real sub claim format is "authId,email" (see AuthClaimsParser on the backend) —
        // it doesn't parse as a long, so Id stays null and Login falls back through the claim chain.
        var token = CreateJwt(new { sub = "1000013,morpheus@zion.io", email = "morpheus@zion.io" });
        var controller = CreateController(accessToken: token);

        var result = await controller.Bootstrap();

        var ok = Assert.IsType<OkObjectResult>(result);
        var bootstrap = Assert.IsType<BootstrapResponse>(ok.Value);
        Assert.Null(bootstrap.UserData!.Id);
        Assert.Equal("morpheus", bootstrap.UserData.Login);
    }

    // --- Me ---

    [Fact]
    public async Task Me_ReturnsUnauthorized_WhenAccessTokenCookieIsMissing()
    {
        var controller = CreateController();

        var result = await controller.Me();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Me_Returns502_WhenTokenIsMalformed()
    {
        var controller = CreateController(accessToken: "x.y");

        var result = await controller.Me();

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(502, statusResult.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsOkWithNullBody_WhenTokenIsValid()
    {
        // Current (odd but intentional) behavior: Me returns bootstrap.HacmanData, which is
        // always null since Hacman data moved client-side — so a valid token yields 200 + null.
        var token = CreateJwt(new { sub = "1000011", email = "neo@zion.io" });
        var controller = CreateController(accessToken: token);

        var result = await controller.Me();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Null(ok.Value);
    }
}