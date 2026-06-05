using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
/// bUnit component tests for FileRecords search functionality.
/// Validates: Requirements 1.1, 1.2, 1.3, 2.1, 2.2, 2.3, 2.5, 5.2, 5.3, 6.5, 6.6
/// </summary>
public class FileRecordsSearchTests : BunitContext
{
    private readonly MockHttpMessageHandler _httpHandler;
    private readonly ITokenProvider _tokenProvider;

    public FileRecordsSearchTests()
    {
        _tokenProvider = Substitute.For<ITokenProvider>();
        _tokenProvider.GetTokenAsync().Returns(Task.FromResult<string?>("fake-token"));

        _httpHandler = new MockHttpMessageHandler();

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("Api").Returns(_ =>
            new HttpClient(_httpHandler) { BaseAddress = new Uri("http://localhost") });

        Services.AddSingleton(httpClientFactory);
        Services.AddSingleton(_tokenProvider);
        Services.AddSingleton(sp =>
            new AuthenticatedHttpClient(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<ITokenProvider>()));

        // Add fake authorization services for bUnit
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthStateProvider());

        // Configure JS interop in loose mode
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void SearchBar_RendersWithCorrectPlaceholderAndMaxLength()
    {
        // Arrange - initial load returns some records
        SetupLoadRecordsResponse(GetSampleRecords());

        // Act
        var cut = Render<FileRecords>();

        // Assert
        var searchInput = cut.Find("input.search-input");
        Assert.Equal("Buscar por nome do arquivo ou cliente...", searchInput.GetAttribute("placeholder"));
        Assert.Equal("200", searchInput.GetAttribute("maxlength"));
    }

