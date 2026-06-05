namespace Api.Services;

using System.Globalization;
using System.Text;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

    public async Task<PaginatedResponse<FileRecordResponse>> GetPagedAsync(int offset, int limit)
    {
        var totalCount = await _dbContext.FileRecords.CountAsync();

        var items = await _dbContext.FileRecords
            .OrderByDescending(f => f.Date ?? DateTime.MinValue)
            .Skip(offset)
            .Take(limit)
            .Select(f => ToResponse(f))
            .ToListAsync();

        return new PaginatedResponse<FileRecordResponse>(items, totalCount, offset + items.Count < totalCount);
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

    public async Task<List<FileRecordResponse>> SearchAsync(string searchTerm)
    {
        var terms = searchTerm.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (terms.Length == 0)
            return [];

        var sql = BuildSearchSql(terms);
        var parameters = BuildSearchParameters(terms);

        var records = await _dbContext.FileRecords
            .FromSqlRaw(sql, parameters)
            .ToListAsync();

        return records.Select(ToResponse).ToList();
    }

    public async Task<PaginatedResponse<FileRecordResponse>> SearchPagedAsync(string searchTerm, int offset, int limit)
    {
        var terms = searchTerm.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (terms.Length == 0)
            return new PaginatedResponse<FileRecordResponse>([], 0, false);

        var countSql = BuildSearchCountSql(terms);
        var countParameters = BuildSearchParameters(terms);
        var totalCount = await _dbContext.Database
            .SqlQueryRaw<int>(countSql, countParameters)
            .SingleAsync();

        var pageSql = BuildSearchPagedSql(terms);
        var pageParameters = BuildSearchParameters(terms)
            .Concat([
                new NpgsqlParameter("@offset", offset),
                new NpgsqlParameter("@limit", limit)
            ])
            .ToArray();

        var records = await _dbContext.FileRecords
            .FromSqlRaw(pageSql, pageParameters)
            .ToListAsync();

        var items = records.Select(ToResponse).ToList();

        return new PaginatedResponse<FileRecordResponse>(items, totalCount, offset + items.Count < totalCount);
    }

    private static string BuildSearchSql(string[] terms)
    {
        var whereClause = BuildSearchWhereClause(terms);
        return $"SELECT * FROM file_records WHERE {whereClause} ORDER BY date DESC NULLS LAST, id LIMIT 100";
    }

    private static string BuildSearchPagedSql(string[] terms)
    {
        var whereClause = BuildSearchWhereClause(terms);
        return $"SELECT * FROM file_records WHERE {whereClause} ORDER BY date DESC NULLS LAST, id OFFSET @offset LIMIT @limit";
    }

    private static string BuildSearchCountSql(string[] terms)
    {
        var whereClause = BuildSearchWhereClause(terms);
        return $"SELECT COUNT(*)::int AS \"Value\" FROM file_records WHERE {whereClause}";
    }

    private static string BuildSearchWhereClause(string[] terms)
    {
        var conditions = new StringBuilder();
        for (int i = 0; i < terms.Length; i++)
        {
            if (i > 0) conditions.Append(" AND ");
            conditions.Append($"(unaccent(lower(name)) ILIKE unaccent(lower(@p{i})) OR unaccent(lower(client)) ILIKE unaccent(lower(@p{i})))");
        }

        return conditions.ToString();
    }

    private static object[] BuildSearchParameters(string[] terms)
    {
        var parameters = new object[terms.Length];
        for (int i = 0; i < terms.Length; i++)
        {
            parameters[i] = new NpgsqlParameter($"@p{i}", $"%{terms[i]}%");
        }
        return parameters;
    }

    internal static int ComputeScore(FileRecord record, string[] terms)
    {
        var normalizedName = RemoveDiacritics(record.Name).ToLowerInvariant();
        var normalizedClient = RemoveDiacritics(record.Client).ToLowerInvariant();

        int score = 0;
        bool allTermsMatchName = true;

        foreach (var rawTerm in terms)
        {
            var term = RemoveDiacritics(rawTerm).ToLowerInvariant();
            bool nameMatch = normalizedName.Contains(term);
            bool clientMatch = normalizedClient.Contains(term);

            if (!nameMatch && !clientMatch) return 0;

            if (nameMatch) score += 10;
            if (clientMatch) score += 5;

            if (!nameMatch) allTermsMatchName = false;
        }

        if (allTermsMatchName) score += 5;

        return score;
    }

    internal static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static FileRecordResponse ToResponse(FileRecord entity) =>
        new(entity.Id, entity.Name, entity.FileType, entity.FlopDiskNumber, entity.Date, entity.Client, entity.FileNumber);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
