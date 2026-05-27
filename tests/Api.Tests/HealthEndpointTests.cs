using System.Net;
using System.Net.Http.Json;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Shared.Dtos;

namespace Api.Tests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove TODOS os registros do EF Core para evitar conflito de providers
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

                // Registra AppDbContext com InMemory provider (sem conflito com Npgsql)
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("HealthTestDb"));
            });
        });
    }

    [Fact]
    public async Task GetHealth_QuandoChamado_RetornaStatusCode200()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth_QuandoChamado_RetornaJsonComCamposStatusETimestamp()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var content = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();

        // Assert
        Assert.NotNull(content);
        Assert.False(string.IsNullOrWhiteSpace(content.Status));
        Assert.NotEqual(default, content.Timestamp);
    }

    [Fact]
    public async Task GetHealth_QuandoBancoInMemoryDisponivel_RetornaStatusHealthy()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var content = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();

        // Assert
        Assert.NotNull(content);
        Assert.Equal("Healthy", content.Status);
    }
}
