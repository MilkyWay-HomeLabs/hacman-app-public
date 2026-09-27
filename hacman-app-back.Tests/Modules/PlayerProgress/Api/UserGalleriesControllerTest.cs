#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using hacman_app_back.Modules.GalleryCatalog.Domain;
using hacman_app_back.Modules.Identity.Api;
using hacman_app_back.Modules.Identity.Application;
using hacman_app_back.Modules.Identity.Domain;
using hacman_app_back.Modules.PlayerProgress.Api;
using hacman_app_back.Modules.PlayerProgress.Application;
using hacman_app_back.Modules.PlayerProgress.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace hacman_app_back.Tests.Modules.PlayerProgress.Api;

public class UserGalleriesControllerTest
{
    private static string? GetMessage(object? value)
        => value?.GetType().GetProperty("message")?.GetValue(value) as string;

    private static UserGalleriesController BuildController(
        IUserGalleryProvider provider,
        IUserProvider userProvider,
        IAuthClaimsParser authParser)
        => new(provider, userProvider, authParser,
            new Mock<ILogger<UserGalleriesController>>().Object);

    [Fact]
    public async Task GetUserGalleries_ReturnsOk_WhenAuthenticatedUserHasGalleries()
    {
        var externalAuthId = "1000011";
        var user = new User { Id = Guid.NewGuid(), ExternalAuthId = externalAuthId };
        var galleries = new List<UserGallery>
        {
            new() { Id = Guid.NewGuid(), UserId = user.Id, Gallery = new Gallery() }
        };

        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>()))
            .Returns((externalAuthId, null));

        var userProviderMock = new Mock<IUserProvider>();
        userProviderMock.Setup(up => up.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync(user);

        var providerMock = new Mock<IUserGalleryProvider>();
        providerMock.Setup(p => p.GetAllByUserIdAsync(user.Id)).ReturnsAsync(galleries);

        var controller = BuildController(providerMock.Object, userProviderMock.Object, authParserMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth")) }
        };

        var result = await controller.GetUserGalleries(null, null);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(galleries, okResult.Value);
    }

    [Fact]
    public async Task GetUserGalleries_ReturnsOk_WhenUserIdProvidedAsQueryParam()
    {
        var targetUserId = Guid.NewGuid();
        var galleries = new List<UserGallery>
        {
            new() { Id = Guid.NewGuid(), UserId = targetUserId, Gallery = new Gallery() }
        };

        var providerMock = new Mock<IUserGalleryProvider>();
        providerMock.Setup(p => p.GetAllByUserIdAsync(targetUserId)).ReturnsAsync(galleries);

        var controller = BuildController(providerMock.Object, Mock.Of<IUserProvider>(), Mock.Of<IAuthClaimsParser>());

        var result = await controller.GetUserGalleries(targetUserId, null);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(galleries, okResult.Value);
    }

    [Fact]
    public async Task GetUserGalleries_ReturnsOk_WhenGalleryIdProvidedAsQueryParam()
    {
        var targetGalleryId = Guid.NewGuid();
        var gallery = new UserGallery { Id = Guid.NewGuid(), Gallery = new Gallery { GalleryId = 13 } };

        var providerMock = new Mock<IUserGalleryProvider>();
        providerMock.Setup(p => p.GetByGalleryIdAsync(targetGalleryId)).ReturnsAsync(gallery);

        var controller = BuildController(providerMock.Object, Mock.Of<IUserProvider>(), Mock.Of<IAuthClaimsParser>());

        var result = await controller.GetUserGalleries(null, targetGalleryId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(gallery, okResult.Value);
    }

    [Fact]
    public async Task GetUserGalleries_ReturnsUnauthorized_WhenAuthClaimsParserReturnsNullId()
    {
        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>()))
            .Returns((null, null));

        var controller = BuildController(
            Mock.Of<IUserGalleryProvider>(), Mock.Of<IUserProvider>(), authParserMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth")) }
        };

        var result = await controller.GetUserGalleries(null, null);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid token claims: sub is missing or invalid", GetMessage(unauthorizedResult.Value));
    }

    [Fact]
    public async Task GetUserGalleries_ReturnsUnauthorized_WhenAuthClaimsParserReturnsEmptyId()
    {
        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>()))
            .Returns(("", null));

        var controller = BuildController(
            Mock.Of<IUserGalleryProvider>(), Mock.Of<IUserProvider>(), authParserMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth")) }
        };

        var result = await controller.GetUserGalleries(null, null);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("Invalid token claims: sub is missing or invalid", GetMessage(unauthorizedResult.Value));
    }

    [Fact]
    public async Task GetUserGalleries_ReturnsUnauthorized_WhenUserNotFoundForAuthenticatedId()
    {
        var externalAuthId = "1000011";

        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>()))
            .Returns((externalAuthId, null));

        var userProviderMock = new Mock<IUserProvider>();
        userProviderMock.Setup(up => up.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync((User?)null);

        var controller =
            BuildController(Mock.Of<IUserGalleryProvider>(), userProviderMock.Object, authParserMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth")) }
        };

        var result = await controller.GetUserGalleries(null, null);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal("User is not mapped to this identity", GetMessage(unauthorizedResult.Value));
    }

    [Fact]
    public async Task GetUserGalleries_ReturnsOkWithEmptyList_WhenUserHasNoGalleries()
    {
        var externalAuthId = "1000011";
        var user = new User { Id = Guid.NewGuid(), ExternalAuthId = externalAuthId };

        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>()))
            .Returns((externalAuthId, null));

        var userProviderMock = new Mock<IUserProvider>();
        userProviderMock.Setup(up => up.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync(user);

        var providerMock = new Mock<IUserGalleryProvider>();
        providerMock.Setup(p => p.GetAllByUserIdAsync(user.Id)).ReturnsAsync(new List<UserGallery>());

        var controller = BuildController(providerMock.Object, userProviderMock.Object, authParserMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth")) }
        };

        var result = await controller.GetUserGalleries(null, null);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Empty((IEnumerable<UserGallery>)okResult.Value!);
    }
}