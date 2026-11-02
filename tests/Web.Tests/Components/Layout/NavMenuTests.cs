using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Web.Components.Layout;
using Web.Services;
using Web.Tests.Components.Pages;

namespace Web.Tests.Components.Layout;

/// <summary>
/// bUnit tests for the side menu: links and logging out.
/// </summary>
public class NavMenuTests : BunitContext
{
    private readonly ITokenProvider _tokenProvider = Substitute.For<ITokenProvider>();

    public NavMenuTests()
    {
        Services.AddSingleton(_tokenProvider);
        Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthStateProvider());
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void Menu_ShowsMainLinks_AndStatusLinkOnlyAtTheBottom()
    {
        // Act
        var cut = Render<NavMenu>();

        // Assert
        var mainLinks = cut.FindAll("nav a").Select(a => a.GetAttribute("href"));
        Assert.Equal(["dashboard", "file-records"], mainLinks);
        Assert.Equal("health", cut.Find(".nav-bottom a.nav-link-subtle").GetAttribute("href"));
    }

    [Fact]
    public void Logout_ClearsTokens_AndGoesToLogin()
    {
        // Arrange
        var cut = Render<NavMenu>();

        // Act
        cut.Find("button.logout-btn").Click();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal("http://localhost/login", Navigation.Uri));
        _tokenProvider.Received(1).SignOutAsync();
    }

    [Fact]
    public void Logout_WhenClearingTokensFails_StillGoesToLogin()
    {
        // Arrange
        _tokenProvider.SignOutAsync().ThrowsAsync(new InvalidOperationException("storage gone"));
        var cut = Render<NavMenu>();

        // Act
        cut.Find("button.logout-btn").Click();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal("http://localhost/login", Navigation.Uri));
    }

    [Fact]
    public void Logout_WhileInProgress_DisablesButton()
    {
        // Arrange — sign-out that doesn't finish
        _tokenProvider.SignOutAsync().Returns(new TaskCompletionSource().Task);
        var cut = Render<NavMenu>();

        // Act
        cut.Find("button.logout-btn").Click();

        // Assert
        var button = cut.Find("button.logout-btn");
        Assert.True(button.HasAttribute("disabled"));
        Assert.Contains("Saindo...", button.TextContent);
    }
}
