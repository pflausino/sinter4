using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Web.Services;

namespace Web.Components.Layout;

public partial class MainLayout
{
    [Inject] private ITokenProvider TokenProvider { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<MainLayout> Logger { get; set; } = default!;

    private bool IsLoggingOut { get; set; }

    private async Task HandleLogoutAsync()
    {
        if (IsLoggingOut)
            return;

        IsLoggingOut = true;
        StateHasChanged();

        try
        {
            // Clear tokens from storage (handles failures gracefully internally)
            await TokenProvider.SignOutAsync();
        }
        catch (Exception ex)
        {
            // Even if storage clear fails, we still transition state and redirect
            Logger.LogWarning(ex, "Failed to clear tokens during logout, proceeding with state transition");
        }

        try
        {
            // Notify auth state provider to transition to unauthenticated
            if (AuthStateProvider is FirebaseAuthStateProvider firebaseAuthState)
            {
                firebaseAuthState.NotifyStateChanged();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to notify auth state change during logout");
        }

        // Redirect to login page
        Navigation.NavigateTo("/login", forceLoad: false);
    }
}
