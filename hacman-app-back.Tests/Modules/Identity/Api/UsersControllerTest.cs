using System;
using System.Security.Claims;
using System.Threading.Tasks;
using hacman_app_back.Modules.Identity.Api;
using hacman_app_back.Modules.Identity.Application;
using hacman_app_back.Modules.Identity.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace hacman_app_back.Tests.Modules.Identity.Api;

[TestClass]
public class UsersControllerTest
{
    private Mock<IUserProvider> _mockProvider;
    private Mock<IUserUpdater> _mockUpdater;
    private Mock<IAuthClaimsParser> _mockAuthClaimsParser;
    private Mock<ILogger<UsersController>> _mockLogger;

    [TestInitialize]
    public void Setup()
    {
        _mockProvider = new Mock<IUserProvider>();
        _mockUpdater = new Mock<IUserUpdater>();
        _mockAuthClaimsParser = new Mock<IAuthClaimsParser>();
        _mockLogger = new Mock<ILogger<UsersController>>();
    }

    private UsersController BuildController()
        => new(_mockProvider.Object, _mockUpdater.Object, _mockAuthClaimsParser.Object, _mockLogger.Object);

    // -------------------------------------------------------------------
    // GET /api/v1/users/{id}
    // -------------------------------------------------------------------

    [TestMethod]
    public async Task Get_ReturnsOk_WhenUserExists()
    {
        var id = Guid.NewGuid();
        var expected = new User
        {
            Id = id,
            ExternalAuthId = "test_external_auth_id",
            Username = "test_username",
            Email = "test_email@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _mockProvider.Setup(s => s.GetByIdAsync(id)).ReturnsAsync(expected);

        var actionResult = await BuildController().Get(id);
        var okResult = actionResult.Result as OkObjectResult;

        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);

        var value = okResult.Value as User;
        Assert.IsNotNull(value);
        Assert.AreEqual(expected.Id, value.Id);
        Assert.AreEqual(expected.Username, value.Username);
        Assert.AreEqual(expected.Email, value.Email);
        _mockProvider.Verify(s => s.GetByIdAsync(id), Times.Once);
    }

    [TestMethod]
    public async Task Get_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var id = Guid.NewGuid();
        _mockProvider.Setup(s => s.GetByIdAsync(id)).ReturnsAsync((User)null);

        var actionResult = await BuildController().Get(id);
        var notFoundResult = actionResult.Result as NotFoundResult;

        Assert.IsNotNull(notFoundResult);
        Assert.AreEqual(404, notFoundResult.StatusCode);
        _mockProvider.Verify(s => s.GetByIdAsync(id), Times.Once);
    }

    // -------------------------------------------------------------------
    // GET /api/v1/users/external-auth-id
    // -------------------------------------------------------------------

    [TestMethod]
    public async Task GetByExternalAuthId_ReturnsOk_WhenClaimsValidAndUserExists()
    {
        var externalAuthId = "auth0|123456";
        var expectedUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalAuthId = externalAuthId,
            Username = "test_user",
            Email = "test@example.com"
        };

        _mockAuthClaimsParser.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>()))
            .Returns((externalAuthId, "test@example.com"));
        _mockProvider.Setup(s => s.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync(expectedUser);

        var controller = BuildController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth")) }
        };

        var result = await controller.GetByExternalAuthId() as OkObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(200, result.StatusCode);
        var returned = result.Value as User;
        Assert.IsNotNull(returned);
        Assert.AreEqual(expectedUser.Id, returned.Id);
        _mockProvider.Verify(s => s.GetByExternalAuthIdAsync(externalAuthId), Times.Once);
    }

    [TestMethod]
    public async Task GetByExternalAuthId_ReturnsNotFound_WhenUserDoesNotExist()
    {
        var externalAuthId = "auth0|nonexistent";

        _mockAuthClaimsParser.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>()))
            .Returns((externalAuthId, null));
        _mockProvider.Setup(s => s.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync((User)null);

        var controller = BuildController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("TestAuth")) }
        };

        var result = await controller.GetByExternalAuthId() as NotFoundResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(404, result.StatusCode);
        _mockProvider.Verify(s => s.GetByExternalAuthIdAsync(externalAuthId), Times.Once);
    }

    [TestMethod]
    public async Task GetByExternalAuthId_ReturnsUnauthorized_WhenAuthClaimsParserReturnsNullId()
    {
        _mockAuthClaimsParser.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>()))
            .Returns((null, null));

        var controller = BuildController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var result = await controller.GetByExternalAuthId() as UnauthorizedObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(401, result.StatusCode);
        _mockProvider.Verify(s => s.GetByExternalAuthIdAsync(It.IsAny<string>()), Times.Never);
    }

    // -------------------------------------------------------------------
    // POST /api/v1/users
    // -------------------------------------------------------------------

    [TestMethod]
    public async Task Create_ReturnsCreated_WhenRequestIsValid()
    {
        var externalAuthId = "12345";
        var email = "test@example.com";
        var request = new CreateUserRequest { Username = "testuser" };
        var createdUser = new User { Id = Guid.NewGuid(), Username = "testuser", Email = email };

        _mockAuthClaimsParser.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>()))
            .Returns((externalAuthId, email));
        _mockUpdater.Setup(u => u.CreateNewUserData(externalAuthId, request.Username, email))
            .ReturnsAsync(createdUser);

        var controller = BuildController();
        var result = await controller.Create(request) as CreatedAtActionResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(nameof(controller.Get), result.ActionName);
        Assert.AreEqual(createdUser, result.Value);
    }

    [TestMethod]
    public async Task Create_ReturnsUnauthorized_WhenClaimsAreInvalid()
    {
        _mockAuthClaimsParser.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>()))
            .Returns((null, null));

        var result =
            await BuildController().Create(new CreateUserRequest { Username = "testuser" }) as UnauthorizedObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(401, result.StatusCode);
    }

    [TestMethod]
    public async Task Create_ReturnsBadRequest_WhenUsernameIsEmpty()
    {
        _mockAuthClaimsParser.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>()))
            .Returns(("12345", "test@example.com"));

        var result = await BuildController().Create(new CreateUserRequest { Username = "" }) as BadRequestObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(400, result.StatusCode);
    }

    [TestMethod]
    public async Task Create_ReturnsBadRequest_WhenUpdaterThrowsInvalidOperationException()
    {
        var externalAuthId = "12345";
        var email = "test@example.com";
        var request = new CreateUserRequest { Username = "testuser" };

        _mockAuthClaimsParser.Setup(p => p.ParseClaims(It.IsAny<ClaimsPrincipal>()))
            .Returns((externalAuthId, email));
        _mockUpdater.Setup(u => u.CreateNewUserData(externalAuthId, request.Username, email))
            .ThrowsAsync(new InvalidOperationException("User already exists"));

        var result = await BuildController().Create(request) as BadRequestObjectResult;

        Assert.IsNotNull(result);
        Assert.AreEqual(400, result.StatusCode);

        var message = result.Value?.GetType().GetProperty("message")?.GetValue(result.Value) as string;
        Assert.AreEqual("User already exists", message);
    }
}