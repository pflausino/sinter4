namespace Web.Services;

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

/// <summary>
/// Provides an HttpClient pre-configured with the Firebase JWT token
/// for authenticated API calls from Blazor components.
/// </summary>
public class AuthenticatedHttpClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenProvider _tokenProvider;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<AuthenticatedHttpClient> _logger;

    public AuthenticatedHttpClient(
        IHttpClientFactory httpClientFactory,
        ITokenProvider tokenProvider,
        AuthenticationStateProvider authStateProvider,
        NavigationManager navigationManager,
        ILogger<AuthenticatedHttpClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _authStateProvider = authStateProvider;
        _navigationManager = navigationManager;
        _logger = logger;
    }

    /// <summary>
    /// Creates an HttpClient with the Authorization header set to the current user's token.
    /// When there is no usable token (expired and not refreshable), the session is cleared and the
    /// user is sent to the login page instead of letting every API call fail with 401.
    /// </summary>
    public async Task<HttpClient> CreateClientAsync()
    {
        var client = _httpClientFactory.CreateClient("Api");
        var token = await _tokenProvider.GetTokenAsync();

        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        else
        {
            await EndSessionAsync();
        }

        return client;
    }

    private async Task EndSessionAsync()
    {
        _logger.LogInformation("No valid ID token for API call. Clearing session and redirecting to login.");

        try
        {
            await _tokenProvider.SignOutAsync();

            if (_authStateProvider is FirebaseAuthStateProvider firebaseAuthState)
            {
                firebaseAuthState.NotifyStateChanged();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear session after token expiry");
        }

        _navigationManager.NavigateTo("/login");
    }
}
