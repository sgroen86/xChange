using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Relations;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Domain.Learning;

/// <summary>
/// Learning from what reviewers correct, without a model: compare what the
/// model read with what was booked, remember the differences, and on the next
/// invoice put them back where the model reads the same thing again. Pure and
/// deterministic (CLAUDE.md, hard rule 2).
/// </summary>
public static class CorrectionMemory
{
    /// <summary>How many corrections an organisation keeps; the most recently seen stay.</summary>
    public const int Capacity = 50;

    /// <summary>How many hints go to the model at most.</summary>
    public const int MaxHints = 20;

    /// <summary>
    /// The seller and payment fields: the organisation's own data, the same on
    /// every invoice it uploads, so a correction is safe to apply again.
    /// </summary>
    private static readonly (string Field, Func<CanonicalInvoiceDraft, string?> Read, Func<CanonicalInvoiceDraft, string?, CanonicalInvoiceDraft> Write)[] OwnFields =
    [
        ("seller.name", d => d.Seller?.Name, (d, v) => d with { Seller = (d.Seller ?? new()) with { Name = v } }),
        ("seller.vatIdentifier", d => d.Seller?.VatIdentifier, (d, v) => d with { Seller = (d.Seller ?? new()) with { VatIdentifier = v } }),
        ("seller.legalRegistrationId", d => d.Seller?.LegalRegistrationId, (d, v) => d with { Seller = (d.Seller ?? new()) with { LegalRegistrationId = v } }),
        ("seller.legalRegistrationScheme", d => d.Seller?.LegalRegistrationScheme, (d, v) => d with { Seller = (d.Seller ?? new()) with { LegalRegistrationScheme = v } }),
        ("seller.address.streetName", d => d.Seller?.Address?.StreetName, (d, v) => WithSellerAddress(d, a => a with { StreetName = v })),
        ("seller.address.postalZone", d => d.Seller?.Address?.PostalZone, (d, v) => WithSellerAddress(d, a => a with { PostalZone = v })),
        ("seller.address.cityName", d => d.Seller?.Address?.CityName, (d, v) => WithSellerAddress(d, a => a with { CityName = v })),
        ("seller.address.countryCode", d => d.Seller?.Address?.CountryCode, (d, v) => WithSellerAddress(d, a => a with { CountryCode = v })),
        ("payment.paymentMeansCode", d => d.Payment?.PaymentMeansCode, (d, v) => d with { Payment = (d.Payment ?? new()) with { PaymentMeansCode = v } }),
        ("payment.iban", d => d.Payment?.Iban, (d, v) => d with { Payment = (d.Payment ?? new()) with { Iban = v } }),
        ("payment.bic", d => d.Payment?.Bic, (d, v) => d with { Payment = (d.Payment ?? new()) with { Bic = v } }),
        ("payment.accountName", d => d.Payment?.AccountName, (d, v) => d with { Payment = (d.Payment ?? new()) with { AccountName = v } }),
    ];

    /// <summary>Header fields whose corrections teach the model where to look; they differ per invoice, so they are hints only.</summary>
    private static readonly (string Field, Func<CanonicalInvoiceDraft, string?> Read)[] HintFields =
    [
        ("buyerReference", d => d.BuyerReference),
        ("purchaseOrderReference", d => d.PurchaseOrderReference),
        ("dueDate", d => d.DueDate),
        ("typeCode", d => d.TypeCode),
        ("currencyCode", d => d.CurrencyCode),
    ];

    /// <summary>The buyer a correction is scoped to: its VAT number, else its registration number.</summary>
    public static string? BuyerKey(CanonicalParty? buyer)
        => RelationKeys.NormalizeVat(buyer?.VatIdentifier) ?? RelationKeys.NormalizeRegistration(buyer?.LegalRegistrationId);

    /// <summary>What the reviewer changed between the model's reading and the booked invoice.</summary>
    public static IReadOnlyList<LearnedCorrection> Learn(CanonicalInvoiceDraft extracted, CanonicalInvoiceDraft booked, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(extracted);
        ArgumentNullException.ThrowIfNull(booked);

        var learned = new List<LearnedCorrection>();
        const string organization = LearnedCorrection.OrganizationScope;

        foreach (var (field, read, _) in OwnFields)
        {
            var to = Clean(read(booked));
            if (to is not null && !Same(read(extracted), to))
            {
                learned.Add(Correction(organization, field, Clean(read(extracted)), to, null, CorrectionUse.Apply, now));
            }
        }

        foreach (var (field, read) in HintFields)
        {
            var to = Clean(read(booked));
            if (to is not null && !Same(read(extracted), to))
            {
                learned.Add(Correction(organization, field, Clean(read(extracted)), to, null, CorrectionUse.Hint, now));
            }
        }

        // Line by line only when the reviewer kept the lines: then line n is still line n.
        if (extracted.Lines.Count == booked.Lines.Count)
        {
            learned.AddRange(LineCorrections(extracted, booked, now));
        }

        return learned.DistinctBy(c => c.Id).ToList();
    }

