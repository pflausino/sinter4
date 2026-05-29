namespace Web.Services;

public interface ITokenProvider
{
    Task<AuthResult> SignInAsync(string email, string password);
    Task<string?> GetTokenAsync();
    Task SignOutAsync();
    Task<bool> RefreshTokenAsync();
}

public record AuthResult(bool Success, string? Token, string? ErrorMessage);
