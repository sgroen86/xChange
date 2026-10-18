# Rulesets and PEPPOL export — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A booked invoice can be downloaded as a PEPPOL BIS Billing 3.0 file (rules release 3.0.21), produced by a ruleset layer that later rulesets plug into, with the organisation's default ruleset and own endpoint id in settings.

**Architecture:** Spec `docs/superpowers/specs/2026-10-18-rulesets-peppol-relaties-design.md`, parts 0–3. First the browser and the API agree on one canonical model with amounts as strings (part 0, fixes the booking data loss). Then Domain gains a totals calculator, `IInvoiceRuleset` with EN 16931 core rules and PEPPOL/NL rules, `ValidatedCanonicalInvoice`, and a UBL 2.1 writer. Application gains organisation settings and an export use case; the API exposes settings, check and export; the review screen gets the PEPPOL fields, the "Aanvullen" block and a format menu. CI validates the writer's reference files with the official 3.0.21 XSLT.

**Tech Stack:** .NET 8, xUnit, System.Xml, DynamoDB (AWS SDK v4), Next.js 16 static export, TypeScript, `node --test` (Node 24 strips types), Saxon-HE (Java) in CI only.

**Execution note:** executed inline by the same session that wrote the spec. Tests are given in full; implementation code is given in full for Domain and Application, where correctness is subtle, and as structure plus the non-obvious code for UI components.

---

## File structure

```
src/InvoicePlatform.Contracts/Json/DecimalStringJsonConverter.cs        NEW  decimals as strings on the wire
src/InvoicePlatform.Contracts/Invoices/RulesetResponses.cs              NEW  settings/check/violation DTOs
src/InvoicePlatform.Domain/Canonical/CanonicalInvoiceDraft.cs           MOD  + LegalRegistrationScheme, PrecedingInvoiceReference
src/InvoicePlatform.Domain/Invoices/StoredInvoice.cs                    MOD  + Direction
src/InvoicePlatform.Domain/Validation/ValidationWarning.cs              MOD  + Info severity
src/InvoicePlatform.Domain/Calculation/InvoiceTotalsCalculator.cs       NEW  EN 16931 totals, deterministic
src/InvoicePlatform.Domain/EInvoicing/RuleViolation.cs                  NEW
src/InvoicePlatform.Domain/EInvoicing/IInvoiceRuleset.cs                NEW
src/InvoicePlatform.Domain/EInvoicing/ValidatedCanonicalInvoice.cs      NEW  private ctor, bound to a ruleset
src/InvoicePlatform.Domain/EInvoicing/InvoiceRulesetRegistry.cs         NEW
src/InvoicePlatform.Domain/EInvoicing/CodeLists.cs                      NEW  code lists the rules check
src/InvoicePlatform.Domain/EInvoicing/En16931CoreRules.cs               NEW  BR-* subset
src/InvoicePlatform.Domain/EInvoicing/Peppol/PeppolBisBilling30Ruleset.cs  NEW
src/InvoicePlatform.Domain/EInvoicing/Peppol/PeppolRules.cs             NEW  PEPPOL-* subset
src/InvoicePlatform.Domain/EInvoicing/Peppol/DutchRules.cs              NEW  NL-R-*
src/InvoicePlatform.Domain/EInvoicing/Ubl/UblInvoiceWriter.cs           NEW  Invoice / CreditNote
src/InvoicePlatform.Application/Organizations/OrganizationSettings.cs   NEW  record + IOrganizationSettingsStore
src/InvoicePlatform.Application/Organizations/OrganizationSettingsService.cs NEW
src/InvoicePlatform.Application/Invoices/InvoiceEnrichment.cs           NEW  fill empty fields from settings (relations later)
src/InvoicePlatform.Application/Invoices/ExportInvoiceService.cs        NEW  check + export
src/InvoicePlatform.Application/Invoices/ExtractInvoiceService.cs       MOD  takes organizationId, enriches
src/InvoicePlatform.Infrastructure/Identity/DynamoDbIdentityStore.cs    MOD  + IOrganizationSettingsStore
src/InvoicePlatform.Infrastructure/Invoices/DynamoDbInvoiceRepository.cs MOD + direction
src/InvoicePlatform.Infrastructure/DependencyInjection.cs               MOD
src/InvoicePlatform.Api/Program.cs                                      MOD  decimal converter, new endpoints
src/InvoicePlatform.Api/Organizations/OrganizationEndpoints.cs          NEW
src/InvoicePlatform.Api/Invoices/ExportEndpoints.cs                     NEW  check + export
src/InvoicePlatform.Api/Invoices/ExtractInvoiceEndpoint.cs              MOD  passes organisation, Info severity
tests/InvoicePlatform.Domain.Tests/Calculation/InvoiceTotalsCalculatorTests.cs
tests/InvoicePlatform.Domain.Tests/EInvoicing/*Tests.cs
tests/InvoicePlatform.Domain.Tests/EInvoicing/Ubl/Expected/*.xml        reference files, validated in CI
tests/InvoicePlatform.Domain.Tests/EInvoicing/SampleInvoices.cs
tests/InvoicePlatform.Application.Tests/ExportInvoiceServiceTests.cs
tests/InvoicePlatform.Application.Tests/OrganizationSettingsServiceTests.cs
tests/InvoicePlatform.Application.Tests/Fakes.cs                        shared in-memory stores
tests/InvoicePlatform.Integration.Tests/SignedInFactory.cs              fake sessions per role, in-memory stores
tests/InvoicePlatform.Integration.Tests/ExportEndpointTests.cs
tests/InvoicePlatform.Integration.Tests/OrganizationEndpointTests.cs
tests/InvoicePlatform.Integration.Tests/DecimalWireFormatTests.cs
tools/peppol-validation/validate.sh                                     NEW  Saxon-HE + official XSLT, pinned
.github/workflows/deploy-api.yml                                        MOD  runs validate.sh
apps/web/lib/decimal.ts            NEW  BigInt decimal arithmetic (no imports; node-testable)
apps/web/lib/decimal.test.ts       NEW
apps/web/lib/totals.ts             NEW  port of InvoiceTotalsCalculator (imports ./decimal.ts)
apps/web/lib/totals.test.ts        NEW  same examples as the C# tests
apps/web/lib/canonical.ts          REWRITE  canonical model = server model; local checks; canonical XML; mock
apps/web/lib/api.ts                MOD  types, toDraft
apps/web/lib/store.ts              MOD  shape guard
apps/web/lib/invoices.ts           MOD  export, check
apps/web/lib/settings.ts           NEW  settings client
apps/web/app/invoices/[invoiceId]/review/ReviewClient.tsx   REWRITE (split into the files below)
apps/web/app/invoices/[invoiceId]/review/fields.tsx          NEW  TextField/SelectField bound to a path
apps/web/app/invoices/[invoiceId]/review/PartyCard.tsx       NEW
apps/web/app/invoices/[invoiceId]/review/LinesCard.tsx       NEW
apps/web/app/invoices/[invoiceId]/review/PaymentCard.tsx     NEW
apps/web/app/invoices/[invoiceId]/review/TotalsCard.tsx      NEW
apps/web/app/invoices/[invoiceId]/review/ExportPanel.tsx     NEW  format menu + Aanvullen block
apps/web/app/invoices/InvoiceListClient.tsx                  MOD  Afnemer column, string amounts
apps/web/app/settings/page.tsx, SettingsClient.tsx           NEW
apps/web/components/nav.ts, AppShell.tsx                     MOD
apps/web/package.json, tsconfig.json                         MOD  test script; exclude *.test.ts
.github/workflows/deploy-ui.yml                              MOD  npm test
```

---

## Task 1: Decimals as strings on the wire

