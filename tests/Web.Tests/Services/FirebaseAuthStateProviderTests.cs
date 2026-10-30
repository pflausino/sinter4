using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Web.Services;

namespace Web.Tests.Services;

/// <summary>
/// Tests for FirebaseAuthStateProvider: who counts as logged in, depending on the token in
/// protected storage. Uses the real ProtectedLocalStorage over an in-memory localStorage.
/// </summary>
public class FirebaseAuthStateProviderTests
{
    private const string IdTokenKey = "firebase_id_token";

    private readonly FakeLocalStorageJsRuntime _js = new();
    private readonly ProtectedLocalStorage _storage;
    private readonly ITokenProvider _tokenProvider = Substitute.For<ITokenProvider>();
    private readonly TestNavigationManager _navigation = new();
    private readonly FirebaseAuthStateProvider _provider;

    public FirebaseAuthStateProviderTests()
    {
        _storage = new ProtectedLocalStorage(_js, new EphemeralDataProtectionProvider());
        _provider = new FirebaseAuthStateProvider(
            _storage, _tokenProvider, _navigation, NullLogger<FirebaseAuthStateProvider>.Instance);
    }

    [Fact]
    public async Task NoStoredToken_IsAnonymous()
    {
        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.Null(_navigation.NavigatedTo);
    }

    [Fact]
    public async Task ValidToken_IsAuthenticated_WithMappedClaims()
    {
        // Arrange
        await StoreTokenAsync(CreateJwt(DateTimeOffset.UtcNow.AddHours(1),
            """ "sub":"uid-1","email":"ana@example.com","name":"Ana","roles":["admin","editor"] """));

        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("Firebase", state.User.Identity?.AuthenticationType);
        Assert.Equal("uid-1", state.User.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("ana@example.com", state.User.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("Ana", state.User.FindFirstValue(ClaimTypes.Name));
        Assert.Equal(["admin", "editor"], state.User.FindAll("roles").Select(c => c.Value));
        await _tokenProvider.DidNotReceive().RefreshTokenAsync();
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("only.two")]
    [InlineData("header.%%%.signature")]          // payload is not base64url
    [InlineData("header.bm90LWpzb24.signature")] // payload is base64url of "not-json"
    public async Task MalformedToken_IsAnonymous(string token)
    {
        // Arrange
        await StoreTokenAsync(token);

        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task ExpiredToken_RefreshSucceeds_IsAuthenticatedWithNewToken()
    {
        // Arrange
        await StoreTokenAsync(CreateJwt(DateTimeOffset.UtcNow.AddMinutes(-1), """ "sub":"old" """));
        _tokenProvider.RefreshTokenAsync().Returns(async _ =>
        {
            await StoreTokenAsync(CreateJwt(DateTimeOffset.UtcNow.AddHours(1), """ "sub":"new" """));
            return true;
        });

        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("new", state.User.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Null(_navigation.NavigatedTo);
    }

    [Fact]
    public async Task ExpiredToken_RefreshFails_SignsOutAndRedirectsToLogin()
    {
        // Arrange
        await StoreTokenAsync(CreateJwt(DateTimeOffset.UtcNow.AddMinutes(-1)));
        _tokenProvider.RefreshTokenAsync().Returns(false);

        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.False(state.User.Identity?.IsAuthenticated);
        await _tokenProvider.Received(1).SignOutAsync();
        Assert.Equal("/login", _navigation.NavigatedTo);
    }

    [Fact]
    public async Task TokenNearExpiry_RefreshThrows_SignsOutAndRedirectsToLogin()
    {
        // Arrange
        await StoreTokenAsync(CreateJwt(DateTimeOffset.UtcNow.AddMinutes(2)));
        _tokenProvider.RefreshTokenAsync().ThrowsAsync(new HttpRequestException("offline"));

        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.False(state.User.Identity?.IsAuthenticated);
        await _tokenProvider.Received(1).SignOutAsync();
        Assert.Equal("/login", _navigation.NavigatedTo);
    }

    [Fact]
    public async Task RefreshFails_AndSignOutThrows_StillRedirectsToLogin()
    {
        // Arrange
        await StoreTokenAsync(CreateJwt(DateTimeOffset.UtcNow.AddMinutes(-1)));
        _tokenProvider.RefreshTokenAsync().Returns(false);
        _tokenProvider.SignOutAsync().ThrowsAsync(new InvalidOperationException("storage gone"));

        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.Equal("/login", _navigation.NavigatedTo);
    }

    [Fact]
    public async Task StorageUnavailable_IsAnonymous_WithoutRedirect()
    {
        // Arrange — JS interop not available yet (e.g. before the circuit is connected)
        _js.Unavailable = true;

        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.False(state.User.Identity?.IsAuthenticated);
        Assert.Null(_navigation.NavigatedTo);
    }

    [Fact]
    public async Task TamperedStoredValue_IsAnonymous()
    {
        // Arrange — a value that was not written through data protection can't be unprotected
        _js.Items[IdTokenKey] = "\"tampered\"";

        // Act
        var state = await _provider.GetAuthenticationStateAsync();

        // Assert
        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task NotifyStateChanged_RaisesEventWithCurrentState()
    {
        // Arrange
        await StoreTokenAsync(CreateJwt(DateTimeOffset.UtcNow.AddHours(1)));
        Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState>? raised = null;
        _provider.AuthenticationStateChanged += task => raised = task;

        // Act
        _provider.NotifyStateChanged();

        // Assert
        Assert.NotNull(raised);
        Assert.True((await raised).User.Identity?.IsAuthenticated);
    }

    private Task StoreTokenAsync(string token) => _storage.SetAsync(IdTokenKey, token).AsTask();

    private static string CreateJwt(DateTimeOffset expiresAt, string extraClaims = """ "sub":"user-uid-001" """)
    {
        static string Encode(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var header = Encode("""{"alg":"none","typ":"JWT"}""");
        var payload = Encode($$"""{{{extraClaims.Trim()}},"exp":{{expiresAt.ToUnixTimeSeconds()}}}""");
        return $"{header}.{payload}.signature";
    }

    /// <summary>
    /// Stands in for the browser's localStorage behind ProtectedLocalStorage.
    /// </summary>
    private sealed class FakeLocalStorageJsRuntime : IJSRuntime
    {
        public Dictionary<string, string> Items { get; } = new();
        public bool Unavailable { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (Unavailable)
                throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time.");

            var key = (string)args![0]!;
            switch (identifier)
            {
                case "localStorage.setItem":
                    Items[key] = (string)args[1]!;
                    return ValueTask.FromResult(default(TValue)!);
                case "localStorage.getItem":
                    object? value = Items.TryGetValue(key, out var stored) ? stored : null;
                    return ValueTask.FromResult((TValue)value!);
                case "localStorage.removeItem":
                    Items.Remove(key);
                    return ValueTask.FromResult(default(TValue)!);
                default:
                    throw new NotSupportedException(identifier);
            }
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/file-records");

        public string? NavigatedTo { get; private set; }

        protected override void NavigateToCore(string uri, NavigationOptions options) => NavigatedTo = uri;
    }
}
