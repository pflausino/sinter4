using System.Net;
using System.Net.Http.Json;
using Bunit;
using Domain.Enums;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared.Dtos;
using Web.Components.Pages;
using Web.Services;

namespace Web.Tests.Components.Pages;

/// <summary>
/// bUnit component tests for FileRecords server-side column sorting UI.
/// Feature: server-side-column-sorting
/// Validates: Requirements 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.5, 3.1, 4.1, 4.2, 5.2, 5.3
/// </summary>
public class FileRecordsSortTests : BunitContext
{
    private readonly RecordingHttpMessageHandler _httpHandler;

    public FileRecordsSortTests()
    {
        var tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.GetTokenAsync().Returns(Task.FromResult<string?>("fake-token"));

        _httpHandler = new RecordingHttpMessageHandler();

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("Api").Returns(_ =>
            new HttpClient(_httpHandler) { BaseAddress = new Uri("http://localhost") });

        Services.AddSingleton(httpClientFactory);
        Services.AddSingleton(tokenProvider);
        Services.AddSingleton(sp =>
            new AuthenticatedHttpClient(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<ITokenProvider>()));

        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthStateProvider());

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // -------- Column header rendering --------

    [Fact]
    public void SortableColumns_RenderWithSortableClassAndAriaSort()
    {
        SetupDefaultListResponse();
        var cut = Render<FileRecords>();

        // 5 columns are default-visible and sortable: name, type, fileNumber, client, date.
        // (flopDiskNumber is hidden by default; Actions is never sortable.)
        var sortableHeaders = cut.FindAll("th.sortable");
        Assert.Equal(5, sortableHeaders.Count);

        // Each has aria-sort attribute set to a valid value
        var validAriaValues = new HashSet<string> { "ascending", "descending", "none" };
        foreach (var header in sortableHeaders)
        {
            var ariaSort = header.GetAttribute("aria-sort");
            Assert.NotNull(ariaSort);
            Assert.Contains(ariaSort, validAriaValues);
        }

        // Each sortable header has an accessible label span
        foreach (var header in sortableHeaders)
        {
            Assert.NotNull(header.QuerySelector(".th-label"));
        }
    }

    [Fact]
    public void SortableColumns_AreKeyboardFocusable()
    {
        SetupDefaultListResponse();
        var cut = Render<FileRecords>();

        var sortableHeaders = cut.FindAll("th.sortable");
        foreach (var header in sortableHeaders)
        {
            Assert.Equal("0", header.GetAttribute("tabindex"));
        }
    }

    [Fact]
    public void ActionsColumn_IsNotSortable()
    {
        SetupDefaultListResponse();
        var cut = Render<FileRecords>();

        var actionsTh = cut.Find("th.col-actions");

        Assert.False(actionsTh.ClassList.Contains("sortable"));
        Assert.Null(actionsTh.GetAttribute("aria-sort"));
        Assert.Null(actionsTh.GetAttribute("tabindex"));
    }

    // -------- Default sort state --------

    [Fact]
    public void InitialLoad_UsesFileNumberDescAsDefaultSort()
    {
        SetupDefaultListResponse();

        var cut = Render<FileRecords>();
        cut.WaitForAssertion(() => Assert.NotEmpty(_httpHandler.Requests));

        var url = _httpHandler.Requests.First();
        Assert.Contains("sortBy=file_number", url);
        Assert.Contains("sortDir=desc", url);
    }

    [Fact]
    public void InitialRender_FileNumberHeaderShowsDescendingIndicator()
    {
        SetupDefaultListResponse();
        var cut = Render<FileRecords>();

        var fileNumberHeader = FindHeaderByLabel(cut, "Nº Arquivo");
        Assert.Equal("descending", fileNumberHeader.GetAttribute("aria-sort"));
        Assert.Contains("sorted", fileNumberHeader.ClassList);

        var indicator = fileNumberHeader.QuerySelector(".sort-indicator");
        Assert.NotNull(indicator);
        Assert.Equal("▼", indicator.TextContent.Trim());
    }

    [Fact]
    public void InitialRender_InactiveHeadersShowNeutralIndicator()
    {
        SetupDefaultListResponse();
        var cut = Render<FileRecords>();

        // Name header is inactive by default (file_number is the active default)
        var nameHeader = FindHeaderByLabel(cut, "Nome");
        Assert.Equal("none", nameHeader.GetAttribute("aria-sort"));
        Assert.DoesNotContain("sorted", nameHeader.ClassList);

        var indicator = nameHeader.QuerySelector(".sort-indicator");
        Assert.NotNull(indicator);
        Assert.Equal("↕", indicator.TextContent.Trim());
    }

    // -------- Click behavior --------