    [Fact]
    public void SearchButton_TriggersApiCall_WithNonEmptyInput()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());
        var cut = Render<FileRecords>();

        var searchResults = new List<FileRecordResponse>
        {
            new(Guid.NewGuid(), "Logo Design", FileType.CorelDRAW, null, DateTime.Now, "Acme Corp", "001")
        };
        SetupSearchResponse(searchResults);

        // Act - type and click search
        var searchInput = cut.Find("input.search-input");
        searchInput.Input("Logo");

        var searchButton = cut.Find("button.btn-search");
        searchButton.Click();

        // Assert - API was called (results are shown)
        cut.WaitForState(() => cut.Markup.Contains("1 resultado para"));
        Assert.Contains("1 resultado para 'Logo'", cut.Markup);
    }

    [Fact]
    public void EnterKey_TriggersSearch()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());
        var cut = Render<FileRecords>();

        var searchResults = new List<FileRecordResponse>
        {
            new(Guid.NewGuid(), "Banner Evento", FileType.Photoshop, null, DateTime.Now, "Cliente X", "002")
        };
        SetupSearchResponse(searchResults);

        // Act - type and press Enter
        var searchInput = cut.Find("input.search-input");
        searchInput.Input("Banner");
        searchInput.KeyDown(key: "Enter");

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("1 resultado para"));
        Assert.Contains("1 resultado para 'Banner'", cut.Markup);
    }

    [Fact]
    public void LoadingState_ShownDuringSearch()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());
        var cut = Render<FileRecords>();

        // Setup a search that never completes
        _httpHandler.SetPendingResponse();

        // Act - type and click search
        var searchInput = cut.Find("input.search-input");
        searchInput.Input("test");

        var searchButton = cut.Find("button.btn-search");
        searchButton.Click();

        // Assert - spinner/loading text shown in the button
        Assert.Contains("Buscando...", cut.Markup);
        Assert.NotNull(searchButton.GetAttribute("disabled"));
    }

    [Fact]
    public void ErrorMessage_DisplayedOnFailure()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());
        var cut = Render<FileRecords>();

        // Setup API to return error
        _httpHandler.SetErrorResponse(HttpStatusCode.InternalServerError);

        // Act - type and click search
        var searchInput = cut.Find("input.search-input");
        searchInput.Input("something");

        var searchButton = cut.Find("button.btn-search");
        searchButton.Click();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Não foi possível completar a busca"));
        Assert.Contains("Não foi possível completar a busca. Tente novamente.", cut.Markup);
    }

    [Fact]
    public void NoResults_ShowsEmptyStateMessage()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());
        var cut = Render<FileRecords>();

        // Setup search to return empty list
        SetupSearchResponse(new List<FileRecordResponse>());

        // Act
        var searchInput = cut.Find("input.search-input");
        searchInput.Input("xyznonexistent");

        var searchButton = cut.Find("button.btn-search");
        searchButton.Click();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Nenhum resultado encontrado"));
        Assert.Contains("Nenhum resultado encontrado para 'xyznonexistent'", cut.Markup);
    }

    [Fact]
    public void ResultsSummary_ShowsCountAndSearchedTerm()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());
        var cut = Render<FileRecords>();

        var searchResults = new List<FileRecordResponse>
        {
            new(Guid.NewGuid(), "Logo Empresa A", FileType.CorelDRAW, null, DateTime.Now, "Cliente A", "001"),
            new(Guid.NewGuid(), "Logo Empresa B", FileType.Illustrator, null, DateTime.Now, "Cliente B", "002"),
            new(Guid.NewGuid(), "Logo Marca C", FileType.Photoshop, null, DateTime.Now, "Cliente C", "003")
        };
        SetupSearchResponse(searchResults);

        // Act
        var searchInput = cut.Find("input.search-input");
        searchInput.Input("Logo");

        var searchButton = cut.Find("button.btn-search");
        searchButton.Click();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("resultados para"));
        Assert.Contains("3 resultados para 'Logo'", cut.Markup);
    }

    [Fact]
    public void ClearSearch_RestoresFullList()
    {
        // Arrange
        var fullList = GetSampleRecords();
        SetupLoadRecordsResponse(fullList);
        var cut = Render<FileRecords>();

        // Perform a search first
        var searchResults = new List<FileRecordResponse>
        {
            new(Guid.NewGuid(), "Logo Design", FileType.CorelDRAW, null, DateTime.Now, "Acme", "001")
        };
        SetupSearchResponse(searchResults);

        var searchInput = cut.Find("input.search-input");
        searchInput.Input("Logo");
        cut.Find("button.btn-search").Click();

        cut.WaitForState(() => cut.Markup.Contains("1 resultado para"));

        // Setup load to return full list again
        SetupLoadRecordsResponse(fullList);

        // Act - click "Limpar busca"
        var clearButton = cut.Find("button.btn-clear-search");
        clearButton.Click();

        // Assert - full list shown, no search summary
        cut.WaitForState(() => !cut.Markup.Contains("resultado para"));
        Assert.DoesNotContain("resultado para", cut.Markup);
        Assert.Contains(fullList[0].Name, cut.Markup);
    }

    [Fact]
    public void AriaLabels_PresentOnSearchElements()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());

        // Act
        var cut = Render<FileRecords>();

        // Assert
        var searchInput = cut.Find("input.search-input");
        Assert.Equal("Buscar fichas de arquivo", searchInput.GetAttribute("aria-label"));

        var searchButton = cut.Find("button.btn-search");
        Assert.Equal("Executar busca", searchButton.GetAttribute("aria-label"));
    }

    [Fact]
    public void TabOrder_SearchBar_SearchButton_NovaFicha()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());

        // Act
        var cut = Render<FileRecords>();

        // Assert - all focusable elements in search row have tabindex="0"
        var searchInput = cut.Find("input.search-input");
        var searchButton = cut.Find("button.btn-search");
        var novaFichaLink = cut.Find("a.btn-new");

        // All should have tabindex 0 (natural tab order following DOM order)
        Assert.Equal("0", searchInput.GetAttribute("tabindex"));
        Assert.Equal("0", searchButton.GetAttribute("tabindex"));
        Assert.Equal("0", novaFichaLink.GetAttribute("tabindex"));

        // Verify DOM order: search input before search button before nova ficha
        var searchRow = cut.Find(".search-row");
        var searchRowMarkup = searchRow.InnerHtml;
        var inputPos = searchRowMarkup.IndexOf("search-input");
        var buttonPos = searchRowMarkup.IndexOf("btn-search");
        var novaFichaPos = searchRowMarkup.IndexOf("btn-new");

        Assert.True(inputPos < buttonPos, "Search input should come before search button in DOM");
        Assert.True(buttonPos < novaFichaPos, "Search button should come before Nova Ficha in DOM");
    }

    [Fact]
    public void ResponsiveLayout_SearchRowHasCorrectCssClasses()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());

        // Act
        var cut = Render<FileRecords>();

        // Assert - verify the search-row and search-group CSS classes exist
        // (responsive stacking at ≤768px is handled by CSS, we verify the structure exists)
        var searchRow = cut.Find(".search-row");
        Assert.NotNull(searchRow);

        var searchGroup = cut.Find(".search-group");
        Assert.NotNull(searchGroup);

        // Verify the search input and button are inside the search-group for responsive layout
        var groupHtml = searchGroup.InnerHtml;
        Assert.Contains("search-input", groupHtml);
        Assert.Contains("btn-search", groupHtml);
    }

    // --- Helper Methods ---

    private void SetupLoadRecordsResponse(List<FileRecordResponse> records)
    {
        _httpHandler.SetResponse("/api/file-records", records);
    }

    private void SetupSearchResponse(List<FileRecordResponse> results)
    {
        _httpHandler.SetResponsePattern("/api/file-records/search", results);
    }

    private static List<FileRecordResponse> GetSampleRecords() =>
    [
        new(Guid.NewGuid(), "Cartão Visita João", FileType.CorelDRAW, 1, new DateTime(2024, 1, 15), "João Silva", "A001"),
        new(Guid.NewGuid(), "Banner Loja", FileType.Photoshop, 2, new DateTime(2024, 2, 20), "Maria Loja", "B002"),
        new(Guid.NewGuid(), "Folder Evento", FileType.Illustrator, null, new DateTime(2024, 3, 10), "Carlos Eventos", "C003")
    ];
}

