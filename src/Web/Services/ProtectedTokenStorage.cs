using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Web.Services;

/// <summary>
/// Production implementation of ITokenStorage that wraps ProtectedLocalStorage.
/// </summary>
public class ProtectedTokenStorage : ITokenStorage
{
    private readonly ProtectedLocalStorage _localStorage;

    public ProtectedTokenStorage(ProtectedLocalStorage localStorage)
    {
        _localStorage = localStorage;
    }

    public async ValueTask<(bool Success, string? Value)> GetAsync(string key)
    {
        var result = await _localStorage.GetAsync<string>(key);
        return (result.Success, result.Value);
    }

    public async ValueTask SetAsync(string key, string value)
    {
        await _localStorage.SetAsync(key, value);
    }

    public async ValueTask DeleteAsync(string key)
    {
        await _localStorage.DeleteAsync(key);
    }
}
