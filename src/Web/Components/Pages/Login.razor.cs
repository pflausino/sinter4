using System.ComponentModel.DataAnnotations;

namespace Web.Components.Pages;

public partial class Login
{
    private LoginModel Model { get; set; } = new();
    private bool IsSubmitting { get; set; }

    private async Task HandleValidSubmit()
    {
        IsSubmitting = true;
        StateHasChanged();

        // Simulate async operation (future auth call)
        await Task.Delay(1500);

        IsSubmitting = false;
        StateHasChanged();
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
