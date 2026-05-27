using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public class DiagnosticTest : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DiagnosticTest(WebApplicationFactory<Program> factory)
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

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase("DiagDb"));
            });
        });
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
