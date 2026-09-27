using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hacman_app_back.Modules.GalleryCatalog.Domain;
using hacman_app_back.Modules.PlayerProgress.Application;
using hacman_app_back.Modules.PlayerProgress.Domain;
using hacman_app_back.Modules.PlayerProgress.Infrastructure;
using Moq;
using Xunit;

namespace hacman_app_back.Tests.Modules.PlayerProgress.Application;

public class UserGalleryProviderTest
{
    [Fact]
    public async Task GetAllByUserIdAsync_ReturnsUserGalleries_WhenUserIdExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedGalleries = new List<UserGallery>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Gallery = new Gallery() },
            new() { Id = Guid.NewGuid(), UserId = userId, Gallery = new Gallery() }
        };
        var repositoryMock = new Mock<IUserGalleryRepository>();
        repositoryMock
            .Setup(repo => repo.GetAllByUserIdAsync(userId))
            .ReturnsAsync(expectedGalleries);

        var provider = new UserGalleryProvider(repositoryMock.Object);

        // Act
        var result = await provider.GetAllByUserIdAsync(userId);

        // Assert
        Assert.NotNull(result);
        var userGalleries = result as UserGallery[] ?? result.ToArray();
        Assert.Equal(expectedGalleries.Count, userGalleries.Length);
        Assert.All(userGalleries, gallery => Assert.Equal(userId, gallery.UserId));
    }

    [Fact]
    public async Task GetAllByUserIdAsync_ReturnsEmpty_WhenNoGalleriesExistForUserId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var repositoryMock = new Mock<IUserGalleryRepository>();
        repositoryMock
            .Setup(repo => repo.GetAllByUserIdAsync(userId))
            .ReturnsAsync(Enumerable.Empty<UserGallery>());

        var provider = new UserGalleryProvider(repositoryMock.Object);

        // Act
        var result = await provider.GetAllByUserIdAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllByUserIdAsync_ThrowsArgumentNullException_WhenRepositoryIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UserGalleryProvider(null));
    }
}