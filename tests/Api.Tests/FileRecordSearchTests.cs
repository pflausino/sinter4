using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Dtos;

namespace Api.Tests;

public class FileRecordSearchTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FileRecordSearchTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // --- Endpoint Tests ---

    [Fact]
    public async Task SearchEndpoint_EmptyQ_Returns200EmptyArray()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/file-records/search?q=");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>(JsonOptions);
        Assert.NotNull(paginated);
        Assert.Empty(paginated.Items);
    }

    [Fact]
    public async Task SearchEndpoint_MissingQ_Returns200EmptyArray()
    {
        // Arrange & Act — q parameter is missing entirely
        var response = await _client.GetAsync("/api/file-records/search");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>(JsonOptions);
        Assert.NotNull(paginated);
        Assert.Empty(paginated.Items);
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
