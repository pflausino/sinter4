using System.Net;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Api.Tests;

/// <summary>
/// Property-based tests for excessive input length rejection in the search endpoint.
/// Feature: smart-search-file-records
/// Property 6: Excessive input length is rejected
///
/// For any string with length greater than 200 characters, the search API SHALL return
/// a 400 Bad Request response.
///
/// **Validates: Requirements 7.3**
/// </summary>
[Trait("Feature", "smart-search-file-records")]
[Trait("Property", "Excessive input length rejected")]
public class ExcessiveInputLengthPropertyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ExcessiveInputLengthPropertyTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Generates strings with length strictly greater than 200 characters.
    /// Uses a mix of alphanumeric and common characters to simulate real input.
    /// </summary>
    private static Arbitrary<string> LongStringArbitrary()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ";

        var gen = Gen.Choose(201, 500).SelectMany(length =>
            Gen.ArrayOf<char>(Gen.Elements(chars.ToCharArray()), length)
                .Select(arr => new string(arr)));

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Property: Any search term with length > 200 returns HTTP 400 Bad Request.
    ///
    /// **Validates: Requirements 7.3**
    /// </summary>
    [Property(
        DisplayName = "Feature: smart-search-file-records, Property 6: Excessive input length is rejected",
        MaxTest = 100)]
    public Property SearchWithExcessiveLength_Returns400()
    {
        return Prop.ForAll(LongStringArbitrary(), longString =>
        {
            SearchReturns400(longString).GetAwaiter().GetResult();
        });
    }

    private async Task SearchReturns400(string longSearchTerm)
    {
        Assert.True(longSearchTerm.Length > 200,
            $"Generated string should be > 200 chars, was {longSearchTerm.Length}");

        var encoded = Uri.EscapeDataString(longSearchTerm);
        var response = await _client.GetAsync($"/api/file-records/search?q={encoded}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
