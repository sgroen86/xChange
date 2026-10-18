using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Application.Tests;

/// <summary>
/// An invoice that satisfies PEPPOL BIS Billing 3.0. The Domain tests have the
/// full set of samples; this project only needs one, and cannot see those.
/// </summary>
internal static class CompleteInvoice
{
    public static CanonicalInvoiceDraft Create(string number = "F-2026-041") => new()
    {
        TypeCode = "380",
        InvoiceNumber = number,
        IssueDate = "2026-10-01",
        CurrencyCode = "EUR",
        BuyerReference = "PO-2026-17",
        Seller = new CanonicalParty
        {
            Name = "Green IT Solutions B.V.",
            VatIdentifier = "NL857231409B01",
            LegalRegistrationId = "12345678",
            LegalRegistrationScheme = "0106",
            ElectronicAddress = "12345678",
            ElectronicAddressScheme = "0106",
            Address = new CanonicalAddress { StreetName = "Keizersgracht 62", PostalZone = "1015 CS", CityName = "Amsterdam", CountryCode = "NL" },
        },
        Buyer = new CanonicalParty
        {
            Name = "Van Dijk Techniek B.V.",
            VatIdentifier = "NL814912345B01",
            ElectronicAddress = "87654321",
            ElectronicAddressScheme = "0106",
            Address = new CanonicalAddress { StreetName = "Industrieweg 24", PostalZone = "3542 AD", CityName = "Utrecht", CountryCode = "NL" },
        },
        Payment = new CanonicalPaymentDetails { PaymentMeansCode = "30", Iban = "NL91ABNA0417164300" },
        Lines =
        [
            new CanonicalInvoiceLine
            {
                Description = "Onderhoudscontract",
                Quantity = 1m,
                UnitCode = "C62",
                UnitPrice = 1250.00m,
                VatCategoryCode = "S",
                VatPercentage = 21m,
            },
        ],
    };

    /// <summary>A Singapore GST invoice that satisfies PINT-SG.</summary>
    public static CanonicalInvoiceDraft Singapore() => new()
    {
        TypeCode = "380",
        InvoiceNumber = "SG-2026-0101",
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
            Address = new CanonicalAddress { StreetName = "1 Raffles Place", PostalZone = "048616", CityName = "Singapore", CountryCode = "SG" },
        },
        Buyer = new CanonicalParty
        {
            Name = "Tan Trading Pte. Ltd.",
            ElectronicAddress = "SGUEN200212345Z",
            ElectronicAddressScheme = "0195",
            Address = new CanonicalAddress { StreetName = "10 Anson Road", PostalZone = "079903", CityName = "Singapore", CountryCode = "SG" },
        },
        Payment = new CanonicalPaymentDetails { PaymentMeansCode = "30", Iban = "0123456789" },
        Lines =
        [
            new CanonicalInvoiceLine
            {
                Description = "IT consulting",
                Quantity = 10m,
                UnitCode = "HUR",
                UnitPrice = 150.00m,
                VatCategoryCode = "SR",
                VatPercentage = 9m,
            },
        ],
    };
}
