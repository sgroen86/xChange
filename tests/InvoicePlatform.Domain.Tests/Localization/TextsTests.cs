using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Domain.Tests.Localization;

public class TextsTests
{
    [Theory]
    [InlineData(null, Language.Nl)]
    [InlineData("", Language.Nl)]
    [InlineData("en", Language.En)]
    [InlineData("en-GB,en;q=0.9,nl;q=0.8", Language.En)]
    [InlineData("nl-NL,nl;q=0.9,en;q=0.8", Language.Nl)]
    [InlineData("fr-BE,fr;q=0.9,en;q=0.5", Language.En)]
    [InlineData("nl;q=0.4,en;q=0.6", Language.En)]
    [InlineData("de-DE", Language.Nl)]
    [InlineData("en;q=0", Language.Nl)]
    public void The_first_supported_language_wins(string? header, Language expected)
        => Assert.Equal(expected, Texts.FromAcceptLanguage(header));

    [Fact]
    public void Dutch_without_a_scope_and_restored_after_one()
    {
        Assert.Equal(Language.Nl, Texts.Current);

        using (Texts.Use(Language.En))
        {
            Assert.Equal(Language.En, Texts.Current);
            Assert.Equal("en", Texts.Pick("nl", "en"));
        }

        Assert.Equal(Language.Nl, Texts.Current);
    }
}
