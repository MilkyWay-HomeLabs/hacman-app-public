#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using hacman_app_back.Modules.Achievements.Api;
using hacman_app_back.Modules.Achievements.Application;
using hacman_app_back.Modules.Identity.Api;
using hacman_app_back.Modules.Identity.Application;
using hacman_app_back.Modules.Identity.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace hacman_app_back.Tests.Modules.Achievements.Api;

public class AchievementsControllerTest
{
    private static string? GetMessage(object? value)
        => value?.GetType().GetProperty("message")?.GetValue(value) as string;

    private static AchievementsController BuildController(
        Mock<IAchievementCatalogProvider>? catalogProviderMock = null,
        Mock<IAchievementProgressService>? progressServiceMock = null,
        IUserProvider? userProvider = null,
        IAuthClaimsParser? authClaimsParser = null)
        => new(
            (catalogProviderMock ?? new Mock<IAchievementCatalogProvider>()).Object,
            (progressServiceMock ?? new Mock<IAchievementProgressService>()).Object,
            userProvider ?? Mock.Of<IUserProvider>(),
            authClaimsParser ?? Mock.Of<IAuthClaimsParser>(),
            new Mock<ILogger<AchievementsController>>().Object);

    private static AchievementsController WithAuthenticatedUser(AchievementsController controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth")) }
        };
        return controller;
    }

    [Fact]
    public async Task GetCatalog_ReturnsOkWithCatalog_WhenProviderSucceeds()
    {
        var expected = new List<AchievementDefDto>
        {
            new() { Code = "COL-01", Category = "COLLECTION", Name = "Archivist", Thresholds = new[] { 10, 25, 50, 75, 100 } }
        };
        var providerMock = new Mock<IAchievementCatalogProvider>();
        providerMock.Setup(p => p.GetCatalogAsync()).ReturnsAsync(expected);

        var result = await BuildController(providerMock).GetCatalog() as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetCatalog_Returns500_WhenProviderThrowsException()
    {
        var providerMock = new Mock<IAchievementCatalogProvider>();
        providerMock.Setup(p => p.GetCatalogAsync()).ThrowsAsync(new Exception("db error"));

        var result = await BuildController(providerMock).GetCatalog() as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetMyProgress_ReturnsOkWithProgress_WhenServiceSucceeds()
    {
        var externalAuthId = "1000011";
        var user = new User { Id = Guid.NewGuid(), Username = "TestUser", ExternalAuthId = externalAuthId };
        var expected = new List<AchievementProgressDto>
        {
            new() { Code = "COL-01", Category = "COLLECTION", Name = "Archivist", Value = 5, Level = 1 }
        };

        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>())).Returns((externalAuthId, null));

        var userProviderMock = new Mock<IUserProvider>();
        userProviderMock.Setup(up => up.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync(user);

        var progressServiceMock = new Mock<IAchievementProgressService>();
        progressServiceMock.Setup(s => s.GetUserProgressAsync(user.Id)).ReturnsAsync(expected);

        var controller = WithAuthenticatedUser(
            BuildController(progressServiceMock: progressServiceMock, userProvider: userProviderMock.Object,
                authClaimsParser: authParserMock.Object));

        var result = await controller.GetMyProgress() as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task GetMyProgress_ReturnsUnauthorized_WhenAuthClaimsParserReturnsNullId()
    {
        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>())).Returns((null, null));

        var controller = WithAuthenticatedUser(BuildController(authClaimsParser: authParserMock.Object));

        var result = await controller.GetMyProgress() as UnauthorizedObjectResult;

        Assert.NotNull(result);
        Assert.Equal("Invalid token claims: sub is missing or invalid", GetMessage(result.Value));
    }

    [Fact]
    public async Task GetMyProgress_ReturnsUnauthorized_WhenUserNotFoundForAuthenticatedId()
    {
        var externalAuthId = "1000011";

        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>())).Returns((externalAuthId, null));

        var userProviderMock = new Mock<IUserProvider>();
        userProviderMock.Setup(up => up.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync((User?)null);

        var controller = WithAuthenticatedUser(
            BuildController(userProvider: userProviderMock.Object, authClaimsParser: authParserMock.Object));

        var result = await controller.GetMyProgress() as UnauthorizedObjectResult;

        Assert.NotNull(result);
        Assert.Equal("User is not mapped to this identity", GetMessage(result.Value));
    }

    [Fact]
    public async Task GetMyProgress_Returns500_WhenServiceThrowsException()
    {
        var externalAuthId = "1000011";
        var user = new User { Id = Guid.NewGuid(), Username = "TestUser", ExternalAuthId = externalAuthId };

        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>())).Returns((externalAuthId, null));

        var userProviderMock = new Mock<IUserProvider>();
        userProviderMock.Setup(up => up.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync(user);

        var progressServiceMock = new Mock<IAchievementProgressService>();
        progressServiceMock.Setup(s => s.GetUserProgressAsync(user.Id)).ThrowsAsync(new Exception("db error"));

        var controller = WithAuthenticatedUser(
            BuildController(progressServiceMock: progressServiceMock, userProvider: userProviderMock.Object,
                authClaimsParser: authParserMock.Object));

        var result = await controller.GetMyProgress() as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }
}