    private static IEnumerable<LearnedCorrection> LineCorrections(CanonicalInvoiceDraft extracted, CanonicalInvoiceDraft booked, DateTimeOffset now)
    {
        var buyerKey = BuyerKey(booked.Buyer);
        var taxChanges = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        for (var i = 0; i < booked.Lines.Count; i++)
        {
            var before = extracted.Lines[i];
            var after = booked.Lines[i];

            var to = Clean(after.UnitCode);
            if (to is not null && !Same(before.UnitCode, to))
            {
                yield return Correction(LearnedCorrection.OrganizationScope, "lines.unitCode", Clean(before.UnitCode), to,
                    Clean(after.Description), CorrectionUse.Hint, now);
            }

            var fromTax = Tax(before);
            var toTax = Tax(after);
            if (Clean(after.VatCategoryCode) is not null)
            {
                (taxChanges.TryGetValue(fromTax, out var targets) ? targets : taxChanges[fromTax] = []).Add(toTax);
            }
        }

        if (buyerKey is null)
        {
            yield break;
        }

        // A tax change is learned for this customer only when every line that
        // was read the same way was corrected the same way.
        foreach (var (from, targets) in taxChanges)
        {
            if (targets.Count == 1 && targets.Single() is var to && to != from)
            {
                yield return Correction(LearnedCorrection.BuyerScope(buyerKey), "lines.tax", from, to, null, CorrectionUse.Apply, now);
            }
        }

        foreach (var reason in booked.VatBreakdown.Where(b => Clean(b.ExemptionReason) is not null && Clean(b.VatCategoryCode) is not null))
        {
            var category = reason.VatCategoryCode!.Trim();
            var before = extracted.VatBreakdown.FirstOrDefault(b => Same(b.VatCategoryCode, category))?.ExemptionReason;
            if (!Same(before, reason.ExemptionReason))
            {
                yield return Correction(LearnedCorrection.BuyerScope(buyerKey), $"exemptionReason:{category}", Clean(before),
                    Clean(reason.ExemptionReason), null, CorrectionUse.Apply, now);
            }
        }
    }

    /// <summary>Adds what was just learned to what was known: a repeat counts up, the most recent stay.</summary>
    public static IReadOnlyList<LearnedCorrection> Merge(IReadOnlyList<LearnedCorrection> known, IEnumerable<LearnedCorrection> learned)
    {
        var merged = known.ToDictionary(c => c.Id, StringComparer.Ordinal);
        foreach (var correction in learned)
        {
            merged[correction.Id] = merged.TryGetValue(correction.Id, out var existing)
                ? existing with { Count = existing.Count + 1, LastSeen = correction.LastSeen, Context = correction.Context ?? existing.Context }
                : correction;
        }

        return merged.Values
            .OrderByDescending(c => c.LastSeen)
            .ThenByDescending(c => c.Count)
            .Take(Capacity)
            .ToList();
    }

    /// <summary>
    /// The model's reading with the remembered corrections put back - only
    /// where it read exactly what it read before. Returns which corrections
    /// were applied, so a reviewer who undoes one makes xChange forget it.
    /// </summary>
    public static AppliedCorrections Apply(CanonicalInvoiceDraft draft, IReadOnlyList<LearnedCorrection> memory)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var applied = new List<LearnedCorrection>();
        var notices = new List<ValidationWarning>();
        var buyerScope = BuyerKey(draft.Buyer) is { } key ? LearnedCorrection.BuyerScope(key) : null;
        var text = CorrectionMemoryText.Current;

        foreach (var correction in memory.Where(c => c.Use == CorrectionUse.Apply))
        {
            if (correction.Scope == LearnedCorrection.OrganizationScope
                && OwnFields.FirstOrDefault(f => f.Field == correction.Field) is { Read: not null } own
                && Same(own.Read(draft), correction.From))
            {
                draft = own.Write(draft, correction.To);
                applied.Add(correction);
                notices.Add(Notice(correction.Field, text.FilledAsCorrectedBefore(correction.To)));
            }
            else if (correction.Scope == buyerScope && correction.Field == "lines.tax")
            {
                var changed = false;
                var lines = draft.Lines.Select(line =>
                {
                    if (Tax(line) != correction.From)
                    {
                        return line;
                    }

                    changed = true;
                    var (category, rate) = SplitTax(correction.To);
                    return line with { VatCategoryCode = category, VatPercentage = rate };
                }).ToList();

                if (changed)
                {
                    draft = draft with { Lines = lines };
                    applied.Add(correction);
                    notices.Add(Notice("lines", text.LineTaxChangedForBuyer(Describe(correction.From, text), Describe(correction.To, text))));
                }
            }
            else if (correction.Scope == buyerScope && correction.Field.StartsWith("exemptionReason:", StringComparison.Ordinal))
            {
                var category = correction.Field["exemptionReason:".Length..];
                var existing = draft.VatBreakdown.FirstOrDefault(b => Same(b.VatCategoryCode, category));
                if (Same(existing?.ExemptionReason, correction.From) && draft.Lines.Any(l => Same(l.VatCategoryCode, category)))
                {
                    var others = draft.VatBreakdown.Where(b => !Same(b.VatCategoryCode, category));
                    var entry = (existing ?? new CanonicalVatBreakdownLine { VatCategoryCode = category }) with { ExemptionReason = correction.To };
                    draft = draft with { VatBreakdown = [.. others, entry] };
                    applied.Add(correction);
                    notices.Add(Notice("vatBreakdown", text.ExemptionReasonFilledForBuyer(category, correction.To)));
                }
            }
        }

