using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.Tests;

/// <summary>
/// Authentication handler that auto-authenticates all requests in the test environment.
/// Produces an authenticated ClaimsPrincipal with a NameIdentifier claim.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";
    public const string TestUserId = "test-user-id";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestUserId),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Email, "test@example.com")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly string FakeServiceAccountPath;

    public static string GetFakeServiceAccountPath() => FakeServiceAccountPath;

    static CustomWebApplicationFactory()
    {
        // Generate a PKCS8 private key for the fake service account
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

        FakeServiceAccountPath = Path.Combine(Path.GetTempPath(), "fake-firebase-sa-test.json");
        File.WriteAllText(FakeServiceAccountPath, fakeServiceAccountJson);

        // Pre-initialize FirebaseApp so that FirebaseInitializer.Initialize() skips
        // (it checks FirebaseApp.DefaultInstance is not null).
        if (FirebaseAdmin.FirebaseApp.DefaultInstance is null)
        {
            FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions
            {
                Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(fakeServiceAccountJson)
            });
        }
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
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
                        || d.ServiceType == typeof(AppDbContext)
                        || d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                        || d.ServiceType == typeof(DbContextOptions)))
                .ToList();

            foreach (var descriptor in efCoreDescriptors)
                services.Remove(descriptor);

            // Register AppDbContext with InMemory provider
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace the default authentication scheme with TestAuthHandler
            // so that all requests are auto-authenticated without real JWT tokens
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        });
    }
}
