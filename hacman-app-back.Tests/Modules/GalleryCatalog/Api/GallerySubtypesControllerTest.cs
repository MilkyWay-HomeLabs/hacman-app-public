using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using hacman_app_back.Modules.GalleryCatalog.Api;
using hacman_app_back.Modules.GalleryCatalog.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace hacman_app_back.Tests.Modules.GalleryCatalog.Api;

[TestClass]
public class GallerySubtypesControllerTest
{
    private static GallerySubtypesController BuildController(Mock<IGallerySubtypeService> serviceMock)
        => new(serviceMock.Object, new Mock<ILogger<GallerySubtypesController>>().Object);

    [TestMethod]
    public async Task Get_ReturnsOkWithListOfSubtypes_WhenServiceReturnsData()
    {
        var mockService = new Mock<IGallerySubtypeService>();
        var expectedSubtypes = new List<string>
            { "Subtype1", "Subtype2", "Subtype3", "Subtype4", "Subtype5", "Subtype6", "Subtype7", "Subtype8" };
        mockService.Setup(s => s.GetAllAsync()).ReturnsAsync(expectedSubtypes);

        var actionResult = await BuildController(mockService).Get();
        var okResult = actionResult.Result as OkObjectResult;

        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);

        var value = okResult.Value as IEnumerable<string>;
        Assert.IsNotNull(value);
        CollectionAssert.AreEqual(expectedSubtypes, value.ToList());
    }

    [TestMethod]
    public async Task Get_ReturnsOkWithEmptyList_WhenServiceReturnsNoData()
    {
        var mockService = new Mock<IGallerySubtypeService>();
        mockService.Setup(s => s.GetAllAsync()).ReturnsAsync(Enumerable.Empty<string>());

        var actionResult = await BuildController(mockService).Get();
        var okResult = actionResult.Result as OkObjectResult;

        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);

        var value = okResult.Value as IEnumerable<string>;
        Assert.IsNotNull(value);
        CollectionAssert.AreEqual(new List<string>(), value.ToList());
    }

    [TestMethod]
    public async Task Get_PropagatesException_WhenServiceThrows()
    {
        var mockService = new Mock<IGallerySubtypeService>();
        mockService.Setup(s => s.GetAllAsync()).ThrowsAsync(new Exception("Service error"));

        try
        {
            await BuildController(mockService).Get();
            Assert.Fail("Expected exception was not thrown.");
        }
        catch (Exception ex) when (ex is not AssertFailedException)
        {
            // expected
        }
    }
}