/// <summary>
/// Mock HTTP message handler for controlling API responses in bUnit tests.
/// </summary>
internal class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Dictionary<string, HttpResponseMessage> _responses = new();
    private readonly Dictionary<string, HttpResponseMessage> _patternResponses = new();
    private bool _isPending;
    private HttpStatusCode? _errorStatusCode;

    public void SetResponse<T>(string path, T content)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(content)
        };
        _responses[path] = response;
        _isPending = false;
        _errorStatusCode = null;
    }

    public void SetResponsePattern<T>(string pathPrefix, T content)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(content)
        };
        _patternResponses[pathPrefix] = response;
        _isPending = false;
        _errorStatusCode = null;
    }

    public void SetPendingResponse()
    {
        _isPending = true;
        _errorStatusCode = null;
    }

    public void SetErrorResponse(HttpStatusCode statusCode)
    {
        _errorStatusCode = statusCode;
        _isPending = false;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_isPending)
        {
            // Simulate a never-completing request
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new OperationCanceledException();
        }

        if (_errorStatusCode.HasValue)
        {
            return new HttpResponseMessage(_errorStatusCode.Value);
        }

        var path = request.RequestUri?.PathAndQuery ?? string.Empty;

        // Check pattern responses first (for search with query params)
        foreach (var (prefix, response) in _patternResponses)
        {
            if (path.StartsWith(prefix))
            {
                // Return a new response with the same content to avoid disposed content issues
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
                };
            }
        }

        // Check exact path responses
        if (_responses.TryGetValue(path, out var exactResponse))
        {
            var content = await exactResponse.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            };
        }

        // Default: return empty list for any unmatched path
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(Array.Empty<object>())
        };
    }
}

/// <summary>
/// Fake authentication state provider for bUnit tests that simulates an authenticated user.
/// </summary>
internal class FakeAuthStateProvider : AuthenticationStateProvider
{
    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = new System.Security.Claims.ClaimsIdentity("TestAuth");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(principal));
    }
}
