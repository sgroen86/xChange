using System.Globalization;
using System.Text.Json.Serialization;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Infrastructure.Anthropic;

/// <summary>
/// The shape the model returns, mapped one-to-one onto the JSON Schema.
///
/// These types stay internal to Infrastructure. They exist so that provider
/// output is parsed and validated here, and only the Domain draft crosses the
/// boundary outward (CLAUDE.md: provider SDK types must never escape
/// Infrastructure - and neither should provider wire shapes).
///
/// Amounts arrive as strings and are converted with
/// <see cref="decimal.TryParse(string, NumberStyles, IFormatProvider, out decimal)"/>
/// under the invariant culture. They never touch double or float.
/// </summary>
internal sealed record ExtractedInvoiceJson
{
    [JsonPropertyName("typeCode")] public string? TypeCode { get; init; }
    [JsonPropertyName("invoiceNumber")] public string? InvoiceNumber { get; init; }
    [JsonPropertyName("issueDate")] public string? IssueDate { get; init; }
    [JsonPropertyName("dueDate")] public string? DueDate { get; init; }
    [JsonPropertyName("currencyCode")] public string? CurrencyCode { get; init; }
    [JsonPropertyName("note")] public string? Note { get; init; }
    [JsonPropertyName("seller")] public PartyJson? Seller { get; init; }
    [JsonPropertyName("buyer")] public PartyJson? Buyer { get; init; }
    [JsonPropertyName("purchaseOrderReference")] public string? PurchaseOrderReference { get; init; }
    [JsonPropertyName("buyerReference")] public string? BuyerReference { get; init; }
    [JsonPropertyName("payment")] public PaymentJson? Payment { get; init; }
    [JsonPropertyName("lines")] public List<LineJson>? Lines { get; init; }
    [JsonPropertyName("allowancesAndCharges")] public List<AdjustmentJson>? AllowancesAndCharges { get; init; }
    [JsonPropertyName("vatBreakdown")] public List<VatJson>? VatBreakdown { get; init; }
    [JsonPropertyName("totals")] public TotalsJson? Totals { get; init; }
    [JsonPropertyName("evidence")] public List<EvidenceJson>? Evidence { get; init; }

    public CanonicalInvoiceDraft ToDomain() => new()
    {
        TypeCode = Clean(TypeCode),
        InvoiceNumber = Clean(InvoiceNumber),
        IssueDate = Clean(IssueDate),
        DueDate = Clean(DueDate),
        CurrencyCode = Clean(CurrencyCode),
        Note = Clean(Note),
        Seller = Seller?.ToDomain(),
        Buyer = Buyer?.ToDomain(),
        PurchaseOrderReference = Clean(PurchaseOrderReference),
        BuyerReference = Clean(BuyerReference),
        Payment = Payment?.ToDomain(),
        Lines = Lines?.Select(line => line.ToDomain()).ToList() ?? [],
        AllowancesAndCharges = AllowancesAndCharges?.Select(a => a.ToDomain()).ToList() ?? [],
        VatBreakdown = VatBreakdown?.Select(v => v.ToDomain()).ToList() ?? [],
        Totals = Totals?.ToDomain(),
        Evidence = Evidence?.Select(e => e.ToDomain()).ToList() ?? [],
    };

    /// <summary>Parses a decimal string exactly. Anything unparseable becomes null, never zero.</summary>
    internal static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return decimal.TryParse(
            value,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    /// <summary>Empty and whitespace-only strings mean "absent", not "present but blank".</summary>
    internal static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed record PartyJson
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("legalRegistrationId")] public string? LegalRegistrationId { get; init; }
    [JsonPropertyName("vatIdentifier")] public string? VatIdentifier { get; init; }
    [JsonPropertyName("taxRegistrationId")] public string? TaxRegistrationId { get; init; }
    [JsonPropertyName("electronicAddress")] public string? ElectronicAddress { get; init; }
    [JsonPropertyName("electronicAddressScheme")] public string? ElectronicAddressScheme { get; init; }
    [JsonPropertyName("contactName")] public string? ContactName { get; init; }
    [JsonPropertyName("contactEmail")] public string? ContactEmail { get; init; }
    [JsonPropertyName("contactPhone")] public string? ContactPhone { get; init; }
    [JsonPropertyName("address")] public AddressJson? Address { get; init; }

    public CanonicalParty ToDomain() => new()
    {
        Name = ExtractedInvoiceJson.Clean(Name),
        LegalRegistrationId = ExtractedInvoiceJson.Clean(LegalRegistrationId),
        VatIdentifier = ExtractedInvoiceJson.Clean(VatIdentifier),
        TaxRegistrationId = ExtractedInvoiceJson.Clean(TaxRegistrationId),
        ElectronicAddress = ExtractedInvoiceJson.Clean(ElectronicAddress),
        ElectronicAddressScheme = ExtractedInvoiceJson.Clean(ElectronicAddressScheme),
        ContactName = ExtractedInvoiceJson.Clean(ContactName),
        ContactEmail = ExtractedInvoiceJson.Clean(ContactEmail),
        ContactPhone = ExtractedInvoiceJson.Clean(ContactPhone),
        Address = Address?.ToDomain(),
    };
}

