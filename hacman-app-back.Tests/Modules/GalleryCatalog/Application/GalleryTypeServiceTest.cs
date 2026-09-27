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
public class GalleryTypeServiceTest
{
    [TestMethod]
    public async Task GetAllAsync_ReturnsListOfTypes_WhenRepositoryReturnsData()
    {
        var mockRepository = new Mock<IGalleryTypeRepository>();
        var expectedTypes = new List<string> { "type1", "type2", "type3", "type4", "type5", "type6" };
        mockRepository.Setup(repo => repo.GetAllAsync()).ReturnsAsync(expectedTypes);

        var service = new GalleryTypeService(mockRepository.Object);

        var result = await service.GetAllAsync();

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(expectedTypes, result.ToList());
    }

    [TestMethod]
    public async Task GetAllAsync_ReturnsEmptyList_WhenRepositoryReturnsNoData()
    {
        var mockRepository = new Mock<IGalleryTypeRepository>();
        mockRepository.Setup(repo => repo.GetAllAsync()).ReturnsAsync(Enumerable.Empty<string>());

        var service = new GalleryTypeService(mockRepository.Object);

        var result = await service.GetAllAsync();

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count());
    }

    [TestMethod]
    public void Constructor_ThrowsArgumentNullException_WhenRepositoryIsNull()
    {
        try
        {
            _ = new GalleryTypeService(null);
            Assert.Fail("Expected ArgumentNullException was not thrown.");
        }
        catch (ArgumentNullException)
        {
            // OK
        }
    }
}