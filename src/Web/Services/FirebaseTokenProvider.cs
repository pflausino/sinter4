using System.Text.Json;
using System.Text.Json.Serialization;

namespace Web.Services;

public class FirebaseTokenProvider : ITokenProvider
{
    private const string IdTokenKey = "firebase_id_token";
    private const string RefreshTokenKey = "firebase_refresh_token";
    private const int HttpTimeoutSeconds = 15;
    private const int RefreshMaxRetries = 2;
    private const int RefreshRetryDelayMs = 2000;

    private readonly HttpClient _httpClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly string _apiKey;
    private readonly ILogger<FirebaseTokenProvider> _logger;

    public FirebaseTokenProvider(
        IHttpClientFactory httpClientFactory,
        ITokenStorage tokenStorage,
        IConfiguration configuration,
        ILogger<FirebaseTokenProvider> logger)
    {
        _httpClient = httpClientFactory.CreateClient("FirebaseAuth");
        _httpClient.Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds);
        _tokenStorage = tokenStorage;
        _apiKey = configuration["Firebase:ApiKey"]
            ?? throw new InvalidOperationException("Required configuration key 'Firebase:ApiKey' is missing or empty");
        _logger = logger;
    }

    public async Task<AuthResult> SignInAsync(string email, string password)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_apiKey}";

        var payload = new
        {
            email,
            password,
            returnSecureToken = true
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, payload);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SignInResponse>();

                if (result?.IdToken is null || result.RefreshToken is null)
                {
                    return new AuthResult(false, null, "Erro ao autenticar. Tente novamente.");
                }

                await StoreTokensAsync(result.IdToken, result.RefreshToken);
                return new AuthResult(true, result.IdToken, null);
            }

            var errorMessage = await ParseFirebaseErrorAsync(response);
            return new AuthResult(false, null, errorMessage);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Firebase sign-in request timed out");
            return new AuthResult(false, null, "O serviço está demorando para responder. Tente novamente.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error during Firebase sign-in");
            return new AuthResult(false, null, "Serviço indisponível. Tente novamente mais tarde.");
        }
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            var result = await _tokenStorage.GetAsync(IdTokenKey);
            return result.Success ? result.Value : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SignOutAsync()
    {
        try
        {
            await _tokenStorage.DeleteAsync(IdTokenKey);
            await _tokenStorage.DeleteAsync(RefreshTokenKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear tokens from storage during sign-out");
        }
    }

    public async Task<bool> RefreshTokenAsync()
    {
        string? refreshToken;
        try
        {
            var result = await _tokenStorage.GetAsync(RefreshTokenKey);
            refreshToken = result.Success ? result.Value : null;
        }
        catch
        {
            return false;
        }

        if (string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        for (var attempt = 0; attempt <= RefreshMaxRetries; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(RefreshRetryDelayMs);
            }

            try
            {
                var url = $"https://securetoken.googleapis.com/v1/token?key={_apiKey}";

                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken
                });

                var response = await _httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<RefreshResponse>();

                    if (result?.IdToken is not null && result.RefreshToken is not null)
                    {
                        await StoreTokensAsync(result.IdToken, result.RefreshToken);
                        return true;
                    }
                }

                _logger.LogWarning("Token refresh attempt {Attempt} failed with status {StatusCode}",
                    attempt + 1, response.StatusCode);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Token refresh attempt {Attempt} timed out", attempt + 1);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Token refresh attempt {Attempt} failed due to network error", attempt + 1);
            }
        }

        return false;
    }

    private async Task StoreTokensAsync(string idToken, string refreshToken)
    {
        await _tokenStorage.SetAsync(IdTokenKey, idToken);
        await _tokenStorage.SetAsync(RefreshTokenKey, refreshToken);
    }

    private async Task<string> ParseFirebaseErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var errorBody = await response.Content.ReadFromJsonAsync<FirebaseErrorResponse>();
            var errorCode = errorBody?.Error?.Message ?? string.Empty;

            return MapFirebaseErrorCode(errorCode);
        }
        catch
        {
            return "Erro ao autenticar. Tente novamente.";
        }
    }

    internal static string MapFirebaseErrorCode(string errorCode) => errorCode switch
    {
        "EMAIL_NOT_FOUND" => "Email ou senha incorretos.",
        "INVALID_PASSWORD" => "Email ou senha incorretos.",
        "INVALID_LOGIN_CREDENTIALS" => "Email ou senha incorretos.",
        "USER_DISABLED" => "Sua conta está desativada. Contate o administrador.",
        "TOO_MANY_ATTEMPTS_TRY_LATER" => "Muitas tentativas. Tente novamente mais tarde.",
        _ => "Erro ao autenticar. Tente novamente."
    };

    // Internal response models for Firebase REST API

    private sealed class SignInResponse
    {
        [JsonPropertyName("idToken")]
        public string? IdToken { get; set; }

        [JsonPropertyName("refreshToken")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expiresIn")]
        public string? ExpiresIn { get; set; }

        [JsonPropertyName("localId")]
        public string? LocalId { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    private sealed class RefreshResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public string? ExpiresIn { get; set; }
    }

    private sealed class FirebaseErrorResponse
    {
        [JsonPropertyName("error")]
        public FirebaseError? Error { get; set; }
    }

    private sealed class FirebaseError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
