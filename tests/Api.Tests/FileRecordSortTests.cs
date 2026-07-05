using Api.Services;
using Domain.Entities;
using Domain.Enums;

namespace Api.Tests;

/// <summary>
/// Unit tests for the sorting logic in <see cref="FileRecordService"/>.
/// Tests <c>ApplySort</c> against in-memory <c>IQueryable</c> and
/// <c>BuildOrderByClause</c> as pure string builders.
/// </summary>
public class FileRecordSortTests
{
    private static List<FileRecord> BuildSampleRecords() =>
    [
        new FileRecord
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Alpha",
            FileType = FileType.CorelDRAW,
            FlopDiskNumber = 3,
            Date = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Client = "Charlie",
            FileNumber = 1001
        },
        new FileRecord
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Name = "Bravo",
            FileType = FileType.Photoshop,
            FlopDiskNumber = 1,
            Date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Client = "Alfa",
            FileNumber = 0
        },
        new FileRecord
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Name = "Charlie",
            FileType = FileType.Illustrator,
            FlopDiskNumber = null,
            Date = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Client = "Bravo",
            FileNumber = 1002
        }
    ];

    // -------- Name --------

    [Fact]
    public void ApplySort_ByNameAsc_ReturnsAlphabeticalOrder()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "name", "asc").ToList();

        Assert.Collection(results,
            r => Assert.Equal("Alpha", r.Name),
            r => Assert.Equal("Bravo", r.Name),
            r => Assert.Equal("Charlie", r.Name));
    }

    [Fact]
    public void ApplySort_ByNameDesc_ReturnsReverseAlphabeticalOrder()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "name", "desc").ToList();

        Assert.Collection(results,
            r => Assert.Equal("Charlie", r.Name),
            r => Assert.Equal("Bravo", r.Name),
            r => Assert.Equal("Alpha", r.Name));
    }

    // -------- File type (enum) --------

    [Fact]
    public void ApplySort_ByFileTypeAsc_ReturnsAscendingByEnumValue()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "file_type", "asc").ToList();

        // CorelDRAW=0, Photoshop=1, Illustrator=2
        Assert.Equal(FileType.CorelDRAW, results[0].FileType);
        Assert.Equal(FileType.Photoshop, results[1].FileType);
        Assert.Equal(FileType.Illustrator, results[2].FileType);
    }

    [Fact]
    public void ApplySort_ByFileTypeDesc_ReturnsDescendingByEnumValue()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "file_type", "desc").ToList();

        Assert.Equal(FileType.Illustrator, results[0].FileType);
        Assert.Equal(FileType.Photoshop, results[1].FileType);
        Assert.Equal(FileType.CorelDRAW, results[2].FileType);
    }

    // -------- Client --------

    [Fact]
    public void ApplySort_ByClientAsc_ReturnsAlphabeticalOrder()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "client", "asc").ToList();

        Assert.Collection(results,
            r => Assert.Equal("Alfa", r.Client),
            r => Assert.Equal("Bravo", r.Client),
            r => Assert.Equal("Charlie", r.Client));
    }

    [Fact]
    public void ApplySort_ByClientDesc_ReturnsReverseAlphabeticalOrder()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "client", "desc").ToList();

        Assert.Collection(results,
            r => Assert.Equal("Charlie", r.Client),
            r => Assert.Equal("Bravo", r.Client),
            r => Assert.Equal("Alfa", r.Client));
    }

    // -------- Date (non-nullable; sentinel 1900-01-01 acts as "oldest" for backfilled rows) --------

    [Fact]
    public void ApplySort_ByDateAsc_ReturnsOldestFirst()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "date", "asc").ToList();

        // 1900-01-01 (sentinel) < 2024-01-01 < 2024-03-01
        Assert.Equal(new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), results[0].Date);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), results[1].Date);
        Assert.Equal(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), results[2].Date);
    }

    [Fact]
    public void ApplySort_ByDateDesc_ReturnsNewestFirst()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "date", "desc").ToList();

        Assert.Equal(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), results[0].Date);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), results[1].Date);
        Assert.Equal(new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), results[2].Date);
    }

    // -------- File number (non-nullable int; sentinel 0 for backfilled rows) --------

    [Fact]
    public void ApplySort_ByFileNumberAsc_ReturnsAscendingNumeric()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "file_number", "asc").ToList();

        // 0 (sentinel) < 1001 < 1002
        Assert.Equal(0, results[0].FileNumber);
        Assert.Equal(1001, results[1].FileNumber);
        Assert.Equal(1002, results[2].FileNumber);
    }

    [Fact]
    public void ApplySort_ByFileNumberDesc_ReturnsDescendingNumeric()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "file_number", "desc").ToList();

        Assert.Equal(1002, results[0].FileNumber);
        Assert.Equal(1001, results[1].FileNumber);
        Assert.Equal(0, results[2].FileNumber);
    }

    // -------- Flop disk number (nullable int) --------

    [Fact]
    public void ApplySort_ByFlopDiskNumberAsc_PlacesNullsLast()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "flop_disk_number", "asc").ToList();

        Assert.Equal(1, results[0].FlopDiskNumber);
        Assert.Equal(3, results[1].FlopDiskNumber);
        Assert.Null(results[2].FlopDiskNumber);
    }

    [Fact]
    public void ApplySort_ByFlopDiskNumberDesc_PlacesNullsFirst()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "flop_disk_number", "desc").ToList();

        Assert.Null(results[0].FlopDiskNumber);
        Assert.Equal(3, results[1].FlopDiskNumber);
        Assert.Equal(1, results[2].FlopDiskNumber);
    }

    // -------- Fallback behavior --------

    [Fact]
    public void ApplySort_InvalidSortBy_FallsBackToDateDesc()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "not_a_field", "asc").ToList();

        // Falls back to date DESC — newest first
        Assert.Equal(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), results[0].Date);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), results[1].Date);
        Assert.Equal(new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), results[2].Date);
    }

    [Fact]
    public void ApplySort_NullSortBy_FallsBackToDateDesc()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), null, null).ToList();

        Assert.Equal(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), results[0].Date);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), results[1].Date);
        Assert.Equal(new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc), results[2].Date);
    }

    [Fact]
    public void ApplySort_NullSortDir_NonDateField_DefaultsToAsc()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "name", null).ToList();

        Assert.Collection(results,
            r => Assert.Equal("Alpha", r.Name),
            r => Assert.Equal("Bravo", r.Name),
            r => Assert.Equal("Charlie", r.Name));
    }

    [Fact]
    public void ApplySort_NullSortDir_DateField_DefaultsToDesc()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "date", null).ToList();

        // date + null direction → DESC → newest first
        Assert.Equal(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc), results[0].Date);
    }

    [Fact]
    public void ApplySort_InvalidSortDir_FallsBackToDefault()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "name", "sideways").ToList();

        // Invalid direction → default is ASC for non-date fields
        Assert.Collection(results,
            r => Assert.Equal("Alpha", r.Name),
            r => Assert.Equal("Bravo", r.Name),
            r => Assert.Equal("Charlie", r.Name));
    }

    [Fact]
    public void ApplySort_SortByIsCaseInsensitive()
    {
        var results = FileRecordService.ApplySort(BuildSampleRecords().AsQueryable(), "NAME", "asc").ToList();

        Assert.Collection(results,
            r => Assert.Equal("Alpha", r.Name),
            r => Assert.Equal("Bravo", r.Name),
            r => Assert.Equal("Charlie", r.Name));
    }

    // -------- Tiebreaker --------

    [Fact]
    public void ApplySort_WithDuplicateSortValues_UsesIdAsTiebreaker()
    {
        // Two records with identical Name — differ only by Id
        var idA = new Guid("11111111-1111-1111-1111-111111111111");
        var idB = new Guid("22222222-2222-2222-2222-222222222222");

        var records = new List<FileRecord>
        {
            new() { Id = idB, Name = "Same", Client = "X", FileType = FileType.PDF },
            new() { Id = idA, Name = "Same", Client = "X", FileType = FileType.PDF }
        }.AsQueryable();

        var asc = FileRecordService.ApplySort(records, "name", "asc").ToList();

        // With duplicates on Name, ThenBy(Id) should place idA (11...) before idB (22...)
        Assert.Equal(idA, asc[0].Id);
        Assert.Equal(idB, asc[1].Id);
    }

    // -------- BuildOrderByClause --------

    [Theory]
    [InlineData("name", "asc", "name ASC NULLS LAST, id")]
    [InlineData("name", "desc", "name DESC NULLS FIRST, id")]
    [InlineData("client", "asc", "client ASC NULLS LAST, id")]
    [InlineData("client", "desc", "client DESC NULLS FIRST, id")]
    [InlineData("file_type", "asc", "file_type ASC NULLS LAST, id")]
    [InlineData("file_type", "desc", "file_type DESC NULLS FIRST, id")]
    [InlineData("file_number", "asc", "file_number ASC NULLS LAST, id")]
    [InlineData("file_number", "desc", "file_number DESC NULLS FIRST, id")]
    [InlineData("date", "asc", "date ASC NULLS LAST, id")]
    [InlineData("date", "desc", "date DESC NULLS FIRST, id")]
    [InlineData("flop_disk_number", "asc", "flop_disk_number ASC NULLS LAST, id")]
    [InlineData("flop_disk_number", "desc", "flop_disk_number DESC NULLS FIRST, id")]
    public void BuildOrderByClause_ValidCombinations_ProducesExpectedSql(
        string sortBy, string sortDir, string expected)
    {
        var clause = FileRecordService.BuildOrderByClause(sortBy, sortDir);
        Assert.Equal(expected, clause);
    }

    [Fact]
    public void BuildOrderByClause_InvalidSortBy_FallsBackToDateDesc()
    {
        var clause = FileRecordService.BuildOrderByClause("drop_table", "asc");
        Assert.Equal("date DESC NULLS FIRST, id", clause);
    }

    [Fact]
    public void BuildOrderByClause_NullSortBy_FallsBackToDateDesc()
    {
        var clause = FileRecordService.BuildOrderByClause(null, null);
        Assert.Equal("date DESC NULLS FIRST, id", clause);
    }

    [Fact]
    public void BuildOrderByClause_NullSortDir_NonDateField_DefaultsToAsc()
    {
        var clause = FileRecordService.BuildOrderByClause("name", null);
        Assert.Equal("name ASC NULLS LAST, id", clause);
    }
}
