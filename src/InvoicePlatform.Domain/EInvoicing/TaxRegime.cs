namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>How a ruleset's tax differs from another's, for the rules all rulesets share.</summary>
/// <param name="TaxName">"btw" or "GST", for messages.</param>
/// <param name="Categories">The tax category codes the ruleset accepts.</param>
/// <param name="IsPaymentMeansCode">Which payment means codes the ruleset accepts.</param>
/// <param name="TaxNumbersHaveCountryPrefix">EU VAT numbers start with the country (BR-CO-09); Singapore GST numbers do not.</param>
internal sealed record TaxRegime(
    string TaxName,
    IReadOnlySet<string> Categories,
    Func<string?, bool> IsPaymentMeansCode,
    bool TaxNumbersHaveCountryPrefix);
