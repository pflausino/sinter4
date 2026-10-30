using System.Net;
using System.Net.Http.Json;
using Bunit;
using Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared.Dtos;
using Web.Components.Pages;
using Web.Services;

namespace Web.Tests.Components.Pages;

/// <summary>
/// bUnit tests for FileRecords behavior under out-of-order responses and API failures:
/// stale responses must not overwrite newer results, and load-more/delete failures must be visible.
/// </summary>
public class FileRecordsResilienceTests : BunitContext
{
    private readonly ControllableHttpMessageHandler _httpHandler = new();

    public FileRecordsResilienceTests()
    {
        var tokenProvider = Substitute.For<ITokenProvider>();
        tokenProvider.GetTokenAsync().Returns(Task.FromResult<string?>("fake-token"));

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("Api").Returns(_ =>
            new HttpClient(_httpHandler) { BaseAddress = new Uri("http://localhost") });

        Services.AddSingleton(httpClientFactory);
        Services.AddSingleton(tokenProvider);
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthStateProvider());
        Services.AddSingleton(sp =>
            new AuthenticatedHttpClient(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<ITokenProvider>(),
                sp.GetRequiredService<AuthenticationStateProvider>(),
                sp.GetRequiredService<NavigationManager>(),
                NullLogger<AuthenticatedHttpClient>.Instance));

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void ListLoad_CompletingAfterSearch_DoesNotOverwriteSearchResults()
    {
        // Arrange - the initial list load is held until the search has finished
        var pendingListLoad = _httpHandler.Hold("/api/file-records?");
        _httpHandler.Respond("/api/file-records/search", Page([Record("Logo Hortodente", "Hortodente")]));
        var cut = Render<FileRecords>();

        // Act - search completes first, then the stale list response arrives
        cut.Find("input.search-input").Input("hortodente");
        cut.Find("button.btn-search").Click();
        cut.WaitForState(() => cut.Markup.Contains("1 resultado para"));

        pendingListLoad.SetResult(Json(Page([Record("Apostila DHL", ""), Record("Pastas Verum", "")])));

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Logo Hortodente", cut.Markup);
            Assert.DoesNotContain("Apostila DHL", cut.Markup);
            Assert.Contains("1 resultado para 'hortodente'", cut.Markup);
            Assert.DoesNotContain("Carregando fichas", cut.Markup);
        });
    }

    [Fact]
    public async Task LoadMore_CompletingAfterSearch_DoesNotAppendRowsFromPreviousList()
    {
        // Arrange - first page of the full list says there is more to load
        _httpHandler.Respond("/api/file-records?offset=0", Page([Record("Cartão Visita", "João")], hasMore: true));
        var pendingNextPage = _httpHandler.Hold("/api/file-records?offset=1");
        _httpHandler.Respond("/api/file-records/search", Page([Record("Logo Hortodente", "Hortodente")]));
        var cut = Render<FileRecords>();
        cut.WaitForState(() => cut.Markup.Contains("Cartão Visita"));

        // Act - scroll triggers the next page, a search replaces the list, then the old page arrives
        var loadMore = cut.InvokeAsync(() => cut.Instance.LoadMoreItems());
        cut.Find("input.search-input").Input("hortodente");
        cut.Find("button.btn-search").Click();
        cut.WaitForState(() => cut.Markup.Contains("1 resultado para"));

        pendingNextPage.SetResult(Json(Page([Record("Apostila DHL", "")])));
        await loadMore.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Contains("Logo Hortodente", cut.Markup);
        Assert.DoesNotContain("Apostila DHL", cut.Markup);
    }

    [Fact]
    public void LoadMore_Failure_ShowsRetryAndRetryAppendsRows()
    {
        // Arrange
        _httpHandler.Respond("/api/file-records?offset=0", Page([Record("Cartão Visita", "João")], hasMore: true));
        _httpHandler.Fail("/api/file-records?offset=1", HttpStatusCode.InternalServerError);
        var cut = Render<FileRecords>();
        cut.WaitForState(() => cut.Markup.Contains("Cartão Visita"));

        // Act
        cut.InvokeAsync(() => cut.Instance.LoadMoreItems());

        // Assert - failure is visible
        cut.WaitForState(() => cut.Markup.Contains("Não foi possível carregar mais registros."));

        // Act - retry succeeds
        _httpHandler.Respond("/api/file-records?offset=1", Page([Record("Banner Loja", "Maria")]));
        cut.Find(".load-more-error button").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Banner Loja", cut.Markup);
            Assert.DoesNotContain("Não foi possível carregar mais registros.", cut.Markup);
        });
    }

    [Fact]
    public void Delete_Failure_ShowsErrorAndKeepsRecord()
    {
        // Arrange
        _httpHandler.Respond("/api/file-records?", Page([Record("Cartão Visita", "João")]));
        _httpHandler.Fail("/api/file-records/", HttpStatusCode.InternalServerError, HttpMethod.Delete);
        var cut = Render<FileRecords>();
        cut.WaitForState(() => cut.Markup.Contains("Cartão Visita"));

        // Act
        cut.Find("button.btn-delete").Click();
        cut.Find("button.btn-modal-delete").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Não foi possível excluir 'Cartão Visita'", cut.Markup);
            Assert.NotEmpty(cut.FindAll("button.btn-delete"));
        });
    }

    [Fact]
    public void Delete_NotFound_RemovesRecordWithoutError()
    {
        // Arrange - record was already deleted elsewhere
        _httpHandler.Respond("/api/file-records?", Page([Record("Cartão Visita", "João")]));
        _httpHandler.Fail("/api/file-records/", HttpStatusCode.NotFound, HttpMethod.Delete);
        var cut = Render<FileRecords>();
        cut.WaitForState(() => cut.Markup.Contains("Cartão Visita"));

        // Act
        cut.Find("button.btn-delete").Click();
        cut.Find("button.btn-modal-delete").Click();

        // Assert
        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll("button.btn-delete"));
            Assert.DoesNotContain("Não foi possível excluir", cut.Markup);
        });
    }

    // --- Helpers ---

    private static FileRecordResponse Record(string name, string client) =>
        new(Guid.NewGuid(), name, FileType.PDF, null, new DateTime(2026, 6, 17), client, 2041);

    private static PaginatedResponse<FileRecordResponse> Page(List<FileRecordResponse> items, bool hasMore = false) =>
        new(items, hasMore ? items.Count + 1 : items.Count, hasMore);

    private static HttpResponseMessage Json<T>(T content) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(content) };

    /// <summary>
    /// Routes requests by method and path prefix (longest prefix wins). Routes can respond immediately,
    /// fail with a status code, or be held until the test completes them, to simulate slow responses.
    /// </summary>
    private sealed class ControllableHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<(HttpMethod Method, string Prefix), Func<Task<HttpResponseMessage>>> _routes = new();

        public void Respond<T>(string prefix, T content) =>
            _routes[(HttpMethod.Get, prefix)] = () => Task.FromResult(Json(content));

        public void Fail(string prefix, HttpStatusCode statusCode, HttpMethod? method = null) =>
            _routes[(method ?? HttpMethod.Get, prefix)] = () => Task.FromResult(new HttpResponseMessage(statusCode));

        public TaskCompletionSource<HttpResponseMessage> Hold(string prefix)
        {
            var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _routes[(HttpMethod.Get, prefix)] = () => pending.Task;
            return pending;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.PathAndQuery ?? string.Empty;

            var route = _routes
                .Where(r => r.Key.Method == request.Method && path.StartsWith(r.Key.Prefix))
                .OrderByDescending(r => r.Key.Prefix.Length)
                .Select(r => r.Value)
                .FirstOrDefault();

            return route?.Invoke()
                ?? Task.FromResult(Json(new PaginatedResponse<FileRecordResponse>([], 0, false)));
        }
    }
}