**Files:**
- Create: `src/InvoicePlatform.Contracts/Json/DecimalStringJsonConverter.cs`
- Modify: `src/InvoicePlatform.Api/Program.cs` (after `AddProblemDetails`)
- Test: `tests/InvoicePlatform.Integration.Tests/DecimalWireFormatTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using System.Text.Json;
using InvoicePlatform.Contracts.Json;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Integration.Tests;

public class DecimalWireFormatTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new DecimalStringJsonConverter() },
    };

    [Fact]
    public void Decimals_are_written_as_strings_keeping_trailing_zeros()
    {
        var json = JsonSerializer.Serialize(new CanonicalInvoiceLine { UnitPrice = 10.50m, Quantity = null }, Options);
        Assert.Contains("\"unitPrice\":\"10.50\"", json);
        Assert.Contains("\"quantity\":null", json);
    }

    [Theory]
    [InlineData("{\"unitPrice\":\"89.5000\"}", "89.5000")]
    [InlineData("{\"unitPrice\":89.50}", "89.50")]
    [InlineData("{\"unitPrice\":\"-0.01\"}", "-0.01")]
    public void Decimals_are_read_from_strings_or_numbers(string json, string expected)
    {
        var line = JsonSerializer.Deserialize<CanonicalInvoiceLine>(json, Options)!;
        Assert.Equal(expected, line.UnitPrice!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("{\"unitPrice\":\"\"}")]
    [InlineData("{\"unitPrice\":\"1,5\"}")]
    [InlineData("{\"unitPrice\":\"1e3\"}")]
    public void Malformed_decimal_strings_are_rejected(string json)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CanonicalInvoiceLine>(json, Options));
}
```

- [ ] **Step 2:** `dotnet test tests/InvoicePlatform.Integration.Tests --filter DecimalWireFormatTests` → FAIL (type missing).
- [ ] **Step 3: Implement**

```csharp
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InvoicePlatform.Contracts.Json;

/// <summary>
/// Puts amounts on the wire as strings (CLAUDE.md, hard rule 3): a JSON number
/// becomes a JS float in the browser, a string stays exact. Reads either form,
/// so an older client sending numbers keeps working. Registered once in the
/// API; System.Text.Json applies it to decimal? as well.
/// </summary>
public sealed class DecimalStringJsonConverter : JsonConverter<decimal>
{
    private const NumberStyles Style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDecimal();
        }

        if (reader.TokenType == JsonTokenType.String
            && decimal.TryParse(reader.GetString(), Style, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new JsonException("Expected a decimal number, as a string such as \"10.50\".");
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}
```

In `Program.cs`, after `builder.Services.AddProblemDetails();`:

```csharp
// Amounts leave as strings and arrive as strings (CLAUDE.md, hard rule 3).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new DecimalStringJsonConverter()));
```

- [ ] **Step 4:** test passes; `dotnet test` all green.
- [ ] **Step 5:** commit `Send amounts as strings between browser and API`.

## Task 2: Model additions — direction, Info severity, legal-registration scheme, preceding invoice

**Files:**
- Modify: `src/InvoicePlatform.Domain/Invoices/StoredInvoice.cs`, `src/InvoicePlatform.Domain/Validation/ValidationWarning.cs`, `src/InvoicePlatform.Domain/Canonical/CanonicalInvoiceDraft.cs`, `src/InvoicePlatform.Infrastructure/Invoices/DynamoDbInvoiceRepository.cs`, `src/InvoicePlatform.Application/Invoices/BookInvoiceService.cs`, `src/InvoicePlatform.Domain/Serialization/CanonicalInvoiceXmlSerializer.cs`, `src/InvoicePlatform.Api/Invoices/ExtractInvoiceEndpoint.cs` (severity mapping)
- Test: `tests/InvoicePlatform.Application.Tests/BookInvoiceServiceTests.cs`, `tests/InvoicePlatform.Domain.Tests/CanonicalInvoiceXmlSerializerTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
// BookInvoiceServiceTests
[Fact]
public async Task A_booked_invoice_is_outgoing()
{
    var stored = await _service.BookAsync(OrgA, "u", ADraft(), new byte[] { 1 }, "a.pdf", "m");
    Assert.Equal(InvoiceDirection.Outgoing, stored.Direction);
}

// CanonicalInvoiceXmlSerializerTests
[Fact]
public void Legal_registration_scheme_and_preceding_invoice_are_written()
{
    var xml = new CanonicalInvoiceXmlSerializer().Serialize(new CanonicalInvoiceDraft
    {
        PrecedingInvoiceReference = "F-1",
        Seller = new CanonicalParty { LegalRegistrationId = "12345678", LegalRegistrationScheme = "0106" },
    });
    Assert.Contains("<PrecedingInvoiceReference>F-1</PrecedingInvoiceReference>", xml);
    Assert.Contains("<LegalRegistrationId schemeId=\"0106\">12345678</LegalRegistrationId>", xml);
}
```

- [ ] **Step 2:** run, FAIL.
- [ ] **Step 3: Implement**
  - `StoredInvoice`: `public enum InvoiceDirection { Outgoing }` (comment: incoming invoices are a later piece of work; the field exists so they fit the same store) and `public InvoiceDirection Direction { get; init; } = InvoiceDirection.Outgoing;`
  - Repository: write `["direction"] = new(invoice.Direction.ToString())`; read `Direction = item.TryGetValue("direction", out var d) && Enum.TryParse<InvoiceDirection>(d.S, out var dir) ? dir : InvoiceDirection.Outgoing`.
  - `ValidationSeverity`: add `Info` (doc: "Not a problem: tells the reviewer something was filled in for them.").
  - `CanonicalParty.LegalRegistrationScheme` (BT-30-1 / BT-47-1, ICD code, e.g. "0106" KvK). `CanonicalInvoiceDraft.PrecedingInvoiceReference` (BT-25, the invoice a credit note corrects).
  - Canonical serializer: `LegalRegistrationId` gets attribute `schemeId` when the scheme is set (same pattern as `ElectronicAddress`); `PrecedingInvoiceReference` after `BuyerReference`.
  - `ExtractInvoiceEndpoint.ToWarning`: severity `error` / `warning` / `info`.
- [ ] **Step 4:** green. **Step 5:** commit `Record invoice direction and the fields PEPPOL needs`.

## Task 3: InvoiceTotalsCalculator (Domain)

**Files:**
- Create: `src/InvoicePlatform.Domain/Calculation/InvoiceTotalsCalculator.cs`
- Test: `tests/InvoicePlatform.Domain.Tests/Calculation/InvoiceTotalsCalculatorTests.cs`

The examples here are repeated verbatim in `apps/web/lib/totals.test.ts` (Task 14) so the two implementations are held to the same answers.

- [ ] **Step 1: Failing tests**

