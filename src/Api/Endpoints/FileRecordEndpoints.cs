namespace Api.Endpoints;

using Api.Filters;
using Api.Services;
using Shared.Dtos;

public static class FileRecordEndpoints
{
    public static void MapFileRecordEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/file-records")
            .RequireAuthorization("Authenticated");

        group.MapGet("/", async (IFileRecordService service) =>
        {
            var records = await service.GetAllAsync();
            return Results.Ok(records);
        });

        group.MapGet("/{id:guid}", async (Guid id, IFileRecordService service) =>
        {
            var record = await service.GetByIdAsync(id);
            return record is null ? Results.NotFound() : Results.Ok(record);
        });

        group.MapPost("/", async (CreateFileRecordRequest request, IFileRecordService service) =>
        {
            var created = await service.CreateAsync(request);
            return Results.Created($"/api/file-records/{created.Id}", created);
        }).AddEndpointFilter<ValidationFilter<CreateFileRecordRequest>>();

        group.MapPut("/{id:guid}", async (Guid id, UpdateFileRecordRequest request, IFileRecordService service) =>
        {
            var updated = await service.UpdateAsync(id, request);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).AddEndpointFilter<ValidationFilter<UpdateFileRecordRequest>>();

        group.MapDelete("/{id:guid}", async (Guid id, IFileRecordService service) =>
        {
            var deleted = await service.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }
}
