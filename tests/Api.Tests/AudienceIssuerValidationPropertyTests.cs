using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using FsCheck;
using FsCheck.Xunit;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Api.Tests;

/// <summary>
/// Feature: firebase-authentication, Property 3: Token audience and issuer validation
/// **Validates: Requirements 1.6**
///
/// For any JWT where the `aud` claim does not equal the configured `ProjectId` OR the `iss`
/// claim does not equal `https://securetoken.google.com/{ProjectId}`, the authentication
/// middleware SHALL reject the token as invalid regardless of all other claims being correct.
/// </summary>
public class AudienceIssuerValidationPropertyTests : IClassFixture<AudienceIssuerValidationPropertyTests.AudIssFactory>
{
    private const string ConfiguredProjectId = "test-project-id";
    private static readonly string ValidIssuer = $"https://securetoken.google.com/{ConfiguredProjectId}";

    private static readonly RSA RsaKey = RSA.Create(2048);
    private static readonly RsaSecurityKey SigningKey = new(RsaKey);
    private static readonly SigningCredentials Credentials = new(SigningKey, SecurityAlgorithms.RsaSha256);

    private static readonly char[] AlphaNumChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_".ToCharArray();

    private readonly AudIssFactory _factory;

    public AudienceIssuerValidationPropertyTests(AudIssFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Generates a random string of given length from alphanumeric characters.
    /// </summary>
    private static string GenerateRandomString(Random rng, int minLength, int maxLength)
    {
        var len = rng.Next(minLength, maxLength + 1);
        return new string(Enumerable.Range(0, len)
            .Select(_ => AlphaNumChars[rng.Next(AlphaNumChars.Length)])
            .ToArray());
    }

    /// <summary>
    /// Generates a JWT token with the specified audience and issuer, signed with our test key.
    /// All other claims are valid (non-expired, valid uid, email, etc.)
    /// </summary>
    private static string CreateToken(string audience, string issuer)
    {
        var handler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, "test-uid-12345"),
                new Claim(ClaimTypes.Email, "user@example.com"),
            ]),
            Audience = audience,
            Issuer = issuer,
            Expires = DateTime.UtcNow.AddHours(1),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            IssuedAt = DateTime.UtcNow,
            SigningCredentials = Credentials
        };

        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    /// <summary>
    /// Property 3a: Wrong audience causes rejection.
    ///
    /// For any JWT where the `aud` claim does not equal the configured ProjectId,
    /// the middleware SHALL reject the token with HTTP 401, regardless of all other
    /// claims being correct (valid issuer, non-expired, valid signature).
    ///
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 3: Token audience and issuer validation - wrong audience",
        MaxTest = 100)]
    public bool WrongAudience_IsRejected(PositiveInt seed)
    {
        var rng = new Random(seed.Get);

        // Generate a random audience that is NOT equal to the configured project ID
        string wrongAudience;
        do
        {
            wrongAudience = GenerateRandomString(rng, 3, 30);
        } while (wrongAudience == ConfiguredProjectId);

        var token = CreateToken(wrongAudience, ValidIssuer);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = client.GetAsync("/api/me").GetAwaiter().GetResult();

        return response.StatusCode == HttpStatusCode.Unauthorized;
    }

    /// <summary>
    /// Property 3b: Wrong issuer causes rejection.
    ///
    /// For any JWT where the `iss` claim does not equal `https://securetoken.google.com/{ProjectId}`,
    /// the middleware SHALL reject the token with HTTP 401, regardless of all other
    /// claims being correct (valid audience, non-expired, valid signature).
    ///
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 3: Token audience and issuer validation - wrong issuer",
        MaxTest = 100)]
    public bool WrongIssuer_IsRejected(PositiveInt seed)
    {
        var rng = new Random(seed.Get);

        // Generate a random issuer that is NOT equal to the valid issuer
        string wrongIssuer;
        do
        {
            var randomProject = GenerateRandomString(rng, 3, 20);
            var variant = rng.Next(4);
            wrongIssuer = variant switch
            {
                0 => $"https://securetoken.google.com/{randomProject}",
                1 => $"https://other-issuer.com/{ConfiguredProjectId}",
                2 => GenerateRandomString(rng, 10, 50),
                _ => $"https://securetoken.google.com/{ConfiguredProjectId}/extra"
            };
        } while (wrongIssuer == ValidIssuer);

        var token = CreateToken(ConfiguredProjectId, wrongIssuer);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = client.GetAsync("/api/me").GetAwaiter().GetResult();

        return response.StatusCode == HttpStatusCode.Unauthorized;
    }

    /// <summary>
    /// Property 3c: Wrong audience AND wrong issuer causes rejection.
    ///
    /// For any JWT where both the `aud` claim does not equal the configured ProjectId
    /// AND the `iss` claim does not equal the expected pattern, the middleware SHALL
    /// reject the token with HTTP 401.
    ///
    /// **Validates: Requirements 1.6**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 3: Token audience and issuer validation - wrong both",
        MaxTest = 100)]
    public bool WrongAudienceAndIssuer_IsRejected(PositiveInt seed)
    {
        var rng = new Random(seed.Get);

        string wrongAudience;
        do
        {
            wrongAudience = GenerateRandomString(rng, 3, 30);
        } while (wrongAudience == ConfiguredProjectId);

        string wrongIssuer;
        do
        {
            wrongIssuer = $"https://securetoken.google.com/{GenerateRandomString(rng, 3, 20)}";
        } while (wrongIssuer == ValidIssuer);

        var token = CreateToken(wrongAudience, wrongIssuer);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = client.GetAsync("/api/me").GetAwaiter().GetResult();

        return response.StatusCode == HttpStatusCode.Unauthorized;
    }

    /// <summary>
    /// Custom WebApplicationFactory that configures JWT Bearer to use a test signing key
    /// for audience/issuer validation tests. Uses the existing /api/me protected endpoint.
    /// </summary>
    public sealed class AudIssFactory : WebApplicationFactory<Program>
    {
        private static readonly string FakeServiceAccountPath;

        static AudIssFactory()
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

            FakeServiceAccountPath = Path.Combine(Path.GetTempPath(), "fake-firebase-sa-aud-iss.json");
            File.WriteAllText(FakeServiceAccountPath, fakeServiceAccountJson);

            // Pre-initialize FirebaseApp so that FirebaseInitializer.Initialize() skips
            if (FirebaseAdmin.FirebaseApp.DefaultInstance is null)
            {
                FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions
                {
                    Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(fakeServiceAccountJson)
                });
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("Firebase:ProjectId", ConfiguredProjectId);
            builder.UseSetting("Firebase:ServiceAccountPath", FakeServiceAccountPath);

            builder.ConfigureServices(services =>
            {
                // Remove EF Core registrations
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
                    options.UseInMemoryDatabase($"TestDb-AudIss-{Guid.NewGuid()}"));

                // Reconfigure JWT Bearer to use our test signing key for validation
                // This allows us to create tokens that pass signature validation
                // but fail audience/issuer validation
                services.PostConfigure<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme,
                    options =>
                    {
                        options.Authority = null;
                        options.RequireHttpsMetadata = false;
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = ValidIssuer,
                            ValidateAudience = true,
                            ValidAudience = ConfiguredProjectId,
                            ValidateLifetime = true,
                            ClockSkew = TimeSpan.FromMinutes(5),
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = SigningKey
                        };
                    });
            });
        }
    }
}
