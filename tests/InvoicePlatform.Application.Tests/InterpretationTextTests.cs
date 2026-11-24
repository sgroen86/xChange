using InvoicePlatform.Application.Interpretation;
using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Application.Tests;

public class InterpretationTextTests
{
    [Theory]
    [InlineData(Language.Nl)]
    [InlineData(Language.En)]
    public void Every_reason_has_a_message_of_its_own(Language language)
    {
        using var scope = Texts.Use(language);

        var messages = Enum.GetValues<InterpretationFailure>()
            .Select(reason => InterpretationText.Current.Message(reason))
            .ToList();

        Assert.All(messages, message => Assert.False(string.IsNullOrWhiteSpace(message)));
        Assert.Equal(messages.Count, messages.Distinct().Count());
        Assert.False(string.IsNullOrWhiteSpace(InterpretationText.Current.Title));
    }

    [Fact]
    public void The_message_follows_the_language()
    {
        using (Texts.Use(Language.Nl))
        {
            Assert.StartsWith("De factuur is te groot", InterpretationText.Current.Message(InterpretationFailure.TooLarge), StringComparison.Ordinal);
        }

        using (Texts.Use(Language.En))
        {
            Assert.StartsWith("The invoice is too large", InterpretationText.Current.Message(InterpretationFailure.TooLarge), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_failure_says_why_and_is_unreadable_when_nothing_more_is_known()
    {
        Assert.Equal(InterpretationFailure.TimedOut,
            new InterpretationFailedException("x", reason: InterpretationFailure.TimedOut).Reason);
        Assert.Equal(InterpretationFailure.Unreadable, new InterpretationFailedException("x").Reason);
    }
}
