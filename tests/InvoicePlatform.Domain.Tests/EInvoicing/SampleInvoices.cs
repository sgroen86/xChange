using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.Tests.EInvoicing;

/// <summary>
/// Complete invoices that satisfy PEPPOL BIS Billing 3.0. The rule tests start
/// from <see cref="Valid"/> and break exactly one thing; the writer tests turn
/// these into the reference files that CI checks with the official rules.
/// </summary>
internal static class SampleInvoices
{
    public static CanonicalInvoiceDraft Valid() => new()
    {
        TypeCode = "380",
        InvoiceNumber = "F-2026-041",
        IssueDate = "2026-10-01",
        DueDate = "2026-10-31",
        CurrencyCode = "EUR",
        Note = "Betaling binnen 30 dagen.",
        BuyerReference = "PO-2026-17",
        Seller = new CanonicalParty
        {
            Name = "Green IT Solutions B.V.",
            VatIdentifier = "NL857231409B01",
            LegalRegistrationId = "12345678",
            LegalRegistrationScheme = "0106",
            ElectronicAddress = "12345678",
            ElectronicAddressScheme = "0106",
            ContactEmail = "facturen@greenitsolutions.net",
            Address = new CanonicalAddress
            {
                StreetName = "Keizersgracht 62",
                PostalZone = "1015 CS",
                CityName = "Amsterdam",
                CountryCode = "NL",
            },
        },
        Buyer = new CanonicalParty
        {
            Name = "Van Dijk Techniek B.V.",
            VatIdentifier = "NL814912345B01",
            LegalRegistrationId = "87654321",
            LegalRegistrationScheme = "0106",
            ElectronicAddress = "87654321",
            ElectronicAddressScheme = "0106",
            Address = new CanonicalAddress
            {
                StreetName = "Industrieweg 24",
                PostalZone = "3542 AD",
                CityName = "Utrecht",
                CountryCode = "NL",
            },
        },
        Payment = new CanonicalPaymentDetails
        {
            PaymentMeansCode = "30",
            Iban = "NL91ABNA0417164300",
            AccountName = "Green IT Solutions B.V.",
            PaymentReference = "F-2026-041",
        },
        Lines =
        [
            new CanonicalInvoiceLine
            {
                LineId = "1",
                Description = "Onderhoudscontract serverruimte",
                Quantity = 1m,
                UnitCode = "C62",
                UnitPrice = 1250.00m,
                VatCategoryCode = "S",
                VatPercentage = 21m,
            },
            new CanonicalInvoiceLine
            {
                LineId = "2",
                Description = "Installatie-uren senior technicus",
                Quantity = 7.5m,
                UnitCode = "HUR",
                UnitPrice = 89.50m,
                VatCategoryCode = "S",
                VatPercentage = 21m,
            },
            new CanonicalInvoiceLine
            {
                LineId = "3",
                Description = "Vervangende UPS-accu",
                SellerItemIdentifier = "UPS-ACC-12V",
                Quantity = 2m,
                UnitCode = "C62",
                UnitPrice = 34.95m,
                VatCategoryCode = "S",
                VatPercentage = 9m,
            },
        ],
    };

    /// <summary>A credit note correcting <see cref="Valid"/> in part.</summary>
    public static CanonicalInvoiceDraft CreditNote() => Valid() with
    {
        TypeCode = "381",
        InvoiceNumber = "C-2026-007",
        IssueDate = "2026-10-15",
        DueDate = null,
        Note = "Creditering van het niet geleverde uur.",
        PrecedingInvoiceReference = "F-2026-041",
        Lines =
        [
            new CanonicalInvoiceLine
            {
                LineId = "1",
                Description = "Installatie-uren senior technicus",
                Quantity = 1m,
                UnitCode = "HUR",
                UnitPrice = 89.50m,
                VatCategoryCode = "S",
                VatPercentage = 21m,
            },
        ],
    };

    /// <summary>
    /// Several VAT categories, allowances and charges on line and document
    /// level, a base quantity, a prepaid amount, and text that needs escaping.
    /// </summary>
    public static CanonicalInvoiceDraft MixedVatWithAllowances() => Valid() with
    {
        InvoiceNumber = "F-2026-042",
        BuyerReference = null,
        PurchaseOrderReference = "INK-4471",
        Note = "Levering & installatie <spoed>",
        Lines =
        [
            new CanonicalInvoiceLine
            {
                LineId = "1",
                Description = "Netwerkkabel Cat6 & connectoren",
                Quantity = 250m,
                UnitCode = "MTR",
                UnitPrice = 45.00m,
                BaseQuantity = 100m,
                VatCategoryCode = "S",
                VatPercentage = 21m,
                AllowancesAndCharges =
                [
                    new CanonicalAllowanceCharge { IsCharge = false, Amount = 12.50m, BaseAmount = 112.50m, Percentage = 11.11m, Reason = "Volumekorting" },
                ],
            },
            new CanonicalInvoiceLine
            {
                LineId = "2",
                Description = "Vakliteratuur netwerkbeheer",
                Quantity = 3m,
                UnitCode = "C62",
                UnitPrice = 39.95m,
                VatCategoryCode = "S",
                VatPercentage = 9m,
            },
            new CanonicalInvoiceLine
            {
                LineId = "3",
                Description = "Cursus (vrijgesteld onderwijs)",
                Quantity = 1m,
                UnitCode = "C62",
                UnitPrice = 495.00m,
                VatCategoryCode = "E",
                VatPercentage = 0m,
            },
        ],
        AllowancesAndCharges =
        [
            new CanonicalAllowanceCharge { IsCharge = true, Amount = 15.00m, Reason = "Verzendkosten", VatCategoryCode = "S", VatPercentage = 21m },
            new CanonicalAllowanceCharge { IsCharge = false, Amount = 5.00m, ReasonCode = "95", Reason = "Korting", VatCategoryCode = "S", VatPercentage = 21m },
        ],
        VatBreakdown =
        [
            new CanonicalVatBreakdownLine { VatCategoryCode = "E", VatPercentage = 0m, ExemptionReason = "Vrijgesteld onderwijs (art. 11 lid 1 sub o Wet OB)" },
        ],
        Totals = new CanonicalTotals { PrepaidAmount = 100.00m },
    };
}
