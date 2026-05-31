namespace Web.Services;

using System.Net.Http.Headers;

/// <summary>
/// Provides an HttpClient pre-configured with the Firebase JWT token
/// for authenticated API calls from Blazor components.
/// </summary>
public class AuthenticatedHttpClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenProvider _tokenProvider;

    public AuthenticatedHttpClient(IHttpClientFactory httpClientFactory, ITokenProvider tokenProvider)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
    }

    /// <summary>
    /// Creates an HttpClient with the Authorization header set to the current user's token.
    /// </summary>
    public async Task<HttpClient> CreateClientAsync()
    {
        var client = _httpClientFactory.CreateClient("Api");
        var token = await _tokenProvider.GetTokenAsync();

        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }
}
