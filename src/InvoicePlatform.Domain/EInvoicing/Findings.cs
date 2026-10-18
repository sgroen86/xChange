using System.Globalization;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>Collects violations while a rule family walks an invoice.</summary>
internal sealed class Findings
{
    private readonly List<RuleViolation> _violations = [];

    public IReadOnlyList<RuleViolation> Violations => _violations;

    public void Add(string ruleId, string? businessTerm, string path, string message)
        => _violations.Add(new RuleViolation(ruleId, businessTerm, path, message));

    public static bool Missing(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>More than two significant decimals: 10.500 is fine, 10.505 is not.</summary>
    public static bool HasMoreThanTwoDecimals(decimal? value)
        => value is { } actual && actual != Math.Round(actual, 2);

    public static string Number(int index) => (index + 1).ToString(CultureInfo.InvariantCulture);

    public static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Facts about an invoice that several rule families need.</summary>
internal static class InvoiceFacts
{
    public static bool IsCreditNote(CanonicalInvoiceDraft invoice) => CodeLists.IsCreditNoteType(invoice.TypeCode);

    public static bool SellerIsDutch(CanonicalInvoiceDraft invoice) => invoice.Seller?.Address?.CountryCode == "NL";

    public static bool BuyerIsDutch(CanonicalInvoiceDraft invoice) => invoice.Buyer?.Address?.CountryCode == "NL";

    /// <summary>Any payment detail at all: then the payment block is written and must be complete.</summary>
    public static bool HasPayment(CanonicalInvoiceDraft invoice)
        => invoice.Payment is { } payment
            && !(Findings.Missing(payment.PaymentMeansCode)
                && Findings.Missing(payment.PaymentMeansText)
                && Findings.Missing(payment.Iban)
                && Findings.Missing(payment.Bic)
                && Findings.Missing(payment.AccountName)
                && Findings.Missing(payment.PaymentReference));

    /// <summary>
    /// Every VAT category in use, with where it was first seen: on a line, a
    /// document allowance or a document charge. The EN 16931 category rules
    /// have one id per place.
    /// </summary>
    public static IEnumerable<(string Category, CategorySource Source)> CategoriesInUse(CanonicalInvoiceDraft invoice)
    {
        var seen = new HashSet<(string, CategorySource)>();

        foreach (var line in invoice.Lines)
        {
            if (!Findings.Missing(line.VatCategoryCode) && seen.Add((line.VatCategoryCode!, CategorySource.Line)))
            {
                yield return (line.VatCategoryCode!, CategorySource.Line);
            }
        }

        foreach (var adjustment in invoice.AllowancesAndCharges)
        {
            var source = adjustment.IsCharge ? CategorySource.Charge : CategorySource.Allowance;
            if (!Findings.Missing(adjustment.VatCategoryCode) && seen.Add((adjustment.VatCategoryCode!, source)))
            {
                yield return (adjustment.VatCategoryCode!, source);
            }
        }
    }
}

internal enum CategorySource
{
    Line,
    Allowance,
    Charge,
}
