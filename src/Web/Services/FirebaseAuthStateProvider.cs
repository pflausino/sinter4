using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace Web.Services;

public class FirebaseAuthStateProvider : AuthenticationStateProvider
{
    private const string IdTokenKey = "firebase_id_token";
    private const int RefreshWindowMinutes = 5;

    private readonly ProtectedLocalStorage _localStorage;
    private readonly ITokenProvider _tokenProvider;
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<FirebaseAuthStateProvider> _logger;

    private static readonly AuthenticationState AnonymousState =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public FirebaseAuthStateProvider(
        ProtectedLocalStorage localStorage,
        ITokenProvider tokenProvider,
        NavigationManager navigationManager,
        ILogger<FirebaseAuthStateProvider> logger)
    {
        _localStorage = localStorage;
        _tokenProvider = tokenProvider;
        _navigationManager = navigationManager;
        _logger = logger;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        string? token;
        try
        {
            var result = await _localStorage.GetAsync<string>(IdTokenKey);
            token = result.Success ? result.Value : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read token from protected storage");
            return AnonymousState;
        }

        if (string.IsNullOrEmpty(token))
        {
            return AnonymousState;
        }

        var claims = ParseClaimsFromJwt(token);
        if (claims is null)
        {
            return AnonymousState;
        }

        // Check token expiration
        var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
        if (expClaim is not null && long.TryParse(expClaim.Value, out var expUnix))
        {
            var expiration = DateTimeOffset.FromUnixTimeSeconds(expUnix);
            var now = DateTimeOffset.UtcNow;

            if (expiration <= now)
            {
                // Token already expired — attempt refresh
                var refreshed = await TryRefreshTokenAsync();
                if (!refreshed)
                {
                    await HandleRefreshFailureAsync();
                    return AnonymousState;
                }

                // Re-read the new token after refresh
                return await GetAuthenticationStateFromStorageAsync();
            }

            if (expiration - now <= TimeSpan.FromMinutes(RefreshWindowMinutes))
            {
                // Token within 5 minutes of expiration — trigger refresh
                var refreshed = await TryRefreshTokenAsync();
                if (!refreshed)
                {
                    await HandleRefreshFailureAsync();
                    return AnonymousState;
                }

                // Re-read the new token after refresh
                return await GetAuthenticationStateFromStorageAsync();
            }
        }

        var identity = new ClaimsIdentity(claims, "Firebase");
        var principal = new ClaimsPrincipal(identity);
        return new AuthenticationState(principal);
    }

    /// <summary>
    /// Notifies the authentication system that the authentication state has changed.
    /// Call this after sign-in or sign-out to update the UI.
    /// </summary>
    public void NotifyStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private async Task<AuthenticationState> GetAuthenticationStateFromStorageAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<string>(IdTokenKey);
            var token = result.Success ? result.Value : null;

            if (string.IsNullOrEmpty(token))
            {
                return AnonymousState;
            }

            var claims = ParseClaimsFromJwt(token);
            if (claims is null)
            {
                return AnonymousState;
            }

            var identity = new ClaimsIdentity(claims, "Firebase");
            var principal = new ClaimsPrincipal(identity);
            return new AuthenticationState(principal);
        }
        catch
        {
            return AnonymousState;
        }
    }

    private async Task<bool> TryRefreshTokenAsync()
    {
        try
        {
            return await _tokenProvider.RefreshTokenAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token refresh threw an exception");
            return false;
        }
    }

    private async Task HandleRefreshFailureAsync()
    {
        _logger.LogInformation("Token refresh failed. Clearing state and redirecting to login.");

        try
        {
            await _tokenProvider.SignOutAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear tokens during refresh failure handling");
        }

        _navigationManager.NavigateTo("/login", forceLoad: false);
    }

    private static IList<Claim>? ParseClaimsFromJwt(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var payload = parts[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        if (jsonBytes is null)
        {
            return null;
        }

        Dictionary<string, JsonElement>? keyValuePairs;
        try
        {
            keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes);
        }
        catch
        {
            return null;
        }

        if (keyValuePairs is null)
        {
            return null;
        }

        var claims = new List<Claim>();

        foreach (var kvp in keyValuePairs)
        {
            var claimType = MapClaimType(kvp.Key);

            switch (kvp.Value.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (var element in kvp.Value.EnumerateArray())
                    {
                        claims.Add(new Claim(claimType, element.ToString()));
                    }
                    break;

                case JsonValueKind.String:
                    claims.Add(new Claim(claimType, kvp.Value.GetString()!));
                    break;

                default:
                    claims.Add(new Claim(claimType, kvp.Value.GetRawText()));
                    break;
            }
        }

        return claims;
    }

    private static string MapClaimType(string jwtClaimType) => jwtClaimType switch
    {
        "sub" or "user_id" => ClaimTypes.NameIdentifier,
        "email" => ClaimTypes.Email,
        "name" => ClaimTypes.Name,
        _ => jwtClaimType
    };

    private static byte[]? ParseBase64WithoutPadding(string base64)
    {
        try
        {
            // Replace URL-safe characters
            var output = base64.Replace('-', '+').Replace('_', '/');

            // Add padding if necessary
            switch (output.Length % 4)
            {
                case 2: output += "=="; break;
                case 3: output += "="; break;
                case 0: break;
                default: return null;
            }

            return Convert.FromBase64String(output);
        }
        catch
        {
            return null;
        }
    }
}
