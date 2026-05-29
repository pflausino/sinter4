using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Web.Services;

namespace Web.Components.Pages;

public partial class Login
{
    [Inject] private ITokenProvider TokenProvider { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private LoginModel Model { get; set; } = new();
    private bool IsSubmitting { get; set; }
    private string? ErrorMessage { get; set; }

    private async Task HandleValidSubmit()
    {
        IsSubmitting = true;
        ErrorMessage = null;
        StateHasChanged();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var result = await TokenProvider.SignInAsync(Model.Email, Model.Password);
            if (result.Success)
            {
                Navigation.NavigateTo("/dashboard");
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "O serviço está demorando para responder. Tente novamente.";
        }
        catch
        {
            ErrorMessage = "Serviço indisponível. Tente novamente mais tarde.";
        }
        finally
        {
            IsSubmitting = false;
            StateHasChanged();
        }
    }

    private void OnFieldChanged()
    {
        if (ErrorMessage is not null)
        {
            ErrorMessage = null;
            StateHasChanged();
        }
    }

    public sealed class LoginModel
    {
        [Required(ErrorMessage = "O email é obrigatório.")]
        [EmailAddress(ErrorMessage = "Formato de email inválido.")]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "A senha é obrigatória.")]
        [StringLength(128, MinimumLength = 6, ErrorMessage = "A senha deve ter no mínimo 6 caracteres.")]
        public string Password { get; set; } = string.Empty;
    }
}
