using System.Globalization;
using System.Text;
using System.Xml;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.Serialization;

/// <summary>
/// Produces the canonical XML document for an invoice.
///
/// Every value written here is read from the supplied <see cref="CanonicalInvoiceDraft"/>.
/// No language model is involved in producing this document and there are no
/// sample or fallback values: a field absent from the draft is omitted from the
/// output rather than defaulted (CLAUDE.md, hard rules 1 and 2).
///
/// Escaping is delegated to <see cref="XmlWriter"/> rather than hand-rolled, so
/// invoice text containing &amp;, &lt;, &gt; or quotes cannot break the document.
/// </summary>
public sealed class CanonicalInvoiceXmlSerializer
{
    public const string Namespace = "urn:greenitsolutions:xchange:canonical:1.0";

    public string Serialize(CanonicalInvoiceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(false),
            // Deterministic output: no environment-dependent newlines.
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
        };

        var buffer = new StringWriterWithEncoding(new UTF8Encoding(false));
        using (var writer = XmlWriter.Create(buffer, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("CanonicalInvoice", Namespace);

            WriteOptional(writer, "TypeCode", draft.TypeCode);
            WriteOptional(writer, "InvoiceNumber", draft.InvoiceNumber);
            WriteOptional(writer, "IssueDate", draft.IssueDate);
            WriteOptional(writer, "DueDate", draft.DueDate);
            WriteOptional(writer, "CurrencyCode", draft.CurrencyCode);
            WriteOptional(writer, "Note", draft.Note);
            WriteOptional(writer, "PurchaseOrderReference", draft.PurchaseOrderReference);
            WriteOptional(writer, "BuyerReference", draft.BuyerReference);

            WriteParty(writer, "Seller", draft.Seller);
            WriteParty(writer, "Buyer", draft.Buyer);
            WritePayment(writer, draft.Payment);
            WriteLines(writer, draft);
            WriteDocumentAdjustments(writer, draft.AllowancesAndCharges);
            WriteVatBreakdown(writer, draft.VatBreakdown);
            WriteTotals(writer, draft.Totals);

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return buffer.ToString();
    }

    private static void WriteParty(XmlWriter writer, string elementName, CanonicalParty? party)
    {
        if (party is null)
        {
            return;
        }

        writer.WriteStartElement(elementName);
        WriteOptional(writer, "Name", party.Name);
        WriteOptional(writer, "LegalRegistrationId", party.LegalRegistrationId);
        WriteOptional(writer, "VatIdentifier", party.VatIdentifier);
        WriteOptional(writer, "TaxRegistrationId", party.TaxRegistrationId);

        if (!string.IsNullOrWhiteSpace(party.ElectronicAddress))
        {
            writer.WriteStartElement("ElectronicAddress");
            if (!string.IsNullOrWhiteSpace(party.ElectronicAddressScheme))
            {
                writer.WriteAttributeString("schemeId", party.ElectronicAddressScheme);
            }

            writer.WriteString(party.ElectronicAddress);
            writer.WriteEndElement();
        }

        if (party.ContactName is not null || party.ContactEmail is not null || party.ContactPhone is not null)
        {
            writer.WriteStartElement("Contact");
            WriteOptional(writer, "Name", party.ContactName);
            WriteOptional(writer, "Email", party.ContactEmail);
            WriteOptional(writer, "Phone", party.ContactPhone);
            writer.WriteEndElement();
        }

        if (party.Address is { } address)
        {
            writer.WriteStartElement("PostalAddress");
            WriteOptional(writer, "StreetName", address.StreetName);
            WriteOptional(writer, "AdditionalStreetName", address.AdditionalStreetName);
            WriteOptional(writer, "PostalZone", address.PostalZone);
            WriteOptional(writer, "CityName", address.CityName);
            WriteOptional(writer, "CountrySubdivision", address.CountrySubdivision);
            WriteOptional(writer, "CountryCode", address.CountryCode);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WritePayment(XmlWriter writer, CanonicalPaymentDetails? payment)
    {
        if (payment is null)
        {
            return;
        }

        writer.WriteStartElement("Payment");
        WriteOptional(writer, "PaymentMeansCode", payment.PaymentMeansCode);
        WriteOptional(writer, "PaymentMeansText", payment.PaymentMeansText);
        WriteOptional(writer, "Iban", payment.Iban);
        WriteOptional(writer, "Bic", payment.Bic);
        WriteOptional(writer, "AccountName", payment.AccountName);
        WriteOptional(writer, "PaymentReference", payment.PaymentReference);
        writer.WriteEndElement();
    }

    private static void WriteLines(XmlWriter writer, CanonicalInvoiceDraft draft)
    {
        if (draft.Lines.Count == 0)
        {
            return;
        }

        var currency = draft.CurrencyCode;

        writer.WriteStartElement("Lines");
        for (var index = 0; index < draft.Lines.Count; index++)
        {
            var line = draft.Lines[index];

            writer.WriteStartElement("InvoiceLine");
            // Fall back to the ordinal only for the identifier, which is structural
            // rather than extracted data.
            writer.WriteElementString("Id", line.LineId ?? (index + 1).ToString(CultureInfo.InvariantCulture));

            WriteOptional(writer, "Description", line.Description);
            WriteOptional(writer, "ItemName", line.ItemName);
            WriteOptional(writer, "SellerItemIdentifier", line.SellerItemIdentifier);

            if (line.Quantity is { } quantity)
            {
                writer.WriteStartElement("Quantity");
                if (!string.IsNullOrWhiteSpace(line.UnitCode))
                {
                    writer.WriteAttributeString("unitCode", line.UnitCode);
                }

                writer.WriteString(FormatDecimal(quantity));
                writer.WriteEndElement();
            }

            WriteAmount(writer, "UnitPrice", line.UnitPrice, currency);
            WriteDecimal(writer, "BaseQuantity", line.BaseQuantity);
            WriteAmount(writer, "NetAmount", line.NetAmount, currency);
            WriteOptional(writer, "VatCategoryCode", line.VatCategoryCode);
            WriteDecimal(writer, "VatPercentage", line.VatPercentage);
            WriteAdjustments(writer, line.AllowancesAndCharges, currency);

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteDocumentAdjustments(
        XmlWriter writer,
        IReadOnlyList<CanonicalAllowanceCharge> adjustments)
    {
        if (adjustments.Count == 0)
        {
            return;
        }

        writer.WriteStartElement("AllowancesAndCharges");
        WriteAdjustmentElements(writer, adjustments, currency: null);
        writer.WriteEndElement();
    }

    private static void WriteAdjustments(
        XmlWriter writer,
        IReadOnlyList<CanonicalAllowanceCharge> adjustments,
        string? currency)
    {
        if (adjustments.Count == 0)
        {
            return;
        }

        writer.WriteStartElement("AllowancesAndCharges");
        WriteAdjustmentElements(writer, adjustments, currency);
        writer.WriteEndElement();
    }

    private static void WriteAdjustmentElements(
        XmlWriter writer,
        IReadOnlyList<CanonicalAllowanceCharge> adjustments,
        string? currency)
    {
        foreach (var adjustment in adjustments)
        {
            writer.WriteStartElement(adjustment.IsCharge ? "Charge" : "Allowance");
            WriteAmount(writer, "Amount", adjustment.Amount, currency);
            WriteAmount(writer, "BaseAmount", adjustment.BaseAmount, currency);
            WriteDecimal(writer, "Percentage", adjustment.Percentage);
            WriteOptional(writer, "ReasonCode", adjustment.ReasonCode);
            WriteOptional(writer, "Reason", adjustment.Reason);
            WriteOptional(writer, "VatCategoryCode", adjustment.VatCategoryCode);
            WriteDecimal(writer, "VatPercentage", adjustment.VatPercentage);
            writer.WriteEndElement();
        }
    }

    private static void WriteVatBreakdown(
        XmlWriter writer,
        IReadOnlyList<CanonicalVatBreakdownLine> breakdown)
    {
        if (breakdown.Count == 0)
        {
            return;
        }

        writer.WriteStartElement("VatBreakdown");
        foreach (var bucket in breakdown)
        {
            writer.WriteStartElement("VatBreakdownLine");
            WriteOptional(writer, "VatCategoryCode", bucket.VatCategoryCode);
            WriteDecimal(writer, "VatPercentage", bucket.VatPercentage);
            WriteDecimal(writer, "TaxableAmount", bucket.TaxableAmount);
            WriteDecimal(writer, "TaxAmount", bucket.TaxAmount);
            WriteOptional(writer, "ExemptionReason", bucket.ExemptionReason);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteTotals(XmlWriter writer, CanonicalTotals? totals)
    {
        if (totals is null)
        {
            return;
        }

        writer.WriteStartElement("Totals");
        WriteDecimal(writer, "LineExtensionAmount", totals.LineExtensionAmount);
        WriteDecimal(writer, "AllowanceTotalAmount", totals.AllowanceTotalAmount);
        WriteDecimal(writer, "ChargeTotalAmount", totals.ChargeTotalAmount);
        WriteDecimal(writer, "TaxExclusiveAmount", totals.TaxExclusiveAmount);
        WriteDecimal(writer, "TaxAmount", totals.TaxAmount);
        WriteDecimal(writer, "TaxInclusiveAmount", totals.TaxInclusiveAmount);
        WriteDecimal(writer, "PrepaidAmount", totals.PrepaidAmount);
        WriteDecimal(writer, "RoundingAmount", totals.RoundingAmount);
        WriteDecimal(writer, "PayableAmount", totals.PayableAmount);
        writer.WriteEndElement();
    }

    private static void WriteOptional(XmlWriter writer, string name, string? value)
    {
        if (value is null)
        {
            return;
        }

        // XmlWriter escapes the value; nothing is concatenated into markup here.
        writer.WriteElementString(name, value);
    }

    private static void WriteDecimal(XmlWriter writer, string name, decimal? value)
    {
        if (value is not { } actual)
        {
            return;
        }

        writer.WriteElementString(name, FormatDecimal(actual));
    }

    private static void WriteAmount(XmlWriter writer, string name, decimal? value, string? currency)
    {
        if (value is not { } actual)
        {
            return;
        }

        writer.WriteStartElement(name);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            writer.WriteAttributeString("currencyId", currency);
        }

        writer.WriteString(FormatDecimal(actual));
        writer.WriteEndElement();
    }

    /// <summary>
    /// Writes the decimal exactly as held, preserving trailing zeros so an
    /// extracted "10.50" is not silently narrowed to "10.5". Invariant culture,
    /// so a comma decimal separator can never leak into the document.
    /// </summary>
    private static string FormatDecimal(decimal value)
        => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>StringWriter defaults to UTF-16; the declaration must say UTF-8.</summary>
    private sealed class StringWriterWithEncoding(Encoding encoding) : StringWriter
    {
        public override Encoding Encoding { get; } = encoding;
    }
}