```csharp
using InvoicePlatform.Domain.Calculation;
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.Tests.Calculation;

public class InvoiceTotalsCalculatorTests
{
    private static CanonicalInvoiceLine Line(string qty, string price, string cat, string? rate, string? baseQty = null) => new()
    {
        Quantity = decimal.Parse(qty, System.Globalization.CultureInfo.InvariantCulture),
        UnitPrice = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture),
        BaseQuantity = baseQty is null ? null : decimal.Parse(baseQty, System.Globalization.CultureInfo.InvariantCulture),
        VatCategoryCode = cat,
        VatPercentage = rate is null ? null : decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture),
    };

    [Fact]
    public void Example_1_two_rates_vat_per_category_not_per_line()
    {
        // 7.5 x 89.50 = 671.25 ; 1 x 1250.00 ; 2 x 34.95 = 69.90
        var result = InvoiceTotalsCalculator.Apply(new CanonicalInvoiceDraft
        {
            Lines = [Line("1", "1250.00", "S", "21"), Line("7.5", "89.50", "S", "21"), Line("2", "34.95", "S", "9")],
        });

        Assert.Equal([1250.00m, 671.25m, 69.90m], result.Lines.Select(l => l.NetAmount!.Value));
        Assert.Equal(2, result.VatBreakdown.Count);
        Assert.Equal(1921.25m, result.VatBreakdown[0].TaxableAmount);   // S/9 sorts after S/21? no: ordered by category, then rate ascending
        Assert.Equal(1991.15m, result.Totals!.LineExtensionAmount);
        Assert.Equal(1991.15m, result.Totals.TaxExclusiveAmount);
        Assert.Equal(409.75m, result.Totals.TaxAmount);                 // 403.46 + 6.29
        Assert.Equal(2400.90m, result.Totals.TaxInclusiveAmount);
        Assert.Equal(2400.90m, result.Totals.PayableAmount);
    }
    // (exact bucket order assertion fixed when writing: ordered by category code, then rate ascending -> S/9 first)

    [Fact]
    public void Example_2_rounding_half_away_from_zero_and_base_quantity()
    {
        // 3 x 0.335 / 1 = 1.005 -> 1.01 ; 10 x 12.50 per 100 = 1.25
        var result = InvoiceTotalsCalculator.Apply(new CanonicalInvoiceDraft
        {
            Lines = [Line("3", "0.335", "S", "21"), Line("10", "12.50", "S", "21", baseQty: "100")],
        });
        Assert.Equal([1.01m, 1.25m], result.Lines.Select(l => l.NetAmount!.Value));
        Assert.Equal(2.26m, result.Totals!.TaxExclusiveAmount);
        Assert.Equal(0.47m, result.Totals.TaxAmount); // 2.26 x 21% = 0.4746
    }

    [Fact]
    public void Example_3_allowances_and_charges_line_and_document()
    {
        var line = Line("1", "100.00", "S", "21") with
        {
            AllowancesAndCharges = [new CanonicalAllowanceCharge { IsCharge = false, Amount = 10.00m, Reason = "Korting" }],
        };
        var result = InvoiceTotalsCalculator.Apply(new CanonicalInvoiceDraft
        {
            Lines = [line],
            AllowancesAndCharges =
            [
                new CanonicalAllowanceCharge { IsCharge = true, Amount = 5.00m, VatCategoryCode = "S", VatPercentage = 21m, Reason = "Vracht" },
                new CanonicalAllowanceCharge { IsCharge = false, Amount = 2.50m, VatCategoryCode = "S", VatPercentage = 21m, Reason = "Klant" },
            ],
            Totals = new CanonicalTotals { PrepaidAmount = 20.00m, RoundingAmount = 0.01m },
        });

        Assert.Equal(90.00m, result.Lines[0].NetAmount);
        Assert.Equal(90.00m, result.Totals!.LineExtensionAmount);
        Assert.Equal(2.50m, result.Totals.AllowanceTotalAmount);
        Assert.Equal(5.00m, result.Totals.ChargeTotalAmount);
        Assert.Equal(92.50m, result.VatBreakdown.Single().TaxableAmount);
        Assert.Equal(19.43m, result.Totals.TaxAmount);           // 92.50 x 21% = 19.425
        Assert.Equal(111.93m, result.Totals.TaxInclusiveAmount);
        Assert.Equal(91.94m, result.Totals.PayableAmount);       // 111.93 - 20.00 + 0.01
    }

    [Fact]
    public void Example_4_exempt_category_keeps_its_exemption_reason_and_has_no_rate()
    {
        var result = InvoiceTotalsCalculator.Apply(new CanonicalInvoiceDraft
        {
            Lines = [Line("2", "50.00", "E", null)],
            VatBreakdown = [new CanonicalVatBreakdownLine { VatCategoryCode = "E", VatPercentage = 0m, ExemptionReason = "Vrijgesteld" }],
        });
        var bucket = result.VatBreakdown.Single();
        Assert.Equal(("E", 0m, 100.00m, 0.00m, "Vrijgesteld"),
            (bucket.VatCategoryCode, bucket.VatPercentage, bucket.TaxableAmount, bucket.TaxAmount, bucket.ExemptionReason));
    }

    [Fact]
    public void A_line_without_quantity_or_price_keeps_its_net_amount()
    {
        var result = InvoiceTotalsCalculator.Apply(new CanonicalInvoiceDraft
        {
            Lines = [new CanonicalInvoiceLine { NetAmount = 42.00m, VatCategoryCode = "S", VatPercentage = 21m }],
        });
        Assert.Equal(42.00m, result.Totals!.LineExtensionAmount);
    }

    [Fact]
    public void The_input_draft_is_not_mutated()
    {
        var draft = new CanonicalInvoiceDraft { Lines = [Line("1", "1.00", "S", "21")] };
        _ = InvoiceTotalsCalculator.Apply(draft);
        Assert.Null(draft.Lines[0].NetAmount);
        Assert.Null(draft.Totals);
    }
}
```

- [ ] **Step 2:** FAIL. **Step 3: Implement**

```csharp
using InvoicePlatform.Domain.Canonical;

namespace InvoicePlatform.Domain.Calculation;

/// <summary>
/// Derives line nets, the VAT breakdown and the document totals from the
/// lines, allowances and charges, the way EN 16931 defines them: VAT per
/// category and rate over the summed base, amounts rounded to the cent, half
/// away from zero. Prepaid and rounding amounts are inputs, taken from the draft.
///
/// Pure: a new draft is returned, the input is untouched (CLAUDE.md, hard rule 2).
/// The review screen's lib/totals.ts is a port of this class and is tested on
/// the same examples.
/// </summary>
public static class InvoiceTotalsCalculator
{
    private const int MoneyScale = 2;

    public static CanonicalInvoiceDraft Apply(CanonicalInvoiceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var lines = draft.Lines.Select(line => line with { NetAmount = LineNet(line) }).ToList();

        var buckets = new SortedDictionary<(string Category, decimal Rate), decimal>(BucketOrder.Instance);
        foreach (var line in lines)
        {
            Add(buckets, line.VatCategoryCode, line.VatPercentage, line.NetAmount ?? 0m);
        }

        foreach (var adjustment in draft.AllowancesAndCharges)
        {
            Add(buckets, adjustment.VatCategoryCode, adjustment.VatPercentage, Signed(adjustment));
        }

        var breakdown = buckets.Select(bucket => new CanonicalVatBreakdownLine
        {
            VatCategoryCode = bucket.Key.Category,
            VatPercentage = bucket.Key.Rate,
            TaxableAmount = Round(bucket.Value),
            TaxAmount = Round(Round(bucket.Value) * bucket.Key.Rate / 100m),
            ExemptionReason = draft.VatBreakdown.FirstOrDefault(b =>
                b.VatCategoryCode == bucket.Key.Category && (b.VatPercentage ?? 0m) == bucket.Key.Rate)?.ExemptionReason,
        }).ToList();

        var lineExtension = Round(lines.Sum(l => l.NetAmount ?? 0m));
        var allowances = draft.AllowancesAndCharges.Where(a => !a.IsCharge).ToList();
        var charges = draft.AllowancesAndCharges.Where(a => a.IsCharge).ToList();
        decimal? allowanceTotal = allowances.Count == 0 ? null : Round(allowances.Sum(a => a.Amount ?? 0m));
        decimal? chargeTotal = charges.Count == 0 ? null : Round(charges.Sum(a => a.Amount ?? 0m));

        var taxExclusive = Round(lineExtension - (allowanceTotal ?? 0m) + (chargeTotal ?? 0m));
        var tax = Round(breakdown.Sum(b => b.TaxAmount ?? 0m));
        var taxInclusive = Round(taxExclusive + tax);
        var prepaid = draft.Totals?.PrepaidAmount;
        var rounding = draft.Totals?.RoundingAmount;

        return draft with
        {
            Lines = lines,
            VatBreakdown = breakdown,
            Totals = new CanonicalTotals
            {
                LineExtensionAmount = lineExtension,
                AllowanceTotalAmount = allowanceTotal,
                ChargeTotalAmount = chargeTotal,
                TaxExclusiveAmount = taxExclusive,
                TaxAmount = tax,
                TaxInclusiveAmount = taxInclusive,
                PrepaidAmount = prepaid,
                RoundingAmount = rounding,
                PayableAmount = Round(taxInclusive - (prepaid ?? 0m) + (rounding ?? 0m)),
            },
        };
    }

    /// <summary>quantity x price / base quantity, rounded, plus charges minus allowances.</summary>
    private static decimal? LineNet(CanonicalInvoiceLine line)
    {
        if (line.Quantity is not { } quantity || line.UnitPrice is not { } price)
        {
            return line.NetAmount;
        }

        var baseQuantity = line.BaseQuantity is { } b && b != 0m ? b : 1m;
        return Round(Round(quantity * price / baseQuantity) + line.AllowancesAndCharges.Sum(Signed));
    }

    private static decimal Signed(CanonicalAllowanceCharge adjustment)
        => adjustment.IsCharge ? adjustment.Amount ?? 0m : -(adjustment.Amount ?? 0m);

    private static void Add(
        SortedDictionary<(string, decimal), decimal> buckets, string? category, decimal? rate, decimal amount)
    {
        var key = (category ?? string.Empty, rate ?? 0m);
        buckets[key] = buckets.GetValueOrDefault(key) + amount;
    }

    private static decimal Round(decimal value) => Math.Round(value, MoneyScale, MidpointRounding.AwayFromZero);

    /// <summary>Category code ordinal, then rate ascending: deterministic output order.</summary>
    private sealed class BucketOrder : IComparer<(string Category, decimal Rate)>
    {
        public static readonly BucketOrder Instance = new();

        public int Compare((string Category, decimal Rate) x, (string Category, decimal Rate) y)
        {
            var byCategory = string.CompareOrdinal(x.Category, y.Category);
            return byCategory != 0 ? byCategory : x.Rate.CompareTo(y.Rate);
        }
    }
}
```

  Correct Example 1's first bucket assertion to the ordering rule (S/9 before S/21): `VatBreakdown[0]` = S/9, taxable 69.90, tax 6.29; `VatBreakdown[1]` = S/21, taxable 1921.25, tax 403.46.

