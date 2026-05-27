using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Dtos;

namespace Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", async (AppDbContext dbContext) =>
        {
            string status;

            try
            {
                var canConnect = await dbContext.Database.CanConnectAsync();
                status = canConnect ? "Healthy" : "Unhealthy";
            }
            catch
            {
                status = "Unhealthy";
            }

            return Results.Ok(new HealthCheckResponse(status, DateTime.UtcNow));
        });
    }
}
