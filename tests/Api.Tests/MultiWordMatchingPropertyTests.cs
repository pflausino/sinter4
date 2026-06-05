using Api.Services;
using Domain.Entities;
using Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Api.Tests;

/// <summary>
/// Property-based tests for multi-word independent matching in search.
/// Feature: smart-search-file-records
/// Property 2: Multi-word independent matching
/// </summary>
[Trait("Feature", "smart-search-file-records")]
[Trait("Property", "Multi-word independent matching")]
public class MultiWordMatchingPropertyTests
{
    private static readonly FileType[] ValidFileTypes = Enum.GetValues<FileType>();

    /// <summary>
    /// Property 2: Multi-word independent matching
    ///
    /// For any FileRecord whose Name contains multiple known words, searching with those
    /// words in any shuffled order SHALL include the record in results (ComputeScore > 0).
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(
        DisplayName = "Feature: smart-search-file-records, Property 2: Multi-word independent matching",
        MaxTest = 100)]
    public Property ComputeScore_MultiWordSearch_MatchesRegardlessOfWordOrder()
    {
        var seedGen = Gen.Choose(1, int.MaxValue).ToArbitrary();

        return Prop.ForAll(seedGen, seed =>
        {
            var rng = new Random(seed);

            // Generate 2-5 distinct words
            var wordCount = rng.Next(2, 6);
            var words = Enumerable.Range(0, wordCount)
                .Select(_ => GenerateAlphaWord(rng))
                .Distinct()
                .ToList();

            // Need at least 2 distinct words
            if (words.Count < 2)
                return true; // trivially true — skip degenerate case

            // Build a Name that contains all the words (joined with random separators)
            var name = BuildNameContainingWords(words, rng);

            var record = new FileRecord
            {
                Id = Guid.NewGuid(),
                Name = name,
                FileType = ValidFileTypes[rng.Next(ValidFileTypes.Length)],
                Client = GenerateAlphaWord(rng),
                FlopDiskNumber = null,
                Date = DateTime.UtcNow,
                FileNumber = null
            };

            // Original order: compute score
            var originalTerms = words.ToArray();
            var originalScore = FileRecordService.ComputeScore(record, originalTerms);

            // Shuffled order: compute score
            var shuffledWords = words.OrderBy(_ => rng.Next()).ToArray();
            var shuffledScore = FileRecordService.ComputeScore(record, shuffledWords);

            // Both must be > 0 (record is included in results regardless of word order)
            return originalScore > 0 && shuffledScore > 0;
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
    /// Builds a Name string that contains all the given words, separated by
    /// random filler characters (spaces, dashes, underscores, other words).
    /// </summary>
    private static string BuildNameContainingWords(List<string> words, Random rng)
    {
        var separators = new[] { " ", "-", "_", " " };
        var parts = new List<string>();

        foreach (var word in words)
        {
            if (parts.Count > 0)
                parts.Add(separators[rng.Next(separators.Length)]);
            parts.Add(word);
        }

        return string.Concat(parts);
    }
}
