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

    /// <summary>A Singapore GST invoice in SGD, as PINT-SG wants it.</summary>
    public static CanonicalInvoiceDraft Singapore() => new()
    {
        TypeCode = "380",
        InvoiceNumber = "SG-2026-0101",
        DocumentUuid = "6f2c1a3e-8b4d-4c7e-9a10-2b3c4d5e6f70",
        IssueDate = "2026-10-01",
        DueDate = "2026-10-31",
        CurrencyCode = "SGD",
        BuyerReference = "PO-88",
        Seller = new CanonicalParty
        {
            Name = "Green IT Solutions Pte. Ltd.",
            VatIdentifier = "M2-1234567-K",
            LegalRegistrationId = "201912345K",
            ElectronicAddress = "SGUEN201912345K",
            ElectronicAddressScheme = "0195",
            Address = new CanonicalAddress
            {
                StreetName = "1 Raffles Place",
                PostalZone = "048616",
                CityName = "Singapore",
                CountryCode = "SG",
            },
        },
        Buyer = new CanonicalParty
        {
            Name = "Tan Trading Pte. Ltd.",
            LegalRegistrationId = "200212345Z",
            ElectronicAddress = "SGUEN200212345Z",
            ElectronicAddressScheme = "0195",
            Address = new CanonicalAddress
            {
                StreetName = "10 Anson Road",
                PostalZone = "079903",
                CityName = "Singapore",
                CountryCode = "SG",
            },
        },
        Payment = new CanonicalPaymentDetails
        {
            PaymentMeansCode = "30",
            Iban = "0123456789",
            AccountName = "Green IT Solutions Pte. Ltd.",
            PaymentReference = "SG-2026-0101",
        },
        Lines =
        [
            new CanonicalInvoiceLine
            {
                LineId = "1",
                Description = "IT consulting",
                Quantity = 10m,
                UnitCode = "HUR",
                UnitPrice = 150.00m,
                VatCategoryCode = "SR",
                VatPercentage = 9m,
            },
            new CanonicalInvoiceLine
            {
                LineId = "2",
                Description = "Network switch, 24 ports",
                Quantity = 2m,
                UnitCode = "C62",
                UnitPrice = 389.90m,
                VatCategoryCode = "SR",
                VatPercentage = 9m,
            },
        ],
    };

    /// <summary>The same invoice in US dollars, with the IRAS rate the user entered.</summary>
    public static CanonicalInvoiceDraft SingaporeInUsDollars() => Singapore() with
    {
        InvoiceNumber = "SG-2026-0102",
        DocumentUuid = "0b9e8d7c-6a5f-4e3d-8c2b-1a0f9e8d7c6b",
        CurrencyCode = "USD",
        TaxCurrencyRate = 1.3425m,
    };

    /// <summary>A Singapore credit note, with the reason PINT-SG requires.</summary>
    public static CanonicalInvoiceDraft SingaporeCreditNote() => Singapore() with
    {
        TypeCode = "381",
        InvoiceNumber = "SG-CN-2026-0007",
        DocumentUuid = "3c4d5e6f-7a8b-4c9d-8e0f-1a2b3c4d5e6f",
        Note = "Credit for two consulting hours not delivered.",
        PrecedingInvoiceReference = "SG-2026-0101",
        Lines = [Singapore().Lines[0] with { Quantity = 2m }],
    };

    /// <summary>A British invoice between two UK companies, in pounds at 20%.</summary>
    public static CanonicalInvoiceDraft UnitedKingdom() => new()
    {
        TypeCode = "380",
        InvoiceNumber = "TD-2026-0310",
        IssueDate = "2026-10-01",
        DueDate = "2026-10-31",
        CurrencyCode = "GBP",
        BuyerReference = "PO-4471",
        Seller = new CanonicalParty
        {
            Name = "Thames Digital Ltd",
            VatIdentifier = "GB123456789",
            LegalRegistrationId = "09876543",
            ElectronicAddress = "GB123456789",
            ElectronicAddressScheme = "9932",
            Address = new CanonicalAddress
            {
                StreetName = "12 Bankside",
                PostalZone = "SE1 9JE",
                CityName = "London",
                CountryCode = "GB",
            },
        },
        Buyer = new CanonicalParty
        {
            Name = "Pennine Logistics Ltd",
            VatIdentifier = "GB987654321",
            ElectronicAddress = "GB987654321",
            ElectronicAddressScheme = "9932",
            Address = new CanonicalAddress
            {
                StreetName = "4 Canal Street",
                PostalZone = "M1 3HE",
                CityName = "Manchester",
                CountryCode = "GB",
            },
        },
        Payment = new CanonicalPaymentDetails
        {
            PaymentMeansCode = "30",
            Iban = "GB29NWBK60161331926819",
            AccountName = "Thames Digital Ltd",
            PaymentReference = "TD-2026-0310",
        },
        Lines =
        [
            new CanonicalInvoiceLine
            {
                LineId = "1",
                Description = "Managed hosting, October",
                Quantity = 1m,
                UnitCode = "C62",
                UnitPrice = 840.00m,
                VatCategoryCode = "S",
                VatPercentage = 20m,
            },
            new CanonicalInvoiceLine
            {
                LineId = "2",
                Description = "Support hours",
                Quantity = 3.5m,
                UnitCode = "HUR",
                UnitPrice = 95.00m,
                VatCategoryCode = "S",
                VatPercentage = 20m,
            },
        ],
    };

    /// <summary>A Belgian invoice between two Belgian companies, addressed by enterprise number (0208).</summary>
    public static CanonicalInvoiceDraft Belgium() => new()
    {
        TypeCode = "380",
        InvoiceNumber = "BD-2026-0077",
        IssueDate = "2026-10-01",
        DueDate = "2026-10-31",
        CurrencyCode = "EUR",
        BuyerReference = "BO-2026-12",
        Seller = new CanonicalParty
        {
            Name = "Brussels Data BV",
            VatIdentifier = "BE0477472701",
            LegalRegistrationId = "0477472701",
            LegalRegistrationScheme = "0208",
            ElectronicAddress = "0477472701",
            ElectronicAddressScheme = "0208",
            Address = new CanonicalAddress
            {
                StreetName = "Wetstraat 16",
                PostalZone = "1000",
                CityName = "Brussel",
                CountryCode = "BE",
            },
        },
        Buyer = new CanonicalParty
        {
            Name = "Antwerp Port Services NV",
            VatIdentifier = "BE0815531854",
            ElectronicAddress = "0815531854",
            ElectronicAddressScheme = "0208",
            Address = new CanonicalAddress
            {
                StreetName = "Havenlaan 2",
                PostalZone = "2000",
                CityName = "Antwerpen",
                CountryCode = "BE",
            },
        },
        Payment = new CanonicalPaymentDetails
        {
            PaymentMeansCode = "30",
            Iban = "BE68539007547034",
            AccountName = "Brussels Data BV",
            PaymentReference = "BD-2026-0077",
        },
        Lines =
        [
            new CanonicalInvoiceLine
            {
                LineId = "1",
                Description = "Datamigratie",
                Quantity = 12m,
                UnitCode = "HUR",
                UnitPrice = 110.00m,
                VatCategoryCode = "S",
                VatPercentage = 21m,
            },
        ],
    };

    /// <summary>
    /// The Singapore organisation invoicing a UK company under BIS 3.0: services
    /// reverse-charged (AE), the UK customer accounts for the VAT. The GST number
    /// sits where the PDF puts it; the ruleset moves it to BT-32.
    /// </summary>
    public static CanonicalInvoiceDraft SingaporeToUnitedKingdom() => Singapore() with
    {
        InvoiceNumber = "SG-2026-0140",
        DocumentUuid = null,
        CurrencyCode = "GBP",
        BuyerReference = "PO-4502",
        Buyer = UnitedKingdom().Buyer,
        Lines =
        [
            Singapore().Lines[0] with { VatCategoryCode = "AE", VatPercentage = 0m },
        ],
        VatBreakdown =
        [
            new CanonicalVatBreakdownLine { VatCategoryCode = "AE", VatPercentage = 0m, ExemptionReason = "Reverse charge" },
        ],
    };
}
