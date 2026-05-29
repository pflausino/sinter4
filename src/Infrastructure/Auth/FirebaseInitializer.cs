using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Auth;

internal static class FirebaseInitializer
{
    public static void Initialize(IConfiguration configuration)
    {
        // Skip if already initialized (idempotent for test scenarios)
        if (FirebaseApp.DefaultInstance is not null)
            return;

        var serviceAccountPath = configuration["Firebase:ServiceAccountPath"];

        ValidateServiceAccountPath(serviceAccountPath);

        try
        {
            var credential = GoogleCredential.FromFile(serviceAccountPath!);
            FirebaseApp.Create(new AppOptions { Credential = credential });
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"Firebase service account file at '{serviceAccountPath}' contains invalid credentials", ex);
        }
    }

    /// <summary>
    /// Validates that the service account path is configured and the file exists.
    /// Throws descriptive exceptions if validation fails.
    /// </summary>
    internal static void ValidateServiceAccountPath(string? serviceAccountPath)
    {
        if (string.IsNullOrWhiteSpace(serviceAccountPath))
            throw new InvalidOperationException(
                "Required configuration key 'Firebase:ServiceAccountPath' is missing or empty");

        if (!File.Exists(serviceAccountPath))
            throw new FileNotFoundException(
                $"Firebase service account file not found at path: {serviceAccountPath}",
                serviceAccountPath);
    }

    /// <summary>
    /// Validates that the file at the given path contains valid service account JSON.
    /// Throws if the content is malformed or missing required fields.
    /// </summary>
    internal static void ValidateServiceAccountContent(string serviceAccountPath)
    {
        try
        {
            GoogleCredential.FromFile(serviceAccountPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Firebase service account file at '{serviceAccountPath}' contains invalid credentials", ex);
        }
    }
}
