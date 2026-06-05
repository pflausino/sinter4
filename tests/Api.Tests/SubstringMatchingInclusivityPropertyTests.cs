using Api.Services;
using Domain.Entities;
using Domain.Enums;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Api.Tests;

/// <summary>
/// Property-based tests for substring matching inclusivity in the smart search scoring logic.
/// Feature: smart-search-file-records
/// Property 1: Substring matching inclusivity
///
/// For any file record and for any search term that is a contiguous substring of the record's
/// Name or Client field (after case-insensitive and accent-insensitive normalization),
/// the search engine SHALL include that record in the results.
///
/// **Validates: Requirements 3.1, 3.2, 3.4, 4.1**
/// </summary>
[Trait("Feature", "smart-search-file-records")]
[Trait("Property", "Substring matching inclusivity")]
public class SubstringMatchingInclusivityPropertyTests
{
    private static readonly FileType[] ValidFileTypes = Enum.GetValues<FileType>();

    /// <summary>
    /// Generates a non-empty string with a mix of ASCII and accented characters.
    /// </summary>
    private static Gen<string> NonEmptyNameGen()
    {
        var asciiChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ";
        var accentedChars = "áàãâéêíóôõúüçÁÀÃÂÉÊÍÓÔÕÚÜÇ";
        var allChars = asciiChars + accentedChars;

        return Gen.Choose(3, 40).SelectMany(length =>
            Gen.ArrayOf<char>(Gen.Elements(allChars.ToCharArray()), length)
                .Select(chars => new string(chars).Trim()))
            .Where(s => !string.IsNullOrWhiteSpace(s));
    }

    /// <summary>
    /// Generates a FileRecord with random Name and Client fields that contain
    /// a mix of characters including accented ones.
    /// </summary>
    private static Gen<FileRecord> FileRecordGen()
    {
        return NonEmptyNameGen().SelectMany(name =>
            NonEmptyNameGen().Select(client =>
                new FileRecord
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    FileType = ValidFileTypes[0],
                    Client = client
                }));
    }

    /// <summary>
    /// Given a string, extracts a random contiguous substring of length >= 1.
    /// </summary>
    private static Gen<string> SubstringOf(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 1)
            return Gen.Constant(text);

        return Gen.Choose(0, text.Length - 1).SelectMany(start =>
            Gen.Choose(1, text.Length - start).Select(length =>
                text.Substring(start, length)));
    }

    /// <summary>
    /// Property: Any contiguous substring of a FileRecord's normalized Name field,
    /// when used as a search term, produces a score > 0 (record is included in results).
    ///
    /// **Validates: Requirements 3.1, 3.2, 3.4, 4.1**
    /// </summary>
    [Property(
        DisplayName = "Feature: smart-search-file-records, Property 1a: Name substring always produces positive score",
        MaxTest = 100)]
    public Property NameSubstring_AlwaysProducesPositiveScore()
    {
        var gen = FileRecordGen().SelectMany(record =>
        {
            var normalizedName = FileRecordService.RemoveDiacritics(record.Name).ToLowerInvariant();
            return SubstringOf(normalizedName)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(substring => (record, substring));
        }).ToArbitrary();

        return Prop.ForAll(gen, tuple =>
        {
            var (record, searchSubstring) = tuple;

            // Use the substring as a single search term
            var terms = new[] { searchSubstring };
            var score = FileRecordService.ComputeScore(record, terms);

            Assert.True(score > 0,
                $"Expected positive score for Name substring '{searchSubstring}' " +
                $"on record with Name='{record.Name}', Client='{record.Client}'. Got score={score}");
        });
    }

    /// <summary>
    /// Property: Any contiguous substring of a FileRecord's normalized Client field,
    /// when used as a search term, produces a score > 0 (record is included in results).
    ///
    /// **Validates: Requirements 3.1, 3.2, 3.4, 4.1**
    /// </summary>
    [Property(
        DisplayName = "Feature: smart-search-file-records, Property 1b: Client substring always produces positive score",
        MaxTest = 100)]
    public Property ClientSubstring_AlwaysProducesPositiveScore()
    {
        var gen = FileRecordGen().SelectMany(record =>
        {
            var normalizedClient = FileRecordService.RemoveDiacritics(record.Client).ToLowerInvariant();
            return SubstringOf(normalizedClient)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(substring => (record, substring));
        }).ToArbitrary();

        return Prop.ForAll(gen, tuple =>
        {
            var (record, searchSubstring) = tuple;

            // Use the substring as a single search term
            var terms = new[] { searchSubstring };
            var score = FileRecordService.ComputeScore(record, terms);

            Assert.True(score > 0,
                $"Expected positive score for Client substring '{searchSubstring}' " +
                $"on record with Name='{record.Name}', Client='{record.Client}'. Got score={score}");
        });
    }

    /// <summary>
    /// Property: Case variation of a Name substring still produces a positive score.
    /// Verifies case-insensitive matching inclusivity.
    ///
    /// **Validates: Requirements 3.1, 3.4**
    /// </summary>
    [Property(
        DisplayName = "Feature: smart-search-file-records, Property 1c: Case-varied Name substring produces positive score",
        MaxTest = 100)]
    public Property CaseVariedNameSubstring_ProducesPositiveScore()
    {
        var gen = FileRecordGen().SelectMany(record =>
        {
            var normalizedName = FileRecordService.RemoveDiacritics(record.Name).ToLowerInvariant();
            return SubstringOf(normalizedName)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(substring =>
                {
                    // Randomly vary the case of the substring
                    var varied = new string(substring.Select((c, i) =>
                        i % 2 == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c)).ToArray());
                    return (record, varied);
                });
        }).ToArbitrary();

        return Prop.ForAll(gen, tuple =>
        {
            var (record, searchSubstring) = tuple;

            var terms = new[] { searchSubstring };
            var score = FileRecordService.ComputeScore(record, terms);

            Assert.True(score > 0,
                $"Expected positive score for case-varied Name substring '{searchSubstring}' " +
                $"on record with Name='{record.Name}'. Got score={score}");
        });
    }
}
