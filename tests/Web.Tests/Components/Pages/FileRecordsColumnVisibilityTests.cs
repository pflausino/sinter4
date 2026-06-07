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
/// bUnit component tests for FileRecords column visibility selector.
/// Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 3.1, 3.2, 4.1, 4.3, 5.2, 5.3
/// </summary>
public class FileRecordsColumnVisibilityTests : BunitContext
{
    private readonly MockHttpMessageHandler _httpHandler;
    private readonly ITokenProvider _tokenProvider;

    public FileRecordsColumnVisibilityTests()
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
    public void CamposButton_RendersInToolbar()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());

        // Act
        var cut = Render<FileRecords>();

        // Assert
        var camposButton = cut.Find(".btn-campos");
        Assert.Contains("Campos", camposButton.TextContent);
    }

    [Fact]
    public void ClickingCampos_OpensDropdownWith7Checkboxes()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());
        var cut = Render<FileRecords>();

        // Act
        var camposButton = cut.Find(".btn-campos");
        camposButton.Click();

        // Assert
        var dropdown = cut.Find(".column-selector-dropdown");
        Assert.NotNull(dropdown);

        var checkboxes = cut.FindAll(".column-selector-dropdown input[type=checkbox]");
        Assert.Equal(7, checkboxes.Count);
    }

    [Fact]
    public void DefaultState_6Checked_1Unchecked()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());
        var cut = Render<FileRecords>();

        // Act - open the dropdown
        cut.Find(".btn-campos").Click();

        // Assert
        var checkboxes = cut.FindAll(".column-selector-dropdown input[type=checkbox]");
        var checkedCount = checkboxes.Count(cb => cb.HasAttribute("checked"));
        var uncheckedCount = checkboxes.Count(cb => !cb.HasAttribute("checked"));

        Assert.Equal(6, checkedCount);
        Assert.Equal(1, uncheckedCount);

        // Verify the unchecked one is Nº Disquete (6th in order, index 5)
        var labels = cut.FindAll(".column-selector-dropdown label.column-option");
        var flopDiskLabel = labels.First(l => l.TextContent.Contains("Nº Disquete"));
        var flopDiskCheckbox = flopDiskLabel.QuerySelector("input[type=checkbox]");
        Assert.NotNull(flopDiskCheckbox);
        Assert.False(flopDiskCheckbox.HasAttribute("checked"));
    }

    [Fact]
    public void UncheckingColumn_RemovesThAndTd()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());
        var cut = Render<FileRecords>();

        // Verify "Cliente" column is visible initially
        var headers = cut.FindAll("table.records-table thead th");
        Assert.Contains(headers, th => th.TextContent.Contains("Cliente"));

        var cells = cut.FindAll("table.records-table tbody td");
        Assert.Contains(cells, td => td.TextContent.Contains("João Silva"));

        // Act - open dropdown and uncheck "Cliente"
        cut.Find(".btn-campos").Click();

        var labels = cut.FindAll(".column-selector-dropdown label.column-option");
        var clienteLabel = labels.First(l => l.TextContent.Contains("Cliente"));
        var clienteCheckbox = clienteLabel.QuerySelector("input[type=checkbox]")!;
        clienteCheckbox.Change(false);

        // Assert - column is removed
        var headersAfter = cut.FindAll("table.records-table thead th");
        Assert.DoesNotContain(headersAfter, th => th.TextContent.Contains("Cliente"));

        var cellsAfter = cut.FindAll("table.records-table tbody td");
        Assert.DoesNotContain(cellsAfter, td => td.TextContent.Contains("João Silva"));
    }

    [Fact]
    public void CheckingFlopDiskNumber_AddsColumn()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());
        var cut = Render<FileRecords>();

        // Verify "Nº Disquete" is NOT visible initially
        var headersBefore = cut.FindAll("table.records-table thead th");
        Assert.DoesNotContain(headersBefore, th => th.TextContent.Contains("Nº Disquete"));

        // Act - open dropdown and check "Nº Disquete"
        cut.Find(".btn-campos").Click();

        var labels = cut.FindAll(".column-selector-dropdown label.column-option");
        var flopLabel = labels.First(l => l.TextContent.Contains("Nº Disquete"));
        var flopCheckbox = flopLabel.QuerySelector("input[type=checkbox]")!;
        flopCheckbox.Change(true);

        // Assert - column is now visible
        var headersAfter = cut.FindAll("table.records-table thead th");
        Assert.Contains(headersAfter, th => th.TextContent.Contains("Nº Disquete"));
    }

    [Fact]
    public void LastVisibleColumn_HasDisabledCheckbox()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());
        var cut = Render<FileRecords>();

        // Open dropdown
        cut.Find(".btn-campos").Click();

        // Uncheck all columns except one (leave only "Nome")
        var columnsToUncheck = new[] { "Tipo", "Nº Arquivo", "Cliente", "Data", "Ações" };
        foreach (var columnName in columnsToUncheck)
        {
            var labels = cut.FindAll(".column-selector-dropdown label.column-option");
            var label = labels.First(l => l.TextContent.Contains(columnName));
            var checkbox = label.QuerySelector("input[type=checkbox]")!;
            checkbox.Change(false);
        }

        // Assert - the last remaining checkbox (Nome) should be disabled
        var labelsAfter = cut.FindAll(".column-selector-dropdown label.column-option");
        var nomeLabel = labelsAfter.First(l => l.TextContent.Contains("Nome"));
        var nomeCheckbox = nomeLabel.QuerySelector("input[type=checkbox]")!;
        Assert.True(nomeCheckbox.HasAttribute("disabled"));
    }

    [Fact]
    public void EscapeKey_ClosesDropdown()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());
        var cut = Render<FileRecords>();

        // Open dropdown
        cut.Find(".btn-campos").Click();

        // Verify dropdown is open
        Assert.NotNull(cut.Find(".column-selector-dropdown"));

        // Act - press Escape on the column-selector div
        var columnSelector = cut.Find(".column-selector");
        columnSelector.KeyDown(key: "Escape");

        // Assert - dropdown is closed
        var dropdowns = cut.FindAll(".column-selector-dropdown");
        Assert.Empty(dropdowns);
    }

    [Fact]
    public void AriaAttributes_CorrectWhenClosed()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());

        // Act
        var cut = Render<FileRecords>();

        // Assert
        var camposButton = cut.Find(".btn-campos");
        Assert.Equal("listbox", camposButton.GetAttribute("aria-haspopup"));
        Assert.Equal("false", camposButton.GetAttribute("aria-expanded"));
        Assert.Equal("column-selector-list", camposButton.GetAttribute("aria-controls"));
    }

    [Fact]
    public void AriaAttributes_CorrectWhenOpen()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());
        var cut = Render<FileRecords>();

        // Act - open dropdown
        cut.Find(".btn-campos").Click();

        // Assert - button reflects open state
        var camposButton = cut.Find(".btn-campos");
        Assert.Equal("true", camposButton.GetAttribute("aria-expanded"));

        // Assert - dropdown has correct role
        var dropdown = cut.Find(".column-selector-dropdown");
        Assert.Equal("listbox", dropdown.GetAttribute("role"));

        // Assert - labels have role="option"
        var labels = cut.FindAll(".column-selector-dropdown label.column-option");
        Assert.All(labels, label => Assert.Equal("option", label.GetAttribute("role")));
    }

    [Fact]
    public void ActiveButtonStyling_WhenDropdownOpen()
    {
        // Arrange
        SetupLoadRecordsResponse(GetSampleRecords());
        var cut = Render<FileRecords>();

        // Verify button does NOT have "active" class when closed
        var camposButton = cut.Find(".btn-campos");
        Assert.DoesNotContain("active", camposButton.ClassList);

        // Act - open dropdown
        camposButton.Click();

        // Assert - button has "active" class when open
        camposButton = cut.Find(".btn-campos");
        Assert.Contains("active", camposButton.ClassList);
    }

    // --- Helper Methods ---

    private void SetupLoadRecordsResponse(List<FileRecordResponse> records)
    {
        var paginated = new PaginatedResponse<FileRecordResponse>(records, records.Count, false);
        _httpHandler.SetResponsePattern("/api/file-records", paginated);
    }

    private static List<FileRecordResponse> GetSampleRecords() =>
    [
        new(Guid.NewGuid(), "Cartão Visita João", FileType.CorelDRAW, 1, new DateTime(2024, 1, 15), "João Silva", "A001"),
        new(Guid.NewGuid(), "Banner Loja", FileType.Photoshop, 2, new DateTime(2024, 2, 20), "Maria Loja", "B002"),
        new(Guid.NewGuid(), "Folder Evento", FileType.Illustrator, null, new DateTime(2024, 3, 10), "Carlos Eventos", "C003")
    ];
}
