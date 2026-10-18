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
}
