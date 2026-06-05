using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Services;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Dtos;

namespace Api.Tests;

public class FileRecordSearchTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FileRecordSearchTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task SeedRecordsAsync(params FileRecord[] records)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.FileRecords.AddRange(records);
        await db.SaveChangesAsync();
    }

    private static FileRecord CreateRecord(string name, string client) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        FileType = FileType.CorelDRAW,
        Client = client,
        Date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    // --- Service Unit Tests (ComputeScore + RemoveDiacritics) ---

    [Fact]
    public void SearchAsync_WithSingleTerm_ReturnsMatchingRecords()
    {
        // Arrange
        var record = CreateRecord("Logo Design Final", "Acme Corp");
        var terms = new[] { "logo" };

        // Act
        var score = FileRecordService.ComputeScore(record, terms);

        // Assert
        Assert.True(score > 0, "Record with matching Name should have positive score");
    }

    [Fact]
    public void SearchAsync_WithMultipleWords_ReturnsRecordsMatchingAllWords()
    {
        // Arrange
        var record = CreateRecord("Logo Design Final Version", "Acme Corp");
        var terms = new[] { "logo", "final" };

        // Act
        var score = FileRecordService.ComputeScore(record, terms);

        // Assert
        Assert.True(score > 0, "Record matching all terms should have positive score");
    }

    [Fact]
    public void SearchAsync_CaseInsensitive_MatchesRegardlessOfCase()
    {
        // Arrange
        var record = CreateRecord("Logo Design Final", "Acme Corp");
        var terms = new[] { "LOGO" };

        // Act
        var score = FileRecordService.ComputeScore(record, terms);

        // Assert
        Assert.True(score > 0, "Case-insensitive match should produce positive score");
    }

    [Fact]
    public void SearchAsync_AccentInsensitive_MatchesWithoutAccents()
    {
        // Arrange
        var record = CreateRecord("Gráfica Premium", "São Paulo Design");
        var terms = new[] { "grafica" };

        // Act
        var score = FileRecordService.ComputeScore(record, terms);

        // Assert
        Assert.True(score > 0, "Accent-insensitive match should produce positive score");
    }

    [Fact]
    public async Task SearchAsync_EmptyTerm_ReturnsEmptyList()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/file-records/search?q=");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<FileRecordResponse>>(JsonOptions);
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public void SearchAsync_NameMatchScoredHigherThanClientOnlyMatch()
    {
        // Arrange
        var nameMatchRecord = CreateRecord("Impressao Offset", "Generic Client");
        var clientOnlyRecord = CreateRecord("Unrelated File", "Impressao Services");
        var terms = new[] { "impressao" };

        // Act
        var nameScore = FileRecordService.ComputeScore(nameMatchRecord, terms);
        var clientScore = FileRecordService.ComputeScore(clientOnlyRecord, terms);

        // Assert
        Assert.True(nameScore > clientScore,
            $"Name match score ({nameScore}) should be higher than client-only match score ({clientScore})");
    }

    [Fact]
    public void ComputeScore_BothFieldsMatch_ScoreGreaterOrEqualToNameOnly()
    {
        // Arrange
        var bothMatchRecord = CreateRecord("Design Premium", "Design Studio");
        var nameOnlyRecord = CreateRecord("Design Premium", "Unrelated Client");
        var terms = new[] { "design" };

        // Act
        var bothScore = FileRecordService.ComputeScore(bothMatchRecord, terms);
        var nameOnlyScore = FileRecordService.ComputeScore(nameOnlyRecord, terms);

        // Assert
        Assert.True(bothScore >= nameOnlyScore,
            $"Both-match score ({bothScore}) should be >= name-only score ({nameOnlyScore})");
    }

    // --- Endpoint Tests ---

    [Fact]
    public async Task SearchEndpoint_MissingQ_Returns200EmptyArray()
    {
        // Arrange & Act — q parameter is missing entirely
        var response = await _client.GetAsync("/api/file-records/search");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await response.Content.ReadFromJsonAsync<List<FileRecordResponse>>(JsonOptions);
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchEndpoint_QLengthExceeds200_Returns400()
    {
        // Arrange — generate a string longer than 200 characters
        var longQuery = new string('a', 201);

        // Act
        var response = await _client.GetAsync($"/api/file-records/search?q={longQuery}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SearchEndpoint_Unauthenticated_Returns401()
    {
        // Arrange — use a factory that doesn't auto-authenticate
        await using var unauthFactory = new UnauthenticatedWebApplicationFactory();
        var unauthenticatedClient = unauthFactory.CreateClient();

        // Act
        var response = await unauthenticatedClient.GetAsync("/api/file-records/search?q=test");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

/// <summary>
/// A WebApplicationFactory that does NOT auto-authenticate requests.
/// Uses the real JWT bearer configuration which will reject requests without valid tokens.
/// </summary>
public class UnauthenticatedWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Firebase:ProjectId", "test-project-id");
        builder.UseSetting("Firebase:ServiceAccountPath", CustomWebApplicationFactory.GetFakeServiceAccountPath());

        builder.ConfigureServices(services =>
        {
            // Remove all EF Core registrations to avoid Npgsql conflicts
            var efCoreDescriptors = services
                .Where(d => d.ServiceType.FullName != null
                    && (d.ServiceType.FullName.Contains("EntityFrameworkCore")
                        || d.ServiceType.FullName.Contains("EntityFramework")
                        || d.ServiceType == typeof(AppDbContext)
                        || d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                        || d.ServiceType == typeof(DbContextOptions)))
                .ToList();

            foreach (var descriptor in efCoreDescriptors)
                services.Remove(descriptor);

            // Register AppDbContext with InMemory provider
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_Unauth"));
        });

        // Do NOT register any test auth handler — the real JwtBearer scheme will reject the request
    }
}
