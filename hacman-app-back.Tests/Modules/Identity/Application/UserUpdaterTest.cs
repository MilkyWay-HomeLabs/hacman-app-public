using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using hacman_app_back.Modules.GalleryCatalog.Infrastructure;
using hacman_app_back.Modules.Identity.Application;
using hacman_app_back.Modules.Identity.Domain;
using hacman_app_back.Modules.Identity.Infrastructure;
using hacman_app_back.Modules.PlayerProgress.Domain;
using hacman_app_back.Modules.PlayerProgress.Infrastructure;
using hacman_app_back.SharedKernel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Npgsql;

namespace hacman_app_back.Tests.Modules.Identity.Application;

[TestClass]
public class UserUpdaterTest
{
    private Mock<IUserRepository> _mockUserRepository;
    private Mock<IGalleryRepository> _mockGalleryRepository;
    private Mock<IUserGalleryRepository> _mockUserGalleryRepository;
    private Mock<IUnitOfWork> _mockUnitOfWork;
    private UserUpdater _userUpdater;

    [TestInitialize]
    public void Setup()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockGalleryRepository = new Mock<IGalleryRepository>();
        _mockUserGalleryRepository = new Mock<IUserGalleryRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockUnitOfWork.Setup(u => u.BeginAsync()).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.CommitAsync()).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.RollbackAsync()).Returns(Task.CompletedTask);
        _mockUnitOfWork.Setup(u => u.DisposeAsync()).Returns(ValueTask.CompletedTask);

        _userUpdater = new UserUpdater(
            _mockUserRepository.Object,
            _mockGalleryRepository.Object,
            _mockUserGalleryRepository.Object,
            () => _mockUnitOfWork.Object);
    }

    [TestMethod]
    public async Task CreateNewUserData_CreatesAndReturnsUser_WhenDataIsValid()
    {
        var externalAuthId = "auth0|123456";
        var username = "testuser";
        var email = "test@example.com";
        var galleryIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        _mockUserRepository.Setup(r => r.GetByExternalAuthIdAsync(externalAuthId))
            .ReturnsAsync((User)null);
        _mockUserRepository.Setup(r =>
                r.CreateAsync(It.IsAny<User>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((User u, NpgsqlConnection _, NpgsqlTransaction _) => u);
        _mockGalleryRepository.Setup(r =>
                r.GetAllGalleryIdsAsync(It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync(galleryIds);
        _mockUserGalleryRepository.Setup(r =>
                r.CreateAsync(It.IsAny<UserGallery>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((UserGallery ug, NpgsqlConnection _, NpgsqlTransaction _) => ug);

        var result = await _userUpdater.CreateNewUserData(externalAuthId, username, email);

        Assert.IsNotNull(result);
        Assert.AreEqual(externalAuthId, result.ExternalAuthId);
        Assert.AreEqual(username, result.Username);
        Assert.AreEqual(email, result.Email);
        Assert.AreNotEqual(Guid.Empty, result.Id);
        _mockUserRepository.Verify(r => r.GetByExternalAuthIdAsync(externalAuthId), Times.Once);
        _mockUserRepository.Verify(
            r => r.CreateAsync(It.IsAny<User>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()),
            Times.Once);
        _mockGalleryRepository.Verify(
            r => r.GetAllGalleryIdsAsync(It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()), Times.Once);
        _mockUserGalleryRepository.Verify(
            r => r.CreateAsync(It.IsAny<UserGallery>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()),
            Times.Exactly(galleryIds.Count));
        _mockUnitOfWork.Verify(u => u.BeginAsync(), Times.Once);
        _mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Once);
        _mockUnitOfWork.Verify(u => u.RollbackAsync(), Times.Never);
    }

    [TestMethod]
    public async Task CreateNewUserData_ThrowsInvalidOperationException_WhenUserAlreadyExists()
    {
        var externalAuthId = "auth0|existing";
        var username = "testuser";
        var email = "test@example.com";
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            ExternalAuthId = externalAuthId,
            Username = "existing",
            Email = "existing@example.com"
        };

        _mockUserRepository.Setup(r => r.GetByExternalAuthIdAsync(externalAuthId))
            .ReturnsAsync(existingUser);

        try
        {
            await _userUpdater.CreateNewUserData(externalAuthId, username, email);
            Assert.Fail("Expected InvalidOperationException was not thrown");
        }
        catch (InvalidOperationException ex)
        {
            Assert.AreEqual("User with this external auth ID already exists", ex.Message);
        }

        _mockUserRepository.Verify(r => r.GetByExternalAuthIdAsync(externalAuthId), Times.Once);
        _mockUserRepository.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Never);
        _mockGalleryRepository.Verify(r => r.GetAllGalleryIdsAsync(), Times.Never);
        _mockUserGalleryRepository.Verify(r => r.CreateAsync(It.IsAny<UserGallery>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.BeginAsync(), Times.Never);
    }

    [TestMethod]
    public async Task CreateNewUserData_SetsCreatedAtAndUpdatedAt_WhenCreatingUser()
    {
        var externalAuthId = "auth0|123456";
        var username = "testuser";
        var email = "test@example.com";
        var galleryIds = new List<Guid> { Guid.NewGuid() };
        var beforeCreation = DateTime.UtcNow;

        _mockUserRepository.Setup(r => r.GetByExternalAuthIdAsync(externalAuthId))
            .ReturnsAsync((User)null);
        _mockUserRepository.Setup(r =>
                r.CreateAsync(It.IsAny<User>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((User u, NpgsqlConnection _, NpgsqlTransaction _) => u);
        _mockGalleryRepository.Setup(r =>
                r.GetAllGalleryIdsAsync(It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync(galleryIds);
        _mockUserGalleryRepository.Setup(r =>
                r.CreateAsync(It.IsAny<UserGallery>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((UserGallery ug, NpgsqlConnection _, NpgsqlTransaction _) => ug);

        var result = await _userUpdater.CreateNewUserData(externalAuthId, username, email);

        var afterCreation = DateTime.UtcNow;
        Assert.IsTrue(result.CreatedAt >= beforeCreation && result.CreatedAt <= afterCreation);
        Assert.IsTrue(result.UpdatedAt >= beforeCreation && result.UpdatedAt <= afterCreation);
    }

    [TestMethod]
    public async Task CreateNewUserData_CreatesUserGalleriesForAllGalleries()
    {
        var externalAuthId = "auth0|123456";
        var username = "testuser";
        var email = "test@example.com";
        var galleryIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var capturedUserGalleries = new List<UserGallery>();

        _mockUserRepository.Setup(r => r.GetByExternalAuthIdAsync(externalAuthId))
            .ReturnsAsync((User)null);
        _mockUserRepository.Setup(r =>
                r.CreateAsync(It.IsAny<User>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((User u, NpgsqlConnection _, NpgsqlTransaction _) => u);
        _mockGalleryRepository.Setup(r =>
                r.GetAllGalleryIdsAsync(It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync(galleryIds);
        _mockUserGalleryRepository.Setup(r =>
                r.CreateAsync(It.IsAny<UserGallery>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((UserGallery ug, NpgsqlConnection _, NpgsqlTransaction _) => ug)
            .Callback<UserGallery, NpgsqlConnection, NpgsqlTransaction>((ug, _, _) => capturedUserGalleries.Add(ug));

        var result = await _userUpdater.CreateNewUserData(externalAuthId, username, email);

        Assert.HasCount(galleryIds.Count, capturedUserGalleries);
        foreach (var ug in capturedUserGalleries)
        {
            Assert.AreEqual(result.Id, ug.UserId);
            Assert.Contains(ug.GalleryId, galleryIds);
            Assert.IsFalse(ug.IsUnlocked);
            Assert.IsNull(ug.UnlockedAt);
            Assert.IsNull(ug.BestTimeMs);
            Assert.IsNull(ug.BestTimeAt);
        }
    }

    [TestMethod]
    public async Task CreateNewUserData_RollsBack_WhenCreateUserGalleriesFails()
    {
        var externalAuthId = "auth0|fail";
        var username = "testuser";
        var email = "test@example.com";

        _mockUserRepository.Setup(r => r.GetByExternalAuthIdAsync(externalAuthId))
            .ReturnsAsync((User)null);
        _mockUserRepository.Setup(r =>
                r.CreateAsync(It.IsAny<User>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ReturnsAsync((User u, NpgsqlConnection _, NpgsqlTransaction _) => u);
        _mockGalleryRepository.Setup(r =>
                r.GetAllGalleryIdsAsync(It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()))
            .ThrowsAsync(new Exception("Database error"));

        try
        {
            await _userUpdater.CreateNewUserData(externalAuthId, username, email);
            Assert.Fail("Expected Exception was not thrown");
        }
        catch (Exception ex)
        {
            Assert.AreEqual("Database error", ex.Message);
        }

        _mockUserRepository.Verify(
            r => r.CreateAsync(It.IsAny<User>(), It.IsAny<NpgsqlConnection>(), It.IsAny<NpgsqlTransaction>()),
            Times.Once);
        _mockUnitOfWork.Verify(u => u.BeginAsync(), Times.Once);
        _mockUnitOfWork.Verify(u => u.RollbackAsync(), Times.Once);
        _mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Never);
    }
}