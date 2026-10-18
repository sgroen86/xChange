using InvoicePlatform.Domain.Calculation;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>
/// The only invoice an output adapter accepts (CLAUDE.md).
///
/// It can be created only by <see cref="Validate"/>, so an incomplete invoice
/// cannot reach a writer: that is a compile error rather than a check someone
/// forgets. It is bound to the ruleset it was checked against, because valid
/// for PEPPOL is not valid for anything else.
/// </summary>
public sealed class ValidatedCanonicalInvoice
{
    private ValidatedCanonicalInvoice(CanonicalInvoiceDraft invoice, IInvoiceRuleset ruleset)
    {
        Invoice = invoice;
        Ruleset = ruleset;
    }

    /// <summary>The invoice with calculated line nets, VAT breakdown and totals.</summary>
    public CanonicalInvoiceDraft Invoice { get; }

    public IInvoiceRuleset Ruleset { get; }

    /// <summary>
    /// Calculates the totals, then checks the result against the ruleset. The
    /// rules see the calculated invoice, which is also what gets written.
    /// </summary>
    public static InvoiceValidationResult Validate(IInvoiceRuleset ruleset, CanonicalInvoiceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(draft);

        var calculated = InvoiceTotalsCalculator.Apply(draft);
        var violations = ruleset.Check(calculated);

        return new InvoiceValidationResult(
            violations.Count == 0 ? new ValidatedCanonicalInvoice(calculated, ruleset) : null,
            violations);
    }
}

/// <param name="Invoice">Null when there are violations.</param>
public sealed record InvoiceValidationResult(
    ValidatedCanonicalInvoice? Invoice,
    IReadOnlyList<RuleViolation> Violations);
