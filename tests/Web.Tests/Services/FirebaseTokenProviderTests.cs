using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Web.Services;

namespace Web.Tests.Services;

/// <summary>
/// Unit tests for FirebaseTokenProvider covering sign-in, error mapping, timeout, and refresh retry logic.
/// Validates: Requirements 3.1, 3.3, 3.4, 3.6
/// </summary>
public class FirebaseTokenProviderTests
{
    private const string TestApiKey = "test-api-key-123";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenStorage _tokenStorage;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FirebaseTokenProvider> _logger;

    public FirebaseTokenProviderTests()
    {
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _tokenStorage = Substitute.For<ITokenStorage>();
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Firebase:ApiKey"] = TestApiKey
            })
            .Build();
        _logger = Substitute.For<ILogger<FirebaseTokenProvider>>();
    }

    private FirebaseTokenProvider CreateProvider(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        _httpClientFactory.CreateClient("FirebaseAuth").Returns(httpClient);
        return new FirebaseTokenProvider(_httpClientFactory, _tokenStorage, _configuration, _logger);
    }

    #region Sign-In Success (Requirement 3.1)

    [Fact]
    public async Task SignInAsync_ValidCredentials_ReturnsSuccessWithToken()
    {
        // Arrange
        var signInResponse = new
        {
            idToken = "valid-jwt-token-abc123",
            refreshToken = "valid-refresh-token-xyz",
            expiresIn = "3600",
            localId = "user-uid-001",
            email = "user@example.com"
        };

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(signInResponse));
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.SignInAsync("user@example.com", "password123");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("valid-jwt-token-abc123", result.Token);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task SignInAsync_ValidCredentials_StoresTokensInStorage()
    {
        // Arrange
        var signInResponse = new
        {
            idToken = "jwt-token-to-store",
            refreshToken = "refresh-token-to-store",
            expiresIn = "3600",
            localId = "user-uid-001",
            email = "user@example.com"
        };

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(signInResponse));
        var provider = CreateProvider(handler);

        // Act
        await provider.SignInAsync("user@example.com", "password123");

        // Assert - verify tokens were stored
        await _tokenStorage.Received(1).SetAsync("firebase_id_token", "jwt-token-to-store");
        await _tokenStorage.Received(1).SetAsync("firebase_refresh_token", "refresh-token-to-store");
    }

    #endregion

    #region Invalid Credentials - Localized Errors (Requirement 3.3)

    [Fact]
    public async Task SignInAsync_InvalidLoginCredentials_ReturnsLocalizedError()
    {
        // Arrange
        var errorResponse = new { error = new { code = 400, message = "INVALID_LOGIN_CREDENTIALS" } };
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, JsonSerializer.Serialize(errorResponse));
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.SignInAsync("user@example.com", "wrong-password");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.Equal("Email ou senha incorretos.", result.ErrorMessage);
    }

    [Fact]
    public async Task SignInAsync_EmailNotFound_ReturnsLocalizedError()
    {
        // Arrange
        var errorResponse = new { error = new { code = 400, message = "EMAIL_NOT_FOUND" } };
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, JsonSerializer.Serialize(errorResponse));
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.SignInAsync("nonexistent@example.com", "password123");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.Equal("Email ou senha incorretos.", result.ErrorMessage);
    }

    [Fact]
    public async Task SignInAsync_InvalidPassword_ReturnsLocalizedError()
    {
        // Arrange
        var errorResponse = new { error = new { code = 400, message = "INVALID_PASSWORD" } };
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, JsonSerializer.Serialize(errorResponse));
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.SignInAsync("user@example.com", "bad-pass");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.Equal("Email ou senha incorretos.", result.ErrorMessage);
    }

    [Fact]
    public async Task SignInAsync_UserDisabled_ReturnsLocalizedError()
    {
        // Arrange
        var errorResponse = new { error = new { code = 400, message = "USER_DISABLED" } };
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, JsonSerializer.Serialize(errorResponse));
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.SignInAsync("disabled@example.com", "password123");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.Equal("Sua conta está desativada. Contate o administrador.", result.ErrorMessage);
    }

    [Fact]
    public async Task SignInAsync_TooManyAttempts_ReturnsLocalizedError()
    {
        // Arrange
        var errorResponse = new { error = new { code = 400, message = "TOO_MANY_ATTEMPTS_TRY_LATER" } };
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, JsonSerializer.Serialize(errorResponse));
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.SignInAsync("user@example.com", "password123");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.Equal("Muitas tentativas. Tente novamente mais tarde.", result.ErrorMessage);
    }

    [Fact]
    public async Task SignInAsync_UnknownFirebaseError_ReturnsGenericLocalizedError()
    {
        // Arrange
        var errorResponse = new { error = new { code = 400, message = "SOME_UNKNOWN_ERROR" } };
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, JsonSerializer.Serialize(errorResponse));
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.SignInAsync("user@example.com", "password123");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.Equal("Erro ao autenticar. Tente novamente.", result.ErrorMessage);
    }

    #endregion

    #region Timeout (Requirement 3.4)

    [Fact]
    public async Task SignInAsync_Timeout_ReturnsConnectivityError()
    {
        // Arrange - simulate HTTP timeout (TaskCanceledException)
        var handler = new TimeoutHttpMessageHandler();
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.SignInAsync("user@example.com", "password123");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.Equal("O serviço está demorando para responder. Tente novamente.", result.ErrorMessage);
    }

    [Fact]
    public async Task SignInAsync_NetworkError_ReturnsServiceUnavailableError()
    {
        // Arrange - simulate network failure (HttpRequestException)
        var handler = new NetworkErrorHttpMessageHandler();
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.SignInAsync("user@example.com", "password123");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.Token);
        Assert.Equal("Serviço indisponível. Tente novamente mais tarde.", result.ErrorMessage);
    }

    #endregion

    #region Refresh Retry Logic (Requirement 3.6)

    [Fact]
    public async Task RefreshTokenAsync_SuccessOnFirstAttempt_ReturnsTrue()
    {
        // Arrange
        var refreshResponse = new
        {
            id_token = "new-jwt-token",
            refresh_token = "new-refresh-token",
            expires_in = "3600"
        };

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(refreshResponse));
        var provider = CreateProvider(handler);
        SetupStorageWithRefreshToken("existing-refresh-token");

        // Act
        var result = await provider.RefreshTokenAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RefreshTokenAsync_SuccessOnFirstAttempt_StoresNewTokens()
    {
        // Arrange
        var refreshResponse = new
        {
            id_token = "refreshed-jwt-token",
            refresh_token = "refreshed-refresh-token",
            expires_in = "3600"
        };

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, JsonSerializer.Serialize(refreshResponse));
        var provider = CreateProvider(handler);
        SetupStorageWithRefreshToken("existing-refresh-token");

        // Act
        await provider.RefreshTokenAsync();

        // Assert - verify new tokens were stored
        await _tokenStorage.Received(1).SetAsync("firebase_id_token", "refreshed-jwt-token");
        await _tokenStorage.Received(1).SetAsync("firebase_refresh_token", "refreshed-refresh-token");
    }

    [Fact]
    public async Task RefreshTokenAsync_FailsAllRetries_ReturnsFalse()
    {
        // Arrange - server always returns 500
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        var provider = CreateProvider(handler);
        SetupStorageWithRefreshToken("existing-refresh-token");

        // Act
        var result = await provider.RefreshTokenAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RefreshTokenAsync_FailsAllRetries_Makes3TotalAttempts()
    {
        // Arrange - server always returns 500 (initial + 2 retries = 3 attempts)
        var trackingHandler = new RequestTrackingHttpMessageHandler(
            Enumerable.Repeat((HttpStatusCode.InternalServerError, "{}"), 3).ToArray());
        var provider = CreateProvider(trackingHandler);
        SetupStorageWithRefreshToken("existing-refresh-token");

        // Act
        await provider.RefreshTokenAsync();

        // Assert - should have made 3 requests (1 initial + 2 retries)
        Assert.Equal(3, trackingHandler.RequestCount);
    }

    [Fact]
    public async Task RefreshTokenAsync_SucceedsOnThirdAttempt_ReturnsTrue()
    {
        // Arrange - fails twice, succeeds on third attempt
        var refreshResponse = JsonSerializer.Serialize(new
        {
            id_token = "success-token",
            refresh_token = "success-refresh",
            expires_in = "3600"
        });

        var trackingHandler = new RequestTrackingHttpMessageHandler(new[]
        {
            (HttpStatusCode.InternalServerError, "{}"),
            (HttpStatusCode.InternalServerError, "{}"),
            (HttpStatusCode.OK, refreshResponse)
        });
        var provider = CreateProvider(trackingHandler);
        SetupStorageWithRefreshToken("existing-refresh-token");

        // Act
        var result = await provider.RefreshTokenAsync();

        // Assert
        Assert.True(result);
        Assert.Equal(3, trackingHandler.RequestCount);
    }

    [Fact]
    public async Task RefreshTokenAsync_RetriesWithDelay_HasMinimumTimeBetweenRetries()
    {
        // Arrange - fails all attempts, we measure timing
        var trackingHandler = new RequestTrackingHttpMessageHandler(
            Enumerable.Repeat((HttpStatusCode.InternalServerError, "{}"), 3).ToArray());
        var provider = CreateProvider(trackingHandler);
        SetupStorageWithRefreshToken("existing-refresh-token");

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await provider.RefreshTokenAsync();
        stopwatch.Stop();

        // Assert - with 2 retries at 2-second delay each, total should be >= 4 seconds
        Assert.True(stopwatch.ElapsedMilliseconds >= 3800,
            $"Expected at least ~4 seconds for 2 retries with 2s delay, but got {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task RefreshTokenAsync_NoStoredRefreshToken_ReturnsFalseWithoutHttpCall()
    {
        // Arrange
        var trackingHandler = new RequestTrackingHttpMessageHandler(
            new[] { (HttpStatusCode.OK, "{}") });
        var provider = CreateProvider(trackingHandler);
        SetupStorageWithRefreshToken(null);

        // Act
        var result = await provider.RefreshTokenAsync();

        // Assert
        Assert.False(result);
        Assert.Equal(0, trackingHandler.RequestCount);
    }

    [Fact]
    public async Task RefreshTokenAsync_StorageAccessFails_ReturnsFalse()
    {
        // Arrange
        var trackingHandler = new RequestTrackingHttpMessageHandler(
            new[] { (HttpStatusCode.OK, "{}") });
        var provider = CreateProvider(trackingHandler);

        // Mock storage to throw exception
        _tokenStorage.GetAsync("firebase_refresh_token")
            .ThrowsAsync(new InvalidOperationException("JS interop failed"));

        // Act
        var result = await provider.RefreshTokenAsync();

        // Assert
        Assert.False(result);
        Assert.Equal(0, trackingHandler.RequestCount);
    }

    #endregion

    #region Helpers

    private void SetupStorageWithRefreshToken(string? refreshToken)
    {
        if (refreshToken is not null)
        {
            _tokenStorage.GetAsync("firebase_refresh_token")
                .Returns(new ValueTask<(bool Success, string? Value)>((true, refreshToken)));
        }
        else
        {
            _tokenStorage.GetAsync("firebase_refresh_token")
                .Returns(new ValueTask<(bool Success, string? Value)>((false, null)));
        }
    }

    #endregion

    #region Test Doubles

    /// <summary>
    /// A fake HttpMessageHandler that returns a predefined response.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    /// <summary>
    /// Simulates a timeout by throwing TaskCanceledException (same as HttpClient.Timeout behavior).
    /// </summary>
    private sealed class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
        }
    }

    /// <summary>
    /// Simulates a network error by throwing HttpRequestException.
    /// </summary>
    private sealed class NetworkErrorHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Unable to connect to the remote server.");
        }
    }

    /// <summary>
    /// Tracks requests and returns different responses for each attempt.
    /// Used to verify retry logic.
    /// </summary>
    private sealed class RequestTrackingHttpMessageHandler : HttpMessageHandler
    {
        private readonly (HttpStatusCode StatusCode, string Content)[] _responses;
        private int _requestCount;

        public int RequestCount => _requestCount;

        public RequestTrackingHttpMessageHandler((HttpStatusCode, string)[] responses)
        {
            _responses = responses;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Math.Min(_requestCount, _responses.Length - 1);
            var (statusCode, content) = _responses[index];
            _requestCount++;

            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    #endregion
}
