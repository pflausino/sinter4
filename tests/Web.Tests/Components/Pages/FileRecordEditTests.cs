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
/// bUnit tests for the edit page: loading the record, not found, saving and returning to the
/// search the page was opened from, and save errors.
/// </summary>
public class FileRecordEditTests : BunitContext
{
    private static readonly Guid RecordId = Guid.NewGuid();
    private readonly RoutingHandler _httpHandler = new();

    public FileRecordEditTests()
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

        _httpHandler.OnGet = _ => Json(HttpStatusCode.OK, new FileRecordResponse(
            RecordId, "Folder Promoção", FileType.PDF, 12, new DateTime(2026, 6, 17), "Horto Dente", 2040));
    }

    private NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void Load_FillsFormWithRecord()
    {
        // Act
        var cut = RenderEdit();

        // Assert
        cut.WaitForState(() => cut.FindAll("form").Count == 1);
        Assert.Equal($"/api/file-records/{RecordId}", _httpHandler.LastGetPath);
        Assert.Equal("Folder Promoção", cut.Find("#name").GetAttribute("value"));
        Assert.Equal(nameof(FileType.PDF), cut.Find("#fileType").GetAttribute("value"));
        Assert.Equal("Horto Dente", cut.Find("#client").GetAttribute("value"));
        Assert.Equal("2026-06-17", cut.Find("#date").GetAttribute("value"));
        Assert.Equal("12", cut.Find("#flopDiskNumber").GetAttribute("value"));
        Assert.Equal("2040", cut.Find("#fileNumber").GetAttribute("value"));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public void LoadFailure_ShowsNotFound_WithLinkBackToSearch(HttpStatusCode status)
    {
        // Arrange
        _httpHandler.OnGet = _ => new HttpResponseMessage(status);
        Navigation.NavigateTo($"/file-records/{RecordId}/edit?q=horto");

        // Act
        var cut = RenderEdit();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Registro não encontrado"));
        Assert.Empty(cut.FindAll("form"));
        Assert.Equal("/file-records?q=horto", cut.Find(".not-found-state a").GetAttribute("href"));
    }

    [Fact]
    public void LoadThrows_ShowsNotFound()
    {
        // Arrange
        _httpHandler.OnGet = _ => throw new HttpRequestException("offline");

        // Act
        var cut = RenderEdit();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Registro não encontrado"));
    }

    [Fact]
    public void Save_SendsChanges_AndReturnsToSearch()
    {
        // Arrange
        UpdateFileRecordRequest? sent = null;
        _httpHandler.OnPut = request =>
        {
            Assert.Equal($"/api/file-records/{RecordId}", request.RequestUri!.PathAndQuery);
            sent = request.Content!.ReadFromJsonAsync<UpdateFileRecordRequest>().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        };
        Navigation.NavigateTo($"/file-records/{RecordId}/edit?q=horto%20dente");
        var cut = RenderEdit();
        cut.WaitForState(() => cut.FindAll("form").Count == 1);

        // Act
        cut.Find("#name").Change("Folder Promoção v2");
        cut.Find("#fileNumber").Change("2041");
        cut.Find("form").Submit();

        // Assert
        cut.WaitForAssertion(() => Assert.EndsWith("/file-records?q=horto%20dente", Navigation.Uri));
        Assert.NotNull(sent);
        Assert.Equal("Folder Promoção v2", sent.Name);
        Assert.Equal(2041, sent.FileNumber);
        Assert.Equal("Horto Dente", sent.Client);
        Assert.Equal(12, sent.FlopDiskNumber);
    }

    [Fact]
    public void Save_WithoutSearch_ReturnsToPlainList()
    {
        // Arrange
        _httpHandler.OnPut = _ => new HttpResponseMessage(HttpStatusCode.OK);
        var cut = RenderEdit();
        cut.WaitForState(() => cut.FindAll("form").Count == 1);

        // Act
        cut.Find("form").Submit();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal("http://localhost/file-records", Navigation.Uri));
    }

    [Fact]
    public void SaveRejected_ShowsError_AndStaysOnPage()
    {
        // Arrange
        _httpHandler.OnPut = _ => new HttpResponseMessage(HttpStatusCode.BadRequest);
        var cut = RenderEdit();
        cut.WaitForState(() => cut.FindAll("form").Count == 1);
        var uriBefore = Navigation.Uri;

        // Act
        cut.Find("form").Submit();

        // Assert
        cut.WaitForState(() => cut.FindAll(".alert-error").Count == 1);
        Assert.Contains("Erro ao atualizar o registro", cut.Find(".alert-error").TextContent);
        Assert.Equal(uriBefore, Navigation.Uri);
        Assert.False(cut.Find("button.btn-submit").HasAttribute("disabled"));
    }

    [Fact]
    public void SaveThrows_ShowsServiceUnavailable()
    {
        // Arrange
        _httpHandler.OnPut = _ => throw new HttpRequestException("offline");
        var cut = RenderEdit();
        cut.WaitForState(() => cut.FindAll("form").Count == 1);

        // Act
        cut.Find("form").Submit();

        // Assert
        cut.WaitForState(() => cut.FindAll(".alert-error").Count == 1);
        Assert.Contains("Serviço indisponível", cut.Find(".alert-error").TextContent);
    }

    [Fact]
    public void EmptyName_IsNotSent()
    {
        // Arrange
        var putCalled = false;
        _httpHandler.OnPut = _ =>
        {
            putCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        };
        var cut = RenderEdit();
        cut.WaitForState(() => cut.FindAll("form").Count == 1);

        // Act
        cut.Find("#name").Change("");
        cut.Find("form").Submit();

        // Assert
        Assert.Contains("O nome é obrigatório.", cut.Markup);
        Assert.False(putCalled);
    }

    private IRenderedComponent<FileRecordEdit> RenderEdit() =>
        Render<FileRecordEdit>(parameters => parameters.Add(p => p.Id, RecordId));

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T body) =>
        new(status) { Content = JsonContent.Create(body) };

    private sealed class RoutingHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> OnGet { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        public Func<HttpRequestMessage, HttpResponseMessage> OnPut { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        public string? LastGetPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Put)
                return Task.FromResult(OnPut(request));

            LastGetPath = request.RequestUri?.PathAndQuery;
            return Task.FromResult(OnGet(request));
        }
    }
}
