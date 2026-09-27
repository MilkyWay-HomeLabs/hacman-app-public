using hacman_app_front.Services;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;
using Xunit;

namespace hacman_app_front.Tests.Services;

public class LocalStorageServiceTest
{
    private readonly Mock<IJSRuntime> _jsRuntime = new();

    private LocalStorageService CreateService() => new(_jsRuntime.Object);

    [Fact]
    public async Task SetItemAsync_InvokesLocalStorageHelperSetItem()
    {
        _jsRuntime.Setup(j => j.InvokeAsync<IJSVoidResult>("localStorageHelper.setItem",
                It.Is<object?[]>(a => (string)a[0]! == "key" && (string)a[1]! == "value")))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());

        await CreateService().SetItemAsync("key", "value");

        _jsRuntime.VerifyAll();
    }

    [Fact]
    public async Task SetItemAsync_SwallowsJSDisconnectedException()
    {
        _jsRuntime.Setup(j => j.InvokeAsync<IJSVoidResult>("localStorageHelper.setItem", It.IsAny<object?[]>()))
            .ThrowsAsync(new JSDisconnectedException("disconnected"));

        await CreateService().SetItemAsync("key", "value");
    }

    [Fact]
    public async Task GetItemAsync_ReturnsValueFromJs()
    {
        _jsRuntime.Setup(j => j.InvokeAsync<string?>("localStorageHelper.getItem",
                It.Is<object?[]>(a => (string)a[0]! == "key")))
            .ReturnsAsync("stored-value");

        var result = await CreateService().GetItemAsync("key");

        Assert.Equal("stored-value", result);
    }

    [Fact]
    public async Task GetItemAsync_ReturnsNull_WhenJSDisconnected()
    {
        _jsRuntime.Setup(j => j.InvokeAsync<string?>("localStorageHelper.getItem", It.IsAny<object?[]>()))
            .ThrowsAsync(new JSDisconnectedException("disconnected"));

        var result = await CreateService().GetItemAsync("key");

        Assert.Null(result);
    }

    [Fact]
    public async Task RemoveItemAsync_InvokesLocalStorageHelperRemoveItem()
    {
        _jsRuntime.Setup(j => j.InvokeAsync<IJSVoidResult>("localStorageHelper.removeItem",
                It.Is<object?[]>(a => (string)a[0]! == "key")))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());

        await CreateService().RemoveItemAsync("key");

        _jsRuntime.VerifyAll();
    }

    [Fact]
    public async Task RemoveItemAsync_SwallowsJSDisconnectedException()
    {
        _jsRuntime.Setup(j => j.InvokeAsync<IJSVoidResult>("localStorageHelper.removeItem", It.IsAny<object?[]>()))
            .ThrowsAsync(new JSDisconnectedException("disconnected"));

        await CreateService().RemoveItemAsync("key");
    }
}
