using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Web.Components.Pages;
using Web.Services;

namespace Web.Tests.Components.Pages;

public class LoginComponentTests : BunitContext
{
    private readonly ITokenProvider _tokenProvider;
    private readonly TaskCompletionSource<AuthResult> _signInTcs = new();

    public LoginComponentTests()
    {
        _tokenProvider = Substitute.For<ITokenProvider>();
        // Default: SignInAsync never completes (keeps IsSubmitting = true for loading state tests)
        _tokenProvider.SignInAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(_signInTcs.Task);
        Services.AddSingleton(_tokenProvider);
    }

    [Fact]
    public void LoginPage_RendersLogo_EmailInput_PasswordInput_AndEntrarButton()
    {
        // Arrange & Act
        var cut = Render<Login>();

        // Assert - logo
        var logo = cut.Find("img.login-logo");
        Assert.Equal("SinterPrints", logo.GetAttribute("alt"));
        Assert.Equal("images/logo.png", logo.GetAttribute("src"));

        // Assert - email input
        var emailInput = cut.Find("input#email");
        Assert.NotNull(emailInput);

        // Assert - password input
        var passwordInput = cut.Find("input#password");
        Assert.NotNull(passwordInput);

        // Assert - Entrar button
        var button = cut.Find("button.btn-entrar");
        Assert.Contains("Entrar", button.TextContent);
    }

    [Fact]
    public void EmailInput_HasTypeEmail_AndAssociatedLabel()
    {
        // Arrange & Act
        var cut = Render<Login>();

        // Assert - type="email"
        var emailInput = cut.Find("input#email");
        Assert.Equal("email", emailInput.GetAttribute("type"));

        // Assert - associated label
        var label = cut.Find("label[for='email']");
        Assert.NotNull(label);
        Assert.Equal("Email", label.TextContent);
    }

    [Fact]
    public void SubmittingEmptyForm_ShowsValidationErrorMessages()
    {
        // Arrange
        var cut = Render<Login>();

        // Act - submit the form without filling any fields
        var form = cut.Find("form");
        form.Submit();

        // Assert - validation messages should appear
        var markup = cut.Markup;
        Assert.Contains("O email é obrigatório.", markup);
        Assert.Contains("A senha é obrigatória.", markup);
    }

    [Fact]
    public void SubmittingWithValidData_InvokesOnValidSubmit_WithoutValidationMessages()
    {
        // Arrange
        var cut = Render<Login>();

        // Act - fill in valid data
        var emailInput = cut.Find("input#email");
        var passwordInput = cut.Find("input#password");

        emailInput.Change("user@example.com");
        passwordInput.Change("password123");

        // Submit the form
        var form = cut.Find("form");
        form.Submit();

        // Assert - no validation error messages should be displayed
        var markup = cut.Markup;
        Assert.DoesNotContain("O email é obrigatório.", markup);
        Assert.DoesNotContain("Formato de email inválido.", markup);
        Assert.DoesNotContain("A senha é obrigatória.", markup);
        Assert.DoesNotContain("A senha deve ter no mínimo 6 caracteres.", markup);
    }

    [Fact]
    public void Button_ShowsEntrando_AndIsDisabled_WhenIsSubmittingIsTrue()
    {
        // Arrange
        var cut = Render<Login>();

        // Fill valid data so form submits successfully
        var emailInput = cut.Find("input#email");
        var passwordInput = cut.Find("input#password");

        emailInput.Change("user@example.com");
        passwordInput.Change("password123");

        // Act - submit the form to trigger IsSubmitting = true
        // Since SignInAsync never completes, IsSubmitting stays true
        var form = cut.Find("form");
        form.Submit();

        // Assert - button should show "Entrando..." and be disabled during submission
        var button = cut.Find("button.btn-entrar");
        Assert.Contains("Entrando...", button.TextContent);
        Assert.NotNull(button.GetAttribute("disabled"));
    }

    [Fact]
    public void Button_ShowsEntrar_WhenIsSubmittingIsFalse()
    {
        // Arrange & Act - initial render (IsSubmitting defaults to false)
        var cut = Render<Login>();

        // Assert
        var button = cut.Find("button.btn-entrar");
        Assert.Contains("Entrar", button.TextContent);
        Assert.DoesNotContain("Entrando...", button.TextContent);
    }
}
