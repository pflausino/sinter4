using FsCheck;
using FsCheck.Xunit;
using Web.Services;

namespace Web.Tests;

/// <summary>
/// Feature: firebase-authentication, Property 6: Firebase error codes map to user-friendly messages
/// **Validates: Requirements 3.3**
/// </summary>
public class FirebaseErrorCodeMappingPropertyTests
{
    /// <summary>
    /// Known Firebase Authentication error codes that can be returned by the REST API.
    /// </summary>
    private static readonly string[] KnownFirebaseErrorCodes =
    [
        "EMAIL_NOT_FOUND",
        "INVALID_PASSWORD",
        "USER_DISABLED",
        "TOO_MANY_ATTEMPTS_TRY_LATER",
        "INVALID_LOGIN_CREDENTIALS",
        "OPERATION_NOT_ALLOWED",
        "WEAK_PASSWORD",
        "INVALID_EMAIL",
        "CREDENTIAL_TOO_OLD_LOGIN_AGAIN",
        "TOKEN_EXPIRED",
        "INVALID_ID_TOKEN",
        "USER_NOT_FOUND",
        "ADMIN_ONLY_OPERATION",
        "INVALID_OOB_CODE"
    ];

    /// <summary>
    /// Characters used to generate arbitrary error code strings.
    /// Firebase error codes use uppercase letters, underscores, and sometimes digits.
    /// </summary>
    private static readonly char[] ErrorCodeChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789".ToCharArray();

    /// <summary>
    /// Patterns that should NEVER appear in user-facing messages.
    /// These represent internal error codes, HTTP status codes, or stack trace indicators.
    /// </summary>
    private static readonly string[] ForbiddenPatterns =
    [
        "EMAIL_NOT_FOUND",
        "INVALID_PASSWORD",
        "USER_DISABLED",
        "TOO_MANY_ATTEMPTS_TRY_LATER",
        "INVALID_LOGIN_CREDENTIALS",
        "OPERATION_NOT_ALLOWED",
        "WEAK_PASSWORD",
        "INVALID_EMAIL",
        "CREDENTIAL_TOO_OLD_LOGIN_AGAIN",
        "TOKEN_EXPIRED",
        "INVALID_ID_TOKEN",
        "USER_NOT_FOUND",
        "ADMIN_ONLY_OPERATION",
        "INVALID_OOB_CODE",
        "Exception",
        "StackTrace",
        "   at ",
        "System.",
        "NullReferenceException",
        "InvalidOperationException"
    ];

    /// <summary>
    /// HTTP status codes that should never appear in user-facing messages.
    /// </summary>
    private static readonly string[] HttpStatusCodes =
    [
        "400", "401", "403", "404", "429", "500", "503"
    ];

    /// <summary>
    /// Builds a Firebase-style error code from seed values.
    /// Generates either a known error code or a random uppercase+underscore string.
    /// </summary>
    private static string BuildErrorCode(PositiveInt seed)
    {
        // 50% chance of known code, 50% chance of random code
        if (seed.Get % 2 == 0)
        {
            return KnownFirebaseErrorCodes[seed.Get % KnownFirebaseErrorCodes.Length];
        }

        // Generate a random Firebase-style error code (uppercase + underscores)
        var len = (seed.Get % 30) + 3; // 3-32 characters
        var rng = new Random(seed.Get);
        return new string(Enumerable.Range(0, len)
            .Select(_ => ErrorCodeChars[rng.Next(ErrorCodeChars.Length)])
            .ToArray());
    }

    /// <summary>
    /// Property 6: Firebase error codes map to user-friendly messages
    ///
    /// For any Firebase Authentication error code (known or arbitrary), the mapped user-facing
    /// message SHALL NOT contain the original Firebase error code, HTTP status codes, or stack traces.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 6: Firebase error codes map to user-friendly messages",
        MaxTest = 100)]
    public bool MappedMessage_DoesNotContain_InternalErrorCodes(PositiveInt seed)
    {
        var errorCode = BuildErrorCode(seed);
        var message = FirebaseTokenProvider.MapFirebaseErrorCode(errorCode);

        // The mapped message must not be null or empty (must provide user-friendly text)
        if (string.IsNullOrWhiteSpace(message))
            return false;

        // The mapped message must not contain the original error code
        if (message.Contains(errorCode, StringComparison.OrdinalIgnoreCase))
            return false;

        // The mapped message must not contain any known Firebase error code patterns
        foreach (var pattern in ForbiddenPatterns)
        {
            if (message.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // The mapped message must not contain HTTP status codes
        foreach (var code in HttpStatusCodes)
        {
            if (message.Contains(code, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Property 6 (supplementary): Known Firebase error codes always produce a localized message
    /// that ends with a period (Portuguese convention) and does not expose the error code.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(
        DisplayName = "Feature: firebase-authentication, Property 6: Known error codes produce localized messages",
        MaxTest = 100)]
    public bool KnownErrorCodes_ProduceLocalized_Messages(PositiveInt seed)
    {
        var errorCode = KnownFirebaseErrorCodes[seed.Get % KnownFirebaseErrorCodes.Length];
        var message = FirebaseTokenProvider.MapFirebaseErrorCode(errorCode);

        // Message must not be null or empty
        if (string.IsNullOrWhiteSpace(message))
            return false;

        // Message must end with a period (Portuguese convention)
        if (!message.EndsWith('.'))
            return false;

        // Message must not contain the error code itself
        if (message.Contains(errorCode, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
