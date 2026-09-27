using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hacman_app_back.Modules.GalleryCatalog.Application;
using hacman_app_back.Modules.GalleryCatalog.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace hacman_app_back.Tests.Modules.GalleryCatalog.Application;

[TestClass]
public class GallerySubtypeServiceTest
{
    [TestMethod]
    public async Task GetAllAsync_ReturnsListOfSubtypes_WhenRepositoryReturnsData()
    {
        var mockRepository = new Mock<IGallerySubtypeRepository>();
        var expectedSubtypes = new List<string>
            { "Subtype1", "Subtype2", "Subtype3", "Subtype4", "Subtype5", "Subtype6", "Subtype7", "Subtype8" };
        mockRepository.Setup(repo => repo.GetAllAsync()).ReturnsAsync(expectedSubtypes);

        var service = new GallerySubtypeService(mockRepository.Object);

        var result = await service.GetAllAsync();

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(expectedSubtypes, result.ToList());
    }

    [TestMethod]
    public async Task GetAllAsync_ReturnsEmptyList_WhenRepositoryReturnsNoData()
    {
        var mockRepository = new Mock<IGallerySubtypeRepository>();
        mockRepository.Setup(repo => repo.GetAllAsync()).ReturnsAsync(Enumerable.Empty<string>());

        var service = new GallerySubtypeService(mockRepository.Object);

        var result = await service.GetAllAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public void Constructor_ThrowsArgumentNullException_WhenRepositoryIsNull()
    {
        try
        {
            _ = new GallerySubtypeService(null);
            Assert.Fail("Expected ArgumentNullException was not thrown.");
        }
        catch (ArgumentNullException)
        {
            // OK
        }
    }
}