using System.Text.Json;
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
/// bUnit tests for highlighting the search term in the list and remembering the chosen columns.
/// </summary>
public class FileRecordsHighlightAndColumnsTests : BunitContext
{
    private const string StorageKey = "sinter.fileRecords.columns";
    private readonly MockHttpMessageHandler _httpHandler = new();

    public FileRecordsHighlightAndColumnsTests()
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
            new(Guid.NewGuid(), "Folheto Promoção", FileType.PDF, null, new DateTime(2026, 6, 17), "Horto Dente", 2)));
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void ActiveSearch_MarksMatchingTextInNameAndClient()
    {
        // Arrange
        Navigation.NavigateTo("/file-records?q=promocao%20horto");

        // Act
        var cut = Render<FileRecords>();
        cut.WaitForState(() => cut.Markup.Contains("1 resultado para"));

        // Assert
        var tableMarks = cut.FindAll(".records-table mark.search-match").Select(m => m.TextContent);
        Assert.Equal(["Promoção", "Horto"], tableMarks);
        Assert.Equal("Folheto Promoção", cut.Find(".records-table td.cell-name").TextContent);
    }

    [Fact]
    public void NoSearch_RendersNoMarks()
    {
        // Arrange
        Navigation.NavigateTo("/file-records");

        // Act
        var cut = Render<FileRecords>();
        cut.WaitForState(() => cut.Markup.Contains("Cartão Visita"));

        // Assert
        Assert.Empty(cut.FindAll("mark"));
    }

    [Fact]
    public void SavedColumnChoice_IsAppliedOnLoad()
    {
        // Arrange
        var saved = JsonSerializer.Serialize(new Dictionary<string, bool>
        {
            ["type"] = false,
            ["flopDiskNumber"] = true,
            ["removedColumn"] = true,
        });
        JSInterop.Setup<string?>("ColumnSelector.loadVisibility", StorageKey).SetResult(saved);
        Navigation.NavigateTo("/file-records");

        // Act
        var cut = Render<FileRecords>();
        cut.WaitForState(() => cut.Markup.Contains("Cartão Visita"));

        // Assert
        var headers = cut.FindAll(".records-table th .th-label").Select(h => h.TextContent).ToList();
        Assert.DoesNotContain("Tipo", headers);
        Assert.Contains("Nº Disquete", headers);
        Assert.Contains("Nome", headers);
    }

    [Fact]
    public void SavedChoiceHidingEveryColumn_IsIgnored()
    {
        // Arrange
        var saved = JsonSerializer.Serialize(new[] { "name", "type", "fileNumber", "client", "date", "flopDiskNumber", "actions" }
            .ToDictionary(id => id, _ => false));
        JSInterop.Setup<string?>("ColumnSelector.loadVisibility", StorageKey).SetResult(saved);
        Navigation.NavigateTo("/file-records");

        // Act
        var cut = Render<FileRecords>();
        cut.WaitForState(() => cut.Markup.Contains("Cartão Visita"));

        // Assert
        Assert.Contains("Nome", cut.FindAll(".records-table th .th-label").Select(h => h.TextContent));
    }

    [Fact]
    public void TogglingColumn_SavesChoice()
    {
        // Arrange
        Navigation.NavigateTo("/file-records");
        var cut = Render<FileRecords>();
        cut.WaitForState(() => cut.Markup.Contains("Cartão Visita"));
        cut.Find("button.btn-campos").Click();

        // Act
        var typeOption = cut.FindAll(".column-option").Single(o => o.TextContent.Trim() == "Tipo");
        typeOption.QuerySelector("input")!.Change(false);

        // Assert
        var save = Assert.Single(JSInterop.Invocations["ColumnSelector.saveVisibility"]);
        Assert.Equal(StorageKey, save.Arguments[0]);
        var savedState = JsonSerializer.Deserialize<Dictionary<string, bool>>((string)save.Arguments[1]!)!;
        Assert.False(savedState["type"]);
        Assert.True(savedState["name"]);
    }

    private static PaginatedResponse<FileRecordResponse> Page(FileRecordResponse record) =>
        new([record], 1, false);
}
