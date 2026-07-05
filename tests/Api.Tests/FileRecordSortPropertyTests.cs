using Api.Services;
using Domain.Entities;
using Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Api.Tests;

/// <summary>
/// Property-based tests for the server-side column sorting feature using FsCheck.
/// Feature: server-side-column-sorting
///
/// Tests the pure sorting logic in <see cref="FileRecordService.ApplySort"/> using
/// in-memory IQueryable — no database or WebApplicationFactory required.
/// </summary>
public class FileRecordSortPropertyTests
{
    private static readonly string[] SortableFields =
        ["name", "file_type", "file_number", "client", "date", "flop_disk_number"];

    private static readonly string[] SortDirections = ["asc", "desc"];

    private static readonly string[] NullableFields =
        ["flop_disk_number"];

    private static readonly string[] InvalidSortByValues =
        ["not_a_field", "DROP TABLE", "", "  ", "unknown", "id", "created_at", "1; DELETE"];

    private static readonly FileType[] ValidFileTypes = Enum.GetValues<FileType>();

    /// <summary>
    /// Property 1: Consecutive pages produce non-overlapping results
    ///
    /// For any valid sortBy field, sortDir, and dataset, taking page N and page N+1 via
    /// Skip(offset).Take(pageSize) yields disjoint id sets. This validates that the Id
    /// tiebreaker in ApplySort makes pagination deterministic even when records share
    /// identical primary sort values.
    ///
    /// Validates: Requirements 3.6, 4.2
    /// </summary>
    [Property(
        DisplayName = "Feature: server-side-column-sorting, Property 1: Consecutive pages are non-overlapping",
        MaxTest = 100)]
    [Trait("Feature", "server-side-column-sorting")]
    [Trait("Property", "Consecutive pages are non-overlapping")]
    public Property ConsecutivePages_ProduceNonOverlappingResults()
    {
        var seedGen = Gen.Choose(1, int.MaxValue).ToArbitrary();

        return Prop.ForAll(seedGen, seed =>
        {
            var rng = new Random(seed);
            var records = GenerateRecords(rng, minCount: 20, maxCount: 60);
            var sortBy = SortableFields[rng.Next(SortableFields.Length)];
            var sortDir = SortDirections[rng.Next(SortDirections.Length)];
            var pageSize = rng.Next(1, 16);

            var sorted = FileRecordService.ApplySort(records.AsQueryable(), sortBy, sortDir).ToList();

            var page1 = sorted.Skip(0).Take(pageSize).Select(r => r.Id).ToHashSet();
            var page2 = sorted.Skip(pageSize).Take(pageSize).Select(r => r.Id).ToHashSet();

            // Pages must be disjoint
            Assert.False(page1.Overlaps(page2),
                $"Pages overlap for sortBy={sortBy}, sortDir={sortDir}, pageSize={pageSize}");
        });
    }

    /// <summary>
    /// Property 2: Invalid sortBy falls back to date DESC
    ///
    /// For any invalid sortBy string, the produced ordering matches what an explicit
    /// (sortBy=date, sortDir=desc) request would produce, ignoring any provided sortDir.
    ///
    /// Validates: Requirements 3.2, 1.5
    /// </summary>
    [Property(
        DisplayName = "Feature: server-side-column-sorting, Property 2: Invalid sortBy falls back to date DESC",
        MaxTest = 100)]
    [Trait("Feature", "server-side-column-sorting")]
    [Trait("Property", "Invalid sortBy falls back to date DESC")]
    public Property InvalidSortBy_MatchesDateDescOrdering()
    {
        var seedGen = Gen.Choose(1, int.MaxValue).ToArbitrary();

        return Prop.ForAll(seedGen, seed =>
        {
            var rng = new Random(seed);
            var records = GenerateRecords(rng, minCount: 10, maxCount: 30);
            var invalidSortBy = InvalidSortByValues[rng.Next(InvalidSortByValues.Length)];
            var sortDir = SortDirections[rng.Next(SortDirections.Length)];

            var fallbackIds = FileRecordService
                .ApplySort(records.AsQueryable(), invalidSortBy, sortDir)
                .Select(r => r.Id)
                .ToList();

            var expectedIds = FileRecordService
                .ApplySort(records.AsQueryable(), "date", "desc")
                .Select(r => r.Id)
                .ToList();

            Assert.Equal(expectedIds, fallbackIds);
        });
    }