internal sealed record AddressJson
{
    [JsonPropertyName("streetName")] public string? StreetName { get; init; }
    [JsonPropertyName("additionalStreetName")] public string? AdditionalStreetName { get; init; }
    [JsonPropertyName("postalZone")] public string? PostalZone { get; init; }
    [JsonPropertyName("cityName")] public string? CityName { get; init; }
    [JsonPropertyName("countrySubdivision")] public string? CountrySubdivision { get; init; }
    [JsonPropertyName("countryCode")] public string? CountryCode { get; init; }

    public CanonicalAddress ToDomain() => new()
    {
        StreetName = ExtractedInvoiceJson.Clean(StreetName),
        AdditionalStreetName = ExtractedInvoiceJson.Clean(AdditionalStreetName),
        PostalZone = ExtractedInvoiceJson.Clean(PostalZone),
        CityName = ExtractedInvoiceJson.Clean(CityName),
        CountrySubdivision = ExtractedInvoiceJson.Clean(CountrySubdivision),
        CountryCode = ExtractedInvoiceJson.Clean(CountryCode),
    };
}

internal sealed record PaymentJson
{
    [JsonPropertyName("paymentMeansCode")] public string? PaymentMeansCode { get; init; }
    [JsonPropertyName("paymentMeansText")] public string? PaymentMeansText { get; init; }
    [JsonPropertyName("iban")] public string? Iban { get; init; }
    [JsonPropertyName("bic")] public string? Bic { get; init; }
    [JsonPropertyName("accountName")] public string? AccountName { get; init; }
    [JsonPropertyName("paymentReference")] public string? PaymentReference { get; init; }

    public CanonicalPaymentDetails ToDomain() => new()
    {
        PaymentMeansCode = ExtractedInvoiceJson.Clean(PaymentMeansCode),
        PaymentMeansText = ExtractedInvoiceJson.Clean(PaymentMeansText),
        Iban = ExtractedInvoiceJson.Clean(Iban),
        Bic = ExtractedInvoiceJson.Clean(Bic),
        AccountName = ExtractedInvoiceJson.Clean(AccountName),
        PaymentReference = ExtractedInvoiceJson.Clean(PaymentReference),
    };
}

internal sealed record LineJson
{
    [JsonPropertyName("lineId")] public string? LineId { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("itemName")] public string? ItemName { get; init; }
    [JsonPropertyName("sellerItemIdentifier")] public string? SellerItemIdentifier { get; init; }
    [JsonPropertyName("quantity")] public string? Quantity { get; init; }
    [JsonPropertyName("unitCode")] public string? UnitCode { get; init; }
    [JsonPropertyName("unitPrice")] public string? UnitPrice { get; init; }
    [JsonPropertyName("baseQuantity")] public string? BaseQuantity { get; init; }
    [JsonPropertyName("netAmount")] public string? NetAmount { get; init; }
    [JsonPropertyName("vatCategoryCode")] public string? VatCategoryCode { get; init; }
    [JsonPropertyName("vatPercentage")] public string? VatPercentage { get; init; }
    [JsonPropertyName("allowancesAndCharges")] public List<AdjustmentJson>? AllowancesAndCharges { get; init; }

    public CanonicalInvoiceLine ToDomain() => new()
    {
        LineId = ExtractedInvoiceJson.Clean(LineId),
        Description = ExtractedInvoiceJson.Clean(Description),
        ItemName = ExtractedInvoiceJson.Clean(ItemName),
        SellerItemIdentifier = ExtractedInvoiceJson.Clean(SellerItemIdentifier),
        Quantity = ExtractedInvoiceJson.ParseDecimal(Quantity),
        UnitCode = ExtractedInvoiceJson.Clean(UnitCode),
        UnitPrice = ExtractedInvoiceJson.ParseDecimal(UnitPrice),
        BaseQuantity = ExtractedInvoiceJson.ParseDecimal(BaseQuantity),
        NetAmount = ExtractedInvoiceJson.ParseDecimal(NetAmount),
        VatCategoryCode = ExtractedInvoiceJson.Clean(VatCategoryCode),
        VatPercentage = ExtractedInvoiceJson.ParseDecimal(VatPercentage),
        AllowancesAndCharges = AllowancesAndCharges?.Select(a => a.ToDomain()).ToList() ?? [],
    };
}

