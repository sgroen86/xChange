using System.Globalization;
using System.Text;
using System.Xml;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.EInvoicing.Ubl;

/// <summary>
/// Writes a validated invoice as a UBL 2.1 Invoice or CreditNote, in the element
/// order of the PEPPOL BIS Billing 3.0 syntax.
///
/// Deterministic and model-free (CLAUDE.md, hard rules 1 and 2): every value
/// comes from the <see cref="ValidatedCanonicalInvoice"/>, a field that is
/// absent or blank is left out rather than defaulted, and escaping is done by
/// <see cref="XmlWriter"/>. It accepts only a validated invoice, so an
/// incomplete draft cannot reach it.
///
/// The ruleset supplies CustomizationID and ProfileID; the structure is UBL
/// and is shared by every UBL-based ruleset.
/// </summary>
public sealed class UblInvoiceWriter
{
    public const string InvoiceNamespace = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    public const string CreditNoteNamespace = "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";
    public const string Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    public const string Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    public string Write(ValidatedCanonicalInvoice validated)
    {
        ArgumentNullException.ThrowIfNull(validated);

        if (validated.Ruleset.Syntax != InvoiceSyntax.Ubl21)
        {
            throw new ArgumentException(
                $"Ruleset {validated.Ruleset.Id} is not expressed in UBL 2.1.", nameof(validated));
        }

        var invoice = validated.Invoice;
        var creditNote = CodeLists.IsCreditNoteType(invoice.TypeCode);
        var currency = invoice.CurrencyCode!;

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(false),
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
        };

        var buffer = new Utf8StringWriter();
        using (var xml = XmlWriter.Create(buffer, settings))
        {
            var writer = new UblWriter(xml, currency);

            xml.WriteStartDocument();
            xml.WriteStartElement(creditNote ? "CreditNote" : "Invoice", creditNote ? CreditNoteNamespace : InvoiceNamespace);
            xml.WriteAttributeString("xmlns", "cac", null, Cac);
            xml.WriteAttributeString("xmlns", "cbc", null, Cbc);

            writer.Basic("CustomizationID", validated.Ruleset.CustomizationId);
            writer.Basic("ProfileID", validated.Ruleset.ProfileId);
            writer.Basic("ID", invoice.InvoiceNumber);
            writer.Basic("IssueDate", invoice.IssueDate);
            if (!creditNote)
            {
                writer.Basic("DueDate", invoice.DueDate);
            }

            writer.Basic(creditNote ? "CreditNoteTypeCode" : "InvoiceTypeCode", invoice.TypeCode);
            writer.Basic("Note", invoice.Note);
            writer.Basic("DocumentCurrencyCode", currency);
            writer.Basic("BuyerReference", invoice.BuyerReference);

            if (UblWriter.Present(invoice.PurchaseOrderReference))
            {
                writer.Start("OrderReference");
                writer.Basic("ID", invoice.PurchaseOrderReference);
                writer.End();
            }

            if (UblWriter.Present(invoice.PrecedingInvoiceReference))
            {
                writer.Start("BillingReference");
                writer.Start("InvoiceDocumentReference");
                writer.Basic("ID", invoice.PrecedingInvoiceReference);
                writer.End();
                writer.End();
            }

            Party(writer, "AccountingSupplierParty", invoice.Seller!, isSeller: true);
            Party(writer, "AccountingCustomerParty", invoice.Buyer!, isSeller: false);
            PaymentMeans(writer, invoice, creditNote);

            foreach (var adjustment in invoice.AllowancesAndCharges)
            {
                AllowanceCharge(writer, adjustment, documentLevel: true);
            }

            TaxTotal(writer, invoice);
            MonetaryTotal(writer, invoice.Totals!);

            for (var index = 0; index < invoice.Lines.Count; index++)
            {
                Line(writer, invoice.Lines[index], index, creditNote);
            }

            xml.WriteEndElement();
            xml.WriteEndDocument();
        }

