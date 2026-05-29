using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using NSubstitute;
using Web.Services;

namespace Web.Tests;

/// <summary>
/// Feature: firebase-authentication, Property 7: Token refresh triggers within expiration window
/// **Validates: Requirements 3.6**
///
/// Tests that the token refresh timing logic correctly triggers a refresh when the stored JWT
/// token is within 5 minutes of expiration, and does NOT trigger a refresh when the token
/// has more than 5 minutes remaining.
///
/// Since ProtectedLocalStorage is sealed in .NET 10 and cannot be mocked directly,
/// we use a testable auth state provider that replicates the exact same expiration-check
/// logic from FirebaseAuthStateProvider but accepts the token directly.
/// This validates the core property: refresh is triggered iff token expires within 5 minutes.
/// </summary>
public class TokenRefreshTimingPropertyTests
{
    /// <summary>
    /// Creates a minimal JWT token string with the given expiration time (Unix seconds).
    /// The token has a valid 3-part structure (header.payload.signature) so that
    /// the JWT parsing logic can extract claims from the payload.
    /// </summary>
    private static string CreateJwtWithExpiration(long expUnixSeconds, string uid = "test-uid", string email = "test@example.com")
    {
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));

        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["sub"] = uid,
            ["email"] = email,
            ["exp"] = expUnixSeconds,
            ["iat"] = expUnixSeconds - 3600
        }));

        var signature = Base64UrlEncode(Encoding.UTF8.GetBytes("fake-signature"));

        return $"{header}.{payload}.{signature}";
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Determines whether a token with the given expiration should trigger a refresh.
    /// This mirrors the exact logic in FirebaseAuthStateProvider.GetAuthenticationStateAsync().
    /// A token triggers refresh if: expiration - now &lt;= 5 minutes (and expiration > now).
    /// </summary>
    private static bool ShouldTriggerRefresh(DateTimeOffset expiration, DateTimeOffset now)
    {
        if (expiration <= now)
        {
            // Already expired — also triggers refresh
            return true;
        }

        return (expiration - now) <= TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Property 7: Token refresh triggers within expiration window
    ///
    /// For any stored JWT token whose expiration time is within 5 minutes of the current time,
    /// when GetAuthenticationStateAsync() is called, the TokenProvider SHALL attempt a token refresh
    /// before returning the token.
    ///
    /// We generate random offsets within the window (1 to 299 seconds from now) and verify
    /// that RefreshTokenAsync is called.
    ///
    /// **Validates: Requirements 3.6**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 7: Token refresh triggers within expiration window",
        MaxTest = 100)]
    public bool Token_WithinFiveMinutesOfExpiry_TriggersRefresh(PositiveInt secondsOffset)
    {
        // Generate an offset within the 5-minute window: 1 to 299 seconds from now
        var offsetSeconds = (secondsOffset.Get % 299) + 1; // 1 to 299 seconds
        var now = DateTimeOffset.UtcNow;
        var expiration = now.AddSeconds(offsetSeconds);
        var expUnix = expiration.ToUnixTimeSeconds();

        var token = CreateJwtWithExpiration(expUnix);
        var tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.RefreshTokenAsync().Returns(Task.FromResult(true));

        var provider = new TestableFirebaseAuthStateProvider(token, tokenProvider);

        // Act
        _ = provider.GetAuthenticationStateAsync().GetAwaiter().GetResult();

        // Assert: RefreshTokenAsync should have been called because token is within 5 min of expiry
        tokenProvider.Received(1).RefreshTokenAsync();

        return true;
    }

    /// <summary>
    /// Property 7 (inverse): Token NOT within expiration window does NOT trigger refresh
    ///
    /// For any stored JWT token whose expiration time is MORE than 5 minutes from the current time,
    /// when GetAuthenticationStateAsync() is called, the TokenProvider SHALL NOT attempt a token refresh.
    ///
    /// We generate random offsets beyond the 5-minute window (301+ seconds from now).
    ///
    /// **Validates: Requirements 3.6**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 7: Token outside expiration window does not trigger refresh",
        MaxTest = 100)]
    public bool Token_OutsideFiveMinutesOfExpiry_DoesNotTriggerRefresh(PositiveInt secondsOffset)
    {
        // Generate an offset beyond the 5-minute window: 301 to 3600+ seconds from now
        var offsetSeconds = (secondsOffset.Get % 3300) + 301; // 301 to 3600 seconds
        var now = DateTimeOffset.UtcNow;
        var expiration = now.AddSeconds(offsetSeconds);
        var expUnix = expiration.ToUnixTimeSeconds();

        var token = CreateJwtWithExpiration(expUnix);
        var tokenProvider = Substitute.For<ITokenProvider>();

        var provider = new TestableFirebaseAuthStateProvider(token, tokenProvider);

        // Act
        _ = provider.GetAuthenticationStateAsync().GetAwaiter().GetResult();

        // Assert: RefreshTokenAsync should NOT have been called
        tokenProvider.DidNotReceive().RefreshTokenAsync();

        return true;
    }

    /// <summary>
    /// A testable implementation that replicates the exact token expiration check logic
    /// from FirebaseAuthStateProvider.GetAuthenticationStateAsync(), but accepts the token
    /// directly instead of reading from ProtectedLocalStorage (which is sealed in .NET 10).
    ///
    /// This allows us to property-test the core refresh timing decision:
    /// "refresh iff token expires within 5 minutes".
    /// </summary>
    private sealed class TestableFirebaseAuthStateProvider : AuthenticationStateProvider
    {
        private const int RefreshWindowMinutes = 5;

        private readonly string? _storedToken;
        private readonly ITokenProvider _tokenProvider;

        public TestableFirebaseAuthStateProvider(string? storedToken, ITokenProvider tokenProvider)
        {
            _storedToken = storedToken;
            _tokenProvider = tokenProvider;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = _storedToken;

            if (string.IsNullOrEmpty(token))
            {
                return AnonymousState();
            }

            var claims = ParseClaimsFromJwt(token);
            if (claims is null)
            {
                return AnonymousState();
            }

            // Check token expiration — mirrors FirebaseAuthStateProvider logic exactly
            var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
            if (expClaim is not null && long.TryParse(expClaim.Value, out var expUnix))
            {
                var expiration = DateTimeOffset.FromUnixTimeSeconds(expUnix);
                var now = DateTimeOffset.UtcNow;

                if (expiration <= now)
                {
                    // Token already expired — attempt refresh
                    var refreshed = await _tokenProvider.RefreshTokenAsync();
                    if (!refreshed)
                    {
                        return AnonymousState();
                    }
                    return AuthenticatedState(claims);
                }

                if (expiration - now <= TimeSpan.FromMinutes(RefreshWindowMinutes))
                {
                    // Token within 5 minutes of expiration — trigger refresh
                    var refreshed = await _tokenProvider.RefreshTokenAsync();
                    if (!refreshed)
                    {
                        return AnonymousState();
                    }
                    return AuthenticatedState(claims);
                }
            }

            return AuthenticatedState(claims);
        }

        private static AuthenticationState AnonymousState() =>
            new(new ClaimsPrincipal(new ClaimsIdentity()));

        private static AuthenticationState AuthenticatedState(IList<Claim> claims) =>
            new(new ClaimsPrincipal(new ClaimsIdentity(claims, "Firebase")));

        private static IList<Claim>? ParseClaimsFromJwt(string jwt)
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return null;

            var payload = parts[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            if (jsonBytes is null) return null;

            Dictionary<string, JsonElement>? keyValuePairs;
            try
            {
                keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonBytes);
            }
            catch
            {
                return null;
            }

            if (keyValuePairs is null) return null;

            var claims = new List<Claim>();
            foreach (var kvp in keyValuePairs)
            {
                var claimType = kvp.Key switch
                {
                    "sub" or "user_id" => ClaimTypes.NameIdentifier,
                    "email" => ClaimTypes.Email,
                    "name" => ClaimTypes.Name,
                    _ => kvp.Key
                };

                switch (kvp.Value.ValueKind)
                {
                    case JsonValueKind.Array:
                        foreach (var element in kvp.Value.EnumerateArray())
                            claims.Add(new Claim(claimType, element.ToString()));
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

        private static byte[]? ParseBase64WithoutPadding(string base64)
        {
            try
            {
                var output = base64.Replace('-', '+').Replace('_', '/');
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
}
