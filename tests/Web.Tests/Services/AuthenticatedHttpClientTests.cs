using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Web.Services;
using Web.Tests.Components.Pages;

namespace Web.Tests.Services;

/// <summary>
/// Unit tests for AuthenticatedHttpClient: bearer header when a token exists, and ending the
/// session (sign out + redirect to login) when no usable token is available.
/// </summary>
public class AuthenticatedHttpClientTests : BunitContext
{
    private readonly ITokenProvider _tokenProvider = Substitute.For<ITokenProvider>();

    private AuthenticatedHttpClient CreateClient()
    {
        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("Api").Returns(_ => new HttpClient { BaseAddress = new Uri("http://localhost") });

        return new AuthenticatedHttpClient(
            httpClientFactory,
            _tokenProvider,
            new FakeAuthStateProvider(),
            Services.GetRequiredService<NavigationManager>(),
            NullLogger<AuthenticatedHttpClient>.Instance);
    }

    [Fact]
    public async Task CreateClientAsync_WithToken_SetsBearerHeaderAndStaysOnPage()
    {
        // Arrange
        _tokenProvider.GetTokenAsync().Returns(Task.FromResult<string?>("valid-token"));
        var navigation = Services.GetRequiredService<NavigationManager>();
        var sut = CreateClient();

        // Act
        var client = await sut.CreateClientAsync();

        // Assert
        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.Equal("valid-token", client.DefaultRequestHeaders.Authorization?.Parameter);
        Assert.DoesNotContain("/login", navigation.Uri);
        await _tokenProvider.DidNotReceive().SignOutAsync();
    }

    [Fact]
    public async Task CreateClientAsync_WithoutToken_SignsOutAndRedirectsToLogin()
    {
        // Arrange - token expired and could not be refreshed
        _tokenProvider.GetTokenAsync().Returns(Task.FromResult<string?>(null));
        var navigation = Services.GetRequiredService<NavigationManager>();
        var sut = CreateClient();

        // Act
        var client = await sut.CreateClientAsync();

        // Assert
        Assert.Null(client.DefaultRequestHeaders.Authorization);
        Assert.EndsWith("/login", navigation.Uri);
        await _tokenProvider.Received(1).SignOutAsync();
    }
}
