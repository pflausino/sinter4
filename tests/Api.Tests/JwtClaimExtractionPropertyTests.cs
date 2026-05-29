using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using FsCheck;
using FsCheck.Xunit;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Api.Tests;

/// <summary>
/// Feature: firebase-authentication, Property 1: Valid JWT claim extraction preserves token identity
/// **Validates: Requirements 1.2**
///
/// For any valid Firebase JWT payload containing a UID and email, when the authentication
/// middleware processes the token, the resulting ClaimsPrincipal SHALL contain a NameIdentifier
/// claim equal to the UID and an Email claim equal to the email from the token.
/// </summary>
public class JwtClaimExtractionPropertyTests : IDisposable
{
    private const string TestProjectId = "test-project-id";
    private const string TestIssuer = $"https://securetoken.google.com/{TestProjectId}";

    private static readonly RSA RsaKey = RSA.Create(2048);
    private static readonly RsaSecurityKey SigningKey = new(RsaKey);
    private static readonly SigningCredentials Credentials = new(SigningKey, SecurityAlgorithms.RsaSha256);

    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public JwtClaimExtractionPropertyTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Firebase:ProjectId", TestProjectId);
                builder.UseSetting("Firebase:ServiceAccountPath", GetFakeServiceAccountPath());

                builder.ConfigureServices(services =>
                {
                    // Register a startup filter that adds our test endpoint
                    services.AddSingleton<IStartupFilter, ClaimsTestEndpointStartupFilter>();
                });

                builder.ConfigureTestServices(services =>
                {
                    // Remove EF Core and use InMemory
                    RemoveEfCore(services);
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseInMemoryDatabase($"TestDb-Claims-{Guid.NewGuid()}"));

                    // Reconfigure JWT Bearer to accept our self-signed tokens
                    services.PostConfigure<JwtBearerOptions>(
                        JwtBearerDefaults.AuthenticationScheme,
                        options =>
                        {
                            options.Authority = null;
                            options.RequireHttpsMetadata = false;
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidIssuer = TestIssuer,
                                ValidateAudience = true,
                                ValidAudience = TestProjectId,
                                ValidateLifetime = true,
                                ClockSkew = TimeSpan.FromMinutes(5),
                                ValidateIssuerSigningKey = true,
                                IssuerSigningKey = SigningKey
                            };
                        });
                });
            });

        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Startup filter that adds a protected test endpoint to echo back claims.
    /// </summary>
    private sealed class ClaimsTestEndpointStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                next(app);

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/test/claims", (HttpContext context) =>
                    {
                        var user = context.User;
                        var uid = user.FindFirstValue(ClaimTypes.NameIdentifier);
                        var email = user.FindFirstValue(ClaimTypes.Email);

                        return Results.Ok(new { uid, email });
                    }).RequireAuthorization("Authenticated");
                });
            };
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>
    /// Property 1: Valid JWT claim extraction preserves token identity
    ///
    /// For any valid Firebase JWT payload containing a UID and email, when the authentication
    /// middleware processes the token, the resulting ClaimsPrincipal SHALL contain a NameIdentifier
    /// claim equal to the UID and an Email claim equal to the email from the token.
    ///
    /// **Validates: Requirements 1.2**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 1: Valid JWT claim extraction preserves token identity",
        MaxTest = 100)]
    public bool ValidJwt_ClaimsPrincipal_ContainsMatchingUidAndEmail(PositiveInt uidSeed, PositiveInt emailSeed)
    {
        var uid = GenerateUid(uidSeed.Get);
        var email = GenerateEmail(emailSeed.Get);

        var token = CreateValidJwt(uid, email);

        var request = new HttpRequestMessage(HttpMethod.Get, "/test/claims");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = _client.SendAsync(request).GetAwaiter().GetResult();

        if (response.StatusCode != HttpStatusCode.OK)
            return false;

        var result = response.Content.ReadFromJsonAsync<ClaimsResponse>().GetAwaiter().GetResult();

        return result is not null
            && result.Uid == uid
            && result.Email == email;
    }

    private static string CreateValidJwt(string uid, string email)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, uid),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("user_id", uid)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = TestIssuer,
            Audience = TestProjectId,
            SigningCredentials = Credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    private static string GenerateUid(int seed)
    {
        // Firebase UIDs are 28-character alphanumeric strings
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var rng = new Random(seed);
        var length = (rng.Next() % 20) + 8; // 8-27 chars
        return new string(Enumerable.Range(0, length)
            .Select(_ => chars[rng.Next(chars.Length)])
            .ToArray());
    }

    private static string GenerateEmail(int seed)
    {
        const string localChars = "abcdefghijklmnopqrstuvwxyz0123456789._";
        const string domainChars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var rng = new Random(seed);

        var localLen = (rng.Next() % 15) + 3; // 3-17 chars
        var local = new string(Enumerable.Range(0, localLen)
            .Select(_ => localChars[rng.Next(localChars.Length)])
            .ToArray());

        // Ensure local part doesn't start/end with dot
        local = local.Trim('.');
        if (local.Length == 0) local = "user";

        var domainLen = (rng.Next() % 10) + 3; // 3-12 chars
        var domain = new string(Enumerable.Range(0, domainLen)
            .Select(_ => domainChars[rng.Next(domainChars.Length)])
            .ToArray());

        var tlds = new[] { "com", "org", "net", "io", "dev" };
        var tld = tlds[rng.Next(tlds.Length)];

        return $"{local}@{domain}.{tld}";
    }

    private static string GetFakeServiceAccountPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "fake-firebase-sa-claims-test.json");

        if (!File.Exists(path))
        {
            using var rsa = RSA.Create(2048);
            var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem().ReplaceLineEndings("\\n");

            var json = $$"""
            {
                "type": "service_account",
                "project_id": "test-project-id",
                "private_key_id": "test-key-id",
                "private_key": "{{privateKeyPem}}",
                "client_email": "test@test-project-id.iam.gserviceaccount.com",
                "client_id": "123456789",
                "auth_uri": "https://accounts.google.com/o/oauth2/auth",
                "token_uri": "https://oauth2.googleapis.com/token"
            }
            """;

            File.WriteAllText(path, json);
        }

        return path;
    }

    private static void RemoveEfCore(IServiceCollection services)
    {
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
    }

    private sealed record ClaimsResponse(string? Uid, string? Email);
}
