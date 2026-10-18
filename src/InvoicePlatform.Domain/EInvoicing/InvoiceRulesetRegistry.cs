using InvoicePlatform.Domain.EInvoicing.Peppol;
using InvoicePlatform.Domain.EInvoicing.Pint;

namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>The rulesets xChange can produce. The first one is the system default.</summary>
public sealed class InvoiceRulesetRegistry
{
    private readonly IReadOnlyList<IInvoiceRuleset> _rulesets;

    /// <summary>Every ruleset xChange ships, PEPPOL BIS Billing 3.0 first.</summary>
    public InvoiceRulesetRegistry()
        : this([new PeppolBisBilling30Ruleset(), new PintSgBilling1Ruleset()])
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
    /// The user's choice, else the first of the defaults that exists - in
    /// xChange the customer's, the organisation's and its country's, in that
    /// order - else the system default. A requested id that does not exist
    /// returns null, so the caller can say so; a stored default that no longer
    /// exists falls back quietly.
    /// </summary>
    public IInvoiceRuleset? Resolve(string? requested, params string?[] defaults)
        => requested is not null
            ? Find(requested)
            // A single null argument arrives as a null array, not as [null].
            : (defaults ?? []).Select(Find).FirstOrDefault(ruleset => ruleset is not null) ?? Default;
}
