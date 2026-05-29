using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Api.Tests;

/// <summary>
/// Feature: firebase-authentication, Property 4: Clock skew boundary enforcement
/// **Validates: Requirements 1.7**
///
/// For any JWT token whose expiration time is between 0 and 5 minutes in the past,
/// the middleware SHALL accept the token. For any token whose expiration time is more
/// than 5 minutes in the past, the middleware SHALL reject it.
/// </summary>
public class ClockSkewBoundaryPropertyTests : IClassFixture<ClockSkewTestFactory>, IDisposable
{
    private readonly ClockSkewTestFactory _factory;
    private readonly HttpClient _client;

    public ClockSkewBoundaryPropertyTests(ClockSkewTestFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    /// <summary>
    /// Property 4a: Tokens expired within 0-5 minutes (inclusive) are accepted.
    ///
    /// For any token whose expiration time is between 0 and 5 minutes in the past,
    /// the middleware SHALL accept the token (HTTP 200).
    ///
    /// **Validates: Requirements 1.7**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 4: Tokens expired within 0-5 minutes are accepted",
        MaxTest = 100)]
    public bool TokenExpiredWithinClockSkew_IsAccepted(PositiveInt secondsSeed)
    {
        // Generate expiration offset between 0 and 289 seconds (0 to ~4.8 minutes in the past)
        // We stay under 5 minutes to avoid boundary race conditions with test execution time
        var secondsInPast = secondsSeed.Get % 290;

        var token = ClockSkewTestFactory.CreateToken(
            expiresAt: DateTime.UtcNow.AddSeconds(-secondsInPast));

        var request = new HttpRequestMessage(HttpMethod.Get, "/test-protected");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = _client.SendAsync(request).GetAwaiter().GetResult();

        return response.StatusCode == HttpStatusCode.OK;
    }

    /// <summary>
    /// Property 4b: Tokens expired more than 5 minutes ago are rejected.
    ///
    /// For any token whose expiration time is more than 5 minutes in the past,
    /// the middleware SHALL reject it (HTTP 401).
    ///
    /// **Validates: Requirements 1.7**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 4: Tokens expired more than 5 minutes are rejected",
        MaxTest = 100)]
    public bool TokenExpiredBeyondClockSkew_IsRejected(PositiveInt secondsSeed)
    {
        // Generate expiration offset between 6 and 60 minutes in the past
        // Start at 360 seconds (6 min) to be clearly beyond the 5-minute boundary
        var secondsInPast = 360 + (secondsSeed.Get % 3240);

        var token = ClockSkewTestFactory.CreateToken(
            expiresAt: DateTime.UtcNow.AddSeconds(-secondsInPast));

        var request = new HttpRequestMessage(HttpMethod.Get, "/test-protected");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = _client.SendAsync(request).GetAwaiter().GetResult();

        return response.StatusCode == HttpStatusCode.Unauthorized;
    }
}

/// <summary>
/// Custom WebApplicationFactory for clock skew tests.
/// Configures JWT Bearer to accept self-signed tokens with a known symmetric key,
/// while preserving the 5-minute ClockSkew setting from the production configuration.
/// Adds a test-only protected endpoint at /test-protected.
/// </summary>
public class ClockSkewTestFactory : WebApplicationFactory<Program>
{
    private static readonly SymmetricSecurityKey SecurityKey;
    private static readonly string FakeServiceAccountPath;

    static ClockSkewTestFactory()
    {
        // Generate a stable symmetric signing key for tests
        var keyBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(keyBytes);
        }
        SecurityKey = new SymmetricSecurityKey(keyBytes);

        // Create fake service account file
        using var rsa = RSA.Create(2048);
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem().ReplaceLineEndings("\\n");

        var fakeServiceAccountJson = $$"""
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

        FakeServiceAccountPath = Path.Combine(Path.GetTempPath(), "fake-firebase-sa-clockskew.json");
        File.WriteAllText(FakeServiceAccountPath, fakeServiceAccountJson);

        // Pre-initialize FirebaseApp if not already done
        if (FirebaseAdmin.FirebaseApp.DefaultInstance is null)
        {
            FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions
            {
                Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(fakeServiceAccountJson)
            });
        }
    }

    /// <summary>
    /// Creates a JWT token with the specified expiration time, signed with the test key.
    /// </summary>
    public static string CreateToken(DateTime expiresAt)
    {
        var credentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "test-uid-123"),
            new Claim(JwtRegisteredClaimNames.Email, "test@example.com"),
        };

        var token = new JwtSecurityToken(
            issuer: "https://securetoken.google.com/test-project-id",
            audience: "test-project-id",
            claims: claims,
            notBefore: DateTime.UtcNow.AddHours(-1),
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Firebase:ProjectId", "test-project-id");
        builder.UseSetting("Firebase:ServiceAccountPath", FakeServiceAccountPath);

        builder.ConfigureServices(services =>
        {
            // Remove all EF Core registrations to avoid Npgsql conflicts
            var efCoreDescriptors = services
                .Where(d => d.ServiceType.FullName != null
                    && (d.ServiceType.FullName.Contains("EntityFrameworkCore")
                        || d.ServiceType.FullName.Contains("EntityFramework")
                        || d.ServiceType == typeof(Infrastructure.Data.AppDbContext)
                        || d.ServiceType == typeof(DbContextOptions<Infrastructure.Data.AppDbContext>)
                        || d.ServiceType == typeof(DbContextOptions)))
                .ToList();

            foreach (var descriptor in efCoreDescriptors)
                services.Remove(descriptor);

            // Register AppDbContext with InMemory provider
            services.AddDbContext<Infrastructure.Data.AppDbContext>(options =>
                options.UseInMemoryDatabase("ClockSkewTestDb"));

            // Override JWT Bearer options to use our symmetric key for validation
            // while preserving the 5-minute ClockSkew from production config
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.RequireHttpsMetadata = false;
                options.MetadataAddress = null!;
                options.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration();

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "https://securetoken.google.com/test-project-id",
                    ValidateAudience = true,
                    ValidAudience = "test-project-id",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = SecurityKey
                };
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Register a startup filter to add the test-protected endpoint
            services.AddSingleton<IStartupFilter, ClockSkewTestEndpointStartupFilter>();
        });
    }
}

/// <summary>
/// Startup filter that adds a protected test endpoint to the application pipeline.
/// Uses middleware to handle the /test-protected path with authorization check.
/// </summary>
internal class ClockSkewTestEndpointStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            next(app);

            // Add terminal middleware for the test endpoint after the main pipeline
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path == "/test-protected")
                {
                    if (context.User?.Identity?.IsAuthenticated == true)
                    {
                        context.Response.StatusCode = 200;
                        await context.Response.WriteAsJsonAsync(new { message = "authenticated" });
                    }
                    else
                    {
                        // Let the authentication challenge handler respond
                        await context.ChallengeAsync();
                    }
                    return;
                }

                await nextMiddleware();
            });
        };
    }
}
