using System.Security.Claims;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Feature: firebase-authentication, Property 8: Authenticated policy accepts any authenticated user
/// **Validates: Requirements 5.5**
///
/// For any ClaimsPrincipal that represents an authenticated identity (regardless of which claims
/// it carries), the Authenticated authorization policy SHALL evaluate to success. For any
/// unauthenticated principal, the policy SHALL evaluate to failure.
/// </summary>
public class AuthenticatedPolicyPropertyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly IAuthorizationService _authorizationService;

    private static readonly string[] SampleClaimTypes =
    [
        System.Security.Claims.ClaimTypes.NameIdentifier,
        System.Security.Claims.ClaimTypes.Email,
        System.Security.Claims.ClaimTypes.Name,
        System.Security.Claims.ClaimTypes.Role,
        System.Security.Claims.ClaimTypes.GivenName,
        System.Security.Claims.ClaimTypes.Surname,
        "firebase_uid",
        "custom_claim",
        "tenant_id",
        "org_id",
        "department",
        "level"
    ];

    private static readonly string[] AuthenticationTypes =
    [
        "Bearer",
        "Firebase",
        "JWT",
        "Custom",
        "OAuth2",
        "TestAuth"
    ];

    public AuthenticatedPolicyPropertyTests(CustomWebApplicationFactory factory)
    {
        var scope = factory.Services.CreateScope();
        _authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
    }

    /// <summary>
    /// Builds an authenticated ClaimsPrincipal with random claims based on seed values.
    /// The principal always has an authenticated identity (AuthenticationType is non-null).
    /// </summary>
    private static ClaimsPrincipal BuildAuthenticatedPrincipal(int seed)
    {
        var rng = new Random(seed);

        // Pick a random authentication type
        var authType = AuthenticationTypes[rng.Next(AuthenticationTypes.Length)];

        // Generate a random number of claims (0 to 8)
        var claimCount = rng.Next(0, 9);
        var claims = new List<Claim>();

        for (var i = 0; i < claimCount; i++)
        {
            var claimType = SampleClaimTypes[rng.Next(SampleClaimTypes.Length)];
            var valueLength = rng.Next(1, 50);
            var value = new string(Enumerable.Range(0, valueLength)
                .Select(_ => (char)rng.Next(32, 127))
                .ToArray());
            claims.Add(new Claim(claimType, value));
        }

        var identity = new ClaimsIdentity(claims, authType);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Builds an unauthenticated ClaimsPrincipal. The identity has no AuthenticationType,
    /// which means IsAuthenticated returns false.
    /// </summary>
    private static ClaimsPrincipal BuildUnauthenticatedPrincipal(int seed)
    {
        var rng = new Random(seed);

        // Variant: different ways to be unauthenticated
        var variant = rng.Next(4);

        return variant switch
        {
            0 => new ClaimsPrincipal(new ClaimsIdentity()), // No auth type, no claims
            1 => new ClaimsPrincipal(), // Empty principal
            2 => BuildUnauthenticatedWithClaims(rng), // Has claims but no auth type
            _ => new ClaimsPrincipal(new ClaimsIdentity(authenticationType: null)) // Explicit null auth type
        };
    }

    private static ClaimsPrincipal BuildUnauthenticatedWithClaims(Random rng)
    {
        // Has claims but AuthenticationType is null → IsAuthenticated = false
        var claimCount = rng.Next(1, 6);
        var claims = new List<Claim>();

        for (var i = 0; i < claimCount; i++)
        {
            var claimType = SampleClaimTypes[rng.Next(SampleClaimTypes.Length)];
            var value = $"value-{rng.Next(1000)}";
            claims.Add(new Claim(claimType, value));
        }

        // ClaimsIdentity with claims but NO authenticationType → not authenticated
        var identity = new ClaimsIdentity(claims);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Property 8a: Authenticated policy accepts any authenticated user.
    ///
    /// For any ClaimsPrincipal that represents an authenticated identity (regardless of which
    /// claims it carries), the Authenticated authorization policy SHALL evaluate to success.
    ///
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 8: Authenticated policy accepts any authenticated user",
        MaxTest = 100)]
    public async void AuthenticatedPrincipal_PolicyEvaluatesToSuccess(PositiveInt seed)
    {
        var principal = BuildAuthenticatedPrincipal(seed.Get);

        // Verify the principal is indeed authenticated
        Assert.True(principal.Identity?.IsAuthenticated);

        var result = await _authorizationService.AuthorizeAsync(principal, "Authenticated");

        Assert.True(result.Succeeded,
            $"Authenticated policy should succeed for authenticated principal with auth type " +
            $"'{principal.Identity?.AuthenticationType}' and {principal.Claims.Count()} claims");
    }

    /// <summary>
    /// Property 8b: Authenticated policy rejects any unauthenticated user.
    ///
    /// For any unauthenticated principal, the Authenticated authorization policy SHALL
    /// evaluate to failure.
    ///
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 8: Authenticated policy rejects unauthenticated user",
        MaxTest = 100)]
    public async void UnauthenticatedPrincipal_PolicyEvaluatesToFailure(PositiveInt seed)
    {
        var principal = BuildUnauthenticatedPrincipal(seed.Get);

        // Verify the principal is indeed NOT authenticated
        Assert.False(principal.Identity?.IsAuthenticated ?? false);

        var result = await _authorizationService.AuthorizeAsync(principal, "Authenticated");

        Assert.False(result.Succeeded,
            $"Authenticated policy should fail for unauthenticated principal");
    }
}
