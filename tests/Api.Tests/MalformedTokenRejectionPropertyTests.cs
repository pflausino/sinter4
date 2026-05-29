using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Feature: firebase-authentication, Property 2: Malformed tokens are uniformly rejected
/// **Validates: Requirements 1.4**
///
/// For any string that is not a valid JWT (random bytes, truncated tokens, tokens with
/// invalid signatures), the authentication middleware SHALL return HTTP 401 with error
/// field "invalid_token".
/// </summary>
public class MalformedTokenRejectionPropertyTests : IClassFixture<MalformedTokenRejectionPropertyTests.ProtectedEndpointFactory>
{
    private readonly ProtectedEndpointFactory _factory;

    public MalformedTokenRejectionPropertyTests(ProtectedEndpointFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// A WebApplicationFactory that registers a protected test endpoint requiring authorization.
    /// The endpoint is added via IStartupFilter so it participates in the full middleware pipeline.
    /// </summary>
    public class ProtectedEndpointFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("Firebase:ProjectId", "test-project-id");
            builder.UseSetting("Firebase:ServiceAccountPath",
                CustomWebApplicationFactory.GetFakeServiceAccountPath());

            builder.ConfigureServices(services =>
            {
                // Remove EF Core registrations to avoid Npgsql conflicts
                var efDescriptors = services
                    .Where(d => d.ServiceType.FullName != null
                        && (d.ServiceType.FullName.Contains("EntityFrameworkCore")
                            || d.ServiceType.FullName.Contains("EntityFramework")
                            || d.ServiceType == typeof(AppDbContext)
                            || d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                            || d.ServiceType == typeof(DbContextOptions)))
                    .ToList();

                foreach (var descriptor in efDescriptors)
                    services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("TestDb-MalformedToken"));
            });
        }
    }

    /// <summary>
    /// Generates random bytes encoded as a string (not a valid JWT).
    /// </summary>
    private static string GenerateRandomBytes(int seed)
    {
        var rng = new Random(seed);
        var length = (Math.Abs(seed) % 200) + 1;
        var bytes = new byte[length];
        rng.NextBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Generates a truncated JWT-like string (has dots but invalid structure).
    /// </summary>
    private static string GenerateTruncatedToken(int seed)
    {
        var rng = new Random(seed);
        var variant = Math.Abs(seed) % 5;

        return variant switch
        {
            0 => "eyJhbGciOiJSUzI1NiJ9.", // header only, missing payload and signature
            1 => "eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.", // missing signature
            2 => $"eyJhbGciOiJSUzI1NiJ9.{GenerateRandomBase64Url(rng, 20)}", // header + random payload, no signature
            3 => $"{GenerateRandomBase64Url(rng, 10)}.{GenerateRandomBase64Url(rng, 20)}.{GenerateRandomBase64Url(rng, 30)}", // three random parts
            _ => $"eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.{GenerateRandomBase64Url(rng, 50)}" // valid-looking but wrong signature
        };
    }

    /// <summary>
    /// Generates a token with an invalid signature (structurally valid JWT but signature doesn't verify).
    /// </summary>
    private static string GenerateInvalidSignatureToken(int seed)
    {
        var rng = new Random(seed);
        // Valid header (RS256) and a valid-looking payload, but random signature
        var header = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9";
        var payload = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(new
                {
                    sub = $"user-{seed}",
                    email = $"user{seed}@test.com",
                    aud = "test-project-id",
                    iss = "https://securetoken.google.com/test-project-id",
                    iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
                })))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var signature = GenerateRandomBase64Url(rng, 64);
        return $"{header}.{payload}.{signature}";
    }

    private static string GenerateRandomBase64Url(Random rng, int byteLength)
    {
        var bytes = new byte[byteLength];
        rng.NextBytes(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Generates a malformed token string based on a seed. Covers:
    /// - Random bytes (not JWT at all)
    /// - Truncated tokens (partial JWT structure)
    /// - Tokens with invalid signatures
    /// - Plain text strings
    /// - Empty-ish strings that still count as "present"
    /// </summary>
    private static string GenerateMalformedToken(PositiveInt seed)
    {
        var category = seed.Get % 6;

        return category switch
        {
            0 => GenerateRandomBytes(seed.Get),
            1 => GenerateTruncatedToken(seed.Get),
            2 => GenerateInvalidSignatureToken(seed.Get),
            3 => $"not-a-jwt-{seed.Get}",
            4 => new string((char)(33 + (seed.Get % 94)), (seed.Get % 50) + 1), // repeated printable char
            _ => $"Bearer.Invalid.Token.{seed.Get}" // too many dots
        };
    }

    /// <summary>
    /// Property 2: Malformed tokens are uniformly rejected
    ///
    /// For any string that is not a valid JWT (random bytes, truncated tokens, tokens with
    /// invalid signatures), the authentication middleware SHALL return HTTP 401 with error
    /// field "invalid_token".
    ///
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 2: Malformed tokens are uniformly rejected",
        MaxTest = 100)]
    public bool MalformedToken_Returns401_WithInvalidTokenError(PositiveInt seed)
    {
        var malformedToken = GenerateMalformedToken(seed);

        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", malformedToken);

        var response = client.SendAsync(request).GetAwaiter().GetResult();

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return false;

        var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        var json = JsonDocument.Parse(content);

        var hasErrorField = json.RootElement.TryGetProperty("error", out var errorElement)
            && errorElement.GetString() == "invalid_token";

        var hasMessageField = json.RootElement.TryGetProperty("message", out var messageElement)
            && !string.IsNullOrWhiteSpace(messageElement.GetString());

        return hasErrorField && hasMessageField;
    }
}