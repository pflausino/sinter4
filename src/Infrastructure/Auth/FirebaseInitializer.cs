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

        if (string.IsNullOrWhiteSpace(serviceAccountPath))
            throw new InvalidOperationException(
                "Required configuration key 'Firebase:ServiceAccountPath' is missing or empty");

        if (!File.Exists(serviceAccountPath))
            throw new FileNotFoundException(
                $"Firebase service account file not found at path: {serviceAccountPath}",
                serviceAccountPath);

        try
        {
            var credential = GoogleCredential.FromFile(serviceAccountPath);
            FirebaseApp.Create(new AppOptions { Credential = credential });
        }
        catch (Exception ex) when (ex is not FileNotFoundException && ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Firebase service account file at '{serviceAccountPath}' contains invalid credentials", ex);
        }
    }
}