        return buffer.ToString();
    }

    private static void Party(UblWriter writer, string role, CanonicalParty party, bool isSeller)
    {
        writer.Start(role);
        writer.Start("Party");

        writer.Identifier("EndpointID", party.ElectronicAddress, party.ElectronicAddressScheme);

        var address = party.Address;
        writer.Start("PostalAddress");
        writer.Basic("StreetName", address?.StreetName);
        writer.Basic("AdditionalStreetName", address?.AdditionalStreetName);
        writer.Basic("CityName", address?.CityName);
        writer.Basic("PostalZone", address?.PostalZone);
        writer.Basic("CountrySubentity", address?.CountrySubdivision);
        writer.Start("Country");
        writer.Basic("IdentificationCode", address?.CountryCode);
        writer.End();
        writer.End();

        TaxScheme(writer, party.VatIdentifier, "VAT");
        if (isSeller)
        {
            // BT-32, the seller's tax registration other than VAT.
            TaxScheme(writer, party.TaxRegistrationId, "TAX");
        }

        writer.Start("PartyLegalEntity");
        writer.Basic("RegistrationName", party.Name);
        writer.Identifier("CompanyID", party.LegalRegistrationId, party.LegalRegistrationScheme);
        writer.End();

        if (UblWriter.Present(party.ContactName) || UblWriter.Present(party.ContactPhone) || UblWriter.Present(party.ContactEmail))
        {
            writer.Start("Contact");
            writer.Basic("Name", party.ContactName);
            writer.Basic("Telephone", party.ContactPhone);
            writer.Basic("ElectronicMail", party.ContactEmail);
            writer.End();
        }

        writer.End();
        writer.End();
    }

    private static void TaxScheme(UblWriter writer, string? companyId, string scheme)
    {
        if (!UblWriter.Present(companyId))
        {
            return;
        }

        writer.Start("PartyTaxScheme");
        writer.Basic("CompanyID", companyId);
        writer.Start("TaxScheme");
        writer.Basic("ID", scheme);
        writer.End();
        writer.End();
    }

    private static void PaymentMeans(UblWriter writer, CanonicalInvoiceDraft invoice, bool creditNote)
    {
        var payment = invoice.Payment;
        if (payment is null || !UblWriter.Present(payment.PaymentMeansCode))
        {
            return;
        }

        writer.Start("PaymentMeans");
        writer.BasicWithAttribute("PaymentMeansCode", payment.PaymentMeansCode, "name", payment.PaymentMeansText);
        if (creditNote)
        {
            writer.Basic("PaymentDueDate", invoice.DueDate);
        }

        writer.Basic("PaymentID", payment.PaymentReference);

        if (UblWriter.Present(payment.Iban))
        {
            writer.Start("PayeeFinancialAccount");
            writer.Basic("ID", payment.Iban);
            writer.Basic("Name", payment.AccountName);
            if (UblWriter.Present(payment.Bic))
            {
                writer.Start("FinancialInstitutionBranch");
                writer.Basic("ID", payment.Bic);
                writer.End();
            }

            writer.End();
        }

        writer.End();
    }

    private static void AllowanceCharge(UblWriter writer, CanonicalAllowanceCharge adjustment, bool documentLevel)
    {
        writer.Start("AllowanceCharge");
        writer.Basic("ChargeIndicator", adjustment.IsCharge ? "true" : "false");
        writer.Basic("AllowanceChargeReasonCode", adjustment.ReasonCode);
        writer.Basic("AllowanceChargeReason", adjustment.Reason);
        writer.Decimal("MultiplierFactorNumeric", adjustment.Percentage);
        writer.Amount("Amount", adjustment.Amount);
        writer.Amount("BaseAmount", adjustment.BaseAmount);

        if (documentLevel)
        {
            TaxCategory(writer, "TaxCategory", adjustment.VatCategoryCode, adjustment.VatPercentage, exemptionReason: null);
        }

        writer.End();
    }

    private static void TaxTotal(UblWriter writer, CanonicalInvoiceDraft invoice)
    {
        writer.Start("TaxTotal");
        writer.Amount("TaxAmount", invoice.Totals!.TaxAmount);

        foreach (var bucket in invoice.VatBreakdown)
        {
            writer.Start("TaxSubtotal");
            writer.Amount("TaxableAmount", bucket.TaxableAmount);
            writer.Amount("TaxAmount", bucket.TaxAmount);
            TaxCategory(writer, "TaxCategory", bucket.VatCategoryCode, bucket.VatPercentage, bucket.ExemptionReason);
            writer.End();
        }

        writer.End();
    }

    /// <summary>
    /// Category O ("not subject to VAT") carries no rate at all (BR-O-05..07,
    /// BR-48); every other category always carries one, 0 when absent.
    /// </summary>
    private static void TaxCategory(UblWriter writer, string element, string? category, decimal? rate, string? exemptionReason)
    {
        writer.Start(element);
        writer.Basic("ID", category);
        if (category != "O")
        {
            writer.Decimal("Percent", rate ?? 0m);
        }

        writer.Basic("TaxExemptionReason", exemptionReason);
        writer.Start("TaxScheme");
        writer.Basic("ID", "VAT");
        writer.End();
        writer.End();
    }

    private static void MonetaryTotal(UblWriter writer, CanonicalTotals totals)
    {
        writer.Start("LegalMonetaryTotal");
        writer.Amount("LineExtensionAmount", totals.LineExtensionAmount);
        writer.Amount("TaxExclusiveAmount", totals.TaxExclusiveAmount);
        writer.Amount("TaxInclusiveAmount", totals.TaxInclusiveAmount);
        writer.Amount("AllowanceTotalAmount", totals.AllowanceTotalAmount);
        writer.Amount("ChargeTotalAmount", totals.ChargeTotalAmount);
        writer.Amount("PrepaidAmount", totals.PrepaidAmount);
        writer.Amount("PayableRoundingAmount", totals.RoundingAmount);
        writer.Amount("PayableAmount", totals.PayableAmount);
        writer.End();
    }

    private static void Line(UblWriter writer, CanonicalInvoiceLine line, int index, bool creditNote)
    {
        writer.Start(creditNote ? "CreditNoteLine" : "InvoiceLine");

        // The ordinal only stands in for a missing identifier, which is structure, not data.
        writer.Basic("ID", UblWriter.Present(line.LineId)
            ? line.LineId
            : (index + 1).ToString(CultureInfo.InvariantCulture));
        writer.DecimalWithAttribute(creditNote ? "CreditedQuantity" : "InvoicedQuantity", line.Quantity, "unitCode", line.UnitCode);
        writer.Amount("LineExtensionAmount", line.NetAmount);

        foreach (var adjustment in line.AllowancesAndCharges)
        {
            AllowanceCharge(writer, adjustment, documentLevel: false);
        }

        writer.Start("Item");
        // BT-153: the description is the item name. BT-154 is not written
        // separately, so the same text does not appear twice.
        writer.Basic("Name", line.Description);
        if (UblWriter.Present(line.SellerItemIdentifier))
        {
            writer.Start("SellersItemIdentification");
            writer.Basic("ID", line.SellerItemIdentifier);
            writer.End();
        }

        TaxCategory(writer, "ClassifiedTaxCategory", line.VatCategoryCode, line.VatPercentage, exemptionReason: null);
        writer.End();

        writer.Start("Price");
        writer.Price("PriceAmount", line.UnitPrice);
        writer.DecimalWithAttribute("BaseQuantity", line.BaseQuantity, "unitCode", line.UnitCode);
        writer.End();

        writer.End();
    }

    /// <summary>The cac/cbc vocabulary over an XmlWriter, skipping absent values.</summary>
    private sealed class UblWriter(XmlWriter xml, string currency)
    {
        public static bool Present(string? value) => !string.IsNullOrWhiteSpace(value);

        public void Start(string aggregate) => xml.WriteStartElement("cac", aggregate, Cac);

        public void End() => xml.WriteEndElement();

        public void Basic(string name, string? value)
        {
            if (Present(value))
            {
                xml.WriteElementString("cbc", name, Cbc, value);
            }
        }

        public void BasicWithAttribute(string name, string? value, string attribute, string? attributeValue)
        {
            if (!Present(value))
            {
                return;
            }

            xml.WriteStartElement("cbc", name, Cbc);
            if (Present(attributeValue))
            {
                xml.WriteAttributeString(attribute, attributeValue);
            }

            xml.WriteString(value);
            xml.WriteEndElement();
        }

        public void Identifier(string name, string? value, string? scheme)
            => BasicWithAttribute(name, value, "schemeID", scheme);

        /// <summary>Monetary amounts: currencyID and exactly two decimals (UBL-DT-01).</summary>
        public void Amount(string name, decimal? value)
        {
            if (value is not { } actual)
            {
                return;
            }

            xml.WriteStartElement("cbc", name, Cbc);
            xml.WriteAttributeString("currencyID", currency);
            xml.WriteString(actual.ToString("0.00", CultureInfo.InvariantCulture));
            xml.WriteEndElement();
        }

        /// <summary>A unit price may carry more than two decimals; it is written as held.</summary>
        public void Price(string name, decimal? value)
        {
            if (value is not { } actual)
            {
                return;
            }

            xml.WriteStartElement("cbc", name, Cbc);
            xml.WriteAttributeString("currencyID", currency);
            xml.WriteString(Plain(actual));
            xml.WriteEndElement();
        }

        public void Decimal(string name, decimal? value)
        {
            if (value is { } actual)
            {
                xml.WriteElementString("cbc", name, Cbc, Plain(actual));
            }
        }

        public void DecimalWithAttribute(string name, decimal? value, string attribute, string? attributeValue)
        {
            if (value is { } actual)
            {
                BasicWithAttribute(name, Plain(actual), attribute, attributeValue);
            }
        }

        private static string Plain(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>StringWriter defaults to UTF-16; the declaration must say UTF-8.</summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding { get; } = new UTF8Encoding(false);
    }
}
