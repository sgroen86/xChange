using InvoicePlatform.Domain.EInvoicing.Peppol;

namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>The rulesets xChange can produce. The first one is the system default.</summary>
public sealed class InvoiceRulesetRegistry
{
    private readonly IReadOnlyList<IInvoiceRuleset> _rulesets;

    /// <summary>Every ruleset xChange ships, PEPPOL BIS Billing 3.0 first.</summary>
    public InvoiceRulesetRegistry()
        : this([new PeppolBisBilling30Ruleset()])
    {
    }

    public InvoiceRulesetRegistry(IReadOnlyList<IInvoiceRuleset> rulesets)
    {
        if (rulesets.Count == 0)
        {
            throw new ArgumentException("At least one ruleset is required.", nameof(rulesets));
        }

        _rulesets = rulesets;
    }

    public IReadOnlyList<IInvoiceRuleset> All => _rulesets;

    public IInvoiceRuleset Default => _rulesets[0];

    public IInvoiceRuleset? Find(string? id)
        => _rulesets.FirstOrDefault(ruleset => string.Equals(ruleset.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// The user's choice, else the organisation's default, else the system
    /// default. A requested id that does not exist returns null, so the caller
    /// can say so; a stored default that no longer exists falls back quietly.
    /// </summary>
    public IInvoiceRuleset? Resolve(string? requested, string? organizationDefault)
        => requested is not null
            ? Find(requested)
            : Find(organizationDefault) ?? Default;
}
