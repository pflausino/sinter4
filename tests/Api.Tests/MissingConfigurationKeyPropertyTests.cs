using System.Security.Cryptography;
using FsCheck;
using FsCheck.Xunit;
using Infrastructure.Auth;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Feature: firebase-authentication, Property 9: Missing configuration key exception identifies the key
/// **Validates: Requirements 6.4**
/// </summary>
[Collection("FirebaseApp")]
public class MissingConfigurationKeyPropertyTests : IDisposable
{
    private static readonly object FirebaseAppLock = new();

    /// <summary>
    /// Generates an empty-ish value based on a seed:
    /// - 0: empty string
    /// - 1: single space
    /// - 2+: whitespace string of varying length
    /// </summary>
    private static string GenerateEmptyValue(int seed)
    {
        var variant = Math.Abs(seed) % 5;
        return variant switch
        {
            0 => string.Empty,
            1 => " ",
            2 => "  ",
            3 => "\t",
            _ => new string(' ', (Math.Abs(seed) % 10) + 1)
        };
    }

    /// <summary>
    /// Property 9: Missing configuration key exception identifies the key
    ///
    /// For any required Firebase configuration key (ProjectId or ServiceAccountPath) that is
    /// missing or empty at startup, the application SHALL throw an exception whose message
    /// contains both the key name and the configuration section path Firebase:{KeyName}.
    ///
    /// This test verifies that when ProjectId is missing or empty, the application throws
    /// an InvalidOperationException whose message contains both "ProjectId" and "Firebase:ProjectId".
    ///
    /// **Validates: Requirements 6.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 9: Missing configuration key exception identifies the key - ProjectId",
        MaxTest = 100)]
    public bool MissingProjectId_ThrowsException_ContainingKeyNameAndSectionPath(PositiveInt seed)
    {
        const string keyName = "ProjectId";
        const string sectionPath = "Firebase:ProjectId";

        var emptyValue = GenerateEmptyValue(seed.Get);

        // Ensure FirebaseApp is initialized so FirebaseInitializer.Initialize() skips
        // and we reach the ProjectId validation in Program.cs
        lock (FirebaseAppLock)
        {
            EnsureFirebaseAppInitialized();
        }

        try
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    // Set ProjectId to the empty value
                    builder.UseSetting("Firebase:ProjectId", emptyValue);

                    // Provide a valid ServiceAccountPath (FirebaseInitializer will skip anyway)
                    builder.UseSetting("Firebase:ServiceAccountPath", "/tmp/fake.json");

                    builder.ConfigureServices(services =>
                    {
                        RemoveEfCore(services);
                        services.AddDbContext<AppDbContext>(options =>
                            options.UseInMemoryDatabase($"TestDb-{Guid.NewGuid()}"));
                    });
                });

            // Creating the server triggers the startup pipeline
            _ = factory.Server;

            // If we get here, no exception was thrown — test fails
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains(keyName) && ex.Message.Contains(sectionPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Property 9: Missing configuration key exception identifies the key
    ///
    /// For any required Firebase configuration key (ProjectId or ServiceAccountPath) that is
    /// missing or empty at startup, the application SHALL throw an exception whose message
    /// contains both the key name and the configuration section path Firebase:{KeyName}.
    ///
    /// This test verifies that when ServiceAccountPath is missing or empty,
    /// FirebaseInitializer.Initialize() throws an InvalidOperationException whose message
    /// contains both "ServiceAccountPath" and "Firebase:ServiceAccountPath".
    ///
    /// **Validates: Requirements 6.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 9: Missing configuration key exception identifies the key - ServiceAccountPath",
        MaxTest = 100)]
    public bool MissingServiceAccountPath_ThrowsException_ContainingKeyNameAndSectionPath(PositiveInt seed)
    {
        const string keyName = "ServiceAccountPath";
        const string sectionPath = "Firebase:ServiceAccountPath";

        var emptyValue = GenerateEmptyValue(seed.Get);

        lock (FirebaseAppLock)
        {
            try
            {
                // Delete the default FirebaseApp so Initialize() runs the validation
                FirebaseAdmin.FirebaseApp.DefaultInstance?.Delete();

                var configData = new Dictionary<string, string?>
                {
                    ["Firebase:ProjectId"] = "test-project-id",
                    ["Firebase:ServiceAccountPath"] = emptyValue
                };

                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configData)
                    .Build();

                // Call the actual FirebaseInitializer.Initialize() method
                FirebaseInitializer.Initialize(configuration);

                // If we get here, no exception was thrown — test fails
                return false;
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message.Contains(keyName) && ex.Message.Contains(sectionPath);
            }
            catch
            {
                return false;
            }
            finally
            {
                EnsureFirebaseAppInitialized();
            }
        }
    }

    public void Dispose()
    {
        lock (FirebaseAppLock)
        {
            EnsureFirebaseAppInitialized();
        }
    }

    private static void EnsureFirebaseAppInitialized()
    {
        if (FirebaseAdmin.FirebaseApp.DefaultInstance is not null)
            return;

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

        FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions
        {
            Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(fakeServiceAccountJson)
        });
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
}
