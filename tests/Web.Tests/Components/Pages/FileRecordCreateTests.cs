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
/// bUnit tests for the create page: field locks for entering records in series, Enter order,
/// the possible-duplicate warning and returning to the search the page was opened from.
/// </summary>
public class FileRecordCreateTests : BunitContext
{
    private readonly RoutingHandler _httpHandler = new();

    public FileRecordCreateTests()
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
    public void EnterOrder_IncludesFlopDiskNumber()
    {
        // Act
        Render<FileRecordCreate>();

        // Assert
        Assert.Equal(
            ["name", "fileType", "client", "date", "flopDiskNumber", "fileNumber"],
            LastNavigationFields());
    }

    [Fact]
    public void LockingField_RemovesItFromEnterOrder()
    {
        // Arrange
        var cut = Render<FileRecordCreate>();

        // Act
        cut.Find("button[aria-label='Bloquear cliente']").Click();

        // Assert
        Assert.Equal(["name", "fileType", "date", "flopDiskNumber", "fileNumber"], LastNavigationFields());
        Assert.Equal("true", cut.Find("button[aria-label='Desbloquear cliente']").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Save_KeepsLockedFields_AndClearsTheOthers()
    {
        // Arrange
        _httpHandler.OnPost = _ => new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = JsonContent.Create(Record("Folder A"))
        };
        var cut = Render<FileRecordCreate>();
        cut.Find("button[aria-label='Bloquear cliente']").Click();
        cut.Find("button[aria-label='Bloquear número do disquete']").Click();
        cut.Find("button[aria-label='Bloquear tipo de arquivo']").Click();

        cut.Find("#name").Change("Folder A");
        cut.Find("#fileType").Change(nameof(FileType.PDF));
        cut.Find("#client").Change("Horto Dente");
        cut.Find("#date").Change("2026-06-17");
        cut.Find("#flopDiskNumber").Change("12");
        cut.Find("#fileNumber").Change("2040");

        // Act
        cut.Find("form").Submit();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("criada com sucesso"));
        Assert.Equal("", cut.Find("#name").GetAttribute("value"));
        Assert.Equal("Horto Dente", cut.Find("#client").GetAttribute("value"));
        Assert.Equal("12", cut.Find("#flopDiskNumber").GetAttribute("value"));
        Assert.Equal(nameof(FileType.PDF), cut.Find("#fileType").GetAttribute("value"));
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), cut.Find("#date").GetAttribute("value"));
        Assert.Equal("0", cut.Find("#fileNumber").GetAttribute("value"));
    }

    [Fact]
    public void NameMatchingExistingRecord_ShowsDuplicateWarning()
    {
        // Arrange
        _httpHandler.OnGet = request =>
        {
            Assert.Equal("/api/file-records/duplicates?name=Folder%20A", request.RequestUri!.PathAndQuery);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { Record("Folder A") }) };
        };
        var cut = Render<FileRecordCreate>();

        // Act
        cut.Find("#name").Change(" Folder A ");

        // Assert
        cut.WaitForState(() => cut.FindAll(".duplicate-warning").Count == 1);
        var warning = cut.Find(".duplicate-warning");
        Assert.Contains("Já existe uma ficha com este nome", warning.TextContent);
        Assert.Contains("arquivo 2040", warning.TextContent);
        Assert.Contains("Horto Dente", warning.TextContent);
        Assert.False(cut.Find("button.btn-submit").HasAttribute("disabled"));
    }

    [Fact]
    public void NameWithoutMatches_ShowsNoWarning()
    {
        // Arrange
        var checkedName = false;
        _httpHandler.OnGet = _ =>
        {
            checkedName = true;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<FileRecordResponse>()) };
        };
        var cut = Render<FileRecordCreate>();

        // Act
        cut.Find("#name").Change("Folder Novo");

        // Assert
        cut.WaitForAssertion(() => Assert.True(checkedName));
        Assert.Empty(cut.FindAll(".duplicate-warning"));
    }

    [Fact]
    public void DuplicateCheckFailure_ShowsNoWarning()
    {
        // Arrange
        _httpHandler.OnGet = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var cut = Render<FileRecordCreate>();

        // Act
        cut.Find("#name").Change("Folder A");

        // Assert
        Assert.Empty(cut.FindAll(".duplicate-warning"));
        Assert.Empty(cut.FindAll(".alert-error"));
    }

    [Fact]
    public void OpenedFromSearch_LinksBackToThatSearch()
    {
        // Arrange
        Services.GetRequiredService<NavigationManager>().NavigateTo("/file-records/new?q=horto%20dente");

        // Act
        var cut = Render<FileRecordCreate>();

        // Assert
        Assert.Equal("/file-records?q=horto%20dente", cut.Find("a.back-link").GetAttribute("href"));
        Assert.Equal("/file-records?q=horto%20dente", cut.Find(".form-actions a").GetAttribute("href"));
    }

    private string[] LastNavigationFields() =>
        (string[])JSInterop.Invocations["FormNavigation.initialize"].Last().Arguments[1]!;

    private static FileRecordResponse Record(string name) =>
        new(Guid.NewGuid(), name, FileType.PDF, 12, new DateTime(2026, 6, 17), "Horto Dente", 2040);

    private sealed class RoutingHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> OnGet { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<FileRecordResponse>()) };

        public Func<HttpRequestMessage, HttpResponseMessage> OnPost { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.Method == HttpMethod.Post ? OnPost(request) : OnGet(request));
    }
}
