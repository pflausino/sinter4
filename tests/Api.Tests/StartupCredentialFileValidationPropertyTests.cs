using FsCheck;
using FsCheck.Xunit;
using Infrastructure.Auth;

namespace Api.Tests;

/// <summary>
/// Feature: firebase-authentication, Property 5: Startup credential file validation
/// **Validates: Requirements 2.3, 2.4, 6.5**
///
/// For any file path configured as Firebase:ServiceAccountPath, if the file does not exist
/// OR contains content that is not valid service account JSON, the application SHALL throw
/// an exception at startup whose message contains the file path.
/// </summary>
public class StartupCredentialFileValidationPropertyTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    private static readonly char[] PathSegmentChars =
        "abcdefghijklmnopqrstuvwxyz0123456789-_".ToCharArray();

    private static readonly string[] InvalidContents =
    [
        "not json at all",
        "{}",
        "{\"type\": \"service_account\"",
        """{"type": "service_account", "project_id": "test"}""",
        """{"project_id": "test", "private_key": "fake"}""",
        "<xml>not json</xml>",
        "null",
        "[]",
        """{"type": "not_service_account", "project_id": "x", "private_key_id": "k", "private_key": "bad", "client_email": "e@e.com", "client_id": "1", "auth_uri": "u", "token_uri": "t"}""",
        """{"type": "service_account", "project_id": "x", "private_key_id": "k", "private_key": "not-a-real-key", "client_email": "e@e.com", "client_id": "1", "auth_uri": "u", "token_uri": "t"}""",
        "12345",
        "{invalid json{{{",
        "",
        "   ",
        """{"type": "service_account", "project_id": "", "private_key_id": "", "private_key": "", "client_email": "", "client_id": "", "auth_uri": "", "token_uri": ""}"""
    ];

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { /* best effort cleanup */ }
        }
    }

    /// <summary>
    /// Builds a non-existent file path from seed values.
    /// Uses the seed to generate random path segments that are guaranteed not to exist.
    /// </summary>
    private static string BuildNonExistentPath(PositiveInt seed1, PositiveInt seed2)
    {
        var rng = new Random(seed1.Get);
        var depth = (seed1.Get % 3) + 1; // 1-3 segments
        var segments = new string[depth];

        for (var i = 0; i < depth; i++)
        {
            var len = (rng.Next() % 15) + 3; // 3-17 chars
            segments[i] = new string(Enumerable.Range(0, len)
                .Select(_ => PathSegmentChars[rng.Next(PathSegmentChars.Length)])
                .ToArray());
        }

        var fileName = new string(Enumerable.Range(0, (seed2.Get % 12) + 3)
            .Select(_ => PathSegmentChars[rng.Next(PathSegmentChars.Length)])
            .ToArray());

        var basePath = Path.Combine(Path.GetTempPath(), "nonexistent_firebase_pbt");
        return Path.Combine(basePath, Path.Combine(segments), $"{fileName}.json");
    }

    /// <summary>
    /// Builds random invalid file content from a seed.
    /// Combines predefined invalid patterns with random string generation.
    /// </summary>
    private static string BuildInvalidContent(PositiveInt seed)
    {
        var index = seed.Get % (InvalidContents.Length + 5); // extra slots for random content

        if (index < InvalidContents.Length)
            return InvalidContents[index];

        // Generate random string content
        var rng = new Random(seed.Get);
        var len = (seed.Get % 200) + 1;
        var chars = Enumerable.Range(0, len)
            .Select(_ => (char)rng.Next(32, 127))
            .ToArray();
        return new string(chars);
    }

    /// <summary>
    /// Property 5a: Non-existent file path throws exception containing the path.
    ///
    /// For any file path that does not exist on disk, ValidateServiceAccountPath SHALL throw
    /// a FileNotFoundException whose message contains the file path.
    ///
    /// **Validates: Requirements 2.3, 6.5**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 5: Non-existent file path throws exception containing the path",
        MaxTest = 100)]
    public bool NonExistentFilePath_ThrowsException_ContainingPath(PositiveInt seed1, PositiveInt seed2)
    {
        var path = BuildNonExistentPath(seed1, seed2);

        // Ensure the path truly doesn't exist (extremely unlikely but guard against it)
        if (File.Exists(path))
            return true;

        try
        {
            FirebaseInitializer.ValidateServiceAccountPath(path);
            return false; // Should have thrown
        }
        catch (FileNotFoundException ex)
        {
            return ex.Message.Contains(path);
        }
        catch
        {
            return false; // Wrong exception type
        }
    }

    /// <summary>
    /// Property 5b: Invalid file content throws exception containing the path.
    ///
    /// For any file that exists but contains content that is not valid service account JSON,
    /// ValidateServiceAccountContent SHALL throw an InvalidOperationException whose message
    /// contains the file path.
    ///
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 5: Invalid file content throws exception containing the path",
        MaxTest = 100)]
    public bool InvalidFileContent_ThrowsException_ContainingPath(PositiveInt seed)
    {
        var invalidContent = BuildInvalidContent(seed);

        // Create a temp file with invalid content
        var tempPath = Path.Combine(Path.GetTempPath(), $"firebase_pbt_{Guid.NewGuid():N}.json");
        _tempFiles.Add(tempPath);
        File.WriteAllText(tempPath, invalidContent);

        try
        {
            FirebaseInitializer.ValidateServiceAccountContent(tempPath);
            return false; // Should have thrown for invalid content
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message.Contains(tempPath);
        }
        catch
        {
            return false; // Wrong exception type
        }
    }
}