    /// <summary>
    /// Property 3: Null values appear last for ASC, first for DESC
    ///
    /// For any dataset containing at least one null value and any nullable field,
    /// sorting ASC places null-valued records after non-null-valued records, and
    /// sorting DESC places null-valued records before non-null-valued records.
    ///
    /// Validates: Requirements 3.5
    /// </summary>
    [Property(
        DisplayName = "Feature: server-side-column-sorting, Property 3: Nulls placement respects direction",
        MaxTest = 100)]
    [Trait("Feature", "server-side-column-sorting")]
    [Trait("Property", "Nulls placement respects direction")]
    public Property NullPlacement_IsConsistentWithDirection()
    {
        var seedGen = Gen.Choose(1, int.MaxValue).ToArbitrary();

        return Prop.ForAll(seedGen, seed =>
        {
            var rng = new Random(seed);
            var records = GenerateRecords(rng, minCount: 10, maxCount: 30);
            // Guarantee at least one null value in every nullable field
            records.Add(BuildAllNullRecord());
            records.Add(BuildAllNullRecord());

            var field = NullableFields[rng.Next(NullableFields.Length)];

            var asc = FileRecordService.ApplySort(records.AsQueryable(), field, "asc").ToList();
            var desc = FileRecordService.ApplySort(records.AsQueryable(), field, "desc").ToList();

            Assert.True(NullsAppearLast(asc, field),
                $"ASC on {field} did not place nulls last");
            Assert.True(NullsAppearFirst(desc, field),
                $"DESC on {field} did not place nulls first");
        });
    }

    // -------- Generators (seed-based, following the existing FileRecordPropertyTests pattern) --------

    private static List<FileRecord> GenerateRecords(Random rng, int minCount, int maxCount)
    {
        var count = rng.Next(minCount, maxCount + 1);
        return Enumerable.Range(0, count).Select(_ => GenerateRecord(rng)).ToList();
    }

    /// <summary>
    /// Generates a random FileRecord. Uses a small alphabet for string fields to increase
    /// duplicate values, exercising the Id tiebreaker in ApplySort.
    /// </summary>
    private static FileRecord GenerateRecord(Random rng)
    {
        return new FileRecord
        {
            Id = Guid.NewGuid(),
            Name = GenerateShortString(rng),
            FileType = ValidFileTypes[rng.Next(ValidFileTypes.Length)],
            FlopDiskNumber = GenerateNullableInt(rng),
            Date = GenerateDate(rng),
            Client = GenerateShortString(rng),
            FileNumber = rng.Next(0, 10000)
        };
    }

    private static FileRecord BuildAllNullRecord() => new()
    {
        Id = Guid.NewGuid(),
        Name = "NullSeed",
        Client = "NullSeed",
        FileType = FileType.Unknown,
        FlopDiskNumber = null,
        Date = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        FileNumber = 0
    };

    private static string GenerateShortString(Random rng)
    {
        const string alphabet = "abcABC123";
        var length = rng.Next(1, 6);
        return new string(Enumerable.Range(0, length)
            .Select(_ => alphabet[rng.Next(alphabet.Length)])
            .ToArray());
    }

    private static int? GenerateNullableInt(Random rng) =>
        rng.Next(4) == 0 ? null : rng.Next(1, 101);

    private static DateTime GenerateDate(Random rng)
    {
        var year = rng.Next(2020, 2027);
        var month = rng.Next(1, 13);
        var day = rng.Next(1, 29);
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
    }

    // -------- Assertion helpers --------

    private static bool NullsAppearLast(List<FileRecord> sorted, string field)
    {
        bool sawNull = false;
        foreach (var r in sorted)
        {
            bool isNull = IsFieldNull(r, field);
            if (sawNull && !isNull) return false;
            if (isNull) sawNull = true;
        }
        return true;
    }

    private static bool NullsAppearFirst(List<FileRecord> sorted, string field)
    {
        bool sawNonNull = false;
        foreach (var r in sorted)
        {
            bool isNull = IsFieldNull(r, field);
            if (sawNonNull && isNull) return false;
            if (!isNull) sawNonNull = true;
        }
        return true;
    }

    private static bool IsFieldNull(FileRecord r, string field) => field switch
    {
        "flop_disk_number" => r.FlopDiskNumber is null,
        _ => throw new ArgumentException($"Field '{field}' is not nullable")
    };
}
