using System.Text.RegularExpressions;

namespace InvoicePlatform.Domain.EInvoicing;

/// <summary>
/// The code lists the runtime rules check, from the PEPPOL BIS Billing 3.0
/// code lists in release 3.0.21.
///
/// Currency, country and unit codes are checked for shape only (three capitals,
/// two capitals, a Rec 20 style code). Embedding the full ISO and UN/ECE lists
/// is left to the official validator in CI; what extraction gets wrong in
/// practice ("eur", "EURO", "stuks") the shape check already catches.
/// </summary>
public static partial class CodeLists
{
    /// <summary>UNCL 1001 invoice codes allowed by PEPPOL-EN16931-P0100.</summary>
    public static readonly IReadOnlySet<string> InvoiceTypeCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "71", "80", "82", "84", "102", "218", "219", "326", "331", "380", "382", "383", "384", "386",
        "388", "393", "395", "553", "575", "623", "780", "817", "870", "875", "876", "877",
    };

    /// <summary>UNCL 1001 credit note codes allowed by PEPPOL-EN16931-P0101.</summary>
    public static readonly IReadOnlySet<string> CreditNoteTypeCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "81", "83", "381", "396", "532",
    };

    /// <summary>Invoice type codes only allowed when seller and buyer are both German (P0112).</summary>
    public static readonly IReadOnlySet<string> GermanOnlyTypeCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "326", "384",
    };

    /// <summary>UNCL 5305 VAT categories used by EN 16931.</summary>
    public static readonly IReadOnlySet<string> VatCategories = new HashSet<string>(StringComparer.Ordinal)
    {
        "S", "Z", "E", "AE", "K", "G", "O", "L", "M", "B",
    };

    /// <summary>Electronic Address Scheme codes valid in release 3.0.21 (0217 and 13 others were removed).</summary>
    public static readonly IReadOnlySet<string> ElectronicAddressSchemes = new HashSet<string>(StringComparer.Ordinal)
    {
        "0002", "0007", "0009", "0060", "0088", "0096", "0097", "0106", "0130", "0135", "0142", "0151",
        "0158", "0183", "0184", "0188", "0190", "0191", "0192", "0195", "0196", "0198", "0199", "0200",
        "0201", "0204", "0208", "0209", "0210", "0211", "0216", "0218", "0221", "0225", "0230", "0235",
        "0240", "0242", "0244", "0245", "0246", "0248", "9910", "9913", "9914", "9915", "9918", "9919",
        "9920", "9922", "9923", "9924", "9925", "9926", "9927", "9928", "9929", "9930", "9931", "9932",
        "9933", "9934", "9935", "9936", "9937", "9938", "9939", "9940", "9941", "9942", "9943", "9944",
        "9945", "9946", "9947", "9948", "9949", "9950", "9951", "9952", "9953", "9957", "9959",
    };

    /// <summary>The legal-registration schemes NL-R-003 and NL-R-005 accept: KvK and OIN.</summary>
    public static readonly IReadOnlySet<string> DutchRegistrationSchemes = new HashSet<string>(StringComparer.Ordinal)
    {
        "0106", "0190",
    };

    /// <summary>Payment means codes NL-R-008 accepts between Dutch parties.</summary>
    public static readonly IReadOnlySet<string> DutchPaymentMeansCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "30", "48", "49", "57", "58", "59",
    };

    public static bool IsInvoiceType(string? code) => code is not null && InvoiceTypeCodes.Contains(code);

    public static bool IsCreditNoteType(string? code) => code is not null && CreditNoteTypeCodes.Contains(code);

    public static bool IsCurrencyCode(string? code) => code is not null && CurrencyShape().IsMatch(code);

    public static bool IsCountryCode(string? code) => code is not null && CountryShape().IsMatch(code);

    public static bool IsUnitCode(string? code) => code is not null && UnitShape().IsMatch(code);

    public static bool IsElectronicAddressScheme(string? code) => code is not null && ElectronicAddressSchemes.Contains(code);

    /// <summary>An ISO 6523 ICD is a four-digit code.</summary>
    public static bool IsRegistrationScheme(string? code) => code is not null && IcdShape().IsMatch(code);

    /// <summary>UNCL 4461: 1 to 97, or ZZZ (mutually defined).</summary>
    public static bool IsPaymentMeansCode(string? code)
        => code == "ZZZ"
            || (code is not null && PaymentMeansShape().IsMatch(code) && int.Parse(code, System.Globalization.CultureInfo.InvariantCulture) is >= 1 and <= 97);

    /// <summary>Credit transfer codes that require an account (BR-61).</summary>
    public static bool IsCreditTransfer(string? code) => code is "30" or "58";

    /// <summary>Direct debit codes that require a mandate (PEPPOL-EN16931-R061).</summary>
    public static bool IsDirectDebit(string? code) => code is "49" or "59";

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex CurrencyShape();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryShape();

    [GeneratedRegex("^[A-Z0-9]{1,3}$")]
    private static partial Regex UnitShape();

    [GeneratedRegex("^[0-9]{4}$")]
    private static partial Regex IcdShape();

    [GeneratedRegex("^[1-9][0-9]?$")]
    private static partial Regex PaymentMeansShape();
}
