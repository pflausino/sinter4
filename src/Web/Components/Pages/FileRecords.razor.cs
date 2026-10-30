using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Shared.Dtos;
using Web.Services;

namespace Web.Components.Pages;

public record ColumnDefinition(string Id, string Label, bool DefaultVisible);

public partial class FileRecords : IAsyncDisposable
{
    private const int PageSize = 50;

    // Sort field constants — match API contract (snake_case, whitelisted server-side).
    internal const string SortFieldName = "name";
    internal const string SortFieldType = "file_type";
    internal const string SortFieldFileNumber = "file_number";
    internal const string SortFieldClient = "client";
    internal const string SortFieldDate = "date";
    internal const string SortFieldFlopDisk = "flop_disk_number";

    internal const string SortDirAsc = "asc";
    internal const string SortDirDesc = "desc";

    [Inject] private AuthenticatedHttpClient ApiClient { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    // Search term kept in the URL (/file-records?q=...) so the search survives a round trip to the
    // edit page, a reload, browser back/forward, and can be bookmarked.
    [SupplyParameterFromQuery(Name = "q")]
    public string? Query { get; set; }

    private bool _parametersInitialized;

    private List<FileRecordResponse> Records { get; set; } = [];
    private bool IsLoading { get; set; } = true;
    private bool HasError { get; set; }

    // Pagination state
    private int TotalCount { get; set; }
    private bool HasMore { get; set; }
    private bool IsLoadingMore { get; set; }
    private bool HasLoadMoreError { get; set; }
    private ElementReference SentinelRef { get; set; }

    // Incremented whenever the list is replaced (load, search, sort, clear). Responses that arrive
    // after a newer request started are discarded, so a slow old response can't overwrite the
    // current results or append rows from a different query.
    private int _listVersion;
    private DotNetObjectReference<FileRecords>? _dotNetRef;
    private bool _observerInitialized;

    // Search state
    private string SearchTerm { get; set; } = string.Empty;
    private bool IsSearching { get; set; }
    private bool HasSearchError { get; set; }
    private bool IsSearchActive { get; set; }
    private int? SearchResultCount { get; set; }
    private string? SearchedTerm { get; set; }

    // Sort state — default matches the UX preference: newest file numbers first when the page loads.
    private string CurrentSortField { get; set; } = SortFieldFileNumber;
    private string CurrentSortDirection { get; set; } = SortDirDesc;

    // Column visibility state
    private static readonly ColumnDefinition[] AllColumns =
    [
        new("name", "Nome", DefaultVisible: true),
        new("type", "Tipo", DefaultVisible: true),
        new("fileNumber", "Nº Arquivo", DefaultVisible: true),
        new("client", "Cliente", DefaultVisible: true),
        new("date", "Data", DefaultVisible: true),
        new("flopDiskNumber", "Nº Disquete", DefaultVisible: false),
        new("actions", "Ações", DefaultVisible: true),
    ];

    private Dictionary<string, bool> ColumnVisibility { get; set; } = new();
    private bool IsColumnSelectorOpen { get; set; }
    private ElementReference ColumnSelectorRef { get; set; }
    private DotNetObjectReference<FileRecords>? _columnSelectorDotNetRef;

    // Column visibility methods

    private void InitializeColumnVisibility()
    {
        ColumnVisibility = AllColumns.ToDictionary(c => c.Id, c => c.DefaultVisible);
    }

    private void ToggleColumnVisibility(string columnId)
    {
        if (!ColumnVisibility.ContainsKey(columnId)) return;

        // If trying to hide and it's the last visible column, do nothing
        if (ColumnVisibility[columnId] && VisibleColumnCount() <= 1) return;

        ColumnVisibility[columnId] = !ColumnVisibility[columnId];
    }

    private int VisibleColumnCount() => ColumnVisibility.Count(kv => kv.Value);

    private bool IsColumnVisible(string columnId) =>
        ColumnVisibility.TryGetValue(columnId, out var visible) && visible;

    private bool IsColumnDisabled(string columnId) =>
        ColumnVisibility.TryGetValue(columnId, out var visible) && visible && VisibleColumnCount() <= 1;

    // Column selector dropdown methods

    private async Task ToggleColumnSelector()
    {
        IsColumnSelectorOpen = !IsColumnSelectorOpen;

        if (IsColumnSelectorOpen)
        {
            _columnSelectorDotNetRef ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("ColumnSelector.open", ColumnSelectorRef, _columnSelectorDotNetRef);
        }
        else
        {
            await JS.InvokeVoidAsync("ColumnSelector.close");
        }
    }

    [JSInvokable]
    public void CloseColumnSelector()
    {
        IsColumnSelectorOpen = false;
        StateHasChanged();
    }

    private async Task HandleColumnSelectorKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && IsColumnSelectorOpen)
        {
            IsColumnSelectorOpen = false;
            await JS.InvokeVoidAsync("ColumnSelector.close");
        }
    }

    // Sort methods

    /// <summary>
    /// Called when a sortable column header is clicked. If the same field is clicked,
    /// toggles ASC ↔ DESC. Otherwise switches to the new field starting in ASC.
    /// Resets pagination and re-fetches from offset 0 so the new sort applies to all pages.
    /// </summary>
    private async Task SortBy(string field)
    {
        if (field == CurrentSortField)
        {
            CurrentSortDirection = CurrentSortDirection == SortDirAsc ? SortDirDesc : SortDirAsc;
        }
        else
        {
            CurrentSortField = field;
            CurrentSortDirection = SortDirAsc;
        }

        await ReloadWithCurrentSort();
    }

    /// <summary>
    /// Handles mobile sort field selection from the dropdown. Direction resets to ASC
    /// when the field changes (matches desktop click behavior).
    /// </summary>
    private async Task ChangeMobileSortField(ChangeEventArgs e)
    {
        var newField = e.Value?.ToString() ?? SortFieldDate;
        if (newField == CurrentSortField) return;

        CurrentSortField = newField;
        CurrentSortDirection = SortDirAsc;
        await ReloadWithCurrentSort();
    }

    /// <summary>
    /// Toggles the sort direction (ASC ↔ DESC) without changing the field.
    /// Used by the mobile direction toggle button.
    /// </summary>
    private async Task ToggleSortDirection()
    {
        CurrentSortDirection = CurrentSortDirection == SortDirAsc ? SortDirDesc : SortDirAsc;
        await ReloadWithCurrentSort();
    }

    private async Task ReloadWithCurrentSort()
    {
        _observerInitialized = false;
        if (IsSearchActive) await ReloadSearchWithCurrentSort();
        else await LoadRecords();
    }

    /// <summary>
    /// Re-runs the currently active search with the new sort params.
    /// Uses <see cref="SearchedTerm"/> (the committed search) instead of
    /// <see cref="SearchTerm"/> (the input box) so a user editing the box
    /// but not submitting doesn't lose their current results on sort change.
    /// </summary>
    private async Task ReloadSearchWithCurrentSort()
    {
        if (string.IsNullOrWhiteSpace(SearchedTerm))
        {
            await LoadRecords();
            return;
        }

        var version = StartListRequest();
        IsSearching = true;
        HasSearchError = false;

        try
        {
            var client = await ApiClient.CreateClientAsync();
            var encoded = Uri.EscapeDataString(SearchedTerm);
            var response = await client.GetFromJsonAsync<PaginatedResponse<FileRecordResponse>>(
                $"/api/file-records/search?q={encoded}&offset=0&limit={PageSize}{BuildSortQueryString()}");

            if (!IsCurrentListRequest(version)) return;

            if (response is not null)
            {
                Records = response.Items;
                SearchResultCount = response.TotalCount;
                HasMore = response.HasMore;
            }
            else
            {
                Records = [];
                SearchResultCount = 0;
                HasMore = false;
            }
        }
        catch
        {
            if (IsCurrentListRequest(version)) HasSearchError = true;
        }
        finally
        {
            if (IsCurrentListRequest(version)) IsSearching = false;
        }
    }

    /// <summary>
    /// Starts a request that replaces the list and invalidates any request still in flight.
    /// Clears the loading flags owned by the superseded request so they can't get stuck on.
    /// </summary>
    private int StartListRequest()
    {
        IsLoading = false;
        IsSearching = false;
        HasLoadMoreError = false;
        DeleteErrorName = null;
        return ++_listVersion;
    }

    private bool IsCurrentListRequest(int version) => version == _listVersion;

    /// <summary>
    /// Returns the aria-sort value for a header: "ascending", "descending", or "none".
    /// </summary>
    private string GetAriaSortValue(string field)
    {
        if (CurrentSortField != field) return "none";
        return CurrentSortDirection == SortDirAsc ? "ascending" : "descending";
    }

    /// <summary>
    /// Returns the visual sort indicator glyph for a header. Active columns show
    /// ▲ or ▼; inactive sortable columns show a subtle ↕.
    /// </summary>
    private string GetSortIcon(string field)
    {
        if (CurrentSortField != field) return "↕";
        return CurrentSortDirection == SortDirAsc ? "▲" : "▼";
    }

    private string BuildSortQueryString() =>
        $"&sortBy={CurrentSortField}&sortDir={CurrentSortDirection}";

    // Delete confirmation modal state
    private bool ShowDeleteModal { get; set; }
    private bool IsDeleting { get; set; }
    private Guid? DeleteTargetId { get; set; }
    private string? DeleteTargetName { get; set; }
    private string? DeleteErrorName { get; set; }

    protected override void OnInitialized()
    {
        InitializeColumnVisibility();
    }

    /// <summary>
    /// Runs the search from <c>?q=</c> on first load and whenever the URL's term changes from outside
    /// (browser back/forward). URL updates made by this page already match <see cref="SearchedTerm"/>,
    /// so they don't trigger a second search.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        var query = Query?.Trim() ?? string.Empty;
        if (_parametersInitialized && query == (SearchedTerm ?? string.Empty)) return;
        _parametersInitialized = true;

        SearchTerm = query;
        if (query.Length == 0) await ClearSearch();
        else await ExecuteSearch();
    }

    private void SyncSearchTermToUrl()
    {
        var urlTerm = Query?.Trim() ?? string.Empty;
        if (urlTerm == (SearchedTerm ?? string.Empty)) return;

        Navigation.NavigateTo(Navigation.GetUriWithQueryParameter("q", SearchedTerm), replace: true);
    }

    /// <summary>
    /// Query string appended to edit links so the edit page can return to the same search.
    /// </summary>
    private string ReturnQueryString =>
        string.IsNullOrEmpty(SearchedTerm) ? string.Empty : $"?q={Uri.EscapeDataString(SearchedTerm)}";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_observerInitialized && !IsLoading && !HasError && Records.Count > 0 && HasMore)
        {
            await InitializeInfiniteScroll();
        }
    }

    private async Task InitializeInfiniteScroll()
    {
        try
        {
            _dotNetRef ??= DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("InfiniteScroll.initialize", SentinelRef, _dotNetRef);
            _observerInitialized = true;
        }
        catch (JSException)
        {
            // Element may not be in DOM yet
        }
    }

    private async Task LoadRecords()
    {
        var version = StartListRequest();
        IsLoading = true;
        HasError = false;
        Records = [];
        _observerInitialized = false;

        try
        {
            var client = await ApiClient.CreateClientAsync();
            var response = await client.GetFromJsonAsync<PaginatedResponse<FileRecordResponse>>(
                $"/api/file-records?offset=0&limit={PageSize}{BuildSortQueryString()}");

            if (!IsCurrentListRequest(version)) return;

            if (response is not null)
            {
                Records = response.Items;
                TotalCount = response.TotalCount;
                HasMore = response.HasMore;
            }
        }
        catch
        {
            if (!IsCurrentListRequest(version)) return;

            Records = [];
            HasError = true;
        }
        finally
        {
            if (IsCurrentListRequest(version)) IsLoading = false;
        }
    }

    [JSInvokable]
    public async Task LoadMoreItems()
    {
        if (IsLoadingMore || !HasMore) return;

        var version = _listVersion;
        IsLoadingMore = true;
        HasLoadMoreError = false;
        StateHasChanged();

        try
        {
            var client = await ApiClient.CreateClientAsync();

            if (IsSearchActive && !string.IsNullOrWhiteSpace(SearchedTerm))
            {
                var encoded = Uri.EscapeDataString(SearchedTerm);
                var response = await client.GetFromJsonAsync<PaginatedResponse<FileRecordResponse>>(
                    $"/api/file-records/search?q={encoded}&offset={Records.Count}&limit={PageSize}{BuildSortQueryString()}");

                // The list was replaced while this page was loading — its rows belong to another query
                if (!IsCurrentListRequest(version)) return;

                if (response is not null)
                {
                    Records.AddRange(response.Items);
                    HasMore = response.HasMore;
                    SearchResultCount = response.TotalCount;
                }
            }
            else
            {
                var response = await client.GetFromJsonAsync<PaginatedResponse<FileRecordResponse>>(
                    $"/api/file-records?offset={Records.Count}&limit={PageSize}{BuildSortQueryString()}");

                if (!IsCurrentListRequest(version)) return;

                if (response is not null)
                {
                    Records.AddRange(response.Items);
                    TotalCount = response.TotalCount;
                    HasMore = response.HasMore;
                }
            }
        }
        catch
        {
            // Shown below the list with a retry button; the scroll observer won't fire again on its own
            if (IsCurrentListRequest(version)) HasLoadMoreError = true;
        }
        finally
        {
            IsLoadingMore = false;
            StateHasChanged();
        }
    }

    private async Task ExecuteSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            await ClearSearch();
            return;
        }

        var version = StartListRequest();
        var term = SearchTerm.Trim();
        IsSearching = true;
        HasSearchError = false;
        _observerInitialized = false;

        try
        {
            var client = await ApiClient.CreateClientAsync();
            var encoded = Uri.EscapeDataString(term);
            var response = await client.GetFromJsonAsync<PaginatedResponse<FileRecordResponse>>(
                $"/api/file-records/search?q={encoded}&offset=0&limit={PageSize}{BuildSortQueryString()}");

            if (!IsCurrentListRequest(version)) return;

            if (response is not null)
            {
                Records = response.Items;
                SearchResultCount = response.TotalCount;
                HasMore = response.HasMore;
            }
            else
            {
                Records = [];
                SearchResultCount = 0;
                HasMore = false;
            }

            // Use the term captured at submit time — the input may have changed while the request ran
            SearchedTerm = term;
            IsSearchActive = true;
            SyncSearchTermToUrl();
        }
        catch
        {
            if (IsCurrentListRequest(version)) HasSearchError = true;
        }
        finally
        {
            if (IsCurrentListRequest(version)) IsSearching = false;
        }
    }

    private async Task ClearSearch()
    {
        SearchTerm = string.Empty;
        IsSearchActive = false;
        SearchedTerm = null;
        SearchResultCount = null;
        HasSearchError = false;
        SyncSearchTermToUrl();
        await LoadRecords();
    }

    private async Task HandleSearchKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await ExecuteSearch();
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
        DeleteErrorName = null;

        try
        {
            var client = await ApiClient.CreateClientAsync();
            var response = await client.DeleteAsync($"/api/file-records/{DeleteTargetId}");

            // 404 means the record is already gone (e.g. deleted in another tab) — drop it from the list too
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Records = Records.Where(r => r.Id != DeleteTargetId).ToList();
                TotalCount--;
                if (IsSearchActive) SearchResultCount--;
            }
            else
            {
                DeleteErrorName = DeleteTargetName;
            }
        }
        catch
        {
            DeleteErrorName = DeleteTargetName;
        }
        finally
        {
            IsDeleting = false;
            ShowDeleteModal = false;
            DeleteTargetId = null;
            DeleteTargetName = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("InfiniteScroll.dispose");
            await JS.InvokeVoidAsync("ColumnSelector.close");
        }
        catch (JSDisconnectedException)
        {
            // Circuit already disconnected
        }

        _dotNetRef?.Dispose();
        _columnSelectorDotNetRef?.Dispose();
    }
}