    [Fact]
    public void ClickingNewColumn_SendsSortByFieldAsc()
    {
        SetupDefaultListResponse();
        var cut = Render<FileRecords>();
        cut.WaitForAssertion(() => Assert.NotEmpty(_httpHandler.Requests));
        _httpHandler.Requests.Clear();

        var nameHeader = FindHeaderByLabel(cut, "Nome");
        nameHeader.Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(_httpHandler.Requests));
        var url = _httpHandler.Requests.First();
        Assert.Contains("sortBy=name", url);
        Assert.Contains("sortDir=asc", url);
    }

    [Fact]
    public void ClickingSameColumnTwice_TogglesToDesc()
    {
        SetupDefaultListResponse();
        var cut = Render<FileRecords>();
        cut.WaitForAssertion(() => Assert.NotEmpty(_httpHandler.Requests));

        var nameHeader = FindHeaderByLabel(cut, "Nome");

        // First click → asc
        nameHeader.Click();
        cut.WaitForAssertion(() =>
            Assert.Contains(_httpHandler.Requests, r => r.Contains("sortBy=name") && r.Contains("sortDir=asc")));

        _httpHandler.Requests.Clear();

        // Second click on same header → toggle to desc
        nameHeader = FindHeaderByLabel(cut, "Nome");
        nameHeader.Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(_httpHandler.Requests));
        var url = _httpHandler.Requests.First();
        Assert.Contains("sortBy=name", url);
        Assert.Contains("sortDir=desc", url);
    }

    [Fact]
    public void AfterSortClick_ActiveHeaderShowsAscendingIndicator()
    {
        SetupDefaultListResponse();
        var cut = Render<FileRecords>();

        var nameHeader = FindHeaderByLabel(cut, "Nome");
        nameHeader.Click();

        // Re-find after re-render
        cut.WaitForAssertion(() =>
        {
            var updated = FindHeaderByLabel(cut, "Nome");
            Assert.Equal("ascending", updated.GetAttribute("aria-sort"));
            Assert.Contains("sorted", updated.ClassList);
            var indicator = updated.QuerySelector(".sort-indicator");
            Assert.NotNull(indicator);
            Assert.Equal("▲", indicator.TextContent.Trim());
        });
    }

    [Fact]
    public void AfterSortClick_PreviouslySortedHeaderResetsIndicator()
    {
        SetupDefaultListResponse();
        var cut = Render<FileRecords>();

        // Default sorted column is Nº Arquivo (file_number). Click Name — the
        // previously active header should be reset to unsorted.
        var nameHeader = FindHeaderByLabel(cut, "Nome");
        nameHeader.Click();

        cut.WaitForAssertion(() =>
        {
            var fileNumberHeader = FindHeaderByLabel(cut, "Nº Arquivo");
            Assert.Equal("none", fileNumberHeader.GetAttribute("aria-sort"));
            Assert.DoesNotContain("sorted", fileNumberHeader.ClassList);
        });
    }

    // -------- Sort integration with search & infinite scroll --------

    [Fact]
    public void SortChange_DuringActiveSearch_IncludesSortParamsInSearchRequest()
    {
        SetupDefaultListResponse();
        var cut = Render<FileRecords>();

        // Execute a search first
        cut.Find("input.search-input").Input("logo");
        cut.Find("button.btn-search").Click();
        cut.WaitForAssertion(() =>
            Assert.Contains(_httpHandler.Requests, r => r.Contains("/api/file-records/search") && r.Contains("q=logo")));

        _httpHandler.Requests.Clear();

        // Now change sort — should re-fetch search with new sort params
        var clientHeader = FindHeaderByLabel(cut, "Cliente");
        clientHeader.Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(_httpHandler.Requests));
        var url = _httpHandler.Requests.First();
        Assert.Contains("/api/file-records/search", url);
        Assert.Contains("q=logo", url);
        Assert.Contains("sortBy=client", url);
        Assert.Contains("sortDir=asc", url);
    }

    [Fact]
    public void InfiniteScrollRequest_IncludesCurrentSortParams()
    {
        // Load enough records to enable "load more"
        SetupPagedResponse(records: MakeRecords(50), hasMore: true, totalCount: 100);
        var cut = Render<FileRecords>();
        cut.WaitForAssertion(() => Assert.NotEmpty(_httpHandler.Requests));

        // Change sort to name asc
        FindHeaderByLabel(cut, "Nome").Click();
        cut.WaitForAssertion(() =>
            Assert.Contains(_httpHandler.Requests, r => r.Contains("sortBy=name") && r.Contains("sortDir=asc")));

        _httpHandler.Requests.Clear();

        // Trigger LoadMoreItems directly (bUnit can't simulate scroll)
        cut.InvokeAsync(async () => await cut.Instance.LoadMoreItems());

        cut.WaitForAssertion(() => Assert.NotEmpty(_httpHandler.Requests));
        var url = _httpHandler.Requests.First();
        Assert.Contains("sortBy=name", url);
        Assert.Contains("sortDir=asc", url);
    }

    // -------- Mobile sort selector --------

    [Fact]
    public void MobileSortControl_RendersWithFieldSelectAndDirectionButton()
    {
        SetupDefaultListResponse();
        var cut = Render<FileRecords>();

        var mobileControl = cut.Find(".mobile-sort-control");
        Assert.NotNull(mobileControl);

        var select = mobileControl.QuerySelector("select#mobile-sort-field");
        Assert.NotNull(select);

        // Six sort field options
        var options = select.QuerySelectorAll("option");
        Assert.Equal(6, options.Length);

        var directionButton = mobileControl.QuerySelector("button.btn-sort-dir");
        Assert.NotNull(directionButton);
    }

    [Fact]
    public void MobileSortControl_HasProperAccessibilityAttributes()
    {
        SetupDefaultListResponse();
        var cut = Render<FileRecords>();

        var mobileControl = cut.Find(".mobile-sort-control");
        Assert.Equal("group", mobileControl.GetAttribute("role"));
        Assert.Equal("Ordenar registros", mobileControl.GetAttribute("aria-label"));

        var label = mobileControl.QuerySelector("label.mobile-sort-label");
        Assert.NotNull(label);
        Assert.Equal("mobile-sort-field", label.GetAttribute("for"));

        var directionButton = mobileControl.QuerySelector("button.btn-sort-dir");
        Assert.NotNull(directionButton?.GetAttribute("aria-label"));
    }

    [Fact]
    public void MobileFieldChange_TriggersFetchWithNewSort()
    {
        SetupDefaultListResponse();
        var cut = Render<FileRecords>();
        cut.WaitForAssertion(() => Assert.NotEmpty(_httpHandler.Requests));
        _httpHandler.Requests.Clear();

        var select = cut.Find("select#mobile-sort-field");
        select.Change("client");

        cut.WaitForAssertion(() => Assert.NotEmpty(_httpHandler.Requests));
        var url = _httpHandler.Requests.First();
        Assert.Contains("sortBy=client", url);
        Assert.Contains("sortDir=asc", url);
    }

    [Fact]
    public void MobileDirectionButton_TogglesSortDirection()
    {
        SetupDefaultListResponse();
        var cut = Render<FileRecords>();
        cut.WaitForAssertion(() => Assert.NotEmpty(_httpHandler.Requests));
        _httpHandler.Requests.Clear();

        // Default is file_number desc; toggle should produce file_number asc
        var directionButton = cut.Find("button.btn-sort-dir");
        directionButton.Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(_httpHandler.Requests));
        var url = _httpHandler.Requests.First();
        Assert.Contains("sortBy=file_number", url);
        Assert.Contains("sortDir=asc", url);
    }

    // -------- Helpers --------

    private void SetupDefaultListResponse()
    {
        SetupPagedResponse(MakeRecords(3), hasMore: false, totalCount: 3);
    }

    private void SetupPagedResponse(List<FileRecordResponse> records, bool hasMore, int totalCount)
    {
        var paginated = new PaginatedResponse<FileRecordResponse>(records, totalCount, hasMore);
        _httpHandler.SetJsonResponse(paginated);
    }

    private static List<FileRecordResponse> MakeRecords(int count) =>
        Enumerable.Range(1, count).Select(i => new FileRecordResponse(
            Guid.NewGuid(),
            $"Record {i}",
            FileType.CorelDRAW,
            null,
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(i - 1),
            $"Client {i}",
            i
        )).ToList();

    private static AngleSharp.Dom.IElement FindHeaderByLabel(IRenderedComponent<FileRecords> cut, string label)
    {
        foreach (var th in cut.FindAll("th.sortable"))
        {
            var labelSpan = th.QuerySelector(".th-label");
            if (labelSpan is not null && labelSpan.TextContent.Trim() == label)
                return th;
        }
        throw new InvalidOperationException($"Sortable header with label '{label}' not found");
    }
}

/// <summary>
/// HttpMessageHandler that records every request URL and returns a configured JSON response.
/// Used to verify the sort query parameters sent by the component.
/// </summary>
internal class RecordingHttpMessageHandler : HttpMessageHandler
{
    public List<string> Requests { get; } = [];

    private object? _jsonPayload;

    public void SetJsonResponse(object payload)
    {
        _jsonPayload = payload;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.PathAndQuery ?? string.Empty;
        Requests.Add(path);

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = _jsonPayload is null
                ? new StringContent("{\"items\":[],\"totalCount\":0,\"hasMore\":false}",
                    System.Text.Encoding.UTF8, "application/json")
                : JsonContent.Create(_jsonPayload)
        };

        return Task.FromResult(response);
    }
}
