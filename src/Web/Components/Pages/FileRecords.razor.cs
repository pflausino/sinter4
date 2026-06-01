using Microsoft.AspNetCore.Components;
using Shared.Dtos;
using Web.Services;

namespace Web.Components.Pages;

public partial class FileRecords
{
    [Inject] private AuthenticatedHttpClient ApiClient { get; set; } = default!;

    private List<FileRecordResponse>? Records { get; set; }
    private bool IsLoading { get; set; } = true;
    private bool HasError { get; set; }

    // Delete confirmation modal state
    private bool ShowDeleteModal { get; set; }
    private bool IsDeleting { get; set; }
    private Guid? DeleteTargetId { get; set; }
    private string? DeleteTargetName { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadRecords();
    }

    private async Task LoadRecords()
    {
        IsLoading = true;
        HasError = false;

        try
        {
            var client = await ApiClient.CreateClientAsync();
            Records = await client.GetFromJsonAsync<List<FileRecordResponse>>("/api/file-records");
        }
        catch
        {
            Records = null;
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RequestDelete(Guid id, string name)
    {
        DeleteTargetId = id;
        DeleteTargetName = name;
        ShowDeleteModal = true;
    }

    private void CancelDelete()
    {
        ShowDeleteModal = false;
        DeleteTargetId = null;
        DeleteTargetName = null;
    }

    private async Task ConfirmDelete()
    {
        if (DeleteTargetId is null) return;

        IsDeleting = true;

        try
        {
            var client = await ApiClient.CreateClientAsync();
            var response = await client.DeleteAsync($"/api/file-records/{DeleteTargetId}");

            if (response.IsSuccessStatusCode)
            {
                Records = Records?.Where(r => r.Id != DeleteTargetId).ToList();
            }
        }
        catch
        {
            // Silently handle errors
        }
        finally
        {
            IsDeleting = false;
            ShowDeleteModal = false;
            DeleteTargetId = null;
            DeleteTargetName = null;
        }
    }
}
