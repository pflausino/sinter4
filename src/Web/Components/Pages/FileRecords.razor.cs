using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Shared.Dtos;
using Web.Services;

namespace Web.Components.Pages;

public partial class FileRecords
{
    [Inject] private AuthenticatedHttpClient ApiClient { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private List<FileRecordResponse>? Records { get; set; }
    private bool IsLoading { get; set; } = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadRecords();
    }

    private async Task LoadRecords()
    {
        IsLoading = true;

        try
        {
            var client = await ApiClient.CreateClientAsync();
            Records = await client.GetFromJsonAsync<List<FileRecordResponse>>("/api/file-records");
        }
        catch
        {
            Records = [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteRecord(Guid id)
    {
        var confirmed = await JS.InvokeAsync<bool>("confirm", "Tem certeza que deseja excluir esta ficha?");
        if (!confirmed) return;

        try
        {
            var client = await ApiClient.CreateClientAsync();
            var response = await client.DeleteAsync($"/api/file-records/{id}");

            if (response.IsSuccessStatusCode)
            {
                Records = Records?.Where(r => r.Id != id).ToList();
            }
        }
        catch
        {
            // Silently handle errors — could add error messaging in the future
        }
    }
}
