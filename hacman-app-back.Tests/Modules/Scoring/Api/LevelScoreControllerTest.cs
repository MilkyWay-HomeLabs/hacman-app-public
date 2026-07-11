#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using hacman_app_back.Modules.Identity.Api;
using hacman_app_back.Modules.Identity.Application;
using hacman_app_back.Modules.Identity.Domain;
using hacman_app_back.Modules.Scoring.Api;
using hacman_app_back.Modules.Scoring.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace hacman_app_back.Tests.Modules.Scoring.Api;

public class LevelScoreControllerTest
{
    private static string? GetMessage(object? value)
        => value?.GetType().GetProperty("message")?.GetValue(value) as string;

    private static LevelScoreController BuildController(
        Mock<ILevelScoreService> serviceMock,
        IUserProvider? userProvider = null,
        IAuthClaimsParser? authClaimsParser = null)
        => new(serviceMock.Object, userProvider ?? Mock.Of<IUserProvider>(),
            authClaimsParser ?? Mock.Of<IAuthClaimsParser>(), new Mock<ILogger<LevelScoreController>>().Object);

    private static LevelScoreController WithAuthenticatedUser(LevelScoreController controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth")) }
        };
        return controller;
    }

    [Fact]
    public async Task GetScores_ReturnsOkWithScores_WhenServiceSucceeds()
    {
        var galleryId = Guid.NewGuid();
        var expectedScores = new List<ScoreEntryDto>
        {
            new() { Difficulty = "EASY", Username = "TestUser", TimeMs = 1000, AchievedAt = DateTime.UtcNow }
        };

        var serviceMock = new Mock<ILevelScoreService>();
        serviceMock.Setup(s => s.GetScores(galleryId, "EASY", 1, 10)).ReturnsAsync(expectedScores);

        var result = await BuildController(serviceMock).GetScores(galleryId, "EASY", 1, 10) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expectedScores, result.Value);
    }

    [Fact]
    public async Task GetScores_Returns500_WhenServiceThrowsException()
    {
        var galleryId = Guid.NewGuid();
        var serviceMock = new Mock<ILevelScoreService>();
        serviceMock.Setup(s => s.GetScores(galleryId, null, 1, 10)).ThrowsAsync(new Exception("db error"));

        var result = await BuildController(serviceMock).GetScores(galleryId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task GetScoresByUserId_ReturnsOkWithScores_WhenServiceSucceeds()
    {
        var userId = Guid.NewGuid();
        var galleryId = Guid.NewGuid();
        var expectedScores = new List<ScoreEntryDto>
        {
            new() { Username = "TestUser", TimeMs = 1500, AchievedAt = DateTime.UtcNow, Difficulty = "EASY" }
        };

        var serviceMock = new Mock<ILevelScoreService>();
        serviceMock.Setup(s => s.GetScoresByUserId(userId, galleryId, "EASY")).ReturnsAsync(expectedScores);

        var result = await BuildController(serviceMock).GetScoresByUserId(userId, galleryId, "EASY") as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(expectedScores, result.Value);
    }

    [Fact]
    public async Task GetScoresByUserId_ReturnsBadRequest_WhenUserIdIsEmpty()
    {
        var serviceMock = new Mock<ILevelScoreService>();

        var result = await BuildController(serviceMock).GetScoresByUserId(Guid.Empty) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("User ID cannot be empty.", result.Value);
    }

    [Fact]
    public async Task GetScoresByUserId_Returns500_WhenServiceThrowsException()
    {
        var userId = Guid.NewGuid();
        var serviceMock = new Mock<ILevelScoreService>();
        serviceMock.Setup(s => s.GetScoresByUserId(userId, null, null)).ThrowsAsync(new Exception("db error"));

        var result = await BuildController(serviceMock).GetScoresByUserId(userId) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsCreated_WhenRequestIsValid()
    {
        var externalAuthId = "1000011";
        var user = new User { Id = Guid.NewGuid(), Username = "TestUser", ExternalAuthId = externalAuthId };
        var request = new CreateLevelScoreRequest { GalleryId = Guid.NewGuid(), Difficulty = "HARD", TimeMs = 4200 };
        var createdScore = new ScoreEntryDto
            { Username = user.Username, Difficulty = "HARD", TimeMs = 4200, AchievedAt = DateTime.UtcNow };

        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>())).Returns((externalAuthId, null));

        var userProviderMock = new Mock<IUserProvider>();
        userProviderMock.Setup(up => up.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync(user);

        var serviceMock = new Mock<ILevelScoreService>();
        serviceMock.Setup(s =>
                s.CreateScore(user.Id, user.Username, request.GalleryId, request.Difficulty, request.TimeMs))
            .ReturnsAsync(createdScore);

        var controller =
            WithAuthenticatedUser(BuildController(serviceMock, userProviderMock.Object, authParserMock.Object));

        var result = await controller.Create(request) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(201, result.StatusCode);
        Assert.Same(createdScore, result.Value);
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenAuthClaimsParserReturnsNullId()
    {
        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>())).Returns((null, null));

        var serviceMock = new Mock<ILevelScoreService>();
        var controller = WithAuthenticatedUser(BuildController(serviceMock, authClaimsParser: authParserMock.Object));
        var request = new CreateLevelScoreRequest { GalleryId = Guid.NewGuid(), Difficulty = "EASY", TimeMs = 1000 };

        var result = await controller.Create(request) as UnauthorizedObjectResult;

        Assert.NotNull(result);
        Assert.Equal("Invalid token claims: sub is missing or invalid", GetMessage(result.Value));
    }

    [Fact]
    public async Task Create_ReturnsUnauthorized_WhenUserNotFoundForAuthenticatedId()
    {
        var externalAuthId = "1000011";

        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>())).Returns((externalAuthId, null));

        var userProviderMock = new Mock<IUserProvider>();
        userProviderMock.Setup(up => up.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync((User?)null);

        var serviceMock = new Mock<ILevelScoreService>();
        var controller = WithAuthenticatedUser(
            BuildController(serviceMock, userProviderMock.Object, authParserMock.Object));
        var request = new CreateLevelScoreRequest { GalleryId = Guid.NewGuid(), Difficulty = "EASY", TimeMs = 1000 };

        var result = await controller.Create(request) as UnauthorizedObjectResult;

        Assert.NotNull(result);
        Assert.Equal("User is not mapped to this identity", GetMessage(result.Value));
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenGalleryIdIsEmpty()
    {
        var externalAuthId = "1000011";
        var user = new User { Id = Guid.NewGuid(), Username = "TestUser", ExternalAuthId = externalAuthId };

        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>())).Returns((externalAuthId, null));

        var userProviderMock = new Mock<IUserProvider>();
        userProviderMock.Setup(up => up.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync(user);

        var serviceMock = new Mock<ILevelScoreService>();
        var controller = WithAuthenticatedUser(
            BuildController(serviceMock, userProviderMock.Object, authParserMock.Object));
        var request = new CreateLevelScoreRequest { GalleryId = Guid.Empty, Difficulty = "EASY", TimeMs = 1000 };

        var result = await controller.Create(request) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal("GalleryId is required", GetMessage(result.Value));
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenDifficultyIsMissing()
    {
        var externalAuthId = "1000011";
        var user = new User { Id = Guid.NewGuid(), Username = "TestUser", ExternalAuthId = externalAuthId };

        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>())).Returns((externalAuthId, null));

        var userProviderMock = new Mock<IUserProvider>();
        userProviderMock.Setup(up => up.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync(user);

        var serviceMock = new Mock<ILevelScoreService>();
        var controller = WithAuthenticatedUser(
            BuildController(serviceMock, userProviderMock.Object, authParserMock.Object));
        var request = new CreateLevelScoreRequest { GalleryId = Guid.NewGuid(), Difficulty = "  ", TimeMs = 1000 };

        var result = await controller.Create(request) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal("Difficulty is required", GetMessage(result.Value));
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenTimeMsIsNotPositive()
    {
        var externalAuthId = "1000011";
        var user = new User { Id = Guid.NewGuid(), Username = "TestUser", ExternalAuthId = externalAuthId };

        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>())).Returns((externalAuthId, null));

        var userProviderMock = new Mock<IUserProvider>();
        userProviderMock.Setup(up => up.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync(user);

        var serviceMock = new Mock<ILevelScoreService>();
        var controller = WithAuthenticatedUser(
            BuildController(serviceMock, userProviderMock.Object, authParserMock.Object));
        var request = new CreateLevelScoreRequest { GalleryId = Guid.NewGuid(), Difficulty = "EASY", TimeMs = 0 };

        var result = await controller.Create(request) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal("TimeMs must be positive", GetMessage(result.Value));
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenServiceThrowsArgumentException()
    {
        var externalAuthId = "1000011";
        var user = new User { Id = Guid.NewGuid(), Username = "TestUser", ExternalAuthId = externalAuthId };
        var request = new CreateLevelScoreRequest { GalleryId = Guid.NewGuid(), Difficulty = "INVALID", TimeMs = 1000 };

        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>())).Returns((externalAuthId, null));

        var userProviderMock = new Mock<IUserProvider>();
        userProviderMock.Setup(up => up.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync(user);

        var serviceMock = new Mock<ILevelScoreService>();
        serviceMock.Setup(s =>
                s.CreateScore(user.Id, user.Username, request.GalleryId, request.Difficulty, request.TimeMs))
            .ThrowsAsync(new ArgumentException("Invalid difficulty: 'INVALID'."));

        var controller =
            WithAuthenticatedUser(BuildController(serviceMock, userProviderMock.Object, authParserMock.Object));

        var result = await controller.Create(request) as BadRequestObjectResult;

        Assert.NotNull(result);
        Assert.Equal("Invalid difficulty: 'INVALID'.", GetMessage(result.Value));
    }

    [Fact]
    public async Task Create_Returns500_WhenServiceThrowsException()
    {
        var externalAuthId = "1000011";
        var user = new User { Id = Guid.NewGuid(), Username = "TestUser", ExternalAuthId = externalAuthId };
        var request = new CreateLevelScoreRequest { GalleryId = Guid.NewGuid(), Difficulty = "EASY", TimeMs = 1000 };

        var authParserMock = new Mock<IAuthClaimsParser>();
        authParserMock.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>())).Returns((externalAuthId, null));

        var userProviderMock = new Mock<IUserProvider>();
        userProviderMock.Setup(up => up.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync(user);

        var serviceMock = new Mock<ILevelScoreService>();
        serviceMock.Setup(s =>
                s.CreateScore(user.Id, user.Username, request.GalleryId, request.Difficulty, request.TimeMs))
            .ThrowsAsync(new Exception("db error"));

        var controller =
            WithAuthenticatedUser(BuildController(serviceMock, userProviderMock.Object, authParserMock.Object));

        var result = await controller.Create(request) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(500, result.StatusCode);
    }
}