        return new AppliedCorrections(draft, applied, notices);
    }

    /// <summary>
    /// The applied corrections the reviewer undid: the booked invoice no
    /// longer has what xChange put there. Those are forgotten.
    /// </summary>
    public static IReadOnlyList<string> Undone(IEnumerable<LearnedCorrection> applied, CanonicalInvoiceDraft booked)
        => applied.Where(c => !StillThere(c, booked)).Select(c => c.Id).ToList();

    private static bool StillThere(LearnedCorrection correction, CanonicalInvoiceDraft booked)
    {
        if (OwnFields.FirstOrDefault(f => f.Field == correction.Field) is { Read: not null } own)
        {
            return Same(own.Read(booked), correction.To);
        }

        if (correction.Field == "lines.tax")
        {
            return booked.Lines.Any(l => Tax(l) == correction.To);
        }

        if (correction.Field.StartsWith("exemptionReason:", StringComparison.Ordinal))
        {
            var category = correction.Field["exemptionReason:".Length..];
            return booked.VatBreakdown.Any(b => Same(b.VatCategoryCode, category) && Same(b.ExemptionReason, correction.To));
        }

        return true;
    }

    /// <summary>The hints for the model: what it read before and what the reviewer entered, most frequent first.</summary>
    public static IReadOnlyList<ReadingHint> Hints(IReadOnlyList<LearnedCorrection> memory)
        => memory
            .Where(c => c.Use == CorrectionUse.Hint)
            .OrderByDescending(c => c.Count)
            .ThenByDescending(c => c.LastSeen)
            .Take(MaxHints)
            .Select(c => new ReadingHint(c.Field, c.From, c.To, c.Context))
            .ToList();

    private static LearnedCorrection Correction(
        string scope, string field, string? from, string? to, string? context, CorrectionUse use, DateTimeOffset now)
    {
        // Line hints differ per kind of line, so the description is part of what makes one unique.
        var identity = $"{scope}\u001f{field}\u001f{from}\u001f{to}\u001f{(field == "lines.unitCode" ? context : null)}";
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16].ToLowerInvariant();
        return new LearnedCorrection(id, scope, field, from, to, context, use, 1, now);
    }

    private static CanonicalInvoiceDraft WithSellerAddress(CanonicalInvoiceDraft draft, Func<CanonicalAddress, CanonicalAddress> change)
    {
        var seller = draft.Seller ?? new CanonicalParty();
        return draft with { Seller = seller with { Address = change(seller.Address ?? new CanonicalAddress()) } };
    }

    /// <summary>"S|21" - category and rate as one value; the rate normalised so 21 and 21.00 are the same.</summary>
    private static string Tax(CanonicalInvoiceLine line)
        => $"{Clean(line.VatCategoryCode)}|{line.VatPercentage?.ToString("0.####", CultureInfo.InvariantCulture)}";

    private static (string? Category, decimal? Rate) SplitTax(string? value)
    {
        var parts = (value ?? "|").Split('|');
        return (Clean(parts[0]),
            decimal.TryParse(parts.ElementAtOrDefault(1), NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) ? rate : null);
    }

    private static string Describe(string? tax, ICorrectionMemoryText text)
    {
        var (category, rate) = SplitTax(tax);
        return rate is null ? category ?? text.None : $"{category} {rate.Value.ToString("0.##", CultureInfo.InvariantCulture)}%";
    }

    private static ValidationWarning Notice(string path, string message) => new("LEARNED", path, message, ValidationSeverity.Info);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool Same(string? a, string? b) => string.Equals(Clean(a), Clean(b), StringComparison.Ordinal);
}

/// <param name="Applied">The corrections that changed something, kept with the extraction.</param>
public sealed record AppliedCorrections(
    CanonicalInvoiceDraft Draft,
    IReadOnlyList<LearnedCorrection> Applied,
    IReadOnlyList<ValidationWarning> Notices);
