using Bunit;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Web.Components.Pages;
using Web.Services;

namespace Web.Tests;

/// <summary>
/// Feature: login-page, Property 4: Loading state disables submission
/// **Validates: Requirements 7.1, 7.4**
/// </summary>
public class LoginLoadingStatePropertyTests
{
    /// <summary>
    /// Property 4: Loading state disables submission
    /// When IsSubmitting is true (after a valid submit), the button SHALL have the disabled attribute,
    /// preventing additional submissions (idempotence).
    /// For any valid email/password combination, after submitting the form, the button must be disabled.
    /// **Validates: Requirements 7.1, 7.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: login-page, Property 4: Loading state disables submission",
        MaxTest = 20)]
    public bool ButtonIsDisabledDuringSubmission(PositiveInt emailIndex, PositiveInt passwordIndex)
    {
        var emails = new[]
        {
            "user@example.com", "test@domain.org", "a@b.co",
            "name@mail.net", "hello@world.io", "foo@bar.dev",
            "admin@site.com", "contact@company.org", "info@service.io",
            "support@app.dev", "dev@test.com", "qa@check.net"
        };

        var passwords = new[]
        {
            "password123", "SecureP@ss1", "mypass99", "abcdef",
            "longpassword!@#", "Test1234", "p@ssw0rd", "qwerty12",
            "Admin!23", "hello!world", "secret42", "pass!678"
        };

        var email = emails[emailIndex.Get % emails.Length];
        var password = passwords[passwordIndex.Get % passwords.Length];

        using var ctx = new BunitContext();

        // Create a token provider that never completes (to keep IsSubmitting = true)
        var tcs = new TaskCompletionSource<AuthResult>();
        var tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.SignInAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(tcs.Task);
        ctx.Services.AddSingleton(tokenProvider);

        var cut = ctx.Render<Login>();

        // Fill in valid form data
        var emailInput = cut.Find("input[id='email']");
        var passwordInput = cut.Find("input[id='password']");

        emailInput.Change(email);
        passwordInput.Change(password);

        // Submit the form to trigger HandleValidSubmit (sets IsSubmitting = true)
        var form = cut.Find("form");
        form.Submit();

        // Immediately after submit, the button should be disabled
        var button = cut.Find("button[type='submit']");
        var isDisabled = button.HasAttribute("disabled");

        // Also verify the button text shows loading state (confirming IsSubmitting = true)
        var showsLoading = button.TextContent.Contains("Entrando...");

        // Property: when IsSubmitting is true, button is disabled (prevents additional submissions)
        return isDisabled && showsLoading;
    }
}
