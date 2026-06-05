using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Shared.Dtos;

namespace Api.Tests;

/// <summary>
/// Property-based tests for the File Records smart search feature using FsCheck.
/// Feature: smart-search-file-records
/// </summary>
public class FileRecordSearchPropertyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FileRecordSearchPropertyTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Property 3: Whitespace input returns empty results
    ///
    /// For any string composed entirely of whitespace characters (including the empty string),
    /// the search engine SHALL return an empty result set without performing matching.
    ///
    /// **Validates: Requirements 3.5, 7.2**
    /// </summary>
    [Property(
        DisplayName = "Feature: smart-search-file-records, Property 3: Whitespace input returns empty results",
        MaxTest = 100)]
    [Trait("Feature", "smart-search-file-records")]
    [Trait("Property", "Whitespace input returns empty results")]
    public Property SearchWithWhitespaceOnlyInput_ReturnsEmptyResults()
    {
        var whitespaceGen = GenWhitespaceString().ToArbitrary();

        return Prop.ForAll(whitespaceGen, whitespaceInput =>
        {
            SearchWhitespaceReturnsEmpty(whitespaceInput).GetAwaiter().GetResult();
        });
    }

    private async Task SearchWhitespaceReturnsEmpty(string whitespaceInput)
    {
        var encoded = Uri.EscapeDataString(whitespaceInput);
        var response = await _client.GetAsync($"/api/file-records/search?q={encoded}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paginated = await response.Content.ReadFromJsonAsync<PaginatedResponse<FileRecordResponse>>(JsonOptions);
        Assert.NotNull(paginated);
        Assert.Empty(paginated.Items);
    }

    /// <summary>
    /// Generates strings composed entirely of whitespace characters (spaces, tabs, newlines).
    /// Also includes the empty string case.
    /// </summary>
    private static Gen<string> GenWhitespaceString()
    {
        var whitespaceChars = new[] { ' ', '\t', '\n', '\r', '\u00A0' };

        var emptyString = Gen.Constant(string.Empty);

        var whitespaceOnly = Gen.Choose(1, 50).SelectMany(length =>
            Gen.ArrayOf<char>(Gen.Elements(whitespaceChars), length)
                .Select(chars => new string(chars)));

        return Gen.OneOf(emptyString, whitespaceOnly);
    }
}
