using System.Text.Json;

namespace Web.Services;

/// <summary>
/// Reads the <c>exp</c> claim of a JWT without validating the token (signature checks are the API's job).
/// </summary>
internal static class JwtExpiration
{
    /// <summary>
    /// Returns the token's expiration, or null when the token is malformed or has no numeric <c>exp</c> claim.
    /// </summary>
    public static DateTimeOffset? Read(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3)
        {
            return null;
        }

        var jsonBytes = DecodeBase64Url(parts[1]);
        if (jsonBytes is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(jsonBytes);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("exp", out var exp)
                && exp.TryGetInt64(out var expUnix))
            {
                return DateTimeOffset.FromUnixTimeSeconds(expUnix);
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    /// <summary>
    /// Decodes a base64url segment (no padding, URL-safe alphabet) as used in JWTs.
    /// </summary>
    public static byte[]? DecodeBase64Url(string base64)
    {
        try
        {
            // Replace URL-safe characters
            var output = base64.Replace('-', '+').Replace('_', '/');

            // Add padding if necessary
            switch (output.Length % 4)
            {
                case 2: output += "=="; break;
                case 3: output += "="; break;
                case 0: break;
                default: return null;
            }

            return Convert.FromBase64String(output);
        }
        catch
        {
            return null;
        }
    }
}
