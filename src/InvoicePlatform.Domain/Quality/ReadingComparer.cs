using System.Globalization;
using InvoicePlatform.Domain.Calculation;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.Quality;

/// <summary>How one field of a reading compares with the booked invoice.</summary>
public enum FieldOutcome
{
    /// <summary>The reading and the booking agree.</summary>
    Correct,

    /// <summary>The reading had a value; the booking has a different one.</summary>
    Corrected,

    /// <summary>The reading was empty; the booking is filled.</summary>
    Missed,

    /// <summary>The reading had a value; the booking is empty.</summary>
    Extra,
}

/// <param name="Path">The field in this invoice, e.g. <c>lines[2].netAmount</c> or <c>vatBreakdown[S/21].taxAmount</c>.</param>
/// <param name="Field">The field in any invoice, e.g. <c>lines[].netAmount</c>: a bounded list, for sums.</param>
public sealed record FieldComparison(string Path, string Field, FieldOutcome Outcome);

/// <summary>Every counted field of one reading. Fields empty on both sides are not in it.</summary>
public sealed record ReadingComparison(IReadOnlyList<FieldComparison> Fields)
{
    public int Counted => Fields.Count;

    public int Correct => Count(FieldOutcome.Correct);

    public int Corrected => Count(FieldOutcome.Corrected);

    public int Missed => Count(FieldOutcome.Missed);

    public int Extra => Count(FieldOutcome.Extra);

    /// <summary>No counted field is anything but correct.</summary>
    public bool Perfect => Fields.All(f => f.Outcome == FieldOutcome.Correct);

    /// <summary>The fields that are not correct, in field order.</summary>
    public IEnumerable<FieldComparison> NotCorrect => Fields.Where(f => f.Outcome != FieldOutcome.Correct);

    public int Count(FieldOutcome outcome) => Fields.Count(f => f.Outcome == outcome);
}

/// <summary>
/// How well a reading of an invoice matches what the reviewer booked, field
/// by field. Pure and deterministic, no model involved (CLAUDE.md, hard rule 2).
///
/// <para><b>The fields that count</b> (<see cref="FieldNames"/> lists them):</para>
/// <list type="bullet">
/// <item>Header: typeCode, invoiceNumber, issueDate, dueDate, currencyCode, buyerReference,
/// purchaseOrderReference, precedingInvoiceReference.</item>
/// <item>Seller and buyer: name, vatIdentifier, legalRegistrationId, legalRegistrationScheme,
/// electronicAddress, electronicAddressScheme, address.streetName, address.additionalStreetName,
/// address.postalZone, address.cityName, address.countryCode.</item>
/// <item>Payment: paymentMeansCode, iban, bic, paymentReference. (The draft has no payment terms
/// field; the due date stands for them.)</item>
/// <item>Lines: their count, and per line <i>by position</i>: description, quantity, unitPrice,
/// netAmount, vatCategoryCode, vatPercentage. When the counts differ, a line only one side has
/// is compared with an empty line, so its fields are missed or extra.</item>
/// <item>VAT breakdown, matched by category and rate: taxableAmount, taxAmount.</item>
/// <item>Totals: lineExtensionAmount, taxExclusiveAmount, taxAmount, taxInclusiveAmount, payableAmount.</item>
/// </list>
///
/// <para><b>Not counted</b>: the note (free text the model may shorten, nobody checks it), contact
/// details, unit codes, item names and identifiers, document-level allowances and charges, prepaid and
/// rounding amounts, and what a ruleset or the user sets rather than the document (tax currency and
/// rate, document UUID, exemption reasons).</para>
///
/// <para><b>Calculated figures</b>: the review screen shows line nets, the VAT breakdown and the totals
/// calculated from the lines and does not let the reviewer edit them, so the booked draft still holds
/// the figures as the model read them - comparing with those would always say "correct". They are
/// compared instead with what booking stores and exports: <see cref="InvoiceTotalsCalculator"/> applied
/// to the booked draft, i.e. the arithmetic of the lines the reviewer confirmed. A total the model read
/// right on an invoice that rounds differently (VAT per line rather than per rate) then counts as
/// corrected: the measure errs on the strict side there.</para>
///
/// <para><b>Normalisation</b>: amounts, quantities and rates compare as decimal values (100 equals
/// 100.00); text is trimmed, whitespace collapsed and case ignored; identifiers (VAT and registration
/// numbers, electronic addresses, IBAN, BIC, postal codes, payment references) also ignore spaces;
/// dates and codes are compared exactly after trimming.</para>
/// </summary>
public static class ReadingComparer
{
    private enum Kind
    {
        /// <summary>Trimmed, whitespace collapsed, case ignored.</summary>
        Text,

        /// <summary>All whitespace removed, case ignored.</summary>
        Identifier,

        /// <summary>Trimmed, otherwise exact (dates and code-list values).</summary>
        Code,
    }

    private sealed record Field<T>(string Name, Kind Kind = Kind.Code, Func<T, string?>? Text = null, Func<T, decimal?>? Number = null);

