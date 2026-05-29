using Bunit;
using FsCheck;
using FsCheck.Xunit;
using Web.Components.Pages;

namespace Web.Tests;

/// <summary>
/// Feature: login-page, Property 5: Submit round-trip restores initial state
/// **Validates: Requirements 7.3**
/// </summary>
public class LoginSubmitRoundTripPropertyTest
{
    /// <summary>
    /// Property 5: Submit round-trip restores initial state
    /// For any valid LoginModel that triggers a submission, after the async operation completes,
    /// the component SHALL return to IsSubmitting = false with the button enabled and displaying "Entrar" text.
    /// **Validates: Requirements 7.3**
    /// </summary>
    [Property(
        DisplayName = "Feature: login-page, Property 5: Submit round-trip restores initial state",
        MaxTest = 20)]
    public bool AfterSubmitCompletion_IsSubmittingReturnsFalse_AndButtonShowsEntrar(PositiveInt emailIndex, PositiveInt passwordIndex)
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
        var cut = ctx.Render<Login>();

        // Fill in valid form data
        var emailInput = cut.Find("input[id='email']");
        var passwordInput = cut.Find("input[id='password']");

        emailInput.Change(email);
        passwordInput.Change(password);

        // Submit the form to trigger HandleValidSubmit
        var form = cut.Find("form");
        form.Submit();

        // Wait for the async operation to complete (Task.Delay(1500) inside HandleValidSubmit)
        cut.WaitForState(() =>
        {
            var btn = cut.Find("button[type='submit']");
            return btn.TextContent.Contains("Entrar") && !btn.TextContent.Contains("Entrando...");
        }, timeout: TimeSpan.FromSeconds(5));

        // Assert: after round-trip, button should show "Entrar", not be disabled
        var buttonAfter = cut.Find("button[type='submit']");
        var showsEntrar = buttonAfter.TextContent.Contains("Entrar");
        var doesNotShowLoading = !buttonAfter.TextContent.Contains("Entrando...");
        var isNotDisabled = !buttonAfter.HasAttribute("disabled");

        // Property: after submit completes, IsSubmitting is false (button enabled, shows "Entrar")
        return showsEntrar && doesNotShowLoading && isNotDisabled;
    }
}
