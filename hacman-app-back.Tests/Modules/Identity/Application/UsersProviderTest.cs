using System;
using System.Threading.Tasks;
using hacman_app_back.Modules.Identity.Application;
using hacman_app_back.Modules.Identity.Domain;
using hacman_app_back.Modules.Identity.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace hacman_app_back.Tests.Modules.Identity.Application;

[TestClass]
public class UsersProviderTest
{
    [TestMethod]
    public async Task GetUserByGuid_ReturnsUser_WhenRepositoryReturnsData()
    {
        var mockRepository = new Mock<IUserRepository>();
        var guid = Guid.Parse("a1b2c3d4-e5f6-7890-1234-567890abcdef");
        var expectedUser = new User
        {
            Id = guid,
            ExternalAuthId = "1000011",
            Username = "xbsmvuzfayyzjxdxak",
            Email = "xbsmvuzfayyzjxdxak@ckptr.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        mockRepository.Setup(repo => repo.GetByIdAsync(guid)).ReturnsAsync(expectedUser);

        var service = new UserProvider(mockRepository.Object);

        var result = await service.GetByIdAsync(guid);

        Assert.IsNotNull(result);
        Assert.AreEqual(expectedUser.Id, result.Id);
        Assert.AreEqual(expectedUser.ExternalAuthId, result.ExternalAuthId);
        Assert.AreEqual(expectedUser.Username, result.Username);
        Assert.AreEqual(expectedUser.Email, result.Email);
        mockRepository.Verify(repo => repo.GetByIdAsync(guid), Times.Once);
    }

    [TestMethod]
    public async Task GetUserByGuid_ReturnsNull_WhenRepositoryReturnsNull()
    {
        var mockRepository = new Mock<IUserRepository>();
        var guid = Guid.NewGuid();
        mockRepository.Setup(repo => repo.GetByIdAsync(guid)).ReturnsAsync((User)null);

        var service = new UserProvider(mockRepository.Object);

        var result = await service.GetByIdAsync(guid);

        Assert.IsNull(result);
        mockRepository.Verify(repo => repo.GetByIdAsync(guid), Times.Once);
    }

    [TestMethod]
    public void Constructor_ThrowsArgumentNullException_WhenRepositoryIsNull()
    {
        try
        {
            new UserProvider(null);
            Assert.Fail("Expected ArgumentNullException was not thrown.");
        }
        catch (ArgumentNullException)
        {
            // Test passes
        }
    }

    [TestMethod]
    public async Task GetUserByExternalAuthId_ReturnsUser_WhenRepositoryReturnsData()
    {
        var mockRepository = new Mock<IUserRepository>();
        var guid = Guid.Parse("a1b2c3d4-e5f6-7890-1234-567890abcdef");
        var externalAuthId = "1000011";
        var expectedUser = new User
        {
            Id = guid,
            ExternalAuthId = "1000011",
            Username = "xbsmvuzfayyzjxdxak",
            Email = "xbsmvuzfayyzjxdxak@ckptr.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        mockRepository.Setup(repo => repo.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync(expectedUser);

        var service = new UserProvider(mockRepository.Object);

        var result = await service.GetByExternalAuthIdAsync(externalAuthId);

        Assert.IsNotNull(result);
        Assert.AreEqual(expectedUser.Id, result.Id);
        Assert.AreEqual(expectedUser.ExternalAuthId, result.ExternalAuthId);
        Assert.AreEqual(expectedUser.Username, result.Username);
        Assert.AreEqual(expectedUser.Email, result.Email);
        mockRepository.Verify(repo => repo.GetByExternalAuthIdAsync(externalAuthId), Times.Once);
    }

    [TestMethod]
    public async Task GetUserByExternalAuthId_ReturnsNull_WhenDataNotFoundRepository()
    {
        var mockRepository = new Mock<IUserRepository>();
        var externalAuthId = "non_existing_id";

        mockRepository.Setup(repo => repo.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync((User)null);

        var service = new UserProvider(mockRepository.Object);

        var result = await service.GetByExternalAuthIdAsync(externalAuthId);

        Assert.IsNull(result);
        mockRepository.Verify(repo => repo.GetByExternalAuthIdAsync(externalAuthId), Times.Once);
    }


    [TestMethod]
    public async Task GetUserByExternalAuthId_ReturnsNull_WhenRepositoryReturnsNull()
    {
        var mockRepository = new Mock<IUserRepository>();
        string externalAuthId = null;
        mockRepository.Setup(repo => repo.GetByExternalAuthIdAsync(externalAuthId)).ReturnsAsync((User)null);

        var service = new UserProvider(mockRepository.Object);

        var result = await service.GetByExternalAuthIdAsync(null);

        Assert.IsNull(result);
        mockRepository.Verify(repo => repo.GetByExternalAuthIdAsync(externalAuthId), Times.Once);
    }
}