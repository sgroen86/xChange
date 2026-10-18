using InvoicePlatform.Domain.EInvoicing;
using TestRuleset = InvoicePlatform.Domain.Tests.EInvoicing.ValidatedCanonicalInvoiceTests.TestRuleset;

namespace InvoicePlatform.Domain.Tests.EInvoicing;

public class InvoiceRulesetRegistryTests
{
    private readonly InvoiceRulesetRegistry _registry = new([new TestRuleset("a"), new TestRuleset("b")]);

    [Fact]
    public void The_requested_ruleset_wins_over_the_organisation_default()
        => Assert.Equal("b", _registry.Resolve("b", "a")!.Id);

    [Fact]
    public void The_organisation_default_wins_over_the_system_default()
        => Assert.Equal("b", _registry.Resolve(null, "b")!.Id);

    [Fact]
    public void Without_any_choice_the_system_default_is_used()
        => Assert.Equal("a", _registry.Resolve(null, null)!.Id);

    [Fact]
    public void A_vanished_organisation_default_falls_back_to_the_system_default()
        => Assert.Equal("a", _registry.Resolve(null, "gone")!.Id);

    [Fact]
    public void An_unknown_requested_ruleset_is_not_silently_replaced()
        => Assert.Null(_registry.Resolve("gone", "a"));
}
