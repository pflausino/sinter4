using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Web.Components.Pages;
using Web.Services;

namespace Web.Tests.Components.Pages;

/// <summary>
/// Integration tests for the Login page interaction with ITokenProvider.
/// Validates: Requirements 7.1, 7.3, 7.4, 7.5, 7.6, 7.7
/// </summary>
public class LoginIntegrationTests : BunitContext
{
    private readonly ITokenProvider _tokenProvider;

    public LoginIntegrationTests()
    {
        _tokenProvider = Substitute.For<ITokenProvider>();
        Services.AddSingleton(_tokenProvider);

        // Configure bUnit JSInterop to loose mode so ProtectedLocalStorage calls don't throw
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Register IDataProtectionProvider needed by ProtectedLocalStorage
        Services.AddDataProtection();

        // Register FirebaseAuthStateProvider as AuthenticationStateProvider
        // The Login page casts AuthenticationStateProvider to FirebaseAuthStateProvider
        Services.AddScoped<ProtectedLocalStorage>();
        Services.AddScoped<AuthenticationStateProvider>(sp =>
            new FirebaseAuthStateProvider(
                sp.GetRequiredService<ProtectedLocalStorage>(),
                sp.GetRequiredService<ITokenProvider>(),
                sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>(),
                NullLogger<FirebaseAuthStateProvider>.Instance));
    }

    [Fact]
    public void HandleValidSubmit_CallsSignInAsync_WithCorrectCredentials()
    {
        // Arrange
        var tcs = new TaskCompletionSource<AuthResult>();
        _tokenProvider.SignInAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(tcs.Task);

        var cut = Render<Login>();

        // Act - fill in valid data and submit
        cut.Find("input#email").Change("user@example.com");
        cut.Find("input#password").Change("Str0ngP@ss!");
        cut.Find("form").Submit();

        // Assert - SignInAsync was called with the correct email and password
        _tokenProvider.Received(1).SignInAsync("user@example.com", "Str0ngP@ss!");
    }

    [Fact]
    public void HandleValidSubmit_OnSuccess_NavigatesToDashboard()
    {
        // Arrange
        _tokenProvider.SignInAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(new AuthResult(true, "fake-token", null)));

        var cut = Render<Login>();
        var navManager = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

        // Act - fill in valid data and submit
        cut.Find("input#email").Change("user@example.com");
        cut.Find("input#password").Change("Str0ngP@ss!");
        cut.Find("form").Submit();

        // Assert - navigated to /dashboard
        Assert.EndsWith("/dashboard", navManager.Uri);
    }

    [Fact]
    public void HandleValidSubmit_OnFailure_DisplaysErrorMessage()
    {
        // Arrange
        _tokenProvider.SignInAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(new AuthResult(false, null, "Email ou senha incorretos.")));

        var cut = Render<Login>();

        // Act - fill in valid data and submit
        cut.Find("input#email").Change("user@example.com");
        cut.Find("input#password").Change("Str0ngP@ss!");
        cut.Find("form").Submit();

        // Assert - error message is displayed
        var errorDiv = cut.Find("div.error-message");
        Assert.Contains("Email ou senha incorretos.", errorDiv.TextContent);
        Assert.Equal("alert", errorDiv.GetAttribute("role"));
    }

    [Fact]
    public void HandleValidSubmit_OnNetworkError_DisplaysGenericErrorMessage()
    {
        // Arrange - simulate an unexpected exception
        _tokenProvider.SignInAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns<AuthResult>(x => throw new HttpRequestException("Network error"));

        var cut = Render<Login>();

        // Act - fill in valid data and submit
        cut.Find("input#email").Change("user@example.com");
        cut.Find("input#password").Change("Str0ngP@ss!");
        cut.Find("form").Submit();

        // Assert - generic error message is displayed
        var errorDiv = cut.Find("div.error-message");
        Assert.Contains("Serviço indisponível. Tente novamente mais tarde.", errorDiv.TextContent);
    }

    [Fact]
    public void OnFieldChanged_ClearsErrorMessage_WhenEmailChanges()
    {
        // Arrange - first trigger an error
        _tokenProvider.SignInAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(new AuthResult(false, null, "Email ou senha incorretos.")));

        var cut = Render<Login>();

        cut.Find("input#email").Change("user@example.com");
        cut.Find("input#password").Change("Str0ngP@ss!");
        cut.Find("form").Submit();

        // Verify error is displayed
        Assert.NotEmpty(cut.FindAll("div.error-message"));

        // Act - change the email field
        cut.Find("input#email").Change("other@example.com");

        // Assert - error message is cleared
        Assert.Empty(cut.FindAll("div.error-message"));
    }

    [Fact]
    public void OnFieldChanged_ClearsErrorMessage_WhenPasswordChanges()
    {
        // Arrange - first trigger an error
        _tokenProvider.SignInAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(new AuthResult(false, null, "Email ou senha incorretos.")));

        var cut = Render<Login>();

        cut.Find("input#email").Change("user@example.com");
        cut.Find("input#password").Change("Str0ngP@ss!");
        cut.Find("form").Submit();

        // Verify error is displayed
        Assert.NotEmpty(cut.FindAll("div.error-message"));

        // Act - change the password field
        cut.Find("input#password").Change("NewP@ssw0rd!");

        // Assert - error message is cleared
        Assert.Empty(cut.FindAll("div.error-message"));
    }

    [Fact]
    public void HandleValidSubmit_OnTimeout_DisplaysTimeoutErrorMessage()
    {
        // Arrange - simulate a task that throws OperationCanceledException (timeout)
        _tokenProvider.SignInAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns<AuthResult>(x => throw new OperationCanceledException("The operation was canceled."));

        var cut = Render<Login>();

        // Act - fill in valid data and submit
        cut.Find("input#email").Change("user@example.com");
        cut.Find("input#password").Change("Str0ngP@ss!");
        cut.Find("form").Submit();

        // Assert - timeout error message is displayed
        var errorDiv = cut.Find("div.error-message");
        Assert.Contains("O serviço está demorando para responder. Tente novamente.", errorDiv.TextContent);
    }

    [Fact]
    public void HandleValidSubmit_OnFailure_RestoresButtonToDefaultState()
    {
        // Arrange
        _tokenProvider.SignInAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(new AuthResult(false, null, "Email ou senha incorretos.")));

        var cut = Render<Login>();

        // Act - fill in valid data and submit
        cut.Find("input#email").Change("user@example.com");
        cut.Find("input#password").Change("Str0ngP@ss!");
        cut.Find("form").Submit();

        // Assert - button is restored to default state (not disabled, shows "Entrar")
        var button = cut.Find("button.btn-entrar");
        Assert.Contains("Entrar", button.TextContent);
        Assert.DoesNotContain("Entrando...", button.TextContent);
    }
}
