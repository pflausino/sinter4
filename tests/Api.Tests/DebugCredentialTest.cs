using Google.Apis.Auth.OAuth2;
using Infrastructure.Auth;

namespace Api.Tests;

public class DebugCredentialTest
{
    [Fact]
    public void EmptyJson_ExceptionType()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"debug_test_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempPath, "{}");
            var ex = Record.Exception(() => GoogleCredential.FromFile(tempPath));
            Assert.NotNull(ex);
            // Output the exception details
            Assert.Fail($"Exception type: {ex.GetType().FullName}, Message: {ex.Message}");
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void EmptyJson_ValidateServiceAccountContent()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"debug_test2_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempPath, "{}");
            var ex = Record.Exception(() => FirebaseInitializer.ValidateServiceAccountContent(tempPath));
            if (ex is null)
            {
                Assert.Fail("ValidateServiceAccountContent did NOT throw for empty JSON");
            }
            else
            {
                Assert.Fail($"Exception type: {ex.GetType().FullName}, Message: {ex.Message}, Contains path: {ex.Message.Contains(tempPath)}");
            }
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void NotJsonAtAll_ValidateServiceAccountContent()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"debug_test3_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempPath, "not json at all");
            var ex = Record.Exception(() => FirebaseInitializer.ValidateServiceAccountContent(tempPath));
            if (ex is null)
            {
                Assert.Fail("ValidateServiceAccountContent did NOT throw for non-JSON content");
            }
            else
            {
                Assert.Fail($"Exception type: {ex.GetType().FullName}, Message: {ex.Message}, Contains path: {ex.Message.Contains(tempPath)}");
            }
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
