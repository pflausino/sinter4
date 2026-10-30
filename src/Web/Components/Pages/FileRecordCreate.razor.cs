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
    internal const string FieldName = "name";
    internal const string FieldFileType = "fileType";
    internal const string FieldClient = "client";
    internal const string FieldDate = "date";
    internal const string FieldFlopDiskNumber = "flopDiskNumber";
    internal const string FieldFileNumber = "fileNumber";

    // Enter moves through the fields in this (on-screen) order; locked fields are skipped
    private static readonly string[] FieldIds =
        [FieldName, FieldFileType, FieldClient, FieldDate, FieldFlopDiskNumber, FieldFileNumber];

    [Inject] private AuthenticatedHttpClient ApiClient { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // Search term of the list this page was opened from, so going back returns to it
    [SupplyParameterFromQuery(Name = "q")]
    public string? ReturnQuery { get; set; }

    private string ListUrl =>
        string.IsNullOrWhiteSpace(ReturnQuery) ? "/file-records" : $"/file-records?q={Uri.EscapeDataString(ReturnQuery)}";

    private FileRecordCreateModel Model { get; set; } = new();
    private bool IsSubmitting { get; set; }
    private string? ErrorMessage { get; set; }
    private string? SuccessMessage { get; set; }

    // Fields that keep their value after saving, for entering many records from the same disk or client
    private readonly HashSet<string> _lockedFields = [];

    // Records that already have the typed name; shown as a warning, never blocks saving
    private List<FileRecordResponse> DuplicateMatches { get; set; } = [];
    private int _duplicateCheckVersion;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await InitializeFormNavigation();
        }
    }

    private bool IsLocked(string fieldId) => _lockedFields.Contains(fieldId);

    private async Task ToggleLock(string fieldId)
    {
        if (!_lockedFields.Remove(fieldId))
        {
            _lockedFields.Add(fieldId);
        }

        await InitializeFormNavigation();
    }

    private Task InitializeFormNavigation() =>
        JS.InvokeVoidAsync("FormNavigation.initialize", "#create-form", FieldIds.Where(id => !IsLocked(id)).ToArray()).AsTask();

    private async Task CheckDuplicateNameAsync()
    {
        var version = ++_duplicateCheckVersion;
        var name = Model.Name.Trim();
        if (name.Length == 0)
        {
            DuplicateMatches = [];
            return;
        }

        List<FileRecordResponse>? matches;
        try
        {
            var client = await ApiClient.CreateClientAsync();
            matches = await client.GetFromJsonAsync<List<FileRecordResponse>>(
                $"/api/file-records/duplicates?name={Uri.EscapeDataString(name)}");
        }
        catch
        {
            // The warning is only a hint; if the check fails, saving works as before
            matches = null;
        }

        // Ignore the answer if the name changed (or the form was saved) while it was on its way
        if (version == _duplicateCheckVersion)
        {
            DuplicateMatches = matches ?? [];
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
                await InitializeFormNavigation();
                await JS.InvokeVoidAsync("FormNavigation.focusField", FieldName);
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
        var previous = Model;
        var fresh = new FileRecordCreateModel();
        if (IsLocked(FieldFileType)) fresh.FileType = previous.FileType;
        if (IsLocked(FieldClient)) fresh.Client = previous.Client;
        if (IsLocked(FieldDate)) fresh.Date = previous.Date;
        if (IsLocked(FieldFlopDiskNumber)) fresh.FlopDiskNumber = previous.FlopDiskNumber;
        if (IsLocked(FieldFileNumber)) fresh.FileNumber = previous.FileNumber;
        Model = fresh;

        DuplicateMatches = [];
        _duplicateCheckVersion++;
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
        [Range(0, 99_999_999_999, ErrorMessage = "O número do arquivo deve ser um inteiro entre 0 e 99999999999.")]
        public long FileNumber { get; set; }

        [Required(ErrorMessage = "A data é obrigatória.")]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "O cliente é obrigatório.")]
        [MinLength(1, ErrorMessage = "O cliente não pode ser vazio.")]
        public string Client { get; set; } = string.Empty;
    }
}