    private static readonly Field<CanonicalInvoiceDraft>[] HeaderFields =
    [
        new("typeCode", Kind.Code, d => d.TypeCode),
        new("invoiceNumber", Kind.Text, d => d.InvoiceNumber),
        new("issueDate", Kind.Code, d => d.IssueDate),
        new("dueDate", Kind.Code, d => d.DueDate),
        new("currencyCode", Kind.Code, d => d.CurrencyCode),
        new("buyerReference", Kind.Text, d => d.BuyerReference),
        new("purchaseOrderReference", Kind.Text, d => d.PurchaseOrderReference),
        new("precedingInvoiceReference", Kind.Text, d => d.PrecedingInvoiceReference),
    ];

    private static readonly Field<CanonicalParty>[] PartyFields =
    [
        new("name", Kind.Text, p => p.Name),
        new("vatIdentifier", Kind.Identifier, p => p.VatIdentifier),
        new("legalRegistrationId", Kind.Identifier, p => p.LegalRegistrationId),
        new("legalRegistrationScheme", Kind.Code, p => p.LegalRegistrationScheme),
        new("electronicAddress", Kind.Identifier, p => p.ElectronicAddress),
        new("electronicAddressScheme", Kind.Code, p => p.ElectronicAddressScheme),
        new("address.streetName", Kind.Text, p => p.Address?.StreetName),
        new("address.additionalStreetName", Kind.Text, p => p.Address?.AdditionalStreetName),
        new("address.postalZone", Kind.Identifier, p => p.Address?.PostalZone),
        new("address.cityName", Kind.Text, p => p.Address?.CityName),
        new("address.countryCode", Kind.Code, p => p.Address?.CountryCode),
    ];

    private static readonly Field<CanonicalPaymentDetails>[] PaymentFields =
    [
        new("paymentMeansCode", Kind.Code, p => p.PaymentMeansCode),
        new("iban", Kind.Identifier, p => p.Iban),
        new("bic", Kind.Identifier, p => p.Bic),
        new("paymentReference", Kind.Identifier, p => p.PaymentReference),
    ];

    /// <summary>Compared with the calculated line: the net is the line's arithmetic, not an input.</summary>
    private static readonly Field<CanonicalInvoiceLine>[] LineFields =
    [
        new("description", Kind.Text, l => l.Description),
        new("quantity", Number: l => l.Quantity),
        new("unitPrice", Number: l => l.UnitPrice),
        new("netAmount", Number: l => l.NetAmount),
        new("vatCategoryCode", Kind.Code, l => l.VatCategoryCode),
        new("vatPercentage", Number: l => l.VatPercentage),
    ];

    private static readonly Field<CanonicalVatBreakdownLine>[] VatFields =
    [
        new("taxableAmount", Number: b => b.TaxableAmount),
        new("taxAmount", Number: b => b.TaxAmount),
    ];

    private static readonly Field<CanonicalTotals>[] TotalFields =
    [
        new("lineExtensionAmount", Number: t => t.LineExtensionAmount),
        new("taxExclusiveAmount", Number: t => t.TaxExclusiveAmount),
        new("taxAmount", Number: t => t.TaxAmount),
        new("taxInclusiveAmount", Number: t => t.TaxInclusiveAmount),
        new("payableAmount", Number: t => t.PayableAmount),
    ];

    /// <summary>Every field that can be counted, as <see cref="FieldComparison.Field"/> names it.</summary>
    public static IReadOnlyList<string> FieldNames { get; } =
    [
        .. HeaderFields.Select(f => f.Name),
        .. PartyFields.Select(f => $"seller.{f.Name}"),
        .. PartyFields.Select(f => $"buyer.{f.Name}"),
        .. PaymentFields.Select(f => $"payment.{f.Name}"),
        "lines.count",
        .. LineFields.Select(f => $"lines[].{f.Name}"),
        .. VatFields.Select(f => $"vatBreakdown[].{f.Name}"),
        .. TotalFields.Select(f => $"totals.{f.Name}"),
    ];

    /// <param name="read">The reading: what the model returned, or what the reviewer was shown.</param>
    /// <param name="booked">The draft as the reviewer booked it, before the calculator.</param>
    /// <param name="filledBeforeReview">
    /// What the reviewer was shown, when <paramref name="read"/> is the model's own reading. A field
    /// the model left empty that xChange filled before review (from learned corrections, the
    /// organisation's settings or its relations - an electronic address is rarely printed) is not
    /// counted: it is neither the model's miss nor its credit.
    /// </param>
    public static ReadingComparison Compare(
        CanonicalInvoiceDraft read,
        CanonicalInvoiceDraft booked,
        CanonicalInvoiceDraft? filledBeforeReview = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(booked);

        var calculated = InvoiceTotalsCalculator.Apply(booked);
        var fields = new List<FieldComparison>();

        foreach (var field in HeaderFields)
        {
            Add(fields, field.Name, field.Name, field, read, booked, filledBeforeReview);
        }

        foreach (var (side, party) in new (string, Func<CanonicalInvoiceDraft, CanonicalParty?>)[] { ("seller", d => d.Seller), ("buyer", d => d.Buyer) })
        {
            foreach (var field in PartyFields)
            {
                var name = $"{side}.{field.Name}";
                Add(fields, name, name, field, party(read), party(booked), Maybe(filledBeforeReview, party));
            }
        }

        foreach (var field in PaymentFields)
        {
            var name = $"payment.{field.Name}";
            Add(fields, name, name, field, read.Payment, booked.Payment, filledBeforeReview?.Payment);
        }

        CompareLines(fields, read, booked, calculated, filledBeforeReview);
        CompareVat(fields, read, calculated);

        foreach (var field in TotalFields)
        {
            var name = $"totals.{field.Name}";
            Add(fields, name, name, field, read.Totals, calculated.Totals, filledBeforeReview?.Totals);
        }

        return new ReadingComparison(fields);
    }