internal sealed record AdjustmentJson
{
    [JsonPropertyName("isCharge")] public bool IsCharge { get; init; }
    [JsonPropertyName("amount")] public string? Amount { get; init; }
    [JsonPropertyName("baseAmount")] public string? BaseAmount { get; init; }
    [JsonPropertyName("percentage")] public string? Percentage { get; init; }
    [JsonPropertyName("reasonCode")] public string? ReasonCode { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
    [JsonPropertyName("vatCategoryCode")] public string? VatCategoryCode { get; init; }
    [JsonPropertyName("vatPercentage")] public string? VatPercentage { get; init; }

    public CanonicalAllowanceCharge ToDomain() => new()
    {
        IsCharge = IsCharge,
        Amount = ExtractedInvoiceJson.ParseDecimal(Amount),
        BaseAmount = ExtractedInvoiceJson.ParseDecimal(BaseAmount),
        Percentage = ExtractedInvoiceJson.ParseDecimal(Percentage),
        ReasonCode = ExtractedInvoiceJson.Clean(ReasonCode),
        Reason = ExtractedInvoiceJson.Clean(Reason),
        VatCategoryCode = ExtractedInvoiceJson.Clean(VatCategoryCode),
        VatPercentage = ExtractedInvoiceJson.ParseDecimal(VatPercentage),
    };
}

internal sealed record VatJson
{
    [JsonPropertyName("vatCategoryCode")] public string? VatCategoryCode { get; init; }
    [JsonPropertyName("vatPercentage")] public string? VatPercentage { get; init; }
    [JsonPropertyName("taxableAmount")] public string? TaxableAmount { get; init; }
    [JsonPropertyName("taxAmount")] public string? TaxAmount { get; init; }
    [JsonPropertyName("exemptionReason")] public string? ExemptionReason { get; init; }

    public CanonicalVatBreakdownLine ToDomain() => new()
    {
        VatCategoryCode = ExtractedInvoiceJson.Clean(VatCategoryCode),
        VatPercentage = ExtractedInvoiceJson.ParseDecimal(VatPercentage),
        TaxableAmount = ExtractedInvoiceJson.ParseDecimal(TaxableAmount),
        TaxAmount = ExtractedInvoiceJson.ParseDecimal(TaxAmount),
        ExemptionReason = ExtractedInvoiceJson.Clean(ExemptionReason),
    };
}

internal sealed record TotalsJson
{
    [JsonPropertyName("lineExtensionAmount")] public string? LineExtensionAmount { get; init; }
    [JsonPropertyName("allowanceTotalAmount")] public string? AllowanceTotalAmount { get; init; }
    [JsonPropertyName("chargeTotalAmount")] public string? ChargeTotalAmount { get; init; }
    [JsonPropertyName("taxExclusiveAmount")] public string? TaxExclusiveAmount { get; init; }
    [JsonPropertyName("taxAmount")] public string? TaxAmount { get; init; }
    [JsonPropertyName("taxInclusiveAmount")] public string? TaxInclusiveAmount { get; init; }
    [JsonPropertyName("prepaidAmount")] public string? PrepaidAmount { get; init; }
    [JsonPropertyName("roundingAmount")] public string? RoundingAmount { get; init; }
    [JsonPropertyName("payableAmount")] public string? PayableAmount { get; init; }

    public CanonicalTotals ToDomain() => new()
    {
        LineExtensionAmount = ExtractedInvoiceJson.ParseDecimal(LineExtensionAmount),
        AllowanceTotalAmount = ExtractedInvoiceJson.ParseDecimal(AllowanceTotalAmount),
        ChargeTotalAmount = ExtractedInvoiceJson.ParseDecimal(ChargeTotalAmount),
        TaxExclusiveAmount = ExtractedInvoiceJson.ParseDecimal(TaxExclusiveAmount),
        TaxAmount = ExtractedInvoiceJson.ParseDecimal(TaxAmount),
        TaxInclusiveAmount = ExtractedInvoiceJson.ParseDecimal(TaxInclusiveAmount),
        PrepaidAmount = ExtractedInvoiceJson.ParseDecimal(PrepaidAmount),
        RoundingAmount = ExtractedInvoiceJson.ParseDecimal(RoundingAmount),
        PayableAmount = ExtractedInvoiceJson.ParseDecimal(PayableAmount),
    };
}

internal sealed record EvidenceJson
{
    [JsonPropertyName("field")] public string? Field { get; init; }
    [JsonPropertyName("confidence")] public string? Confidence { get; init; }
    // A string, not an int: the schema carries every optional value as a string
    // so that no field needs a union type.
    [JsonPropertyName("pageNumber")] public string? PageNumber { get; init; }
    [JsonPropertyName("sourceText")] public string? SourceText { get; init; }

    public FieldEvidence ToDomain() => new(
        ExtractedInvoiceJson.Clean(Field) ?? string.Empty,
        ExtractedInvoiceJson.ParseDecimal(Confidence),
        int.TryParse(PageNumber, out var page) ? page : null,
        ExtractedInvoiceJson.Clean(SourceText));
}
