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
public class GalleryTypesControllerTest
{
    private static GalleryTypesController BuildController(Mock<IGalleryTypeService> serviceMock)
        => new(serviceMock.Object, new Mock<ILogger<GalleryTypesController>>().Object);

    [TestMethod]
    public async Task Get_ReturnsOkWithListOfTypes_WhenServiceReturnsData()
    {
        var mockService = new Mock<IGalleryTypeService>();
        var expectedTypes = new List<string> { "type1", "type2", "type3", "type4", "type5", "type6" };
        mockService.Setup(s => s.GetAllAsync()).ReturnsAsync(expectedTypes);

        var actionResult = await BuildController(mockService).Get();
        var okResult = actionResult.Result as OkObjectResult;

        Assert.IsNotNull(okResult);
        Assert.AreEqual(200, okResult.StatusCode);

        var value = okResult.Value as IEnumerable<string>;
        Assert.IsNotNull(value);
        CollectionAssert.AreEqual(expectedTypes, value.ToList());
    }

    [TestMethod]
    public async Task Get_ReturnsOkWithEmptyList_WhenServiceReturnsNoData()
    {
        var mockService = new Mock<IGalleryTypeService>();
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
        var mockService = new Mock<IGalleryTypeService>();
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