- [ ] **Step 4:** green. **Step 5:** commit `Add the EN 16931 totals calculator`.

## Task 4: Ruleset core types and registry

**Files:**
- Create: `src/InvoicePlatform.Domain/EInvoicing/{RuleViolation,IInvoiceRuleset,ValidatedCanonicalInvoice,InvoiceRulesetRegistry}.cs`
- Test: `tests/InvoicePlatform.Domain.Tests/EInvoicing/ValidatedCanonicalInvoiceTests.cs`, `InvoiceRulesetRegistryTests.cs`

- [ ] **Step 1: Failing tests** — with a test-only ruleset:

```csharp
private sealed class NeedsNumber : IInvoiceRuleset
{
    public string Id => "test";
    public string DisplayName => "Test";
    public string RulesRelease => "1";
    public InvoiceSyntax Syntax => InvoiceSyntax.Ubl21;
    public string CustomizationId => "c";
    public string ProfileId => "p";
    public IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft d)
        => d.InvoiceNumber is null ? [new("T-1", "BT-1", "invoiceNumber", "Factuurnummer ontbreekt.")] : [];
}

[Fact] public void An_incomplete_invoice_does_not_validate()
{
    var result = ValidatedCanonicalInvoice.Validate(new NeedsNumber(), new CanonicalInvoiceDraft());
    Assert.Null(result.Invoice);
    Assert.Equal("T-1", Assert.Single(result.Violations).RuleId);
}

[Fact] public void A_complete_invoice_validates_carries_its_ruleset_and_calculated_totals()
{
    var result = ValidatedCanonicalInvoice.Validate(new NeedsNumber(), new CanonicalInvoiceDraft
    {
        InvoiceNumber = "F-1",
        Lines = [new CanonicalInvoiceLine { Quantity = 2m, UnitPrice = 5m, VatCategoryCode = "S", VatPercentage = 21m }],
    });
    Assert.NotNull(result.Invoice);
    Assert.Equal("test", result.Invoice!.Ruleset.Id);
    Assert.Equal(12.10m, result.Invoice.Invoice.Totals!.PayableAmount);
}

[Fact] public void Rules_see_the_calculated_invoice_not_the_raw_draft()
{
    // a ruleset asserting Totals is non-null must pass for a draft without totals
}

// Registry
[Fact] public void Requested_ruleset_wins_over_the_organisation_default() { ... Resolve("b", "a") -> b }
[Fact] public void Organisation_default_wins_over_the_system_default() { ... Resolve(null, "b") -> b }
[Fact] public void A_vanished_organisation_default_falls_back_to_the_system_default() { Resolve(null, "gone") -> system }
[Fact] public void An_unknown_requested_ruleset_is_not_silently_replaced() { Resolve("gone", "a") -> null }
[Fact] public void The_system_default_is_peppol_bis_billing_3() { new InvoiceRulesetRegistry().Default.Id == "peppol-bis-billing-3.0" }
```

- [ ] **Step 3: Implement**

```csharp
// RuleViolation.cs
/// <param name="RuleId">Official rule id where one exists ("BR-07", "PEPPOL-EN16931-R003", "NL-R-003").</param>
/// <param name="BusinessTerm">EN 16931 business term, e.g. "BT-49", when the rule is about one field.</param>
/// <param name="Path">Dotted field path; equal to the input id on the review screen.</param>
/// <param name="Message">Dutch, for the reviewer.</param>
public sealed record RuleViolation(string RuleId, string? BusinessTerm, string Path, string Message);

// IInvoiceRuleset.cs
public enum InvoiceSyntax { Ubl21 }

/// <summary>One e-invoicing ruleset: a CIUS or extension of EN 16931, in one syntax.</summary>
public interface IInvoiceRuleset
{
    string Id { get; }
    string DisplayName { get; }
    /// <summary>The release of the official rules this implementation follows, e.g. "3.0.21".</summary>
    string RulesRelease { get; }
    InvoiceSyntax Syntax { get; }
    string CustomizationId { get; }
    string ProfileId { get; }
    /// <summary>Checks an invoice whose totals have already been calculated.</summary>
    IReadOnlyList<RuleViolation> Check(CanonicalInvoiceDraft invoice);
}

// ValidatedCanonicalInvoice.cs
/// <summary>
/// The only invoice an output adapter accepts (CLAUDE.md). Created solely by
/// Validate, so an incomplete invoice cannot reach a writer: that is a compile
/// error, not a check someone forgets. Bound to the ruleset it was checked
/// against - valid for PEPPOL is not valid for anything else.
/// </summary>
public sealed class ValidatedCanonicalInvoice
{
    private ValidatedCanonicalInvoice(CanonicalInvoiceDraft invoice, IInvoiceRuleset ruleset)
    { Invoice = invoice; Ruleset = ruleset; }

    /// <summary>The invoice with calculated line nets, VAT breakdown and totals.</summary>
    public CanonicalInvoiceDraft Invoice { get; }
    public IInvoiceRuleset Ruleset { get; }

    public static InvoiceValidationResult Validate(IInvoiceRuleset ruleset, CanonicalInvoiceDraft draft)
    {
        ArgumentNullException.ThrowIfNull(ruleset);
        ArgumentNullException.ThrowIfNull(draft);
        var calculated = InvoiceTotalsCalculator.Apply(draft);
        var violations = ruleset.Check(calculated);
        return new InvoiceValidationResult(
            violations.Count == 0 ? new ValidatedCanonicalInvoice(calculated, ruleset) : null,
            violations);
    }
}

public sealed record InvoiceValidationResult(ValidatedCanonicalInvoice? Invoice, IReadOnlyList<RuleViolation> Violations);

// InvoiceRulesetRegistry.cs
public sealed class InvoiceRulesetRegistry
{
    private readonly IReadOnlyList<IInvoiceRuleset> _rulesets;
    public InvoiceRulesetRegistry() : this([new PeppolBisBilling30Ruleset()]) { }
    public InvoiceRulesetRegistry(IReadOnlyList<IInvoiceRuleset> rulesets) { _rulesets = rulesets; }
    public IReadOnlyList<IInvoiceRuleset> All => _rulesets;
    /// <summary>The first registered ruleset: PEPPOL BIS Billing 3.0.</summary>
    public IInvoiceRuleset Default => _rulesets[0];
    public IInvoiceRuleset? Find(string? id) => _rulesets.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// The user's choice, else the organisation's default, else the system
    /// default. An explicitly requested id that does not exist returns null, so
    /// the caller can say so; a stored default that no longer exists falls back.
    /// </summary>
    public IInvoiceRuleset? Resolve(string? requested, string? organizationDefault)
        => requested is not null ? Find(requested) : Find(organizationDefault) ?? Default;
}
```