    private static void CompareLines(
        List<FieldComparison> fields,
        CanonicalInvoiceDraft read,
        CanonicalInvoiceDraft booked,
        CanonicalInvoiceDraft calculated,
        CanonicalInvoiceDraft? filled)
    {
        var readCount = read.Lines.Count;
        var bookedCount = booked.Lines.Count;
        if (readCount > 0 || bookedCount > 0)
        {
            fields.Add(new FieldComparison("lines.count", "lines.count", (readCount, bookedCount) switch
            {
                (0, _) => FieldOutcome.Missed,
                (_, 0) => FieldOutcome.Extra,
                _ when readCount == bookedCount => FieldOutcome.Correct,
                _ => FieldOutcome.Corrected,
            }));
        }

        for (var i = 0; i < Math.Max(readCount, bookedCount); i++)
        {
            foreach (var field in LineFields)
            {
                // The net is what the calculator makes of the line, as booking stores it.
                var target = field.Name == "netAmount" ? calculated : booked;
                Add(fields, $"lines[{i}].{field.Name}", $"lines[].{field.Name}", field,
                    At(read.Lines, i), At(target.Lines, i), filled is null ? null : At(filled.Lines, i));
            }
        }
    }

    /// <summary>Matched by category and rate: the order of the breakdown means nothing.</summary>
    private static void CompareVat(List<FieldComparison> fields, CanonicalInvoiceDraft read, CanonicalInvoiceDraft calculated)
    {
        var keys = calculated.VatBreakdown.Concat(read.VatBreakdown)
            .Select(b => (Category: (b.VatCategoryCode ?? "").Trim(), Rate: b.VatPercentage ?? 0m))
            .Distinct() // decimal equality and hashing ignore the scale: 21 and 21.00 are one rate
            .ToList();

        foreach (var (category, rate) in keys)
        {
            var key = $"{(category.Length == 0 ? "-" : category)}/{rate.ToString("0.############", CultureInfo.InvariantCulture)}";
            foreach (var field in VatFields)
            {
                Add(fields, $"vatBreakdown[{key}].{field.Name}", $"vatBreakdown[].{field.Name}", field,
                    Bucket(read, category, rate), Bucket(calculated, category, rate), null);
            }
        }
    }

    private static CanonicalVatBreakdownLine? Bucket(CanonicalInvoiceDraft draft, string category, decimal rate)
        => draft.VatBreakdown.FirstOrDefault(b => (b.VatCategoryCode ?? "").Trim() == category && (b.VatPercentage ?? 0m) == rate);

    private static void Add<T>(List<FieldComparison> fields, string path, string name, Field<T> field, T? read, T? target, T? filled)
        where T : class
    {
        var outcome = field.Number is { } number
            ? Outcome(Value(read, number), Value(target, number), Value(filled, number), (a, b) => a == b)
            : Outcome(Normal(read, field), Normal(target, field), Normal(filled, field), (a, b) => string.Equals(a, b, StringComparison.Ordinal));

        if (outcome is { } counted)
        {
            fields.Add(new FieldComparison(path, name, counted));
        }
    }

    /// <returns>Null when the field is not counted: empty on both sides, or filled by xChange where the model left it empty.</returns>
    private static FieldOutcome? Outcome<TValue>(TValue? read, TValue? target, TValue? filled, Func<TValue, TValue, bool> same)
    {
        if (read is null)
        {
            return target is null || filled is not null ? null : FieldOutcome.Missed;
        }

        if (target is null)
        {
            return FieldOutcome.Extra;
        }

        return same(read, target) ? FieldOutcome.Correct : FieldOutcome.Corrected;
    }

    private static decimal? Value<T>(T? source, Func<T, decimal?> read)
        where T : class
        => source is null ? null : read(source);

    private static string? Normal<T>(T? source, Field<T> field)
        where T : class
    {
        var value = source is null ? null : field.Text!(source);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return field.Kind switch
        {
            Kind.Text => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant(),
            Kind.Identifier => string.Concat(value.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant(),
            _ => value.Trim(),
        };
    }

    private static T? At<T>(IReadOnlyList<T> items, int index)
        where T : class
        => index < items.Count ? items[index] : null;

    private static TOut? Maybe<TIn, TOut>(TIn? source, Func<TIn, TOut?> read)
        where TIn : class
        where TOut : class
        => source is null ? null : read(source);
}
