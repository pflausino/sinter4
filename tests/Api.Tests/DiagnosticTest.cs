using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public class DiagnosticTest : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DiagnosticTest(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DiagnoseCanConnect()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var providerName = db.Database.ProviderName;
        var canConnect = await db.Database.CanConnectAsync();

        Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", providerName);
        Assert.True(canConnect);
    }
}