(`PeppolBisBilling30Ruleset` is created in Task 6; until then the registry test file uses the list constructor and the parameterless constructor + its test are added in Task 6.)

- [ ] **Step 4/5:** green; commit `Add the ruleset layer: violations, validated invoice, registry`.

## Task 5: Code lists

**Files:** `src/InvoicePlatform.Domain/EInvoicing/CodeLists.cs`, test `tests/InvoicePlatform.Domain.Tests/EInvoicing/CodeListsTests.cs`

Contents (sources: PEPPOL BIS 3.0 code lists for release 3.0.21, see research notes in Task 6):
- `InvoiceTypeCodes`, `CreditNoteTypeCodes` (PEPPOL subsets of UNCL1001).
- `VatCategories` = S, Z, E, AE, K, G, O, L, M (UNCL5305 subset used by EN 16931) — L and M are listed but rejected by the Dutch-context rule set? No: they are valid codes; rules for them are not implemented, so the PEPPOL ruleset reports them as unsupported (see Task 6).
- `ElectronicAddressSchemes` (EAS list).
- `PaymentMeansCodes` (UNCL4461 codes allowed by PEPPOL).
- `IsCurrencyCode(s)`: three ASCII upper-case letters. `IsCountryCode(s)`: two ASCII upper-case letters. `IsUnitCode(s)`: 1–3 upper-case alphanumerics (UN/ECE Rec 20 shape). Full ISO/Rec 20 lists are deliberately not embedded: the CI validator checks them, and a shape check catches what extraction gets wrong in practice (lower case, "EURO", "stuks").

Tests: one `[Theory]` per list with valid and invalid samples (e.g. `380` valid invoice, `381` credit note only, `999` neither; EAS `0106`, `0208`, `9944` valid, `0000` invalid; `EUR` valid, `eur`/`EURO` invalid; `NL` valid, `NLD` invalid; `C62`, `HUR` valid, `stuks` invalid).

Commit `Add the code lists the rules check`.

## Task 6: EN 16931 core rules, PEPPOL rules, Dutch rules, the PEPPOL ruleset

**Files:**
- Create: `src/InvoicePlatform.Domain/EInvoicing/En16931CoreRules.cs`, `Peppol/PeppolRules.cs`, `Peppol/DutchRules.cs`, `Peppol/PeppolBisBilling30Ruleset.cs`
- Test: `tests/InvoicePlatform.Domain.Tests/EInvoicing/SampleInvoices.cs` (a complete valid Dutch invoice and credit note), `En16931CoreRulesTests.cs`, `PeppolRulesTests.cs`, `DutchRulesTests.cs`, `PeppolBisBilling30RulesetTests.cs`

Shape of every rule class:

```csharp
internal static class En16931CoreRules
{
    public static IEnumerable<RuleViolation> Check(CanonicalInvoiceDraft invoice) { ... yield return ...; }
}
```

Helper `Missing(string? value)` = null or whitespace. Paths match review-screen input ids: `invoiceNumber`, `issueDate`, `typeCode`, `currencyCode`, `seller.name`, `seller.address.countryCode`, `seller.electronicAddress`, `seller.electronicAddressScheme`, `seller.vatIdentifier`, `seller.legalRegistrationId`, `seller.legalRegistrationScheme`, `buyer.*` likewise, `buyerReference`, `precedingInvoiceReference`, `payment.paymentMeansCode`, `payment.iban`, `lines`, `lines[i].quantity|unitCode|unitPrice|description|vatCategoryCode|vatPercentage`, `vatBreakdown[i].exemptionReason`, `allowancesAndCharges[i].reason`.

**Rules implemented at runtime.** Source: the 3.0.21 Schematron (`PEPPOL-EN16931-UBL.sch`, release 3.0.21; `CEN-EN16931-UBL.sch`, artefacts 1.3.16), fetched 2026-10-18. Only `fatal` rules; the warning-level `PEPPOL-COMMON-R054..R057` (Dutch number formats) are not blocking. Each id gets one test `<id>` in a `[Theory]` that starts from `SampleInvoices.Valid()` and breaks exactly that rule, plus one test that the valid sample yields no violations:

- Presence: BR-02, BR-03, BR-04, BR-05, BR-06, BR-07, BR-08/BR-09 (seller address with country), BR-10/BR-11 (buyer), BR-16, BR-22, BR-23, BR-25, BR-26, BR-27 (price not negative), BR-CO-04 (VAT category per line), BR-CO-26 (seller VAT or legal registration), BR-62/BR-63 (endpoint schemes), BR-31/BR-36 and BR-41/BR-43 (allowance/charge amount), BR-32/BR-37 (document allowance/charge VAT category), BR-33/BR-38/BR-42/BR-44 (reason), BR-49 (payment means code when any payment detail is given), BR-61 (account id for 30 and 58), BR-CO-09 (VAT identifiers start with a country prefix), BR-DEC-01/02/05/06/24/25/27/28 (allowance/charge amounts and bases ≤ 2 decimals).
- VAT categories (lines and document allowances/charges): BR-S-02 and BR-Z-02, BR-E-02, BR-AE-02, BR-G-02 (identifiers); BR-S-05/06/07, BR-Z-05, BR-E-05, BR-AE-05, BR-G-05 (rates); BR-O-02 (no VAT identifiers), BR-O-05 (no rate), BR-O-11/12 (O stands alone); BR-S-10, BR-Z-10 (no exemption reason); BR-E-10, BR-AE-10, BR-G-10, BR-O-10 (exemption reason required). Categories K (needs delivery date and deliver-to country, BR-IC-11/12), L, M and B: `XC-VAT-UNSUPPORTED`, because the model does not carry the data they need.
- PEPPOL: R003 (buyer reference or order reference), R010, R020 (electronic addresses), R040 (amount = base × % / 100, slack 0.02), R041/R042 (base and percentage together), R061 (direct debit 49/59 needs a mandate the model lacks → reported as unsupported under R061), R121 (base quantity > 0), F001 (dates `YYYY-MM-DD`), P0100/P0101 (type-code subsets), P0112 (326/384 only DE–DE), CL007 (currency shape), CL008 (EAS list).
- Dutch, when the seller's postal country is NL: NL-R-001 (credit note needs preceding invoice), NL-R-002 (seller street, city, postcode), NL-R-003 (seller registration scheme 0106/0190 when a registration number is given), NL-R-004/NL-R-005 (same for an NL buyer), NL-R-007 (payment means unless nothing is payable), NL-R-008 (NL buyer: code in 30, 48, 49, 57, 58, 59). NL-R-006 and NL-R-009 concern a tax representative and order line references, which the model does not have.

