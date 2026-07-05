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

    internal static readonly HashSet<string> ValidSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "file_type",
        "file_number",
        "client",
        "date",
        "flop_disk_number"
    };

    public FileRecordService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<FileRecordResponse>> GetAllAsync()
    {
        return await _dbContext.FileRecords
            .OrderByDescending(f => f.Date)
            .Take(100)
            .Select(f => ToResponse(f))
            .ToListAsync();
    }

    public async Task<PaginatedResponse<FileRecordResponse>> GetPagedAsync(
        int offset, int limit, string? sortBy = null, string? sortDir = null)
    {
        var totalCount = await _dbContext.FileRecords.CountAsync();

        var query = ApplySort(_dbContext.FileRecords.AsQueryable(), sortBy, sortDir);

        var items = await query
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
            FileNumber = request.FileNumber
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
        entity.Date = DateTime.SpecifyKind(request.Date, DateTimeKind.Utc);
        entity.Client = request.Client.Trim();
        entity.FileNumber = request.FileNumber;

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

    public async Task<PaginatedResponse<FileRecordResponse>> SearchPagedAsync(
        string searchTerm, int offset, int limit, string? sortBy = null, string? sortDir = null)
    {
        var terms = searchTerm.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (terms.Length == 0)
            return new PaginatedResponse<FileRecordResponse>([], 0, false);

        var countSql = BuildSearchCountSql(terms);
        var countParameters = BuildSearchParameters(terms);
        var totalCount = await _dbContext.Database
            .SqlQueryRaw<int>(countSql, countParameters)
            .SingleAsync();

        var pageSql = BuildSearchPagedSql(terms, sortBy, sortDir);
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

    /// <summary>
    /// Applies dynamic ordering to a FileRecord query based on sortBy and sortDir parameters.
    /// Falls back to <c>date DESC</c> when sortBy is null/invalid. Nulls appear LAST for ASC,
    /// FIRST for DESC. Always appends <c>ThenBy(Id)</c> as tiebreaker for stable pagination.
    /// </summary>
    internal static IOrderedQueryable<FileRecord> ApplySort(
        IQueryable<FileRecord> query, string? sortBy, string? sortDir)
    {
        var (field, descending) = ResolveSort(sortBy, sortDir);

        IOrderedQueryable<FileRecord> ordered = (field, descending) switch
        {
            ("name", false) => query.OrderBy(f => f.Name),
            ("name", true) => query.OrderByDescending(f => f.Name),

            ("file_type", false) => query.OrderBy(f => f.FileType),
            ("file_type", true) => query.OrderByDescending(f => f.FileType),

            ("file_number", false) => query.OrderBy(f => f.FileNumber),
            ("file_number", true) => query.OrderByDescending(f => f.FileNumber),

            ("client", false) => query.OrderBy(f => f.Client),
            ("client", true) => query.OrderByDescending(f => f.Client),

            ("date", false) => query.OrderBy(f => f.Date),
            ("date", true) => query.OrderByDescending(f => f.Date),

            ("flop_disk_number", false) => query.OrderBy(f => f.FlopDiskNumber == null).ThenBy(f => f.FlopDiskNumber),
            ("flop_disk_number", true) => query.OrderByDescending(f => f.FlopDiskNumber == null).ThenByDescending(f => f.FlopDiskNumber),

            // Safety net — falls back to date DESC (column is NOT NULL, no null-handling needed)
            _ => query.OrderByDescending(f => f.Date)
        };

        return ordered.ThenBy(f => f.Id);
    }

    /// <summary>
    /// Builds the ORDER BY clause used inside the raw SQL search query.
    /// Only allows whitelisted field names to prevent SQL injection.
    /// </summary>
    internal static string BuildOrderByClause(string? sortBy, string? sortDir)
    {
        var (field, descending) = ResolveSort(sortBy, sortDir);

        var direction = descending ? "DESC" : "ASC";
        var nulls = descending ? "NULLS FIRST" : "NULLS LAST";

        return $"{field} {direction} {nulls}, id";
    }

    /// <summary>
    /// Resolves the effective sort field and direction from user input.
    /// Invalid or missing sortBy forces the Default_Sort (date DESC), ignoring any sortDir
    /// (Requirement 3.2). For a valid field, sortDir defaults to ASC unless the field is
    /// <c>date</c>, which defaults to DESC (preserves legacy behavior).
    /// </summary>
    private static (string Field, bool Descending) ResolveSort(string? sortBy, string? sortDir)
    {
        var isValidField = !string.IsNullOrWhiteSpace(sortBy) && ValidSortFields.Contains(sortBy);

        if (!isValidField)
        {
            // Invalid/missing sortBy → force Default_Sort (date DESC), ignore sortDir
            return ("date", true);
        }

        var field = sortBy!.ToLowerInvariant();
        var descending = ResolveDirection(field, sortDir);
        return (field, descending);
    }

    /// <summary>
    /// Resolves the effective sort direction for a valid field.
    /// When sortDir is null/invalid, defaults to ASC except for <c>date</c> which defaults to DESC.
    /// </summary>
    private static bool ResolveDirection(string field, string? sortDir)
    {
        if (string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase))
            return false;

        // Null/invalid sortDir → date defaults to DESC, others to ASC
        return field == "date";
    }

    private static string BuildSearchSql(string[] terms)
    {
        var whereClause = BuildSearchWhereClause(terms);
        return $"SELECT * FROM file_records WHERE {whereClause} ORDER BY date DESC NULLS LAST, id LIMIT 100";
    }

    private static string BuildSearchPagedSql(string[] terms, string? sortBy, string? sortDir)
    {
        var whereClause = BuildSearchWhereClause(terms);
        var orderBy = BuildOrderByClause(sortBy, sortDir);
        return $"SELECT * FROM file_records WHERE {whereClause} ORDER BY {orderBy} OFFSET @offset LIMIT @limit";
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
}
