using Web.Services;

namespace Web.Tests.Services;

public class SearchHighlightTests
{
    [Fact]
    public void Split_NoQuery_ReturnsWholeTextUnmarked()
    {
        var parts = SearchHighlight.Split("Logo Hortodente", null);

        Assert.Equal([("Logo Hortodente", false)], parts);
    }

    [Fact]
    public void Split_IgnoresCaseAndAccents_KeepsOriginalText()
    {
        var parts = SearchHighlight.Split("Promoção de Verão", "promocao VERAO");

        Assert.Equal(
            [("Promoção", true), (" de ", false), ("Verão", true)],
            parts);
    }

    [Fact]
    public void Split_OverlappingAndRepeatedMatches_AreMerged()
    {
        var parts = SearchHighlight.Split("aaa-baa", "aa");

        Assert.Equal([("aaa", true), ("-b", false), ("aa", true)], parts);
    }

    [Fact]
    public void Split_TermNotFound_ReturnsWholeTextUnmarked()
    {
        var parts = SearchHighlight.Split("Cartão Visita", "folder");

        Assert.Equal([("Cartão Visita", false)], parts);
    }
}
