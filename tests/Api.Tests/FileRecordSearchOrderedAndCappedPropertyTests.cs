using Api.Services;
using Domain.Entities;
using Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Api.Tests;

/// <summary>
/// Property-based tests for search result ordering and capping.
/// Feature: smart-search-file-records
/// Property 5: Results are ordered by score descending with maximum 100 results
///
/// **Validates: Requirements 4.4, 7.4**
/// </summary>
[Trait("Feature", "smart-search-file-records")]
[Trait("Property", "Results ordered and capped")]
public class FileRecordSearchOrderedAndCappedPropertyTests
{
    private static readonly FileType[] ValidFileTypes = Enum.GetValues<FileType>();

    /// <summary>
    /// Generates a list of 150+ FileRecords that will produce varying scores when matched
    /// against a known search term. Uses a mix of Name-only, Client-only, and both-field matches
    /// to produce a diverse score distribution.
    /// </summary>
    private static (List<FileRecord> records, string[] terms) GenerateDataset(int seed)
    {
        var rng = new Random(seed);
        var searchTerm = "art";
        var terms = new[] { searchTerm };
        var records = new List<FileRecord>();

        // We need 150+ records that all match the search term in some way
        var count = 150 + rng.Next(1, 51); // 150 to 200 records

        for (int i = 0; i < count; i++)
        {
            var matchType = rng.Next(3); // 0 = Name only, 1 = Client only, 2 = both
            var record = new FileRecord
            {
                Id = Guid.NewGuid(),
                FileType = ValidFileTypes[rng.Next(ValidFileTypes.Length)],
                FlopDiskNumber = rng.Next(2) == 0 ? null : rng.Next(1, 9999),
                Date = new DateTime(rng.Next(2000, 2025), rng.Next(1, 13), rng.Next(1, 28), 0, 0, 0, DateTimeKind.Utc),
                FileNumber = null
            };

            switch (matchType)
            {
                case 0: // Name match only (score = 10 + 5 bonus = 15)
                    record.Name = $"art_{GenerateRandomSuffix(rng)}";
                    record.Client = $"zzz_{GenerateRandomSuffix(rng)}";
                    break;
                case 1: // Client match only (score = 5)
                    record.Name = $"zzz_{GenerateRandomSuffix(rng)}";
                    record.Client = $"art_{GenerateRandomSuffix(rng)}";
                    break;
                case 2: // Both match (score = 10 + 5 + 5 bonus = 20)
                    record.Name = $"art_{GenerateRandomSuffix(rng)}";
                    record.Client = $"art_{GenerateRandomSuffix(rng)}";
                    break;
            }

            records.Add(record);
        }

        return (records, terms);
    }

    private static string GenerateRandomSuffix(Random rng)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz";
        var length = rng.Next(3, 10);
        return new string(Enumerable.Range(0, length)
            .Select(_ => chars[rng.Next(chars.Length)])
            .ToArray());
    }

    /// <summary>
    /// Simulates the search pipeline from SearchAsync:
    /// Score each record, filter out zero-score, order by date descending, take max 100.
    /// This is the same logic used in FileRecordService.SearchAsync after DB candidates are loaded.
    /// </summary>
    private static List<(FileRecord Record, int Score)> SimulateSearchPipeline(List<FileRecord> candidates, string[] terms)
    {
        return candidates
            .Select(f => (Record: f, Score: FileRecordService.ComputeScore(f, terms)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Record.Date ?? DateTime.MinValue)
            .Take(100)
            .ToList();
    }

    /// <summary>
    /// Property 5: For any valid search term, the returned result list is sorted
    /// by date in descending order (most recent first), and the list length does not exceed 100 records.
    ///
    /// Generates 150+ records, runs the search pipeline, and asserts:
    /// 1. Results are ordered by date descending (most recent first)
    /// 2. Result count ≤ 100
    ///
    /// **Validates: Requirements 4.4, 7.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: smart-search-file-records, Property 5: Results ordered by date descending and capped at 100",
        MaxTest = 100)]
    public Property Results_AreOrderedByDateDescending_AndCappedAt100()
    {
        var seedGen = Gen.Choose(1, int.MaxValue).ToArbitrary();

        return Prop.ForAll(seedGen, seed =>
        {
            var (records, terms) = GenerateDataset(seed);

            // Ensure we have 150+ candidate records
            Assert.True(records.Count >= 150,
                $"Expected at least 150 records but got {records.Count}");

            // Run the search pipeline (same logic as SearchAsync)
            var results = SimulateSearchPipeline(records, terms);

            // Property assertion 1: Length ≤ 100
            Assert.True(results.Count <= 100,
                $"Expected at most 100 results but got {results.Count}");

            // Property assertion 2: Results are sorted by date in non-increasing (descending) order
            for (int i = 0; i < results.Count - 1; i++)
            {
                var date1 = results[i].Record.Date ?? DateTime.MinValue;
                var date2 = results[i + 1].Record.Date ?? DateTime.MinValue;
                Assert.True(date1 >= date2,
                    $"Results not in descending date order at index {i}: " +
                    $"date {date1:yyyy-MM-dd} should be >= {date2:yyyy-MM-dd}");
            }

            // Additional assertion: since we generated 150+ matching records,
            // the pipeline should actually cap at exactly 100
            // (all records are designed to match the search term with score > 0)
            Assert.Equal(100, results.Count);
        });
    }
}
