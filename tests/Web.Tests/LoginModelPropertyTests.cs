using System.ComponentModel.DataAnnotations;
using FsCheck;
using FsCheck.Xunit;
using Web.Components.Pages;

namespace Web.Tests;

/// <summary>
/// Feature: login-page, Property 1: LoginModel validation accepts only valid inputs
/// **Validates: Requirements 8.4, 8.5**
/// </summary>
public class LoginModelPropertyTests
{
    private static readonly char[] LocalPartChars =
        "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    private static readonly char[] DomainChars =
        "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();

    private static readonly char[] PasswordChars =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_=+"
            .ToCharArray();

    private static readonly string[] Tlds = ["com", "net", "org", "io", "dev", "br", "uk", "de"];

    /// <summary>
    /// Builds a valid email from seed values.
    /// </summary>
    private static string BuildValidEmail(PositiveInt localSeed, PositiveInt domainSeed, PositiveInt tldSeed)
    {
        var localLen = (localSeed.Get % 18) + 3; // 3-20
        var domainLen = (domainSeed.Get % 13) + 3; // 3-15
        var tld = Tlds[tldSeed.Get % Tlds.Length];

        var rng = new Random(localSeed.Get);
        var local = new string(Enumerable.Range(0, localLen)
            .Select(_ => LocalPartChars[rng.Next(LocalPartChars.Length)])
            .ToArray());

        rng = new Random(domainSeed.Get + 1000);
        var domain = new string(Enumerable.Range(0, domainLen)
            .Select(_ => DomainChars[rng.Next(DomainChars.Length)])
            .ToArray());

        return $"{local}@{domain}.{tld}";
    }

    /// <summary>
    /// Builds a valid password from seed values.
    /// </summary>
    private static string BuildValidPassword(PositiveInt seed)
    {
        var len = (seed.Get % 123) + 6; // 6-128
        var rng = new Random(seed.Get);
        return new string(Enumerable.Range(0, len)
            .Select(_ => PasswordChars[rng.Next(PasswordChars.Length)])
            .ToArray());
    }

    /// <summary>
    /// Property 1: LoginModel validation accepts only valid inputs
    /// For any email string that matches a valid email format and has length ≤ 256,
    /// combined with any password string with length between 6 and 128 (inclusive),
    /// the LoginModel SHALL pass DataAnnotations validation with zero errors.
    /// **Validates: Requirements 8.4, 8.5**
    /// </summary>
    [Property(
        DisplayName = "Feature: login-page, Property 1: LoginModel validation accepts only valid inputs",
        MaxTest = 20)]
    public bool LoginModel_ValidInputs_PassValidation(
        PositiveInt localSeed,
        PositiveInt domainSeed,
        PositiveInt tldSeed,
        PositiveInt passwordSeed)
    {
        var email = BuildValidEmail(localSeed, domainSeed, tldSeed);
        var password = BuildValidPassword(passwordSeed);

        var model = new Login.LoginModel
        {
            Email = email,
            Password = password
        };

        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        return isValid && results.Count == 0;
    }
}