Invoice type codes (P0100): 71 80 82 84 102 218 219 326 331 380 382 383 384 386 388 393 395 553 575 623 780 817 870 875 876 877. Credit note codes (P0101): 81 83 381 396 532. EAS list: as in the 3.0.21 code list (0217 removed).

`PeppolBisBilling30Ruleset.Check` = core ∪ PEPPOL ∪ (seller is NL ? Dutch : ∅), de-duplicated on (RuleId, Path), ordered by path then rule id for a stable list.

Commit per rule family: `Add EN 16931 core rules`, `Add PEPPOL and Dutch rules and the PEPPOL ruleset`.

## Task 7: UBL 2.1 writer and reference files

**Files:**
- Create: `src/InvoicePlatform.Domain/EInvoicing/Ubl/UblInvoiceWriter.cs`
- Test: `tests/InvoicePlatform.Domain.Tests/EInvoicing/Ubl/UblInvoiceWriterTests.cs`, `Expected/invoice-nl-standard.xml`, `Expected/creditnote-nl.xml`, `Expected/invoice-nl-mixed-vat-allowances.xml`
- Modify: `tests/InvoicePlatform.Domain.Tests/InvoicePlatform.Domain.Tests.csproj` — `<None Update="EInvoicing\Ubl\Expected\*.xml" CopyToOutputDirectory="PreserveNewest" />`

Element order follows the PEPPOL BIS 3.0 UBL syntax tree (research task). Writer rules:
- `string Write(ValidatedCanonicalInvoice invoice)`; root `Invoice` or `CreditNote` (type 381 and the other credit-note codes), namespaces `urn:oasis:names:specification:ubl:schema:xsd:Invoice-2` / `...:CreditNote-2`, `cac`, `cbc` prefixes.
- `CustomizationID`/`ProfileID` from `invoice.Ruleset`.
- Every amount: `currencyID` attribute, `F2` invariant formatting; quantities and prices: invariant, as held.
- Item name = line description (spec, BT-153); BT-154 not written.
- `cbc:Note`, `BuyerReference`, `OrderReference`, `BillingReference` (credit note), parties (EndpointID@schemeID, PartyIdentification omitted, PostalAddress, PartyTaxScheme VAT, PartyLegalEntity RegistrationName + CompanyID@schemeID), PaymentMeans (code, PaymentID, PayeeFinancialAccount ID/Name/FinancialInstitutionBranch), AllowanceCharge (document), TaxTotal + TaxSubtotal per bucket (TaxCategory ID, Percent unless O, TaxExemptionReason, TaxScheme VAT), LegalMonetaryTotal, lines (ID = LineId or ordinal, InvoicedQuantity/CreditedQuantity@unitCode, LineExtensionAmount, line AllowanceCharge, Item Name + SellersItemIdentification + ClassifiedTaxCategory, Price PriceAmount + BaseQuantity@unitCode).
- Whitespace-only strings treated as absent (PEPPOL-EN16931-R008: no empty elements).
- Deterministic: `XmlWriterSettings` as in the canonical serializer (LF, two spaces, UTF-8 without BOM).

Tests:

```csharp
[Theory]
[InlineData("invoice-nl-standard.xml")]
[InlineData("creditnote-nl.xml")]
[InlineData("invoice-nl-mixed-vat-allowances.xml")]
public void Output_matches_the_reference_file(string file)
{
    var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "EInvoicing", "Ubl", "Expected", file)).ReplaceLineEndings("\n");
    Assert.Equal(expected, Write(SampleInvoices.ForReferenceFile(file)));
}

[Fact] public void Every_amount_has_a_currency_and_two_decimals() { /* XPath over all *Amount elements */ }
[Fact] public void Special_characters_are_escaped() { /* name "A & B <C>" round-trips through XDocument.Parse */ }
[Fact] public void Absent_fields_are_omitted_not_defaulted() { /* no BuyerReference element when only order ref set */ }
[Fact] public void A_credit_note_uses_the_credit_note_root_and_credited_quantity() { }
[Fact] public void Identifiers_come_from_the_ruleset() { /* CustomizationID == ruleset.CustomizationId */ }
```

Reference files are produced once by the writer, inspected by hand, then validated with Task 8's script before being committed.

Commit `Add the UBL 2.1 writer`.

## Task 8: Official validation in CI

**Files:**
- Create: `tools/peppol-validation/validate.sh`, `tools/peppol-validation/README.md`
- Modify: `.github/workflows/deploy-api.yml` (step after Test)

