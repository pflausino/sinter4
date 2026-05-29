namespace Web.Services;

/// <summary>
/// Abstraction over browser-based token storage to enable testability.
/// </summary>
public interface ITokenStorage
{
    ValueTask<(bool Success, string? Value)> GetAsync(string key);
    ValueTask SetAsync(string key, string value);
    ValueTask DeleteAsync(string key);
}
