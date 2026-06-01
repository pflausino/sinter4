namespace Api.Services;

using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Dtos;

public class FileRecordService : IFileRecordService
{
    private readonly AppDbContext _dbContext;

    public FileRecordService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<FileRecordResponse>> GetAllAsync()
    {
        return await _dbContext.FileRecords
            .OrderByDescending(f => f.Date ?? DateTime.MinValue)
            .Take(100)
            .Select(f => ToResponse(f))
            .ToListAsync();
    }

    public async Task<FileRecordResponse?> GetByIdAsync(Guid id)
    {
        var entity = await _dbContext.FileRecords.FindAsync(id);
        return entity is null ? null : ToResponse(entity);
    }

    public async Task<FileRecordResponse> CreateAsync(CreateFileRecordRequest request)
    {
        var entity = new FileRecord
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            FileType = request.FileType,
            FlopDiskNumber = request.FlopDiskNumber,
            Date = DateTime.SpecifyKind(request.Date, DateTimeKind.Utc),
            Client = request.Client.Trim(),
            FileNumber = NormalizeOptional(request.FileNumber)
        };

        _dbContext.FileRecords.Add(entity);
        await _dbContext.SaveChangesAsync();

        return ToResponse(entity);
    }

    public async Task<FileRecordResponse?> UpdateAsync(Guid id, UpdateFileRecordRequest request)
    {
        var entity = await _dbContext.FileRecords.FindAsync(id);
        if (entity is null) return null;

        entity.Name = request.Name.Trim();
        entity.FileType = request.FileType;
        entity.FlopDiskNumber = request.FlopDiskNumber;
        entity.Date = request.Date.HasValue
            ? DateTime.SpecifyKind(request.Date.Value, DateTimeKind.Utc)
            : null;
        entity.Client = request.Client.Trim();
        entity.FileNumber = NormalizeOptional(request.FileNumber);

        await _dbContext.SaveChangesAsync();

        return ToResponse(entity);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _dbContext.FileRecords.FindAsync(id);
        if (entity is null) return false;

        _dbContext.FileRecords.Remove(entity);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    private static FileRecordResponse ToResponse(FileRecord entity) =>
        new(entity.Id, entity.Name, entity.FileType, entity.FlopDiskNumber, entity.Date, entity.Client, entity.FileNumber);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
