using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Domain.Enums;
using Microsoft.AspNetCore.Components;
using Shared.Dtos;
using Web.Services;

namespace Web.Components.Pages;

public partial class FileRecordEdit
{
    [Inject] private AuthenticatedHttpClient ApiClient { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter] public Guid Id { get; set; }

    private FileRecordEditModel Model { get; set; } = new();
    private bool IsLoading { get; set; } = true;
    private bool NotFound { get; set; }
    private bool IsSubmitting { get; set; }
    private string? ErrorMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var client = await ApiClient.CreateClientAsync();
            var response = await client.GetAsync($"/api/file-records/{Id}");

            if (response.IsSuccessStatusCode)
            {
                var record = await response.Content.ReadFromJsonAsync<FileRecordResponse>();
                if (record is not null)
                {
                    Model.Name = record.Name;
                    Model.FileType = record.FileType;
                    Model.FlopDiskNumber = record.FlopDiskNumber;
                    Model.Date = record.Date;
                    Model.Client = record.Client;
                }
                else
                {
                    NotFound = true;
                }
            }
            else
            {
                NotFound = true;
            }
        }
        catch
        {
            NotFound = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task HandleValidSubmit()
    {
        IsSubmitting = true;
        ErrorMessage = null;
        StateHasChanged();

        try
        {
            var client = await ApiClient.CreateClientAsync();

            var request = new UpdateFileRecordRequest(
                Model.Name,
                Model.FileType,
                Model.FlopDiskNumber,
                Model.Date,
                Model.Client
            );

            var response = await client.PutAsJsonAsync($"/api/file-records/{Id}", request);

            if (response.IsSuccessStatusCode)
            {
                Navigation.NavigateTo("/file-records");
            }
            else
            {
                ErrorMessage = "Erro ao atualizar o registro. Tente novamente.";
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

    public sealed class FileRecordEditModel
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [MinLength(1, ErrorMessage = "O nome não pode ser vazio.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "O tipo de arquivo é obrigatório.")]
        [EnumDataType(typeof(FileType), ErrorMessage = "Tipo de arquivo inválido.")]
        public FileType FileType { get; set; } = FileType.CorelDRAW;

        public int? FlopDiskNumber { get; set; }

        [Required(ErrorMessage = "A data é obrigatória.")]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "O cliente é obrigatório.")]
        [MinLength(1, ErrorMessage = "O cliente não pode ser vazio.")]
        public string Client { get; set; } = string.Empty;
    }
}
