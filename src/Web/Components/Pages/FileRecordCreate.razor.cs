using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Shared.Dtos;
using Web.Services;

namespace Web.Components.Pages;

public partial class FileRecordCreate
{
    private static readonly string[] FieldIds = ["name", "fileType", "client", "date", "fileNumber"];

    [Inject] private AuthenticatedHttpClient ApiClient { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private FileRecordCreateModel Model { get; set; } = new();
    private bool IsSubmitting { get; set; }
    private string? ErrorMessage { get; set; }
    private string? SuccessMessage { get; set; }
    private bool LockFileNumber { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JS.InvokeVoidAsync("FormNavigation.initialize", "#create-form", FieldIds);
        }
    }

    private async Task HandleValidSubmit()
    {
        IsSubmitting = true;
        ErrorMessage = null;
        SuccessMessage = null;
        StateHasChanged();

        try
        {
            var client = await ApiClient.CreateClientAsync();

            var request = new CreateFileRecordRequest(
                Model.Name,
                Model.FileType,
                Model.FlopDiskNumber,
                Model.Date,
                Model.Client,
                Model.FileNumber
            );

            var response = await client.PostAsJsonAsync("/api/file-records", request);

            if (response.IsSuccessStatusCode)
            {
                SuccessMessage = $"Ficha '{Model.Name}' criada com sucesso!";
                ResetForm();
                StateHasChanged();
                await Task.Yield();
                await JS.InvokeVoidAsync("FormNavigation.initialize", "#create-form", FieldIds);
                await JS.InvokeVoidAsync("FormNavigation.focusField", "name");
            }
            else
            {
                ErrorMessage = "Erro ao criar o registro. Tente novamente.";
            }
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

    private void ResetForm()
    {
        var preservedFileNumber = LockFileNumber ? Model.FileNumber : null;
        Model = new FileRecordCreateModel();
        if (preservedFileNumber is not null)
        {
            Model.FileNumber = preservedFileNumber;
        }
    }

    private void ToggleLockFileNumber()
    {
        LockFileNumber = !LockFileNumber;
    }

    public sealed class FileRecordCreateModel
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [MinLength(1, ErrorMessage = "O nome não pode ser vazio.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "O tipo de arquivo é obrigatório.")]
        [EnumDataType(typeof(FileType), ErrorMessage = "Tipo de arquivo inválido.")]
        public FileType FileType { get; set; } = FileType.CorelDRAW;

        public int? FlopDiskNumber { get; set; }

        [Required(ErrorMessage = "O número do arquivo é obrigatório.")]
        public string FileNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "A data é obrigatória.")]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "O cliente é obrigatório.")]
        [MinLength(1, ErrorMessage = "O cliente não pode ser vazio.")]
        public string Client { get; set; } = string.Empty;
    }
}
