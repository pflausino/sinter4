using System.ComponentModel.DataAnnotations;
using FsCheck;
using FsCheck.Xunit;
using Web.Components.Pages;

namespace Web.Tests;

/// <summary>
/// Feature: login-page, Property 3: Short password is rejected
/// **Validates: Requirements 5.4, 8.4**
/// </summary>
public class LoginModelShortPasswordPropertyTests
{
    private static readonly char[] PasswordChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_=+"
            .ToCharArray();

    /// <summary>
    /// Property 3: Short password is rejected
    /// For any non-empty string with fewer than 6 characters as the password,
    /// the LoginModel SHALL fail validation with a minimum-length error message,
    /// regardless of the email value.
    /// **Validates: Requirements 5.4, 8.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: login-page, Property 3: Short password is rejected",
        MaxTest = 20)]
    public bool ShortPassword_FailsValidation_WithMinLengthError(
        PositiveInt lengthSeed,
        PositiveInt charSeed)
    {
        // Generate a password with length 1-5
        var length = (lengthSeed.Get % 5) + 1; // 1 to 5

        var rng = new Random(charSeed.Get);
        var shortPassword = new string(Enumerable.Range(0, length)
            .Select(_ => PasswordChars[rng.Next(PasswordChars.Length)])
            .ToArray());

        // Use a valid email to isolate password validation
        var model = new Login.LoginModel
        {
            Email = "user@example.com",
            Password = shortPassword
        };

        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        // Must fail validation
        if (isValid)
            return false;

        // Must contain the minimum-length error message
        var hasMinLengthError = results.Any(r =>
            r.ErrorMessage == "A senha deve ter no mínimo 6 caracteres.");

        return hasMinLengthError;
    }
}
