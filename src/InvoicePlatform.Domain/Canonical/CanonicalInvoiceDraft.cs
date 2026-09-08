namespace InvoicePlatform.Domain.Canonical;

/// <summary>
/// An interpreted but unvalidated invoice.
///
/// Every field is nullable on purpose: the interpreter must return null for
/// anything it cannot determine rather than inventing a plausible value, so a
/// null here means "not found in the document", never "zero" or "empty".
///
/// All monetary and quantity values are <see cref="decimal"/>. They are parsed
/// from strings at the Infrastructure boundary and never pass through double or
/// float (CLAUDE.md, hard rule 3).
/// </summary>
public sealed record CanonicalInvoiceDraft
{
    /// <summary>UNTDID 1001 code, e.g. "380" invoice, "381" credit note.</summary>
    public string? TypeCode { get; init; }

    public string? InvoiceNumber { get; init; }

    /// <summary>ISO date, yyyy-MM-dd.</summary>
    public string? IssueDate { get; init; }

    /// <summary>ISO date, yyyy-MM-dd.</summary>
    public string? DueDate { get; init; }

    /// <summary>ISO 4217, e.g. "EUR".</summary>
    public string? CurrencyCode { get; init; }

    public string? Note { get; init; }

    public CanonicalParty? Seller { get; init; }

    public CanonicalParty? Buyer { get; init; }

    public string? PurchaseOrderReference { get; init; }

    public string? BuyerReference { get; init; }

    public CanonicalPaymentDetails? Payment { get; init; }

    public IReadOnlyList<CanonicalInvoiceLine> Lines { get; init; } = [];

    /// <summary>Document-level allowances and charges (not line level).</summary>
    public IReadOnlyList<CanonicalAllowanceCharge> AllowancesAndCharges { get; init; } = [];

    public IReadOnlyList<CanonicalVatBreakdownLine> VatBreakdown { get; init; } = [];

    public CanonicalTotals? Totals { get; init; }

    /// <summary>Per-field provenance, keyed by dotted field path.</summary>
    public IReadOnlyList<FieldEvidence> Evidence { get; init; } = [];
}

/// <param name="ElectronicAddress">e.g. a PEPPOL participant id or invoicing email.</param>
/// <param name="ElectronicAddressScheme">Scheme of the electronic address, e.g. "0106", "EM".</param>
public sealed record CanonicalParty
{
    public string? Name { get; init; }
    public string? LegalRegistrationId { get; init; }
    public string? VatIdentifier { get; init; }
    public string? TaxRegistrationId { get; init; }
    public string? ElectronicAddress { get; init; }
    public string? ElectronicAddressScheme { get; init; }
    public string? ContactName { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
    public CanonicalAddress? Address { get; init; }
}

public sealed record CanonicalAddress
{
    public string? StreetName { get; init; }
    public string? AdditionalStreetName { get; init; }
    public string? PostalZone { get; init; }
    public string? CityName { get; init; }
    public string? CountrySubdivision { get; init; }

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string? CountryCode { get; init; }
}

public sealed record CanonicalPaymentDetails
{
    /// <summary>UNTDID 4461 payment means code, e.g. "30" credit transfer.</summary>
    public string? PaymentMeansCode { get; init; }

    public string? PaymentMeansText { get; init; }
    public string? Iban { get; init; }
    public string? Bic { get; init; }
    public string? AccountName { get; init; }

    /// <summary>Structured or free-text remittance reference.</summary>
    public string? PaymentReference { get; init; }
}

public sealed record CanonicalInvoiceLine
{
    public string? LineId { get; init; }
    public string? Description { get; init; }
    public string? ItemName { get; init; }
    public string? SellerItemIdentifier { get; init; }
    public decimal? Quantity { get; init; }

    /// <summary>UN/ECE Recommendation 20 code, e.g. "C62" each, "HUR" hour.</summary>
    public string? UnitCode { get; init; }

    public decimal? UnitPrice { get; init; }

    /// <summary>Quantity the unit price applies to; null means 1.</summary>
    public decimal? BaseQuantity { get; init; }

    /// <summary>Line net amount as printed on the invoice, before VAT.</summary>
    public decimal? NetAmount { get; init; }

    /// <summary>UNTDID 5305 category code, e.g. "S" standard, "Z" zero, "AE" reverse charge.</summary>
    public string? VatCategoryCode { get; init; }

    public decimal? VatPercentage { get; init; }

    public IReadOnlyList<CanonicalAllowanceCharge> AllowancesAndCharges { get; init; } = [];
}

/// <param name="IsCharge">True for a charge, false for an allowance (discount).</param>
public sealed record CanonicalAllowanceCharge
{
    public bool IsCharge { get; init; }
    public decimal? Amount { get; init; }
    public decimal? BaseAmount { get; init; }
    public decimal? Percentage { get; init; }
    public string? ReasonCode { get; init; }
    public string? Reason { get; init; }
    public string? VatCategoryCode { get; init; }
    public decimal? VatPercentage { get; init; }
}

public sealed record CanonicalVatBreakdownLine
{
    public string? VatCategoryCode { get; init; }
    public decimal? VatPercentage { get; init; }
    public decimal? TaxableAmount { get; init; }
    public decimal? TaxAmount { get; init; }
    public string? ExemptionReason { get; init; }
}

public sealed record CanonicalTotals
{
    /// <summary>Sum of line net amounts.</summary>
    public decimal? LineExtensionAmount { get; init; }

    public decimal? AllowanceTotalAmount { get; init; }
    public decimal? ChargeTotalAmount { get; init; }

    /// <summary>Net total: taxable base for the whole document.</summary>
    public decimal? TaxExclusiveAmount { get; init; }

    /// <summary>VAT total.</summary>
    public decimal? TaxAmount { get; init; }

    /// <summary>Gross total.</summary>
    public decimal? TaxInclusiveAmount { get; init; }

    public decimal? PrepaidAmount { get; init; }
    public decimal? RoundingAmount { get; init; }

    /// <summary>Amount actually due for payment.</summary>
    public decimal? PayableAmount { get; init; }
}