`validate.sh <xml>...`: downloads into `tools/peppol-validation/.cache` (gitignored), each pinned and SHA-256 verified:
- Saxon-HE 9.9.1-8 from Maven Central (runs XSLT 2.0 on Java 8 and later, so the same script works on a laptop with Java 8 and on the runner's Java 17);
- CEN rules: the official compiled `EN16931-UBL-validation.xslt` from `https://github.com/ConnectingEurope/eInvoicing-EN16931/releases/download/validation-1.3.16/en16931-ubl-1.3.16.zip` (1.3.16 is the version PEPPOL 3.0.21 adopts);
- PEPPOL rules: OpenPeppol publishes only Schematron for 3.0.21, no compiled XSLT. The compiled `PEPPOL-EN16931-UBL.xslt` is taken from `phive-rules-peppol` 4.6.3 on Maven Central (`external/schematron/openpeppol/2026.5/xslt/`), whose rule texts were checked identical to the 3.0.21 Schematron.

It runs both XSLTs on each file to SVRL, prints every failed assert with flag, id and text, and exits non-zero if any is `fatal`.

Workflow step:

```yaml
      - name: Setup Java
        uses: actions/setup-java@v4
        with:
          distribution: temurin
          java-version: '17'

      # Our reference UBL files against the official PEPPOL BIS 3.0.21 rules.
      # The writer tests require the writer's output to equal these files, so a
      # pass here means the writer produces valid documents for those invoices.
      - name: Validate UBL reference files against PEPPOL BIS 3.0.21
        run: tools/peppol-validation/validate.sh tests/InvoicePlatform.Domain.Tests/EInvoicing/Ubl/Expected/*.xml
```

Run it locally against the reference files before committing them. Commit `Validate UBL reference files against the official PEPPOL rules in CI`.

## Task 9: Organisation settings (Application + Infrastructure)

**Files:**
- Create: `src/InvoicePlatform.Application/Organizations/OrganizationSettings.cs`, `OrganizationSettingsService.cs`
- Modify: `src/InvoicePlatform.Infrastructure/Identity/DynamoDbIdentityStore.cs`, `DependencyInjection.cs`
- Test: `tests/InvoicePlatform.Application.Tests/OrganizationSettingsServiceTests.cs`, `tests/InvoicePlatform.Application.Tests/Fakes.cs` (move `FakeInvoiceStore`/`TestClock` here, add `FakeSettingsStore`)

```csharp
// OrganizationSettings.cs
public sealed record OrganizationSettings
{
    public required string OrganizationId { get; init; }
    /// <summary>Null means the system default (PEPPOL BIS Billing 3.0).</summary>
    public string? DefaultRulesetId { get; init; }
    /// <summary>The organisation's own electronic address (BT-34) and its EAS scheme.</summary>
    public string? EndpointId { get; init; }
    public string? EndpointScheme { get; init; }
}

public interface IOrganizationSettingsStore
{
    /// <returns>The stored settings, or null when none were ever saved.</returns>
    Task<OrganizationSettings?> GetAsync(string organizationId, CancellationToken cancellationToken = default);
    Task SaveAsync(OrganizationSettings settings, CancellationToken cancellationToken = default);
}
```

`OrganizationSettingsService(IOrganizationSettingsStore, InvoiceRulesetRegistry)`:
- `GetAsync(org)` → stored or `new() { OrganizationId = org }`.
- `UpdateAsync(org, defaultRulesetId, endpointId, endpointScheme)` → `SettingsUpdateResult` (`Succeeded`, `Error`): unknown non-null ruleset → error "Onbekende regelset."; endpoint id without scheme or scheme not in EAS → error; trims values, empty → null; saves with `OrganizationId = org` (never from input).

Tests: defaults when nothing stored; update round-trips; unknown ruleset refused; scheme required with endpoint; organisation B does not see A's settings.

DynamoDB: item `pk=ORG#<org>`, `sk=SETTINGS`, attributes `defaultRulesetId`, `endpointId`, `endpointScheme` (NULL when absent). Register `IOrganizationSettingsStore` → the shared `DynamoDbIdentityStore` singleton; `InvoiceRulesetRegistry` singleton; `OrganizationSettingsService` scoped.

Commit `Add organisation settings`.

## Task 10: Enrichment and export use cases

**Files:**
- Create: `src/InvoicePlatform.Application/Invoices/InvoiceEnrichment.cs`, `ExportInvoiceService.cs`
- Modify: `ExtractInvoiceService.cs` (signature `ExtractAsync(string organizationId, ReadOnlyMemory<byte> pdf, ...)`; enrich after interpretation, notices appended to warnings as `Info`)
- Test: `tests/InvoicePlatform.Application.Tests/InvoiceEnrichmentTests.cs`, `ExportInvoiceServiceTests.cs`

```csharp
/// <summary>
/// Fills empty fields from what the organisation already told us. Only empty
/// fields: a value read from the document is never overwritten.
/// </summary>
public sealed class InvoiceEnrichment(IOrganizationSettingsStore settings)
{
    public async Task<EnrichmentResult> EnrichAsync(string organizationId, CanonicalInvoiceDraft draft, CancellationToken ct = default)
    // seller.electronicAddress + scheme from settings when both empty -> notice
    //   ValidationWarning("ENRICH-ORG", "seller.electronicAddress", "Elektronisch adres van de verkoper ingevuld uit de organisatie-instellingen.", ValidationSeverity.Info)
}

public sealed record EnrichmentResult(CanonicalInvoiceDraft Draft, IReadOnlyList<ValidationWarning> Notices);
```

`ExportInvoiceService(IInvoiceRepository, IOrganizationSettingsStore, InvoiceRulesetRegistry, InvoiceEnrichment, UblInvoiceWriter)`:

```csharp
public Task<CheckOutcome> CheckAsync(string organizationId, string rulesetId, CanonicalInvoiceDraft draft, CancellationToken ct = default);
// unknown ruleset -> CheckOutcome.UnknownRuleset; else enrich, validate, return violations

public Task<ExportOutcome> ExportAsync(string organizationId, string invoiceId, string? requestedRulesetId, CancellationToken ct = default);
// resolve ruleset (registry.Resolve(requested, settings?.DefaultRulesetId)) -> null => UnknownRuleset
// load invoice (organisation-scoped) -> null => NotFound
// enrich, validate -> violations => Incomplete(violations)
// write -> Exported(FileName, Content, MediaType "application/xml")
// file name: "<invoiceNumber>-<ruleset id>.xml", characters outside [A-Za-z0-9._-] replaced by "-"
```

Outcomes as small records with a `Kind` enum (`Exported`, `Incomplete`, `NotFound`, `UnknownRuleset`), mirroring how `AuthResult` reports failures without exceptions.

Tests: org default used when no ruleset requested; explicit ruleset wins; unknown explicit ruleset → UnknownRuleset; other organisation's invoice → NotFound; incomplete → Incomplete with violations; complete → file named `F-2026-041-peppol-bis-billing-3.0.xml` whose content starts with `<?xml` and contains `<cbc:ID>F-2026-041</cbc:ID>`; seller endpoint from settings fills an empty one; check never stores anything.

Commit `Add the check and export use cases`.

## Task 11: API endpoints and signed-in integration tests

**Files:**
- Create: `src/InvoicePlatform.Api/Organizations/OrganizationEndpoints.cs`, `src/InvoicePlatform.Api/Invoices/ExportEndpoints.cs`, `src/InvoicePlatform.Contracts/Invoices/RulesetResponses.cs`
- Modify: `Program.cs` (map endpoints), `ExtractInvoiceEndpoint.cs` (organisation from session)
- Test: `tests/InvoicePlatform.Integration.Tests/SignedInFactory.cs`, `OrganizationEndpointTests.cs`, `ExportEndpointTests.cs`; extend `StoredInvoiceEndpointTests` theory with the new GET routes requiring login

`SignedInFactory : WebApplicationFactory<Program>` replaces identity, invoice, document and settings stores with in-memory fakes. Tokens: `admin-a` (Admin, org-a), `user-a` (User, org-a), `readonly-a` (ReadOnly, org-a), `admin-b` (Admin, org-b). Helper `HttpClient As(string token)`. Exposes the in-memory invoice store so a test can seed a booked invoice.

Contracts:

```csharp
public sealed record RulesetInfo { [JsonPropertyName("id")] public required string Id { get; init; } [JsonPropertyName("displayName")] ... [JsonPropertyName("rulesRelease")] ... }
public sealed record OrganizationSettingsResponse { defaultRulesetId, endpointId, endpointScheme, rulesets: RulesetInfo[] }
public sealed record UpdateOrganizationSettingsRequest { defaultRulesetId, endpointId, endpointScheme }
public sealed record RuleViolationResponse { ruleId, businessTerm, path, message }
public sealed record CheckResponse { violations: RuleViolationResponse[] }
```

Endpoints (guards per endpoint, never on the group; body read inside the handler as `BookAsync` does, so a 401 is never preceded by a 400):

```
GET  /organization/settings          RequireAuthenticatedUser
PUT  /organization/settings          RequireAuthenticatedUser + RequireRole(Admin)
POST /rulesets/{rulesetId}/check     RequireAuthenticatedUser
GET  /invoices/{invoiceId}/export    RequireAuthenticatedUser   (?ruleset=)
```

Export 200: `Results.File(bytes, "application/xml", fileName)`. 422: `Results.Json(new CheckResponse{...}, statusCode: 422)`.

Tests:
- anonymous → 401 on all four;
- `user-a` PUT settings → 403; `admin-a` PUT then GET round-trips; `admin-a` PUT unknown ruleset → 400;
- check with an empty draft → 200 with violations including `BR-02`; unknown ruleset → 404;
- export of a seeded complete invoice → 200, `Content-Disposition` filename, body starts with `<?xml`;
- export of a seeded incomplete invoice → 422 with violations;
- export of org-a's invoice as `admin-b` → 404;
- `readonly-a` may export (200).

Commit `Expose settings, check and export over HTTP`.

## Task 12: Frontend decimal and totals modules with tests

**Files:**
- Create: `apps/web/lib/decimal.ts`, `apps/web/lib/decimal.test.ts`, `apps/web/lib/totals.ts`, `apps/web/lib/totals.test.ts`
- Modify: `apps/web/package.json` (`"test": "node --test \"lib/**/*.test.ts\""`), `apps/web/tsconfig.json` (`"exclude": ["node_modules", "**/*.test.ts"]`), `.github/workflows/deploy-ui.yml` (`npm test` after Lint)

`decimal.ts` (no imports, BigInt): `parseDecimal(s): Dec | null` (`{ units: bigint; scale: number }`, accepts `-?\d+(\.\d+)?`), `toFixed(d, scale)` (round half away from zero), `add`, `sub`, `mul`, `divRound(a, b, scale)`, `percentOf(amount, rate)` (amount × rate / 100 rounded to 2), `format(d)` (`"1250.00"`), `isZero`, `compare`.

`totals.ts` imports `./decimal.ts` (explicit extension — required by Node's type stripping; `allowImportingTsExtensions` is not needed because Next's `moduleResolution: bundler` accepts it? → if `tsc` rejects it, add `"allowImportingTsExtensions": true` to tsconfig, which is permitted with `noEmit`). `calculateTotals(invoice: CanonicalInvoice): CalculatedTotals` returns `{ lineNet: (string|null)[], vatBreakdown: {category, rate, taxable, tax}[], lineExtension, allowanceTotal, chargeTotal, taxExclusive, tax, taxInclusive, payable }` all as strings; same algorithm and ordering as `InvoiceTotalsCalculator`.

`totals.test.ts` repeats Examples 1–4 and the "no quantity keeps net" case from Task 3 with identical numbers. `decimal.test.ts` covers parsing, half-away rounding (`1.005 → 1.01`, `-1.005 → -1.01`), and that `0.1 + 0.2 = 0.3` exactly.

Run `cd apps/web && npm test`. Commit `Add exact decimal arithmetic and the totals port to the frontend`.

## Task 13: Frontend canonical model = server model

**Files:**
- Rewrite: `apps/web/lib/canonical.ts`; Modify: `apps/web/lib/api.ts`, `apps/web/lib/store.ts`, `apps/web/lib/invoices.ts`, `apps/web/app/upload/page.tsx`, `apps/web/app/invoices/InvoiceListClient.tsx`

`canonical.ts` exports the canonical types mirroring C# (camelCase; every decimal `string | null`; `typeCode`, `invoiceNumber`, …, `precedingInvoiceReference`, `seller`/`buyer` with `legalRegistrationScheme`, `evidence`), plus:
- `ReviewDraft { id; invoice: CanonicalInvoice; source: { fileName; byteSize; engine; extractedAt } }`
- `normalizeInvoice(raw: unknown): CanonicalInvoice` — fills missing arrays/objects with empty values so the screen never reads `undefined`; used for API responses and stored invoices.
- `isReviewDraft(value)` — shape guard for `sessionStorage` (old prototype drafts return false and are ignored).
- `localIssues(invoice)` — only what must be fixed before the server can read the draft: every decimal field parses (`BR-DEC`-style messages "Regel 2: aantal is geen geldig getal."). Everything else comes from the server check.
- `toCanonicalXml(invoice)` — port of `CanonicalInvoiceXmlSerializer` (same element names and order, values as held, absent fields omitted, totals from `calculateTotals`).
- `mockDraft(invoiceId, fileName, byteSize)` — the existing mock data in the new shape (seller = Green IT Solutions as the organisation, buyer = Van Dijk Techniek).
- `setAtPath(invoice, path, value)` / `getAtPath(invoice, path)` — immutable update by the same dotted paths the rules use (`seller.address.countryCode`, `lines[1].vatCategoryCode`); creates intermediate objects (`address`, `payment`) on first write.

`api.ts`: `ApiValidationWarning.severity` adds `"info"`, values become `string | null`; `toDraft(response, fileName, byteSize): ReviewDraft` = `normalizeInvoice(response.canonicalInvoice)` + source.
`invoices.ts`: `exportInvoice(invoiceId, rulesetId)` → `{ kind: "file", blob, fileName } | { kind: "incomplete", violations }`; `checkInvoice(rulesetId, invoice)` → violations; book sends `canonicalInvoice: draft.invoice`.
`store.ts`: `loadDraft` returns null unless `isReviewDraft`.
Invoice list: column "Afnemer" (`buyer.name`), amount string shown via `formatAmountString`.

`npm run lint` clean. Commit `Use the server's canonical model in the browser`.

## Task 14: Review screen

**Files:** rewrite `ReviewClient.tsx`; create `fields.tsx`, `PartyCard.tsx`, `LinesCard.tsx`, `PaymentCard.tsx`, `TotalsCard.tsx`, `ExportPanel.tsx` in `apps/web/app/invoices/[invoiceId]/review/`; `apps/web/lib/settings.ts`.

- `fields.tsx`: `TextField({ path, label, invoice, onChange, highlight })` renders `<input id={path}>` bound through `getAtPath`/`setAtPath`, empty input ↔ `null`; `SelectField` likewise with options; a field whose path has a violation gets class `form-input--invalid` (add to `styles/xchange.css` with the existing danger token).
- `PartyCard` (title "Verkoper (uw organisatie)" / "Afnemer"): name, VAT, KvK + scheme select (`0106` KvK, `0190` OIN, empty), electronic address + scheme select (EAS list incl. `0106`, `0190`, `9944` NL VAT, `0208`, `0088`, `9925`), street, postcode, city, country.
- Invoice card adds: type select (380 Factuur, 381 Creditnota, others shown if present), buyer reference, purchase order reference, preceding invoice (when 381).
- `LinesCard`: description, quantity, unit (select common Rec 20: C62 stuk, HUR uur, DAY dag, KGM kg, MTR meter, LTR liter, plus current value), price, VAT category select (S, Z, E, AE, G, O), VAT %, net (calculated, read-only).
- `PaymentCard`: payment means select (30 Overschrijving, 58 SEPA-overschrijving, 48 Kaart, 49 Incasso, 59 SEPA-incasso, 57 Doorlopende opdracht), IBAN, BIC, account name, payment reference.
- `TotalsCard`: from `calculateTotals`; document allowances/charges listed read-only when present; exemption reason input per exempt bucket (`vatBreakdown[i].exemptionReason`, stored back into `invoice.vatBreakdown` matched on category and rate).
- `ExportPanel`: format select (rulesets from settings, then "Canonieke XML"), initial = organisation default; "Aanvullen voor <ruleset>" block with the check endpoint's violations (debounced 600 ms on draft change, aborted when superseded), each violation a button that focuses `document.getElementById(path)`; note "Gecontroleerd op verplichte velden en kernregels (release 3.0.21)."; download button: canonical → local XML; ruleset → disabled with "Eerst boeken" when not booked or dirty, disabled while violations exist, else `exportInvoice` and save the blob.
- `ReviewClient`: state `draft: ReviewDraft`, `bookedSnapshot: string | null` (JSON of the invoice as last booked or loaded from the server); on mount, `getInvoice(id)` quietly to learn whether it is booked (and load it when there is no local draft); `dirty = JSON.stringify(draft.invoice) !== bookedSnapshot`; booking sends `draft.invoice` and sets the snapshot; extraction notices (`info`) and warnings listed in the Waarschuwingen tab.

`npm run lint && npm run build`. Commit `Add PEPPOL fields, the Aanvullen block and the format menu to the review screen`.

## Task 15: Settings page

**Files:** `apps/web/app/settings/page.tsx`, `SettingsClient.tsx`; modify `components/nav.ts` (Beheer → Instellingen `href: "/settings"`, not hidden), `AppShell.tsx` (`activeNavId` + page title "Instellingen").

Form: default export format (rulesets from GET), own electronic address + scheme. Non-admins see the values read-only with the note "Alleen een beheerder kan dit wijzigen." Save → PUT, toast.

`npm run lint && npm run build`. Commit `Add the settings page`.

## Task 16: Verify end to end

- [ ] `dotnet build` and `dotnet test` green.
- [ ] `tools/peppol-validation/validate.sh` on the reference files: no fatal asserts.
- [ ] `cd apps/web && npm test && npm run lint && npm run build` green.
- [ ] Run the frontend in mock mode (`npm run dev`) and click through upload → review → canonical XML download; check the review screen renders the new cards without console errors.
