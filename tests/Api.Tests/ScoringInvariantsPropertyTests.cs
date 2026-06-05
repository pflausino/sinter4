using Api.Services;
using Domain.Entities;
using Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Api.Tests;

/// <summary>
/// Property-based tests for scoring invariants in the smart search feature.
/// Feature: smart-search-file-records
/// Property 4: Scoring invariants
/// </summary>
[Trait("Feature", "smart-search-file-records")]
[Trait("Property", "Scoring invariants")]
public class ScoringInvariantsPropertyTests
{
    private static readonly FileType[] ValidFileTypes = Enum.GetValues<FileType>();

    /// <summary>
    /// Property 4a: Name-match record has strictly higher score than Client-only match
    ///
    /// For any search term and for any two FileRecords where one matches on the Name field
    /// and the other matches only on the Client field, the Name-matching record SHALL have
    /// a strictly higher score.
    ///
    /// **Validates: Requirements 4.2, 4.3**
    /// </summary>
    [Property(
        DisplayName = "Feature: smart-search-file-records, Property 4a: Name match scores higher than Client-only match",
        MaxTest = 100)]
    public Property ComputeScore_NameMatch_StrictlyHigherThanClientOnlyMatch()
    {
        var seedGen = Gen.Choose(1, int.MaxValue).ToArbitrary();

        return Prop.ForAll(seedGen, seed =>
        {
            var rng = new Random(seed);

            // Generate 1-3 search terms
            var termCount = rng.Next(1, 4);
            var terms = Enumerable.Range(0, termCount)
                .Select(_ => GenerateAlphaWord(rng))
                .Distinct()
                .ToArray();

            if (terms.Length == 0)
                return true; // degenerate case

            // Record 1: Name matches all terms, Client does NOT match any term
            var nameMatchRecord = new FileRecord
            {
                Id = Guid.NewGuid(),
                Name = BuildStringContainingTerms(terms, rng),
                FileType = ValidFileTypes[rng.Next(ValidFileTypes.Length)],
                Client = GenerateNonMatchingString(terms, rng),
                FlopDiskNumber = null,
                Date = DateTime.UtcNow,
                FileNumber = null
            };

            // Record 2: Client matches all terms, Name does NOT match any term
            var clientOnlyMatchRecord = new FileRecord
            {
                Id = Guid.NewGuid(),
                Name = GenerateNonMatchingString(terms, rng),
                FileType = ValidFileTypes[rng.Next(ValidFileTypes.Length)],
                Client = BuildStringContainingTerms(terms, rng),
                FlopDiskNumber = null,
                Date = DateTime.UtcNow,
                FileNumber = null
            };

            var nameScore = FileRecordService.ComputeScore(nameMatchRecord, terms);
            var clientOnlyScore = FileRecordService.ComputeScore(clientOnlyMatchRecord, terms);

            // Name-match must score strictly higher than Client-only match
            return nameScore > clientOnlyScore;
        });
    }

    /// <summary>
    /// Property 4b: Both-match score is greater than or equal to Name-only match
    ///
    /// For any search term and for any record matching on both Name and Client fields,
    /// its score SHALL be greater than or equal to a record matching only on the Name field.
    ///
    /// **Validates: Requirements 4.2, 4.3**
    /// </summary>
    [Property(
        DisplayName = "Feature: smart-search-file-records, Property 4b: Both-match score >= Name-only match",
        MaxTest = 100)]
    public Property ComputeScore_BothMatch_GreaterOrEqualToNameOnlyMatch()
    {
        var seedGen = Gen.Choose(1, int.MaxValue).ToArbitrary();

        return Prop.ForAll(seedGen, seed =>
        {
            var rng = new Random(seed);

            // Generate 1-3 search terms
            var termCount = rng.Next(1, 4);
            var terms = Enumerable.Range(0, termCount)
                .Select(_ => GenerateAlphaWord(rng))
                .Distinct()
                .ToArray();

            if (terms.Length == 0)
                return true; // degenerate case

            // Record matching Name only (Client does NOT match)
            var nameOnlyRecord = new FileRecord
            {
                Id = Guid.NewGuid(),
                Name = BuildStringContainingTerms(terms, rng),
                FileType = ValidFileTypes[rng.Next(ValidFileTypes.Length)],
                Client = GenerateNonMatchingString(terms, rng),
                FlopDiskNumber = null,
                Date = DateTime.UtcNow,
                FileNumber = null
            };

            // Record matching BOTH Name and Client
            var bothMatchRecord = new FileRecord
            {
                Id = Guid.NewGuid(),
                Name = BuildStringContainingTerms(terms, rng),
                FileType = ValidFileTypes[rng.Next(ValidFileTypes.Length)],
                Client = BuildStringContainingTerms(terms, rng),
                FlopDiskNumber = null,
                Date = DateTime.UtcNow,
                FileNumber = null
            };

            var nameOnlyScore = FileRecordService.ComputeScore(nameOnlyRecord, terms);
            var bothScore = FileRecordService.ComputeScore(bothMatchRecord, terms);

            // Both-match score must be >= Name-only match score
            return bothScore >= nameOnlyScore;
        });
    }

    /// <summary>
    /// Generates a random alphabetic word of length 3-8 characters.
    /// </summary>
    private static string GenerateAlphaWord(Random rng)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz";
        var length = rng.Next(3, 9);
        return new string(Enumerable.Range(0, length)
            .Select(_ => chars[rng.Next(chars.Length)])
            .ToArray());
    }

    /// <summary>
    /// Builds a string that contains all the given terms, joined with random separators.
    /// </summary>
    private static string BuildStringContainingTerms(string[] terms, Random rng)
    {
        var separators = new[] { " ", "-", "_", " " };
        var parts = new List<string>();

        foreach (var term in terms)
        {
            if (parts.Count > 0)
                parts.Add(separators[rng.Next(separators.Length)]);
            parts.Add(term);
        }

        return string.Concat(parts);
    }

    /// <summary>
    /// Generates a string guaranteed not to contain any of the given terms as substrings.
    /// Uses digits and special characters that won't match alpha-only search terms.
    /// </summary>
    private static string GenerateNonMatchingString(string[] terms, Random rng)
    {
        // Use digits only — since terms are alpha-only, digits cannot match
        const string chars = "0123456789";
        var length = rng.Next(5, 15);
        return new string(Enumerable.Range(0, length)
            .Select(_ => chars[rng.Next(chars.Length)])
            .ToArray());
    }
}
