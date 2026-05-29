using System.Net;
using System.Net.Http.Json;
using Shared.Dtos;

namespace Api.Tests;

public class HealthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public HealthEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
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
