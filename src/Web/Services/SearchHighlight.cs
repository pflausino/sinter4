using System.Globalization;
using System.Text;

namespace Web.Services;

/// <summary>
/// Splits a text into the parts that match the search terms and the parts that don't, ignoring case
/// and accents like the API search does, so the list can mark why each record was found.
/// </summary>
internal static class SearchHighlight
{
    public static IReadOnlyList<(string Text, bool IsMatch)> Split(string text, string? query)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(query))
            return [(text, false)];

        var folded = Fold(text);
        var matched = new bool[text.Length];
        foreach (var term in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var foldedTerm = Fold(term);
            for (var i = folded.IndexOf(foldedTerm, StringComparison.Ordinal); i >= 0;
                 i = folded.IndexOf(foldedTerm, i + 1, StringComparison.Ordinal))
            {
                Array.Fill(matched, true, i, foldedTerm.Length);
            }
        }

        var parts = new List<(string Text, bool IsMatch)>();
        var start = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i == text.Length || matched[i] != matched[start])
            {
                parts.Add((text[start..i], matched[start]));
                start = i;
            }
        }
        return parts;
    }

    // Lowercase and drop accents one character at a time, keeping positions aligned with the original
    private static string Fold(string text) =>
        string.Create(text.Length, text, (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var baseChar = source[i].ToString().Normalize(NormalizationForm.FormD)[0];
                span[i] = char.ToLower(baseChar, CultureInfo.InvariantCulture);
            }
        });
}
