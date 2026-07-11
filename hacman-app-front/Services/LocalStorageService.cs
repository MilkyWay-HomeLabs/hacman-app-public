using Microsoft.JSInterop;

namespace hacman_app_front.Services;

public class LocalStorageService : ILocalStorageService
{
    private readonly IJSRuntime _jsRuntime;

    public LocalStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task SetItemAsync(string key, string value)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorageHelper.setItem", key, value);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async Task<string?> GetItemAsync(string key)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorageHelper.getItem", key);
        }
        catch (JSDisconnectedException)
        {
            return null;
        }
    }

    public async Task RemoveItemAsync(string key)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorageHelper.removeItem", key);
        }
        catch (JSDisconnectedException)
        {
        }
    }
}