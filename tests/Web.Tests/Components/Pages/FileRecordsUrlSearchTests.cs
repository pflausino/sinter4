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
/// bUnit tests for keeping the FileRecords search term in the URL (?q=), so a search survives
/// returning from the edit page, reloads and browser navigation.
/// </summary>
public class FileRecordsUrlSearchTests : BunitContext
{
    private readonly MockHttpMessageHandler _httpHandler = new();

    public FileRecordsUrlSearchTests()
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

        _httpHandler.SetResponsePattern("/api/file-records", Page(
            new(Guid.NewGuid(), "Cartão Visita", FileType.CorelDRAW, null, new DateTime(2026, 6, 17), "João", 1)));
        _httpHandler.SetResponsePattern("/api/file-records/search", Page(
            new(Guid.NewGuid(), "Logo Hortodente", FileType.PDF, null, new DateTime(2026, 6, 17), "Hortodente", 2)));
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void QueryInUrl_RunsSearchOnLoad()
    {
        // Arrange
        Navigation.NavigateTo("/file-records?q=hortodente");

        // Act
        var cut = Render<FileRecords>();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("1 resultado para 'hortodente'"));
        Assert.Equal("hortodente", cut.Find("input.search-input").GetAttribute("value"));
        Assert.Contains("Logo Hortodente", cut.Markup);
    }

    [Fact]
    public void ExecuteSearch_WritesTermToUrl()
    {
        // Arrange
        Navigation.NavigateTo("/file-records");
        var cut = Render<FileRecords>();
        cut.WaitForState(() => cut.Markup.Contains("Cartão Visita"));

        // Act
        cut.Find("input.search-input").Input("horto dente");
        cut.Find("button.btn-search").Click();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("1 resultado para"));
        Assert.EndsWith("/file-records?q=horto%20dente", Navigation.Uri);
    }

    [Fact]
    public void ClearSearch_RemovesTermFromUrl()
    {
        // Arrange
        Navigation.NavigateTo("/file-records?q=hortodente");
        var cut = Render<FileRecords>();
        cut.WaitForState(() => cut.Markup.Contains("1 resultado para"));

        // Act
        cut.Find("button.btn-clear-search").Click();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Cartão Visita"));
        Assert.EndsWith("/file-records", Navigation.Uri);
    }

    [Fact]
    public void EditLink_CarriesSearchTerm()
    {
        // Arrange
        Navigation.NavigateTo("/file-records?q=hortodente");

        // Act
        var cut = Render<FileRecords>();
        cut.WaitForState(() => cut.Markup.Contains("1 resultado para"));

        // Assert
        var editHref = cut.Find("a.btn-edit").GetAttribute("href");
        Assert.EndsWith("/edit?q=hortodente", editHref);
    }

    private static PaginatedResponse<FileRecordResponse> Page(FileRecordResponse record) =>
        new([record], 1, false);
}
