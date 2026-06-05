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

        group.MapGet("/", async (int? offset, int? limit, IFileRecordService service) =>
        {
            var pageOffset = Math.Max(offset ?? 0, 0);
            var pageLimit = Math.Clamp(limit ?? 50, 1, 100);
            var result = await service.GetPagedAsync(pageOffset, pageLimit);
            return Results.Ok(result);
        });

        group.MapGet("/search", async (string? q, int? offset, int? limit, IFileRecordService service, ILogger<Program> logger) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.Ok(new PaginatedResponse<FileRecordResponse>([], 0, false));

            if (q.Length > 200)
                return Results.BadRequest(new { error = "Search term must not exceed 200 characters" });

            try
            {
                var pageOffset = Math.Max(offset ?? 0, 0);
                var pageLimit = Math.Clamp(limit ?? 50, 1, 100);
                var results = await service.SearchPagedAsync(q, pageOffset, pageLimit);
                return Results.Ok(results);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database error while searching file records");
                return Results.StatusCode(500);
            }
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
