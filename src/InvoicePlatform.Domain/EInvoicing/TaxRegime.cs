namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>Which tax a ruleset is about. The catalogues name it per language: btw/VAT, GST.</summary>
internal enum TaxKind
{
    Vat,
    Gst,
}

/// <summary>How a ruleset's tax differs from another's, for the rules all rulesets share.</summary>
/// <param name="Tax">VAT or GST, for messages.</param>
/// <param name="Categories">The tax category codes the ruleset accepts.</param>
/// <param name="IsPaymentMeansCode">Which payment means codes the ruleset accepts.</param>
/// <param name="TaxNumbersHaveCountryPrefix">EU VAT numbers start with the country (BR-CO-09); Singapore GST numbers do not.</param>
internal sealed record TaxRegime(
    TaxKind Tax,
    IReadOnlySet<string> Categories,
    Func<string?, bool> IsPaymentMeansCode,
    bool TaxNumbersHaveCountryPrefix);
