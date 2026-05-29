using System.ComponentModel.DataAnnotations;
using FsCheck;
using FsCheck.Xunit;
using Web.Components.Pages;

namespace Web.Tests;

/// <summary>
/// Feature: login-page, Property 2: Invalid email format is rejected
/// **Validates: Requirements 4.5, 8.4**
/// </summary>
public class LoginModelInvalidEmailPropertyTests
{
    private static readonly char[] AlphaChars =
        "abcdefghijklmnopqrstuvwxyz".ToCharArray();

    /// <summary>
    /// Generates a string of given length using only alpha characters (no '@').
    /// </summary>
    private static string GenerateAlphaString(int seed, int length)
    {
        var rng = new Random(seed);
        return new string(Enumerable.Range(0, length)
            .Select(_ => AlphaChars[rng.Next(AlphaChars.Length)])
            .ToArray());
    }

    /// <summary>
    /// Generates a guaranteed-invalid email based on a strategy index.
    /// All strategies produce strings that .NET's EmailAddressAttribute will reject.
    ///
    /// Note: EmailAddressAttribute is extremely lenient — it accepts spaces, dots,
    /// commas, semicolons, and many other characters. The only reliable invalid patterns are:
    ///
    /// 0 - No '@' character at all (plain alphabetic string)
    /// 1 - Starts with '@' (missing local part)
    /// 2 - Ends with '@' (missing domain)
    /// 3 - Multiple '@' characters
    /// </summary>
    private static string GenerateInvalidEmail(int strategySeed, int contentSeed)
    {
        var strategy = Math.Abs(strategySeed) % 4;
        var len = (Math.Abs(contentSeed) % 20) + 3; // 3-22 chars for content parts

        return strategy switch
        {
            // Strategy 0: No '@' at all — just letters
            0 => GenerateAlphaString(contentSeed, len),

            // Strategy 1: Starts with '@' — e.g. "@domain.com"
            1 => "@" + GenerateAlphaString(contentSeed, len) + ".com",

            // Strategy 2: Ends with '@' — e.g. "user@"
            2 => GenerateAlphaString(contentSeed, len) + "@",

            // Strategy 3: Multiple '@' signs — e.g. "user@mid@domain.com"
            3 => GenerateAlphaString(contentSeed, len / 3 + 1) + "@" +
                 GenerateAlphaString(contentSeed + 1, len / 3 + 1) + "@" +
                 GenerateAlphaString(contentSeed + 2, len / 3 + 1) + ".com",

            _ => "invalid"
        };
    }

    /// <summary>
    /// Property 2: Invalid email format is rejected
    /// For any string that does not conform to a valid email address format,
    /// the LoginModel SHALL fail validation with an email-format error message,
    /// regardless of the password value.
    /// **Validates: Requirements 4.5, 8.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: login-page, Property 2: Invalid email format is rejected",
        MaxTest = 20)]
    public bool InvalidEmail_FailsValidation_WithEmailFormatError(
        PositiveInt strategySeed,
        PositiveInt contentSeed)
    {
        var invalidEmail = GenerateInvalidEmail(strategySeed.Get, contentSeed.Get);

        // Use a valid password to isolate email validation
        var password = "ValidPass123";

        var model = new Login.LoginModel
        {
            Email = invalidEmail,
            Password = password
        };

        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        // Must fail validation
        if (isValid)
            return false;

        // Must contain the email format error message
        var hasEmailError = results.Any(r =>
            r.ErrorMessage == "Formato de email inválido.");

        return hasEmailError;
    }
}
