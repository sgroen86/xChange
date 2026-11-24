# Company profile — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An organisation enters its own seller details once (legal name, VAT, KvK/KBO, address, contact, bank account, PEPPOL id) on a "Company profile" page with a tab per format, and xChange puts them on every PDF it reads.

**Architecture:** Spec `docs/superpowers/specs/2026-11-24-bedrijfsprofiel-design.md`. A new record `ORG#<org>` / `COMPANY_PROFILE` in `xchange-identity` (next to `SETTINGS`), with a `General` block shared by every format and a `Peppol` block. Domain owns the profile type and its checks; Application owns the store interface, `OrganizationProfileService` (with the fallback to the PEPPOL id still under Settings) and the seller filling in `InvoiceEnrichment`. Reading a PDF uses `SellerFill.ProfileWins`; checking and exporting use `SellerFill.OnlyEmpty`, so a booked invoice never changes.

**Tech Stack:** .NET 10, xUnit, DynamoDB (AWS SDK), ASP.NET Core minimal APIs, Next.js 16 static export, `node:test`.

**Branch:** `feature/bedrijfsprofiel` (the spec is committed there).

---

## File map

| File | Change | Responsibility |
|---|---|---|
| `src/InvoicePlatform.Domain/Organizations/OrganizationProfile.cs` | create | the profile records |
| `src/InvoicePlatform.Domain/Organizations/OrganizationProfileRules.cs` | create | normalise + check before saving |
| `src/InvoicePlatform.Domain/Organizations/ProfileRuleText.cs` | create | NL/EN messages of the checks |
| `src/InvoicePlatform.Domain/Organizations/Iban.cs` | create | ISO 13616 mod-97 check |
| `src/InvoicePlatform.Domain/EInvoicing/Peppol/VatNumberShapes.cs` | modify | expose `Fits` and `Mod97` inside Domain |
| `src/InvoicePlatform.Application/Organizations/IOrganizationProfileStore.cs` | create | persistence interface |
| `src/InvoicePlatform.Application/Organizations/OrganizationProfileService.cs` | create | get (with legacy fallback), update, seller source |
| `src/InvoicePlatform.Application/Organizations/OrganizationSettings.cs` | modify | mark the endpoint fields as legacy |
| `src/InvoicePlatform.Application/Organizations/OrganizationSettingsService.cs` | modify | only the default format; keep a legacy PEPPOL id |
| `src/InvoicePlatform.Application/Organizations/OrganizationText.cs` | modify | drop the endpoint messages |
| `src/InvoicePlatform.Application/Invoices/SellerFromProfile.cs` | create | fills the seller from the profile |
| `src/InvoicePlatform.Application/Invoices/InvoiceEnrichment.cs` | modify | uses the profile; `SellerFill` |
| `src/InvoicePlatform.Application/Invoices/EnrichmentText.cs` | modify | profile notices |
| `src/InvoicePlatform.Application/Invoices/ExtractInvoiceService.cs` | modify | reads with `SellerFill.ProfileWins` |
| `src/InvoicePlatform.Infrastructure/Identity/DynamoDbIdentityStore.cs` | modify | implements `IOrganizationProfileStore` |
| `src/InvoicePlatform.Infrastructure/Identity/DynamoDbOrganizationIdentityDeletion.cs` | modify | deletes the profile with the settings |
| `src/InvoicePlatform.Infrastructure/DependencyInjection.cs` | modify | registrations |
| `src/InvoicePlatform.Contracts/Organizations/OrganizationProfileContracts.cs` | create | wire DTOs |
| `src/InvoicePlatform.Contracts/Invoices/RulesetContracts.cs` | modify | settings lose the endpoint fields |
| `src/InvoicePlatform.Api/Organizations/ProfileEndpoints.cs` | create | `GET`/`PUT /organization/profile` |
| `src/InvoicePlatform.Api/Organizations/OrganizationEndpoints.cs` | modify | settings without endpoint |
| `src/InvoicePlatform.Api/Organizations/OrganizationApiText.cs` | modify | `ProfileNotSaved` title |
| `src/InvoicePlatform.Api/Program.cs` | modify | map the profile endpoints |
| `apps/web/lib/profile.ts`, `apps/web/lib/profileForm.ts` | create | API client; pure form helpers |
| `apps/web/lib/i18n/profile.ts` | create | texts |
| `apps/web/app/profile/page.tsx`, `ProfileClient.tsx` | create | the page with tabs |
| `apps/web/app/settings/SettingsClient.tsx`, `apps/web/lib/settings.ts`, `apps/web/lib/i18n/settings.ts` | modify | PEPPOL id leaves Settings |
| `apps/web/components/nav.ts`, `AppShell.tsx`, `apps/web/lib/i18n/shell.ts`, `apps/web/lib/i18n/index.ts` | modify | menu item and page title |
| tests (per task) | create/modify | see each task |

---

## Task 1: The profile and its checks (Domain)

**Files:**
- Create: `src/InvoicePlatform.Domain/Organizations/OrganizationProfile.cs`
- Create: `src/InvoicePlatform.Domain/Organizations/Iban.cs`
- Create: `src/InvoicePlatform.Domain/Organizations/ProfileRuleText.cs`
- Create: `src/InvoicePlatform.Domain/Organizations/OrganizationProfileRules.cs`
- Modify: `src/InvoicePlatform.Domain/EInvoicing/Peppol/VatNumberShapes.cs`
- Test: `tests/InvoicePlatform.Domain.Tests/Organizations/OrganizationProfileRulesTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/InvoicePlatform.Domain.Tests/Organizations/OrganizationProfileRulesTests.cs`:

```csharp
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Domain.Tests.Organizations;

public class OrganizationProfileRulesTests
{
    private static OrganizationProfile With(CompanyDetails? general = null, PeppolDetails? peppol = null)
        => new() { OrganizationId = "org-a", General = general ?? new(), Peppol = peppol ?? new() };

    /// <summary>The fields with an error, after the same normalising the service does.</summary>
    private static IReadOnlyList<string> Errors(OrganizationProfile profile, string? country = "NL")
        => OrganizationProfileRules.Check(OrganizationProfileRules.Normalize(profile), country).Select(e => e.Field).ToList();

    [Fact]
    public void An_empty_profile_is_valid()
        => Assert.Empty(Errors(With()));

    [Fact]
    public void Normalising_trims_compacts_and_empties()
    {
        var profile = OrganizationProfileRules.Normalize(With(
            new CompanyDetails
            {
                LegalName = "  Groen B.V. ",
                VatIdentifier = "nl 1234.56.789.b01",
                LegalRegistrationId = "1234 5678",
                LegalRegistrationScheme = "0106",
                Iban = "nl91 abna 0417 1643 00",
                Bic = " abnanl2a ",
                StreetName = "   ",
            },
            new PeppolDetails { EndpointId = " 12345678 ", EndpointScheme = "0106" }));

        Assert.Equal("Groen B.V.", profile.General.LegalName);
        Assert.Equal("NL123456789B01", profile.General.VatIdentifier);
        Assert.Equal("12345678", profile.General.LegalRegistrationId);
        Assert.Equal("NL91ABNA0417164300", profile.General.Iban);
        Assert.Equal("ABNANL2A", profile.General.Bic);
        Assert.Null(profile.General.StreetName);
        Assert.Equal("12345678", profile.Peppol.EndpointId);
    }

    [Fact]
    public void A_scheme_without_a_value_is_dropped()
    {
        var profile = OrganizationProfileRules.Normalize(With(
            new CompanyDetails { LegalRegistrationScheme = "0106" },
            new PeppolDetails { EndpointScheme = "0106" }));

        Assert.Null(profile.General.LegalRegistrationScheme);
        Assert.Null(profile.Peppol.EndpointScheme);
    }

    [Theory]
    [InlineData("NL123456789B01", "NL", true)]
    [InlineData("NL12345678B01", "NL", false)]
    [InlineData("BE0123456749", "BE", true)]
    [InlineData("BE0123456748", "BE", false)]
    [InlineData("BE0123456749", "NL", false)] // a Dutch organisation with a Belgian number
    [InlineData("M90000000X", "SG", true)]    // no prefix rule outside NL, BE and GB
    public void The_vat_number_has_its_countrys_shape(string vat, string country, bool valid)
        => Assert.Equal(valid, !Errors(With(new CompanyDetails { VatIdentifier = vat }), country).Contains("general.vatIdentifier"));

    [Theory]
    [InlineData("12345678", "0106", null)]
    [InlineData("1234567", "0106", "general.legalRegistrationId")]
    [InlineData("0123456749", "0208", null)]
    [InlineData("0123456748", "0208", "general.legalRegistrationId")]
    [InlineData("12345678", null, "general.legalRegistrationScheme")]
    [InlineData("12345678", "12", "general.legalRegistrationScheme")]
    public void The_registration_number_fits_its_scheme(string id, string? scheme, string? error)
    {
        var errors = Errors(With(new CompanyDetails { LegalRegistrationId = id, LegalRegistrationScheme = scheme }));

        if (error is null)
        {
            Assert.Empty(errors);
        }
        else
        {
            Assert.Equal(new[] { error }, errors);
        }
    }

    [Theory]
    [InlineData("NL91ABNA0417164300", true)]
    [InlineData("BE68 5390 0754 7034", true)]
    [InlineData("NL91ABNA0417164301", false)]
    [InlineData("NOTANIBAN", false)]
    public void An_iban_passes_the_mod_97_check(string iban, bool valid)
        => Assert.Equal(valid, !Errors(With(new CompanyDetails { Iban = iban })).Contains("general.iban"));

    [Theory]
    [InlineData("30", true)]
    [InlineData("58", true)]
    [InlineData("98", false)]
    public void The_payment_means_is_a_uncl_4461_code(string code, bool valid)
        => Assert.Equal(valid, !Errors(With(new CompanyDetails { PaymentMeansCode = code })).Contains("general.paymentMeansCode"));

    [Theory]
    [InlineData("0106", true)]
    [InlineData(null, false)]
    [InlineData("0217", false)]
    public void A_peppol_id_needs_a_valid_scheme(string? scheme, bool valid)
        => Assert.Equal(valid, !Errors(With(peppol: new PeppolDetails { EndpointId = "12345678", EndpointScheme = scheme })).Contains("peppol.endpointScheme"));

    [Fact]
    public void Iban_is_checked_on_its_own()
    {
        Assert.True(Iban.IsValid("NL91ABNA0417164300"));
        Assert.False(Iban.IsValid("nl91abna0417164300")); // the check takes the compacted, upper-case form
        Assert.False(Iban.IsValid(null));
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

Run: `dotnet test tests/InvoicePlatform.Domain.Tests --filter "FullyQualifiedName~OrganizationProfileRulesTests"`
Expected: build error, `The type or namespace name 'OrganizationProfile' could not be found`.

- [ ] **Step 3: Add the profile records**

`src/InvoicePlatform.Domain/Organizations/OrganizationProfile.cs`:

```csharp
namespace InvoicePlatform.Domain.Organizations;

/// <summary>
/// What an organisation tells xChange about itself as the seller on its
/// invoices. <see cref="General"/> is shared by every e-invoice format (they all
/// specialise EN 16931); a network or format that asks for more gets a block of
/// its own, PEPPOL first. The country is not here: it is the organisation's
/// (<see cref="Organization.CountryCode"/>).
/// </summary>
public sealed record OrganizationProfile
{
    public required string OrganizationId { get; init; }

    public CompanyDetails General { get; init; } = new();

    public PeppolDetails Peppol { get; init; } = new();
}

/// <summary>The seller's details every format uses. BT numbers per EN 16931.</summary>
public sealed record CompanyDetails
{
    /// <summary>BT-27.</summary>
    public string? LegalName { get; init; }

    /// <summary>BT-31.</summary>
    public string? VatIdentifier { get; init; }

    /// <summary>BT-30, with its ISO 6523 scheme BT-30-1: "0106" KvK, "0208" KBO.</summary>
    public string? LegalRegistrationId { get; init; }

    public string? LegalRegistrationScheme { get; init; }

    /// <summary>BT-35, BT-36, BT-38, BT-37.</summary>
    public string? StreetName { get; init; }

    public string? AdditionalStreetName { get; init; }

    public string? PostalZone { get; init; }

    public string? CityName { get; init; }

    /// <summary>BT-41, BT-43, BT-42.</summary>
    public string? ContactName { get; init; }

    public string? ContactEmail { get; init; }

    public string? ContactPhone { get; init; }

    /// <summary>BT-81, UNCL 4461: the usual payment means, e.g. "30" or "58".</summary>
    public string? PaymentMeansCode { get; init; }

    /// <summary>BT-84, BT-86, BT-85.</summary>
    public string? Iban { get; init; }

    public string? Bic { get; init; }

    public string? AccountName { get; init; }
}

/// <summary>BT-34 and its EAS scheme: the seller's PEPPOL participant id.</summary>
public sealed record PeppolDetails
{
    public string? EndpointId { get; init; }

    public string? EndpointScheme { get; init; }
}
```

- [ ] **Step 4: Add the IBAN check**

`src/InvoicePlatform.Domain/Organizations/Iban.cs`:

```csharp
using System.Text.RegularExpressions;

namespace InvoicePlatform.Domain.Organizations;

/// <summary>The ISO 13616 check: move the first four characters to the end, letters to numbers, mod 97 is 1.</summary>
public static partial class Iban
{
    /// <param name="iban">Compacted and upper case, as <see cref="OrganizationProfileRules.Normalize"/> leaves it.</param>
    public static bool IsValid(string? iban)
    {
        if (iban is null || !Shape().IsMatch(iban))
        {
            return false;
        }

        var remainder = 0;
        foreach (var c in iban[4..] + iban[..4])
        {
            remainder = char.IsAsciiDigit(c)
                ? ((remainder * 10) + (c - '0')) % 97
                : ((remainder * 100) + (c - 'A' + 10)) % 97;
        }

        return remainder == 1;
    }

    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
    private static partial Regex Shape();
}
```

- [ ] **Step 5: Let the VAT shapes be used outside an invoice**

In `src/InvoicePlatform.Domain/EInvoicing/Peppol/VatNumberShapes.cs`, add below `Check(CanonicalInvoiceDraft invoice)`:

```csharp
    /// <summary>
    /// True unless the number starts with NL, BE or GB and lacks that
    /// country's shape. For the company profile, which has no invoice to check.
    /// </summary>
    internal static bool Fits(string vat) => vat[..Math.Min(2, vat.Length)] switch
    {
        "NL" => Dutch().IsMatch(vat),
        "BE" => Belgian().IsMatch(vat) && Mod97(vat[2..]),
        "GB" => British().IsMatch(vat),
        _ => true,
    };
```

and change `private static bool Mod97(string digits)` to `internal static bool Mod97(string digits)` (the KBO check uses it too).

- [ ] **Step 6: Add the messages**

`src/InvoicePlatform.Domain/Organizations/ProfileRuleText.cs`:

```csharp
using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Domain.Organizations;

/// <summary>The messages of <see cref="OrganizationProfileRules"/>.</summary>
internal interface IProfileRuleText
{
    string VatPrefix(string country);

    string VatShape(string vat);

    string RegistrationSchemeMissing { get; }

    string RegistrationSchemeInvalid(string scheme);

    string KvkShape { get; }

    string KboShape { get; }

    string PaymentMeansInvalid(string code);

    string IbanInvalid { get; }

    string EndpointSchemeMissing { get; }

    string EndpointSchemeInvalid(string scheme);
}

internal sealed class ProfileRuleTextNl : IProfileRuleText
{
    public string VatPrefix(string country) => $"Een btw-nummer uit {country} begint met {country}.";

    public string VatShape(string vat) => $"'{vat}' heeft niet de vorm van een btw-nummer uit dit land.";

    public string RegistrationSchemeMissing => "Kies het schema van het inschrijvingsnummer (KvK of KBO).";

    public string RegistrationSchemeInvalid(string scheme) => $"'{scheme}' is geen geldig schema voor een inschrijvingsnummer.";

    public string KvkShape => "Een KvK-nummer heeft 8 cijfers.";

    public string KboShape => "Een KBO-nummer heeft 10 cijfers en een geldige controle.";

    public string PaymentMeansInvalid(string code) => $"'{code}' is geen geldige betaalwijze.";

    public string IbanInvalid => "Dit IBAN klopt niet. Controleer het nummer.";

    public string EndpointSchemeMissing => "Kies het schema van het PEPPOL-id.";

    public string EndpointSchemeInvalid(string scheme) => $"'{scheme}' is geen geldig schema voor een PEPPOL-id.";
}

internal sealed class ProfileRuleTextEn : IProfileRuleText
{
    public string VatPrefix(string country) => $"A VAT number from {country} starts with {country}.";

    public string VatShape(string vat) => $"'{vat}' does not have the shape of a VAT number from this country.";

    public string RegistrationSchemeMissing => "Choose the scheme of the registration number (KvK or KBO).";

    public string RegistrationSchemeInvalid(string scheme) => $"'{scheme}' is not a valid registration number scheme.";

    public string KvkShape => "A KvK number has 8 digits.";

    public string KboShape => "A KBO number has 10 digits and a valid check.";

    public string PaymentMeansInvalid(string code) => $"'{code}' is not a valid payment means.";

    public string IbanInvalid => "This IBAN is not correct. Check the number.";

    public string EndpointSchemeMissing => "Choose the scheme of the PEPPOL ID.";

    public string EndpointSchemeInvalid(string scheme) => $"'{scheme}' is not a valid PEPPOL ID scheme.";
}

internal static class ProfileRuleText
{
    private static readonly IProfileRuleText Nl = new ProfileRuleTextNl(), En = new ProfileRuleTextEn();

    public static IProfileRuleText Current => Texts.Pick(Nl, En);
}
```

- [ ] **Step 7: Add normalising and checking**

`src/InvoicePlatform.Domain/Organizations/OrganizationProfileRules.cs`:

```csharp
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.EInvoicing.Peppol;
using InvoicePlatform.Domain.Relations;

namespace InvoicePlatform.Domain.Organizations;

/// <param name="Field">Dotted path in the profile, e.g. "general.vatIdentifier"; the profile screen's input ids.</param>
public sealed record ProfileFieldError(string Field, string Message);

/// <summary>
/// Cleaning and checking a company profile before it is stored. Deterministic.
/// Every field is optional: an empty profile is valid and changes nothing.
/// </summary>
public static class OrganizationProfileRules
{
    /// <summary>Countries whose VAT number carries the country code as prefix and has a known shape.</summary>
    private static readonly string[] PrefixedCountries = ["NL", "BE", "GB"];

    /// <summary>Trimmed, empty as null, VAT and IBAN compacted, KvK and KBO as digits, a scheme without its value dropped.</summary>
    public static OrganizationProfile Normalize(OrganizationProfile profile)
    {
        var general = profile.General;
        var scheme = Clean(general.LegalRegistrationScheme);
        var registration = Clean(general.LegalRegistrationId);
        if (scheme is "0106" or "0208")
        {
            registration = RelationKeys.NormalizeRegistration(registration);
        }

        var endpoint = Clean(profile.Peppol.EndpointId);

        return profile with
        {
            General = new CompanyDetails
            {
                LegalName = Clean(general.LegalName),
                VatIdentifier = RelationKeys.NormalizeVat(general.VatIdentifier),
                LegalRegistrationId = registration,
                LegalRegistrationScheme = registration is null ? null : scheme,
                StreetName = Clean(general.StreetName),
                AdditionalStreetName = Clean(general.AdditionalStreetName),
                PostalZone = Clean(general.PostalZone),
                CityName = Clean(general.CityName),
                ContactName = Clean(general.ContactName),
                ContactEmail = Clean(general.ContactEmail),
                ContactPhone = Clean(general.ContactPhone),
                PaymentMeansCode = Clean(general.PaymentMeansCode),
                Iban = Compact(general.Iban),
                Bic = Compact(general.Bic),
                AccountName = Clean(general.AccountName),
            },
            Peppol = new PeppolDetails
            {
                EndpointId = endpoint,
                EndpointScheme = endpoint is null ? null : Clean(profile.Peppol.EndpointScheme),
            },
        };
    }

    /// <param name="profile">A profile as <see cref="Normalize"/> returns it.</param>
    /// <param name="countryCode">The organisation's country: a VAT number from NL, BE or GB must carry its prefix.</param>
    public static IReadOnlyList<ProfileFieldError> Check(OrganizationProfile profile, string? countryCode)
    {
        var errors = new List<ProfileFieldError>();
        var text = ProfileRuleText.Current;
        var general = profile.General;

        if (general.VatIdentifier is { } vat)
        {
            var country = countryCode?.Trim().ToUpperInvariant();
            if (country is not null && PrefixedCountries.Contains(country) && !vat.StartsWith(country, StringComparison.Ordinal))
            {
                errors.Add(new("general.vatIdentifier", text.VatPrefix(country)));
            }
            else if (!VatNumberShapes.Fits(vat))
            {
                errors.Add(new("general.vatIdentifier", text.VatShape(vat)));
            }
        }

        if (general.LegalRegistrationId is { } registration)
        {
            switch (general.LegalRegistrationScheme)
            {
                case null:
                    errors.Add(new("general.legalRegistrationScheme", text.RegistrationSchemeMissing));
                    break;
                case "0106" when registration.Length != 8:
                    errors.Add(new("general.legalRegistrationId", text.KvkShape));
                    break;
                case "0208" when registration.Length != 10 || !VatNumberShapes.Mod97(registration):
                    errors.Add(new("general.legalRegistrationId", text.KboShape));
                    break;
                case { } scheme when !CodeLists.IsRegistrationScheme(scheme):
                    errors.Add(new("general.legalRegistrationScheme", text.RegistrationSchemeInvalid(scheme)));
                    break;
            }
        }

        if (general.PaymentMeansCode is { } means && !CodeLists.IsPaymentMeansCode(means))
        {
            errors.Add(new("general.paymentMeansCode", text.PaymentMeansInvalid(means)));
        }

        if (general.Iban is { } iban && !Iban.IsValid(iban))
        {
            errors.Add(new("general.iban", text.IbanInvalid));
        }

        if (profile.Peppol.EndpointId is not null)
        {
            if (profile.Peppol.EndpointScheme is not { } eas)
            {
                errors.Add(new("peppol.endpointScheme", text.EndpointSchemeMissing));
            }
            else if (!CodeLists.IsElectronicAddressScheme(eas))
            {
                errors.Add(new("peppol.endpointScheme", text.EndpointSchemeInvalid(eas)));
            }
        }

        return errors;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Compact(string? value)
        => Clean(value) is { } clean ? new string(clean.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant() : null;
}
```

- [ ] **Step 8: Run the tests and watch them pass**

Run: `dotnet test tests/InvoicePlatform.Domain.Tests`
Expected: all pass, including `DependencyRuleTests`.

- [ ] **Step 9: Commit**

```
git add src/InvoicePlatform.Domain tests/InvoicePlatform.Domain.Tests
git commit -m "Add the company profile and the checks it must pass"
```

---

## Task 2: Store interface and profile service (Application)

**Files:**
- Create: `src/InvoicePlatform.Application/Organizations/IOrganizationProfileStore.cs`
- Create: `src/InvoicePlatform.Application/Organizations/OrganizationProfileService.cs`
- Modify: `tests/InvoicePlatform.Application.Tests/Fakes.cs`
- Test: `tests/InvoicePlatform.Application.Tests/OrganizationProfileServiceTests.cs`

- [ ] **Step 1: Add the fake store and a helper to `Fakes.cs`**

Append to `tests/InvoicePlatform.Application.Tests/Fakes.cs` (its usings already include `Application.Organizations` and `Domain.Organizations`):

```csharp
internal sealed class FakeProfileStore : IOrganizationProfileStore
{
    private readonly Dictionary<string, OrganizationProfile> _profiles = [];

    public Task<OrganizationProfile?> GetAsync(string organizationId, CancellationToken cancellationToken = default)
        => Task.FromResult(_profiles.GetValueOrDefault(organizationId));

    public Task SaveAsync(OrganizationProfile profile, CancellationToken cancellationToken = default)
    {
        _profiles[profile.OrganizationId] = profile;
        return Task.CompletedTask;
    }
}

internal static class TestProfiles
{
    /// <summary>A profile service over fakes; pass the stores a test fills itself.</summary>
    public static OrganizationProfileService Over(
        FakeSettingsStore? settings = null,
        FakeProfileStore? profiles = null,
        FakePlatformStore? platform = null)
        => new(profiles ?? new FakeProfileStore(), settings ?? new FakeSettingsStore(), platform ?? new FakePlatformStore());
}
```

- [ ] **Step 2: Write the failing tests**

`tests/InvoicePlatform.Application.Tests/OrganizationProfileServiceTests.cs`:

```csharp
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Application.Tests;

public class OrganizationProfileServiceTests
{
    private readonly FakeProfileStore _profiles = new();
    private readonly FakeSettingsStore _settings = new();
    private readonly FakePlatformStore _platform = new();
    private readonly OrganizationProfileService _service;

    public OrganizationProfileServiceTests()
    {
        _service = new OrganizationProfileService(_profiles, _settings, _platform);
        _platform.SaveOrganizationAsync(new Organization
        {
            OrganizationId = "org-a",
            Name = "Groen",
            CountryCode = "NL",
            CreatedAt = DateTimeOffset.UnixEpoch,
        }).GetAwaiter().GetResult();
    }

    private static OrganizationProfile Profile(string vat = "NL123456789B01") => new()
    {
        OrganizationId = "not-the-callers",
        General = new CompanyDetails { LegalName = "Groen B.V.", VatIdentifier = vat },
        Peppol = new PeppolDetails { EndpointId = "12345678", EndpointScheme = "0106" },
    };

    private Task OldPeppolIdInSettings(string id = "87654321")
        => _settings.SaveAsync(new OrganizationSettings
        {
            OrganizationId = "org-a",
            DefaultRulesetId = "peppol-bis-billing-3.0",
            EndpointId = id,
            EndpointScheme = "0106",
        });

    [Fact]
    public async Task Nothing_saved_yet_means_an_empty_profile()
    {
        var profile = await _service.GetAsync("org-a");

        Assert.Equal("org-a", profile.OrganizationId);
        Assert.Null(profile.General.LegalName);
        Assert.Null(profile.Peppol.EndpointId);
    }

    [Fact]
    public async Task Before_the_first_save_the_peppol_id_comes_from_the_settings()
    {
        await OldPeppolIdInSettings();

        var profile = await _service.GetAsync("org-a");

        Assert.Equal(("87654321", "0106"), (profile.Peppol.EndpointId, profile.Peppol.EndpointScheme));
    }

    [Fact]
    public async Task Saving_moves_the_peppol_id_out_of_the_settings()
    {
        await OldPeppolIdInSettings();

        var result = await _service.UpdateAsync("org-a", Profile());

        Assert.True(result.Succeeded);
        var settings = await _settings.GetAsync("org-a");
        Assert.Null(settings!.EndpointId);
        Assert.Null(settings.EndpointScheme);
        Assert.Equal("peppol-bis-billing-3.0", settings.DefaultRulesetId);
        Assert.Equal("12345678", (await _service.GetAsync("org-a")).Peppol.EndpointId);
    }

    [Fact]
    public async Task Clearing_the_peppol_id_does_not_bring_back_the_old_one()
    {
        await OldPeppolIdInSettings();

        await _service.UpdateAsync("org-a", Profile() with { Peppol = new PeppolDetails() });

        Assert.Null((await _service.GetAsync("org-a")).Peppol.EndpointId);
    }

    [Fact]
    public async Task An_invalid_profile_is_refused_and_nothing_is_stored()
    {
        var result = await _service.UpdateAsync("org-a", Profile(vat: "BE0123456749"));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Field == "general.vatIdentifier");
        Assert.Null(await _profiles.GetAsync("org-a"));
    }

    [Fact]
    public async Task The_profile_is_stored_under_the_callers_organisation_only()
    {
        await _service.UpdateAsync("org-a", Profile());

        Assert.Equal("Groen B.V.", (await _profiles.GetAsync("org-a"))!.General.LegalName);
        Assert.Null(await _profiles.GetAsync("not-the-callers"));
        Assert.Null((await _service.GetAsync("org-b")).General.LegalName);
    }

    [Fact]
    public async Task For_invoices_the_country_comes_with_a_saved_profile_only()
    {
        Assert.Null((await _service.ForInvoicesAsync("org-a")).CountryCode);

        await _service.UpdateAsync("org-a", Profile());

        var seller = await _service.ForInvoicesAsync("org-a");
        Assert.Equal("NL", seller.CountryCode);
        Assert.Equal("Groen B.V.", seller.Profile.General.LegalName);
    }
}
```

- [ ] **Step 3: Run them and watch them fail**

Run: `dotnet test tests/InvoicePlatform.Application.Tests --filter "FullyQualifiedName~OrganizationProfileServiceTests"`
Expected: build error, `IOrganizationProfileStore` / `OrganizationProfileService` not found.

- [ ] **Step 4: Add the store interface**

`src/InvoicePlatform.Application/Organizations/IOrganizationProfileStore.cs`:

```csharp
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Application.Organizations;

/// <summary>Persistence for the company profile, keyed by organisation.</summary>
public interface IOrganizationProfileStore
{
    /// <returns>The stored profile, or null when it was never saved.</returns>
    Task<OrganizationProfile?> GetAsync(string organizationId, CancellationToken cancellationToken = default);

    Task SaveAsync(OrganizationProfile profile, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Add the service**

`src/InvoicePlatform.Application/Organizations/OrganizationProfileService.cs`:

```csharp
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Application.Organizations;

/// <summary>
/// Reading and changing an organisation's company profile. Who may change it
/// is the API's concern (Admin only); this class cleans and checks what is
/// stored, and stores it under the caller's organisation and no other.
/// </summary>
public sealed class OrganizationProfileService(
    IOrganizationProfileStore profiles,
    IOrganizationSettingsStore settings,
    IPlatformStore platform)
{
    /// <summary>The caller's own organisation record: the name it was created with, and its country.</summary>
    public Task<Organization?> GetOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
        => platform.FindOrganizationAsync(organizationId, cancellationToken);

    /// <summary>
    /// The stored profile. Before it is first saved, an empty one carrying the
    /// PEPPOL id the organisation entered under Settings, where it used to live.
    /// </summary>
    public async Task<OrganizationProfile> GetAsync(string organizationId, CancellationToken cancellationToken = default)
        => await profiles.GetAsync(organizationId, cancellationToken).ConfigureAwait(false)
            ?? await FromSettingsAsync(organizationId, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// What the seller on an invoice is filled from. The country only once the
    /// profile is saved: an organisation that never filled it in keeps today's
    /// behaviour, where only the PEPPOL id is filled.
    /// </summary>
    public async Task<SellerProfile> ForInvoicesAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        var stored = await profiles.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return new SellerProfile(await FromSettingsAsync(organizationId, cancellationToken).ConfigureAwait(false), CountryCode: null);
        }

        var organization = await platform.FindOrganizationAsync(organizationId, cancellationToken).ConfigureAwait(false);
        return new SellerProfile(stored, organization?.CountryCode);
    }

    /// <param name="update">What the screen sent. Its OrganizationId is ignored: the caller's is used.</param>
    public async Task<ProfileUpdateResult> UpdateAsync(
        string organizationId,
        OrganizationProfile update,
        CancellationToken cancellationToken = default)
    {
        var profile = OrganizationProfileRules.Normalize(update with { OrganizationId = organizationId });
        var organization = await platform.FindOrganizationAsync(organizationId, cancellationToken).ConfigureAwait(false);

        var errors = OrganizationProfileRules.Check(profile, organization?.CountryCode);
        if (errors.Count > 0)
        {
            return new ProfileUpdateResult(false, errors);
        }

        await profiles.SaveAsync(profile, cancellationToken).ConfigureAwait(false);

        // The PEPPOL id has moved here. The copy under Settings goes, so a
        // cleared id cannot come back from it.
        var legacy = await settings.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        if (legacy is { EndpointId: not null } or { EndpointScheme: not null })
        {
            await settings.SaveAsync(legacy with { EndpointId = null, EndpointScheme = null }, cancellationToken).ConfigureAwait(false);
        }

        return new ProfileUpdateResult(true, [], profile);
    }

    private async Task<OrganizationProfile> FromSettingsAsync(string organizationId, CancellationToken cancellationToken)
    {
        var legacy = await settings.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        return new OrganizationProfile
        {
            OrganizationId = organizationId,
            Peppol = legacy?.EndpointId is { } endpoint
                ? new PeppolDetails { EndpointId = endpoint, EndpointScheme = legacy.EndpointScheme }
                : new PeppolDetails(),
        };
    }
}

/// <param name="CountryCode">The organisation's country, or null while no profile is saved.</param>
public sealed record SellerProfile(OrganizationProfile Profile, string? CountryCode);

public sealed record ProfileUpdateResult(bool Succeeded, IReadOnlyList<ProfileFieldError> Errors, OrganizationProfile? Profile = null);
```

- [ ] **Step 6: Run the tests and watch them pass**

Run: `dotnet test tests/InvoicePlatform.Application.Tests --filter "FullyQualifiedName~OrganizationProfileServiceTests"`
Expected: 7 passed.

- [ ] **Step 7: Commit**

```
git add src/InvoicePlatform.Application/Organizations tests/InvoicePlatform.Application.Tests
git commit -m "Read and save the company profile, taking over the PEPPOL id from settings"
```

---

## Task 3: Fill the seller from the profile (Application)

**Files:**
- Create: `src/InvoicePlatform.Application/Invoices/SellerFromProfile.cs`
- Modify: `src/InvoicePlatform.Application/Invoices/InvoiceEnrichment.cs`
- Modify: `src/InvoicePlatform.Application/Invoices/EnrichmentText.cs`
- Modify: `src/InvoicePlatform.Application/Invoices/ExtractInvoiceService.cs:92`
- Modify: every test that constructs `InvoiceEnrichment` (list in Step 6)
- Test: `tests/InvoicePlatform.Application.Tests/InvoiceEnrichmentTests.cs`

- [ ] **Step 1: Write the failing tests**

Replace `tests/InvoicePlatform.Application.Tests/InvoiceEnrichmentTests.cs` with:

```csharp
using InvoicePlatform.Application.Invoices;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Application.Relations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.EInvoicing;
using InvoicePlatform.Domain.Organizations;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Tests;

public class InvoiceEnrichmentTests
{
    private readonly FakeSettingsStore _settings = new();
    private readonly FakeProfileStore _profiles = new();
    private readonly FakePlatformStore _platform = new();
    private readonly InvoiceEnrichment _enrichment;

    public InvoiceEnrichmentTests()
    {
        _enrichment = new InvoiceEnrichment(
            TestProfiles.Over(_settings, _profiles, _platform),
            new RelationService(new FakeRelationStore(), new TestClock(DateTimeOffset.UnixEpoch), new InvoiceRulesetRegistry()));
        _platform.SaveOrganizationAsync(new Organization
        {
            OrganizationId = "org-a",
            Name = "Groen",
            CountryCode = "NL",
            CreatedAt = DateTimeOffset.UnixEpoch,
        }).GetAwaiter().GetResult();
    }

    /// <summary>The PEPPOL id where it lived before the profile.</summary>
    private Task Configure(string endpoint, string scheme)
        => _settings.SaveAsync(new OrganizationSettings { OrganizationId = "org-a", EndpointId = endpoint, EndpointScheme = scheme });

    private Task Profile(CompanyDetails general, PeppolDetails? peppol = null)
        => _profiles.SaveAsync(new OrganizationProfile { OrganizationId = "org-a", General = general, Peppol = peppol ?? new() });

    /// <summary>As when a PDF is read.</summary>
    private Task<EnrichmentResult> Read(CanonicalInvoiceDraft draft, string organizationId = "org-a")
        => _enrichment.EnrichAsync(organizationId, draft, SellerFill.ProfileWins);

    [Fact]
    public async Task The_organisations_endpoint_fills_an_empty_seller_endpoint()
    {
        await Configure("12345678", "0106");

        var result = await _enrichment.EnrichAsync("org-a", new CanonicalInvoiceDraft { Seller = new CanonicalParty { Name = "Wij" } });

        Assert.Equal(("12345678", "0106"), (result.Draft.Seller!.ElectronicAddress, result.Draft.Seller.ElectronicAddressScheme));
        Assert.Equal("Wij", result.Draft.Seller.Name);
        var notice = Assert.Single(result.Notices);
        Assert.Equal(ValidationSeverity.Info, notice.Severity);
        Assert.Equal("seller.electronicAddress", notice.Path);
    }

    [Fact]
    public async Task Checking_or_exporting_never_overwrites_a_value_from_the_document()
    {
        await Configure("12345678", "0106");
        await Profile(new CompanyDetails { VatIdentifier = "NL123456789B01" }, new PeppolDetails { EndpointId = "12345678", EndpointScheme = "0106" });

        var result = await _enrichment.EnrichAsync("org-a", new CanonicalInvoiceDraft
        {
            Seller = new CanonicalParty
            {
                VatIdentifier = "NL999999999B01",
                ElectronicAddress = "NL857231409B01",
                ElectronicAddressScheme = "9944",
            },
        });

        Assert.Equal("NL999999999B01", result.Draft.Seller!.VatIdentifier);
        Assert.Equal("NL857231409B01", result.Draft.Seller.ElectronicAddress);
        Assert.DoesNotContain(result.Notices, n => n.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public async Task Another_organisations_settings_are_not_used()
    {
        await Configure("12345678", "0106");

        var result = await _enrichment.EnrichAsync("org-b", new CanonicalInvoiceDraft());

        Assert.Null(result.Draft.Seller?.ElectronicAddress);
        Assert.Empty(result.Notices);
    }

    [Fact]
    public async Task Reading_a_pdf_the_profile_replaces_what_the_model_read_and_says_so()
    {
        await Profile(new CompanyDetails { VatIdentifier = "NL123456789B01", Iban = "NL91ABNA0417164300" });

        var result = await Read(new CanonicalInvoiceDraft
        {
            Seller = new CanonicalParty { VatIdentifier = "NL123456789B02" },
            Payment = new CanonicalPaymentDetails { Iban = "NL91ABNA0417164301" },
        });

        Assert.Equal("NL123456789B01", result.Draft.Seller!.VatIdentifier);
        Assert.Equal("NL91ABNA0417164300", result.Draft.Payment!.Iban);
        var warnings = result.Notices.Where(n => n.Severity == ValidationSeverity.Warning).ToList();
        Assert.Equal(new[] { "payment.iban", "seller.vatIdentifier" }, warnings.Select(n => n.Path).Order());
        Assert.Contains("NL123456789B02", warnings.Single(n => n.Path == "seller.vatIdentifier").Message);
        Assert.All(warnings, n => Assert.Equal("ENRICH-PROFILE", n.Code));
    }

    [Fact]
    public async Task Reading_a_pdf_an_empty_field_is_filled_with_an_info_notice()
    {
        await Profile(new CompanyDetails { LegalName = "Groen B.V.", StreetName = "Hoofdstraat 1" });

        var result = await Read(new CanonicalInvoiceDraft());

        Assert.Equal("Groen B.V.", result.Draft.Seller!.Name);
        Assert.Equal("Hoofdstraat 1", result.Draft.Seller.Address!.StreetName);
        Assert.Equal("NL", result.Draft.Seller.Address.CountryCode);
        Assert.Null(result.Draft.Payment);
        Assert.All(result.Notices, n => Assert.Equal(ValidationSeverity.Info, n.Severity));
        Assert.Contains(result.Notices, n => n.Path == "seller.address.countryCode");
    }

    [Fact]
    public async Task The_same_value_written_differently_is_no_conflict()
    {
        await Profile(new CompanyDetails { VatIdentifier = "NL123456789B01" });

        var result = await Read(new CanonicalInvoiceDraft { Seller = new CanonicalParty { VatIdentifier = "nl 1234.56.789.b01" } });

        Assert.Equal("NL123456789B01", result.Draft.Seller!.VatIdentifier);
        Assert.DoesNotContain(result.Notices, n => n.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public async Task Contact_and_payment_means_are_only_filled_when_empty()
    {
        await Profile(new CompanyDetails { ContactName = "Jan Jansen", PaymentMeansCode = "58" });

        var result = await Read(new CanonicalInvoiceDraft
        {
            Seller = new CanonicalParty { ContactName = "Piet Pieters" },
            Payment = new CanonicalPaymentDetails { PaymentMeansCode = "30" },
        });

        Assert.Equal("Piet Pieters", result.Draft.Seller!.ContactName);
        Assert.Equal("30", result.Draft.Payment!.PaymentMeansCode);
        Assert.DoesNotContain(result.Notices, n => n.Path is "seller.contactName" or "payment.paymentMeansCode");
    }

    [Fact]
    public async Task Without_a_saved_profile_the_seller_is_left_as_read()
    {
        var seller = new CanonicalParty { Name = "Wij" };

        var result = await Read(new CanonicalInvoiceDraft { Seller = seller });

        Assert.Same(seller, result.Draft.Seller);
        Assert.Empty(result.Notices);
    }

    [Fact]
    public async Task Another_organisations_profile_is_not_used()
    {
        await Profile(new CompanyDetails { LegalName = "Groen B.V.", VatIdentifier = "NL123456789B01" });

        var result = await Read(new CanonicalInvoiceDraft(), organizationId: "org-b");

        Assert.Null(result.Draft.Seller);
        Assert.Empty(result.Notices);
    }
}
```

- [ ] **Step 2: Run them and watch them fail**

Run: `dotnet test tests/InvoicePlatform.Application.Tests --filter "FullyQualifiedName~InvoiceEnrichmentTests"`
Expected: build error, `SellerFill` not found and `InvoiceEnrichment` has no constructor taking `OrganizationProfileService`.

- [ ] **Step 3: Add the notices**

In `src/InvoicePlatform.Application/Invoices/EnrichmentText.cs`:

- remove `SellerEndpointFromSettings` from the interface and both classes;
- add to `IEnrichmentText`:

```csharp
    string SellerFilledFromProfile(string field);

    string SellerReplacedFromProfile(string field, string documentValue);

    /// <summary>A seller field's name by its path, for the two notices above.</summary>
    string SellerField(string path);
```

- add to `EnrichmentTextNl`:

```csharp
    public string SellerFilledFromProfile(string field) => $"{field} van de verkoper ingevuld uit het bedrijfsprofiel.";

    public string SellerReplacedFromProfile(string field, string documentValue)
        => $"{field} van de verkoper komt uit het bedrijfsprofiel; de PDF toonde {documentValue}.";

    public string SellerField(string path) => path switch
    {
        "seller.name" => "Naam",
        "seller.vatIdentifier" => "Btw-nummer",
        "seller.legalRegistrationId" => "Inschrijvingsnummer",
        "seller.electronicAddress" => "PEPPOL-id",
        "seller.contactName" => "Contactpersoon",
        "seller.contactEmail" => "E-mailadres",
        "seller.contactPhone" => "Telefoonnummer",
        "seller.address.streetName" => "Straat",
        "seller.address.additionalStreetName" => "Extra adresregel",
        "seller.address.postalZone" => "Postcode",
        "seller.address.cityName" => "Plaats",
        "seller.address.countryCode" => "Landcode",
        "payment.paymentMeansCode" => "Betaalwijze",
        "payment.iban" => "IBAN",
        "payment.bic" => "BIC",
        "payment.accountName" => "Rekeninghouder",
        _ => path,
    };
```

- add to `EnrichmentTextEn`:

```csharp
    public string SellerFilledFromProfile(string field) => $"The seller's {field} was filled in from the company profile.";

    public string SellerReplacedFromProfile(string field, string documentValue)
        => $"The seller's {field} comes from the company profile; the PDF showed {documentValue}.";

    public string SellerField(string path) => path switch
    {
        "seller.name" => "name",
        "seller.vatIdentifier" => "VAT number",
        "seller.legalRegistrationId" => "registration number",
        "seller.electronicAddress" => "PEPPOL ID",
        "seller.contactName" => "contact person",
        "seller.contactEmail" => "email address",
        "seller.contactPhone" => "phone number",
        "seller.address.streetName" => "street",
        "seller.address.additionalStreetName" => "additional address line",
        "seller.address.postalZone" => "postcode",
        "seller.address.cityName" => "city",
        "seller.address.countryCode" => "country code",
        "payment.paymentMeansCode" => "payment means",
        "payment.iban" => "IBAN",
        "payment.bic" => "BIC",
        "payment.accountName" => "account holder",
        _ => path,
    };
```

- [ ] **Step 4: Add the seller filling**

`src/InvoicePlatform.Application/Invoices/SellerFromProfile.cs`:

```csharp
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Domain.Canonical;
using InvoicePlatform.Domain.Relations;
using InvoicePlatform.Domain.Validation;

namespace InvoicePlatform.Application.Invoices;

/// <summary>
/// The seller from the company profile. Fixed details (name, numbers, address,
/// country, PEPPOL id, bank account) win over what the model read when a PDF
/// is read; contact and payment means are defaults, filled only where the
/// document had nothing. Deterministic - no model is involved.
/// </summary>
internal static class SellerFromProfile
{
    public const string Code = "ENRICH-PROFILE";

    public static CanonicalInvoiceDraft Apply(
        CanonicalInvoiceDraft draft,
        SellerProfile source,
        SellerFill fill,
        List<ValidationWarning> notices)
    {
        var profile = source.Profile.General;
        var peppol = source.Profile.Peppol;
        var filler = new Filler(fill, notices);

        var original = draft.Seller ?? new CanonicalParty();
        var originalAddress = original.Address ?? new CanonicalAddress();
        var originalPayment = draft.Payment ?? new CanonicalPaymentDetails();

        var seller = original with
        {
            Name = filler.Fixed("seller.name", original.Name, profile.LegalName, Loose),
            VatIdentifier = filler.Fixed("seller.vatIdentifier", original.VatIdentifier, profile.VatIdentifier, RelationKeys.NormalizeVat),
            ContactName = filler.Default("seller.contactName", original.ContactName, profile.ContactName),
            ContactEmail = filler.Default("seller.contactEmail", original.ContactEmail, profile.ContactEmail),
            ContactPhone = filler.Default("seller.contactPhone", original.ContactPhone, profile.ContactPhone),
        };

        // A number and its scheme travel together.
        if (profile.LegalRegistrationId is { } registration
            && filler.Fixed("seller.legalRegistrationId", original.LegalRegistrationId, registration, RelationKeys.NormalizeRegistration) == registration)
        {
            seller = seller with { LegalRegistrationId = registration, LegalRegistrationScheme = profile.LegalRegistrationScheme };
        }

        if (peppol.EndpointId is { } endpoint
            && filler.Fixed("seller.electronicAddress", original.ElectronicAddress, endpoint, Loose) == endpoint)
        {
            seller = seller with { ElectronicAddress = endpoint, ElectronicAddressScheme = peppol.EndpointScheme };
        }

        var address = originalAddress with
        {
            StreetName = filler.Fixed("seller.address.streetName", originalAddress.StreetName, profile.StreetName, Loose),
            AdditionalStreetName = filler.Fixed("seller.address.additionalStreetName", originalAddress.AdditionalStreetName, profile.AdditionalStreetName, Loose),
            PostalZone = filler.Fixed("seller.address.postalZone", originalAddress.PostalZone, profile.PostalZone, Loose),
            CityName = filler.Fixed("seller.address.cityName", originalAddress.CityName, profile.CityName, Loose),
            CountryCode = filler.Fixed("seller.address.countryCode", originalAddress.CountryCode, source.CountryCode, Loose),
        };
        if (address != originalAddress)
        {
            seller = seller with { Address = address };
        }

        var payment = originalPayment with
        {
            PaymentMeansCode = filler.Default("payment.paymentMeansCode", originalPayment.PaymentMeansCode, profile.PaymentMeansCode),
            Iban = filler.Fixed("payment.iban", originalPayment.Iban, profile.Iban, Compact),
            Bic = filler.Fixed("payment.bic", originalPayment.Bic, profile.Bic, Compact),
            AccountName = filler.Fixed("payment.accountName", originalPayment.AccountName, profile.AccountName, Loose),
        };

        // Untouched parts stay exactly as they were, null included.
        return draft with
        {
            Seller = seller == original ? draft.Seller : seller,
            Payment = payment == originalPayment ? draft.Payment : payment,
        };
    }

    /// <summary>Case and spacing do not make a different value.</summary>
    private static string? Loose(string? value)
        => value is null ? null : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static string? Compact(string? value)
        => value is null ? null : new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    private sealed class Filler(SellerFill fill, List<ValidationWarning> notices)
    {
        private readonly IEnrichmentText _text = EnrichmentText.Current;

        /// <summary>A detail that is the same on every invoice.</summary>
        /// <returns>The profile's value when the field was empty, held the same value, or the profile wins; else the document's.</returns>
        public string? Fixed(string path, string? current, string? fromProfile, Func<string?, string?> normalize)
        {
            if (fromProfile is null)
            {
                return current;
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                notices.Add(new(Code, path, _text.SellerFilledFromProfile(_text.SellerField(path)), ValidationSeverity.Info));
                return fromProfile;
            }

            if (normalize(current) == normalize(fromProfile))
            {
                return fromProfile;
            }

            if (fill == SellerFill.OnlyEmpty)
            {
                return current;
            }

            notices.Add(new(Code, path, _text.SellerReplacedFromProfile(_text.SellerField(path), current.Trim()), ValidationSeverity.Warning));
            return fromProfile;
        }

        /// <summary>A default: used only where the document had nothing.</summary>
        public string? Default(string path, string? current, string? fromProfile)
        {
            if (fromProfile is null || !string.IsNullOrWhiteSpace(current))
            {
                return current;
            }

            notices.Add(new(Code, path, _text.SellerFilledFromProfile(_text.SellerField(path)), ValidationSeverity.Info));
            return fromProfile;
        }
    }
}
```

- [ ] **Step 5: Switch `InvoiceEnrichment` to the profile**

In `src/InvoicePlatform.Application/Invoices/InvoiceEnrichment.cs`, replace the class header, `EnrichAsync` and `FillSellerAsync` (lines 9-51) with:

```csharp
/// <summary>
/// Fills fields the PDF usually lacks from what the organisation already told
/// xChange: its company profile for the seller, its relations for the buyer.
/// For the buyer only empty fields are filled; where the document disagrees with
/// a relation, the document wins and the reviewer is told. For the seller see
/// <see cref="SellerFill"/>. Deterministic - no model is involved.
/// </summary>
public sealed class InvoiceEnrichment(OrganizationProfileService profiles, RelationService relations)
{
    /// <summary>Checking and exporting: the profile fills empty seller fields only.</summary>
    public Task<EnrichmentResult> EnrichAsync(
        string organizationId,
        CanonicalInvoiceDraft draft,
        CancellationToken cancellationToken = default)
        => EnrichAsync(organizationId, draft, SellerFill.OnlyEmpty, cancellationToken);

    public async Task<EnrichmentResult> EnrichAsync(
        string organizationId,
        CanonicalInvoiceDraft draft,
        SellerFill sellerFill,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var notices = new List<ValidationWarning>();

        var seller = await profiles.ForInvoicesAsync(organizationId, cancellationToken).ConfigureAwait(false);
        draft = SellerFromProfile.Apply(draft, seller, sellerFill, notices);

        var relation = await relations.FindForAsync(organizationId, draft.Buyer, cancellationToken).ConfigureAwait(false);
        draft = FillBuyer(draft, relation, notices);

        return new EnrichmentResult(draft, notices, relation);
    }
```

Remove the now-unused `using InvoicePlatform.Application.Organizations;`? No — keep it: `OrganizationProfileService` lives there. Then add at the end of the file:

```csharp
/// <summary>How the company profile meets what a document says about the seller.</summary>
public enum SellerFill
{
    /// <summary>Checking and exporting: the profile fills empty fields only, so a booked invoice stays as booked.</summary>
    OnlyEmpty,

    /// <summary>Reading a new PDF: the profile's fixed details replace what the model read, with a warning.</summary>
    ProfileWins,
}
```

- [ ] **Step 6: Read PDFs with the profile winning, and update the constructors in tests**

In `src/InvoicePlatform.Application/Invoices/ExtractInvoiceService.cs:92` change

```csharp
        var enriched = await enrichment.EnrichAsync(organizationId, learned.Draft, cancellationToken)
```

to

```csharp
        var enriched = await enrichment.EnrichAsync(organizationId, learned.Draft, SellerFill.ProfileWins, cancellationToken)
```

Then update every test that builds an `InvoiceEnrichment` (BillingServiceTests, CostLedgerTests, ExportInvoiceServiceTests ×2, ImportServiceTests, InboxServiceTests, LearningFlowTests, ReadingQualityTests, RelationServiceTests ×3, SignupServiceTests):

```
sed -i 's/new InvoiceEnrichment(new FakeSettingsStore(), /new InvoiceEnrichment(TestProfiles.Over(), /; s/new InvoiceEnrichment(_settings, /new InvoiceEnrichment(TestProfiles.Over(_settings), /' tests/InvoicePlatform.Application.Tests/*.cs
grep -n "new InvoiceEnrichment(" tests/InvoicePlatform.Application.Tests/*.cs
```

Expected: every hit now starts with `new InvoiceEnrichment(TestProfiles.Over(`.

- [ ] **Step 7: Run the whole Application test project**

Run: `dotnet test tests/InvoicePlatform.Application.Tests`
Expected: all pass. `ReadingQualityTests.What_xchange_filled_from_the_settings_is_not_held_against_the_model` and `ExportInvoiceServiceTests.The_organisations_endpoint_completes_an_invoice_booked_without_one` still pass through the settings fallback. If a learning or quality test fails, read it before changing anything: the only behaviour change is that a *saved* profile now overwrites seller fields when a PDF is read, and no existing test saves a profile.

- [ ] **Step 8: Commit**

```
git add src/InvoicePlatform.Application tests/InvoicePlatform.Application.Tests
git commit -m "Fill the seller on a read PDF from the company profile"
```

---

## Task 4: DynamoDB storage, registration and deletion (Infrastructure)

**Files:**
- Modify: `src/InvoicePlatform.Infrastructure/Identity/DynamoDbIdentityStore.cs`
- Modify: `src/InvoicePlatform.Infrastructure/Identity/DynamoDbOrganizationIdentityDeletion.cs:18,84-85`
- Modify: `src/InvoicePlatform.Application/Organizations/IOrganizationDeletionStores.cs:34`
- Modify: `src/InvoicePlatform.Infrastructure/DependencyInjection.cs:93,122`
- Modify: `tests/InvoicePlatform.Integration.Tests/SignedInFactory.cs`

- [ ] **Step 1: Implement the store**

In `DynamoDbIdentityStore.cs`:

- add `using InvoicePlatform.Domain.Organizations;`;
- add to the layout comment, under the `SETTINGS` line:
  `///   ORG#&lt;org&gt;      / COMPANY_PROFILE  the organisation's company profile`
- add `IOrganizationProfileStore` to the implemented interfaces: `: IUserStore, ISessionStore, ILoginAttemptStore, IOrganizationSettingsStore, IOrganizationProfileStore`;
- add after the settings section:

```csharp
    // ── Company profile ──────────────────────────────────────────────────────

    /// <summary>
    /// Next to the settings, in the organisation's partition. Not "PROFILE":
    /// that is the login item under USER#. Flat attributes; the PEPPOL block's
    /// are prefixed, so a later format adds its own prefix. Explicit, for the
    /// same reason as the settings' GetAsync.
    /// </summary>
    async Task<OrganizationProfile?> IOrganizationProfileStore.GetAsync(string organizationId, CancellationToken cancellationToken)
    {
        var item = await GetItemAsync($"ORG#{organizationId}", "COMPANY_PROFILE", cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return null;
        }

        return new OrganizationProfile
        {
            OrganizationId = organizationId,
            General = new CompanyDetails
            {
                LegalName = OptionalString(item, "legalName"),
                VatIdentifier = OptionalString(item, "vatIdentifier"),
                LegalRegistrationId = OptionalString(item, "legalRegistrationId"),
                LegalRegistrationScheme = OptionalString(item, "legalRegistrationScheme"),
                StreetName = OptionalString(item, "streetName"),
                AdditionalStreetName = OptionalString(item, "additionalStreetName"),
                PostalZone = OptionalString(item, "postalZone"),
                CityName = OptionalString(item, "cityName"),
                ContactName = OptionalString(item, "contactName"),
                ContactEmail = OptionalString(item, "contactEmail"),
                ContactPhone = OptionalString(item, "contactPhone"),
                PaymentMeansCode = OptionalString(item, "paymentMeansCode"),
                Iban = OptionalString(item, "iban"),
                Bic = OptionalString(item, "bic"),
                AccountName = OptionalString(item, "accountName"),
            },
            Peppol = new PeppolDetails
            {
                EndpointId = OptionalString(item, "peppolEndpointId"),
                EndpointScheme = OptionalString(item, "peppolEndpointScheme"),
            },
        };
    }

    public Task SaveAsync(OrganizationProfile profile, CancellationToken cancellationToken = default)
    {
        var general = profile.General;
        return dynamo.PutItemAsync(
            new PutItemRequest
            {
                TableName = _table,
                Item = new Dictionary<string, AttributeValue>
                {
                    [Pk] = new($"ORG#{profile.OrganizationId}"),
                    [Sk] = new("COMPANY_PROFILE"),
                    ["legalName"] = NullableString(general.LegalName),
                    ["vatIdentifier"] = NullableString(general.VatIdentifier),
                    ["legalRegistrationId"] = NullableString(general.LegalRegistrationId),
                    ["legalRegistrationScheme"] = NullableString(general.LegalRegistrationScheme),
                    ["streetName"] = NullableString(general.StreetName),
                    ["additionalStreetName"] = NullableString(general.AdditionalStreetName),
                    ["postalZone"] = NullableString(general.PostalZone),
                    ["cityName"] = NullableString(general.CityName),
                    ["contactName"] = NullableString(general.ContactName),
                    ["contactEmail"] = NullableString(general.ContactEmail),
                    ["contactPhone"] = NullableString(general.ContactPhone),
                    ["paymentMeansCode"] = NullableString(general.PaymentMeansCode),
                    ["iban"] = NullableString(general.Iban),
                    ["bic"] = NullableString(general.Bic),
                    ["accountName"] = NullableString(general.AccountName),
                    ["peppolEndpointId"] = NullableString(profile.Peppol.EndpointId),
                    ["peppolEndpointScheme"] = NullableString(profile.Peppol.EndpointScheme),
                },
            },
            cancellationToken);
    }
```

- [ ] **Step 2: Delete the profile with the organisation**

In `DynamoDbOrganizationIdentityDeletion.cs`, add to the layout comment under the `SETTINGS` line
`///   ORG#&lt;org&gt;          / COMPANY_PROFILE      the company profile   (<see cref="DynamoDbIdentityStore"/>)`
and replace `DeleteSettingsAsync` with:

```csharp
    /// <summary>The settings and the company profile: both describe the organisation, both go with it.</summary>
    public async Task<int> DeleteSettingsAsync(string organizationId, CancellationToken cancellationToken = default)
        => await DeleteAsync(Key($"ORG#{organizationId}", "SETTINGS"), cancellationToken).ConfigureAwait(false)
            + await DeleteAsync(Key($"ORG#{organizationId}", "COMPANY_PROFILE"), cancellationToken).ConfigureAwait(false);
```

In `IOrganizationDeletionStores.cs`, change the summary of `DeleteSettingsAsync` (line 33) to `/// <summary>The organisation's settings and company profile.</summary>`.

- [ ] **Step 3: Register**

In `DependencyInjection.cs`, after line 93 add

```csharp
        services.AddSingleton<IOrganizationProfileStore>(sp => sp.GetRequiredService<DynamoDbIdentityStore>());
```

and after `services.AddScoped<OrganizationSettingsService>();` add

```csharp
        services.AddScoped<OrganizationProfileService>();
```

The Worker calls `AddIdentity` (`ImportFunction.cs:41`), so `InvoiceEnrichment` resolves there too.

- [ ] **Step 4: Give the integration tests an in-memory store**

In `tests/InvoicePlatform.Integration.Tests/SignedInFactory.cs`:

- next to `public InMemorySettings Settings { get; } = new();` add `public InMemoryProfiles Profiles { get; } = new();`;
- next to `services.RemoveAll<IOrganizationSettingsStore>();` add `services.RemoveAll<IOrganizationProfileStore>();`;
- next to `services.AddSingleton<IOrganizationSettingsStore>(Settings);` add `services.AddSingleton<IOrganizationProfileStore>(Profiles);`;
- in the identity-deletion fake, replace `DeleteSettingsAsync` with

```csharp
    public Task<int> DeleteSettingsAsync(string organizationId, CancellationToken cancellationToken = default)
        => Task.FromResult(factory.Settings.RemoveOrganization(organizationId) + factory.Profiles.RemoveOrganization(organizationId));
```

- after the `InMemorySettings` class add (add `using InvoicePlatform.Domain.Organizations;` at the top if it is not there):

```csharp
public sealed class InMemoryProfiles : IOrganizationProfileStore
{
    private readonly Dictionary<string, OrganizationProfile> _profiles = [];

    public Task<OrganizationProfile?> GetAsync(string organizationId, CancellationToken cancellationToken = default)
    {
        lock (_profiles)
        {
            return Task.FromResult(_profiles.GetValueOrDefault(organizationId));
        }
    }

    public Task SaveAsync(OrganizationProfile profile, CancellationToken cancellationToken = default)
    {
        lock (_profiles)
        {
            _profiles[profile.OrganizationId] = profile;
        }

        return Task.CompletedTask;
    }

    public int RemoveOrganization(string organizationId)
    {
        lock (_profiles)
        {
            return _profiles.Remove(organizationId) ? 1 : 0;
        }
    }
}
```

- [ ] **Step 5: Build and test**

Run: `dotnet build` then `dotnet test`
Expected: build succeeds with no new warnings; all tests pass.

- [ ] **Step 6: Commit**

```
git add src/InvoicePlatform.Infrastructure src/InvoicePlatform.Application/Organizations/IOrganizationDeletionStores.cs tests/InvoicePlatform.Integration.Tests/SignedInFactory.cs
git commit -m "Store the company profile next to the settings and delete it with the organisation"
```

---

## Task 5: API — profile endpoints, settings without the PEPPOL id

**Files:**
- Create: `src/InvoicePlatform.Contracts/Organizations/OrganizationProfileContracts.cs`
- Create: `src/InvoicePlatform.Api/Organizations/ProfileEndpoints.cs`
- Modify: `src/InvoicePlatform.Api/Program.cs:116`
- Modify: `src/InvoicePlatform.Api/Organizations/OrganizationApiText.cs`
- Modify: `src/InvoicePlatform.Api/Organizations/OrganizationEndpoints.cs`
- Modify: `src/InvoicePlatform.Contracts/Invoices/RulesetContracts.cs:54-58,70-74`
- Modify: `src/InvoicePlatform.Application/Organizations/OrganizationSettings.cs`, `OrganizationSettingsService.cs`, `OrganizationText.cs`
- Test: `tests/InvoicePlatform.Integration.Tests/ProfileEndpointTests.cs` (create), `OrganizationEndpointTests.cs`, `ExportEndpointTests.cs:17-21`, `tests/InvoicePlatform.Application.Tests/OrganizationSettingsServiceTests.cs`

- [ ] **Step 1: Write the failing integration tests**

`tests/InvoicePlatform.Integration.Tests/ProfileEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace InvoicePlatform.Integration.Tests;

public class ProfileEndpointTests : IClassFixture<SignedInFactory>
{
    private const string Route = "/api/v1/organization/profile";

    private readonly SignedInFactory _factory;

    public ProfileEndpointTests(SignedInFactory factory) => _factory = factory;

    [Theory]
    [InlineData("user-a")]
    [InlineData("readonly-a")]
    public async Task Everyone_in_the_organisation_can_read_the_profile(string token)
    {
        var response = await _factory.As(token).GetAsync(Route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("user-a")]
    [InlineData("readonly-a")]
    public async Task Only_an_admin_may_change_the_profile(string token)
    {
        var response = await _factory.As(token).PutAsJsonAsync(Route, new { general = new { legalName = "Alfa B.V." } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_admins_profile_round_trips_and_stays_in_the_organisation()
    {
        var put = await _factory.As("admin-b").PutAsJsonAsync(Route, new
        {
            general = new { legalName = "Beta B.V.", vatIdentifier = "nl123456789b01", iban = "NL91 ABNA 0417 1643 00" },
            peppol = new { endpointId = "12345678", endpointScheme = "0106" },
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var mine = await _factory.As("admin-b").GetFromJsonAsync<JsonElement>(Route);
        Assert.Equal("NL123456789B01", mine.GetProperty("general").GetProperty("vatIdentifier").GetString());
        Assert.Equal("NL91ABNA0417164300", mine.GetProperty("general").GetProperty("iban").GetString());
        Assert.Equal("12345678", mine.GetProperty("peppol").GetProperty("endpointId").GetString());

        var theirs = await _factory.As("admin-a").GetFromJsonAsync<JsonElement>(Route);
        Assert.NotEqual("Beta B.V.", theirs.GetProperty("general").GetProperty("legalName").GetString());
    }

    [Fact]
    public async Task An_invalid_vat_number_is_refused_with_an_error_on_that_field()
    {
        var response = await _factory.As("admin-a").PutAsJsonAsync(Route, new { general = new { vatIdentifier = "NL123" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").TryGetProperty("general.vatIdentifier", out _));
    }
}
```

In `ExportEndpointTests.cs`, add to `Every_route_requires_a_login`:

```csharp
    [InlineData("GET", "/api/v1/organization/profile")]
    [InlineData("PUT", "/api/v1/organization/profile")]
```

- [ ] **Step 2: Run them and watch them fail**

Run: `dotnet test tests/InvoicePlatform.Integration.Tests --filter "FullyQualifiedName~ProfileEndpointTests|FullyQualifiedName~Every_route_requires_a_login"`
Expected: FAIL with 404 (`NotFound`) instead of the expected status codes.

- [ ] **Step 3: Add the contracts**

`src/InvoicePlatform.Contracts/Organizations/OrganizationProfileContracts.cs`:

```csharp
using System.Text.Json.Serialization;

namespace InvoicePlatform.Contracts.Organizations;

/// <summary>The seller's details every e-invoice format uses: the profile's General tab.</summary>
public sealed record CompanyDetailsDto
{
    [JsonPropertyName("legalName")]
    public string? LegalName { get; init; }

    [JsonPropertyName("vatIdentifier")]
    public string? VatIdentifier { get; init; }

    [JsonPropertyName("legalRegistrationId")]
    public string? LegalRegistrationId { get; init; }

    [JsonPropertyName("legalRegistrationScheme")]
    public string? LegalRegistrationScheme { get; init; }

    [JsonPropertyName("streetName")]
    public string? StreetName { get; init; }

    [JsonPropertyName("additionalStreetName")]
    public string? AdditionalStreetName { get; init; }

    [JsonPropertyName("postalZone")]
    public string? PostalZone { get; init; }

    [JsonPropertyName("cityName")]
    public string? CityName { get; init; }

    [JsonPropertyName("contactName")]
    public string? ContactName { get; init; }

    [JsonPropertyName("contactEmail")]
    public string? ContactEmail { get; init; }

    [JsonPropertyName("contactPhone")]
    public string? ContactPhone { get; init; }

    [JsonPropertyName("paymentMeansCode")]
    public string? PaymentMeansCode { get; init; }

    [JsonPropertyName("iban")]
    public string? Iban { get; init; }

    [JsonPropertyName("bic")]
    public string? Bic { get; init; }

    [JsonPropertyName("accountName")]
    public string? AccountName { get; init; }
}

/// <summary>The PEPPOL tab.</summary>
public sealed record PeppolDetailsDto
{
    [JsonPropertyName("endpointId")]
    public string? EndpointId { get; init; }

    [JsonPropertyName("endpointScheme")]
    public string? EndpointScheme { get; init; }
}

public sealed record OrganizationProfileResponse
{
    /// <summary>Read-only: the name the organisation was created with; the screen offers it as the legal name.</summary>
    [JsonPropertyName("organizationName")]
    public string? OrganizationName { get; init; }

    /// <summary>Read-only: only the platform administrator changes the country.</summary>
    [JsonPropertyName("countryCode")]
    public string? CountryCode { get; init; }

    [JsonPropertyName("general")]
    public required CompanyDetailsDto General { get; init; }

    [JsonPropertyName("peppol")]
    public required PeppolDetailsDto Peppol { get; init; }
}

public sealed record UpdateOrganizationProfileRequest
{
    [JsonPropertyName("general")]
    public CompanyDetailsDto? General { get; init; }

    [JsonPropertyName("peppol")]
    public PeppolDetailsDto? Peppol { get; init; }
}
```

- [ ] **Step 4: Add the problem title**

In `OrganizationApiText.cs`, add `string ProfileNotSaved { get; }` to `IOrganizationApiText`,
`public string ProfileNotSaved => "Bedrijfsprofiel niet opgeslagen";` to the NL class and
`public string ProfileNotSaved => "Company profile not saved";` to the EN class.

- [ ] **Step 5: Add the endpoints**

`src/InvoicePlatform.Api/Organizations/ProfileEndpoints.cs`:

```csharp
using InvoicePlatform.Api.Identity;
using InvoicePlatform.Application.Organizations;
using InvoicePlatform.Contracts.Organizations;
using InvoicePlatform.Domain.Organizations;

namespace InvoicePlatform.Api.Organizations;

/// <summary>
/// The signed-in user's company profile. There is no organisation id in the
/// route: it comes from the session, so nobody can address another tenant's.
/// </summary>
public static class ProfileEndpoints
{
    public static RouteGroupBuilder MapProfileEndpoints(this RouteGroupBuilder group)
    {
        // Guards per endpoint, never on the group (see Program.cs).
        group.MapGet("/organization/profile", GetAsync)
            .WithName("GetOrganizationProfile")
            .RequireAuthenticatedUser();

        group.MapPut("/organization/profile", UpdateAsync)
            .WithName("UpdateOrganizationProfile")
            .RequireAuthenticatedUser()
            .RequireRole(UserRoleName.Admin);

        return group;
    }

    private static async Task<IResult> GetAsync(
        HttpContext context,
        OrganizationProfileService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;
        var profile = await service.GetAsync(user.OrganizationId, cancellationToken).ConfigureAwait(false);
        return Results.Ok(await ToResponseAsync(profile, service, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<IResult> UpdateAsync(
        HttpContext context,
        OrganizationProfileService service,
        CancellationToken cancellationToken)
    {
        var user = context.GetAuthenticatedUser()!;

        // Read here, not bound as a parameter: binding runs before the guards,
        // and an anonymous caller must get 401, not body-validation feedback.
        var request = await RequestBody.ReadAsync<UpdateOrganizationProfileRequest>(context, cancellationToken)
            .ConfigureAwait(false);
        if (request is null)
        {
            return RequestBody.Invalid();
        }

        var result = await service.UpdateAsync(user.OrganizationId, FromRequest(request), cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(
                result.Errors.GroupBy(e => e.Field).ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray()),
                title: OrganizationApiText.Current.ProfileNotSaved);
        }

        return Results.Ok(await ToResponseAsync(result.Profile!, service, cancellationToken).ConfigureAwait(false));
    }

    private static OrganizationProfile FromRequest(UpdateOrganizationProfileRequest request)
    {
        var general = request.General ?? new CompanyDetailsDto();
        var peppol = request.Peppol ?? new PeppolDetailsDto();
        return new OrganizationProfile
        {
            // The service puts the caller's organisation here; never the request's.
            OrganizationId = string.Empty,
            General = new CompanyDetails
            {
                LegalName = general.LegalName,
                VatIdentifier = general.VatIdentifier,
                LegalRegistrationId = general.LegalRegistrationId,
                LegalRegistrationScheme = general.LegalRegistrationScheme,
                StreetName = general.StreetName,
                AdditionalStreetName = general.AdditionalStreetName,
                PostalZone = general.PostalZone,
                CityName = general.CityName,
                ContactName = general.ContactName,
                ContactEmail = general.ContactEmail,
                ContactPhone = general.ContactPhone,
                PaymentMeansCode = general.PaymentMeansCode,
                Iban = general.Iban,
                Bic = general.Bic,
                AccountName = general.AccountName,
            },
            Peppol = new PeppolDetails { EndpointId = peppol.EndpointId, EndpointScheme = peppol.EndpointScheme },
        };
    }

    private static async Task<OrganizationProfileResponse> ToResponseAsync(
        OrganizationProfile profile,
        OrganizationProfileService service,
        CancellationToken cancellationToken)
    {
        var organization = await service.GetOrganizationAsync(profile.OrganizationId, cancellationToken).ConfigureAwait(false);
        var general = profile.General;
        return new OrganizationProfileResponse
        {
            OrganizationName = organization?.Name,
            CountryCode = organization?.CountryCode,
            General = new CompanyDetailsDto
            {
                LegalName = general.LegalName,
                VatIdentifier = general.VatIdentifier,
                LegalRegistrationId = general.LegalRegistrationId,
                LegalRegistrationScheme = general.LegalRegistrationScheme,
                StreetName = general.StreetName,
                AdditionalStreetName = general.AdditionalStreetName,
                PostalZone = general.PostalZone,
                CityName = general.CityName,
                ContactName = general.ContactName,
                ContactEmail = general.ContactEmail,
                ContactPhone = general.ContactPhone,
                PaymentMeansCode = general.PaymentMeansCode,
                Iban = general.Iban,
                Bic = general.Bic,
                AccountName = general.AccountName,
            },
            Peppol = new PeppolDetailsDto { EndpointId = profile.Peppol.EndpointId, EndpointScheme = profile.Peppol.EndpointScheme },
        };
    }
}
```

In `Program.cs`, after `api.MapOrganizationEndpoints();` (line 116) add `api.MapProfileEndpoints();`.

- [ ] **Step 6: Run the profile tests and watch them pass**

Run: `dotnet test tests/InvoicePlatform.Integration.Tests --filter "FullyQualifiedName~ProfileEndpointTests|FullyQualifiedName~Every_route_requires_a_login"`
Expected: all pass.

- [ ] **Step 7: Change the settings tests to the slimmer settings**

In `tests/InvoicePlatform.Application.Tests/OrganizationSettingsServiceTests.cs`, replace the tests from `An_update_round_trips_trimmed` to the end of the class with:

```csharp
    [Fact]
    public async Task An_update_round_trips_trimmed()
    {
        var result = await _service.UpdateAsync("org-a", " peppol-bis-billing-3.0 ");

        Assert.True(result.Succeeded);
        Assert.Equal("peppol-bis-billing-3.0", (await _service.GetAsync("org-a")).DefaultRulesetId);
    }

    [Fact]
    public async Task An_unknown_ruleset_is_refused()
    {
        var result = await _service.UpdateAsync("org-a", "xrechnung-9");

        Assert.False(result.Succeeded);
        Assert.Null(await _store.GetAsync("org-a"));
    }

    [Fact]
    public async Task An_empty_value_clears_the_default()
    {
        await _service.UpdateAsync("org-a", "peppol-bis-billing-3.0");

        await _service.UpdateAsync("org-a", "  ");

        Assert.Null((await _service.GetAsync("org-a")).DefaultRulesetId);
    }

    [Fact]
    public async Task Saving_the_settings_keeps_a_peppol_id_that_has_not_moved_yet()
    {
        await _store.SaveAsync(new OrganizationSettings { OrganizationId = "org-a", EndpointId = "12345678", EndpointScheme = "0106" });

        await _service.UpdateAsync("org-a", "peppol-bis-billing-3.0");

        var settings = await _service.GetAsync("org-a");
        Assert.Equal(("12345678", "0106"), (settings.EndpointId, settings.EndpointScheme));
    }

    [Fact]
    public async Task One_organisation_never_sees_anothers_settings()
    {
        await _service.UpdateAsync("org-a", "peppol-bis-billing-3.0");

        Assert.Null((await _service.GetAsync("org-b")).DefaultRulesetId);
    }
}
```

In `tests/InvoicePlatform.Integration.Tests/OrganizationEndpointTests.cs`:

- in `Only_an_admin_may_change_settings`, send `new { defaultRulesetId = "peppol-bis-billing-3.0" }`;
- replace `An_admins_change_round_trips_and_stays_in_the_organisation` with:

```csharp
    [Fact]
    public async Task An_admins_change_round_trips_and_stays_in_the_organisation()
    {
        var put = await _factory.As("admin-b").PutAsJsonAsync("/api/v1/organization/settings",
            new { defaultRulesetId = "nlcius-si-ubl-2.0" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var mine = await _factory.As("admin-b").GetFromJsonAsync<JsonElement>("/api/v1/organization/settings");
        Assert.Equal("nlcius-si-ubl-2.0", mine.GetProperty("defaultRulesetId").GetString());
        Assert.False(mine.TryGetProperty("endpointId", out _));

        var theirs = await _factory.As("admin-a").GetFromJsonAsync<JsonElement>("/api/v1/organization/settings");
        Assert.NotEqual("nlcius-si-ubl-2.0", theirs.GetProperty("defaultRulesetId").GetString());
    }
```

(`nlcius-si-ubl-2.0` is `NlciusSiUbl20Ruleset.RulesetId`.)

- [ ] **Step 8: Run them and watch them fail**

Run: `dotnet test tests/InvoicePlatform.Application.Tests --filter "FullyQualifiedName~OrganizationSettingsServiceTests"`
Expected: build error, no overload of `UpdateAsync` takes 2 arguments.

- [ ] **Step 9: Slim the settings down**

`OrganizationSettings.cs` — replace the comment on `EndpointId` with:

```csharp
    /// <summary>
    /// Where the organisation's PEPPOL id lived before the company profile.
    /// Still read until the profile is first saved
    /// (<see cref="OrganizationProfileService"/>), which clears it. Nothing
    /// writes a new value here any more.
    /// </summary>
```

`OrganizationSettingsService.cs` — replace `UpdateAsync` with:

```csharp
    public async Task<SettingsUpdateResult> UpdateAsync(
        string organizationId,
        string? defaultRulesetId,
        CancellationToken cancellationToken = default)
    {
        var ruleset = Clean(defaultRulesetId);
        if (ruleset is not null && rulesets.Find(ruleset) is null)
        {
            return SettingsUpdateResult.Fail(OrganizationText.Current.UnknownRuleset);
        }

        var existing = await store.GetAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var settings = new OrganizationSettings
        {
            OrganizationId = organizationId,
            DefaultRulesetId = ruleset,
            // A PEPPOL id that has not moved to the profile yet must survive.
            EndpointId = existing?.EndpointId,
            EndpointScheme = existing?.EndpointScheme,
        };

        await store.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        return new SettingsUpdateResult(true, Settings: settings);
    }
```

and drop the now-unused `using InvoicePlatform.Domain.EInvoicing;` only if the compiler reports it unused (`InvoiceRulesetRegistry` lives there, so it probably stays).

`OrganizationText.cs` — remove `EndpointSchemeMissing` and `EndpointSchemeInvalid` from the interface and both classes.

`RulesetContracts.cs` — remove `EndpointId` and `EndpointScheme` (with their attributes) from `OrganizationSettingsResponse` and `UpdateOrganizationSettingsRequest`.

`OrganizationEndpoints.cs` — the update call becomes

```csharp
        var result = await service.UpdateAsync(user.OrganizationId, request.DefaultRulesetId, cancellationToken)
            .ConfigureAwait(false);
```

and remove the `EndpointId = …` and `EndpointScheme = …` lines from `ToResponse`. Update the class summary: `/// The signed-in user's organisation settings: the default export format.`

- [ ] **Step 10: Run everything**

Run: `dotnet build` then `dotnet test`
Expected: all pass, no warnings about unused members.

- [ ] **Step 11: Commit**

```
git add src tests
git commit -m "Serve the company profile and take the PEPPOL id off the settings"
```

---

## Task 6: The Company profile page (web)

**Files:**
- Create: `apps/web/lib/profileForm.ts`, `apps/web/lib/profileForm.test.ts`
- Create: `apps/web/lib/profile.ts`
- Create: `apps/web/lib/i18n/profile.ts`
- Create: `apps/web/app/profile/page.tsx`, `apps/web/app/profile/ProfileClient.tsx`
- Modify: `apps/web/lib/i18n/index.ts`, `apps/web/lib/i18n/shell.ts`, `apps/web/components/nav.ts`, `apps/web/components/AppShell.tsx:36`
- Modify: `apps/web/lib/settings.ts`, `apps/web/app/settings/SettingsClient.tsx`, `apps/web/lib/i18n/settings.ts`

Run all commands in this task from `apps/web` (Node ≥ 20.9.0).

- [ ] **Step 1: Write the failing test for the pure helpers**

`apps/web/lib/profileForm.test.ts`:

```ts
import assert from "node:assert/strict";
import test from "node:test";

import { fieldErrorsOf, legalNameToShow, tabOf, toWire } from "./profileForm.ts";

test("field errors take the first message per field of a validation problem", () => {
  assert.deepEqual(
    fieldErrorsOf({ title: "x", errors: { "general.iban": ["Dit IBAN klopt niet.", "second"], "peppol.endpointScheme": ["Kies"] } }),
    { "general.iban": "Dit IBAN klopt niet.", "peppol.endpointScheme": "Kies" },
  );
});

test("a body without errors gives none", () => {
  assert.deepEqual(fieldErrorsOf(null), {});
  assert.deepEqual(fieldErrorsOf({ detail: "boom" }), {});
  assert.deepEqual(fieldErrorsOf({ errors: { a: "not an array" } }), {});
});

test("the legal name falls back to the organisation's name", () => {
  assert.equal(legalNameToShow("Groen B.V.", "Groen"), "Groen B.V.");
  assert.equal(legalNameToShow(null, "Groen"), "Groen");
  assert.equal(legalNameToShow(null, undefined), "");
});

test("form text goes over the wire trimmed, empty as null", () => {
  assert.equal(toWire("  NL91 "), "NL91");
  assert.equal(toWire("   "), null);
});

test("a field path names its tab", () => {
  assert.equal(tabOf("peppol.endpointId"), "peppol");
  assert.equal(tabOf("general.iban"), "general");
});
```

- [ ] **Step 2: Run it and watch it fail**

Run: `npm test`
Expected: FAIL, `Cannot find module '.../lib/profileForm.ts'`.

- [ ] **Step 3: Add the helpers**

`apps/web/lib/profileForm.ts`:

```ts
/**
 * Pure helpers for the company profile screen. No imports, so node:test can
 * load this file as it is.
 */

export type ProfileTab = "general" | "peppol";

/** First message per field path, e.g. { "general.iban": "…" }. */
export type FieldErrors = Record<string, string>;

/** The first message per field of an ASP.NET validation problem (`errors`), or none. */
export function fieldErrorsOf(payload: unknown): FieldErrors {
  const errors = (payload as { errors?: unknown } | null)?.errors;
  if (!errors || typeof errors !== "object") return {};

  const result: FieldErrors = {};
  for (const [field, messages] of Object.entries(errors as Record<string, unknown>)) {
    if (Array.isArray(messages) && typeof messages[0] === "string") result[field] = messages[0];
  }
  return result;
}

/** The saved legal name, else the name the organisation was created with, so the first save records it. */
export function legalNameToShow(saved: string | null | undefined, organizationName: string | null | undefined): string {
  return saved ?? organizationName ?? "";
}

/** Form text to wire value: trimmed, empty as null. */
export function toWire(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === "" ? null : trimmed;
}

/** The tab a field path is on, so an error on a hidden tab can open it. */
export function tabOf(field: string): ProfileTab {
  return field.startsWith("peppol.") ? "peppol" : "general";
}
```

- [ ] **Step 4: Run the test and watch it pass**

Run: `npm test`
Expected: all tests pass, the five new ones included.

- [ ] **Step 5: Add the API client**

`apps/web/lib/profile.ts`:

```ts
/**
 * Client for the organisation's company profile: the seller's details on
 * every invoice, with a block per format (PEPPOL first). The organisation
 * comes from the session; only an admin may change it, which the API enforces.
 */

import { API_BASE_URL, ApiError, isApiConfigured } from "./api";
import { authHeaders } from "./auth";
import { text } from "./i18n";
import { fieldErrorsOf, type FieldErrors } from "./profileForm";

export interface CompanyDetails {
  legalName: string | null;
  vatIdentifier: string | null;
  legalRegistrationId: string | null;
  legalRegistrationScheme: string | null;
  streetName: string | null;
  additionalStreetName: string | null;
  postalZone: string | null;
  cityName: string | null;
  contactName: string | null;
  contactEmail: string | null;
  contactPhone: string | null;
  paymentMeansCode: string | null;
  iban: string | null;
  bic: string | null;
  accountName: string | null;
}

export interface PeppolDetails {
  endpointId: string | null;
  endpointScheme: string | null;
}

export interface OrganizationProfile {
  /** Read-only: the name the organisation was created with. */
  organizationName?: string | null;
  /** Read-only: only the platform administrator changes it. */
  countryCode?: string | null;
  general: CompanyDetails;
  peppol: PeppolDetails;
}

/** A refused save, with the message per field the API gave. */
export class ProfileSaveError extends ApiError {
  constructor(
    message: string,
    status: number,
    readonly fieldErrors: FieldErrors,
  ) {
    super(message, status);
    this.name = "ProfileSaveError";
  }
}

/** Without an API there is nothing to load: an empty profile. */
export const FALLBACK_PROFILE: OrganizationProfile = {
  general: {
    legalName: null,
    vatIdentifier: null,
    legalRegistrationId: null,
    legalRegistrationScheme: null,
    streetName: null,
    additionalStreetName: null,
    postalZone: null,
    cityName: null,
    contactName: null,
    contactEmail: null,
    contactPhone: null,
    paymentMeansCode: null,
    iban: null,
    bic: null,
    accountName: null,
  },
  peppol: { endpointId: null, endpointScheme: null },
};

async function call(method: "GET" | "PUT", body?: unknown): Promise<OrganizationProfile> {
  if (!isApiConfigured) return FALLBACK_PROFILE;

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}/api/v1/organization/profile`, {
      method,
      headers: { "Content-Type": "application/json", ...authHeaders() },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(text().common.apiUnreachable, 0);
  }

  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    const problem = payload as { title?: string; detail?: string } | null;
    const message = problem?.detail ?? problem?.title ?? text().common.apiStatus(response.status);
    throw new ProfileSaveError(message, response.status, fieldErrorsOf(payload));
  }
  return payload as OrganizationProfile;
}

export function getProfile(): Promise<OrganizationProfile> {
  return call("GET");
}

export function updateProfile(update: { general: CompanyDetails; peppol: PeppolDetails }): Promise<OrganizationProfile> {
  return call("PUT", update);
}
```

- [ ] **Step 6: Add the texts**

`apps/web/lib/i18n/profile.ts`:

```ts
/** Texts for the company profile. Dutch first; `en` must have exactly the same keys (typeof nl). */

const nl = {
  title: "Bedrijfsprofiel",
  subtitle: "Uw gegevens als verkoper. Ze komen op elke factuur die u inleest.",
  noApi: "Er is geen API geconfigureerd. Het profiel kan niet worden opgeslagen.",
  saved: "Bedrijfsprofiel opgeslagen.",
  saveFailed: "Opslaan mislukt.",
  adminOnly: "Alleen een beheerder kan dit wijzigen.",
  howUsed:
    "Naam, nummers, adres, PEPPOL-id en bankgegevens uit dit profiel gaan voor wat er in een PDF staat; u ziet een melding als ze verschillen. Contact en betaalwijze vullen alleen aan wat ontbreekt. Al geboekte facturen veranderen niet.",
  tabs: { general: "Algemeen", peppol: "PEPPOL" },
  cards: { company: "Bedrijf", address: "Adres", contact: "Contact", payment: "Betaling", peppol: "PEPPOL" },
  fields: {
    legalName: "Juridische naam",
    vatIdentifier: "Btw-nummer",
    legalRegistrationId: "Inschrijvingsnummer (KvK of KBO)",
    legalRegistrationScheme: "Schema",
    streetName: "Straat en huisnummer",
    additionalStreetName: "Extra adresregel",
    postalZone: "Postcode",
    cityName: "Plaats",
    country: "Land",
    contactName: "Contactpersoon",
    contactEmail: "E-mailadres",
    contactPhone: "Telefoonnummer",
    paymentMeansCode: "Standaard betaalwijze",
    iban: "IBAN",
    bic: "BIC",
    accountName: "Rekeninghouder",
    endpointId: "PEPPOL-id",
    endpointScheme: "Schema",
  },
  legalNameHelp: "Zoals ingeschreven bij de KvK of KBO.",
  countryHelp: "Het land wijzigt de platformbeheerder.",
  countryNotSet: "Nog niet vastgelegd",
  peppolIntro:
    "Uw adres op het PEPPOL-netwerk. In Nederland is dat meestal 0106 met uw KvK-nummer, in België 0208 met uw KBO-nummer.",
};

const en: typeof nl = {
  title: "Company profile",
  subtitle: "Your details as the seller. They go on every invoice you read in.",
  noApi: "No API is configured. The profile cannot be saved.",
  saved: "Company profile saved.",
  saveFailed: "Saving failed.",
  adminOnly: "Only an administrator can change this.",
  howUsed:
    "Name, numbers, address, PEPPOL ID and bank details from this profile take precedence over what a PDF says; you see a notice when they differ. Contact and payment means only fill in what is missing. Invoices already booked do not change.",
  tabs: { general: "General", peppol: "PEPPOL" },
  cards: { company: "Company", address: "Address", contact: "Contact", payment: "Payment", peppol: "PEPPOL" },
  fields: {
    legalName: "Legal name",
    vatIdentifier: "VAT number",
    legalRegistrationId: "Registration number (KvK or KBO)",
    legalRegistrationScheme: "Scheme",
    streetName: "Street and number",
    additionalStreetName: "Additional address line",
    postalZone: "Postcode",
    cityName: "City",
    country: "Country",
    contactName: "Contact person",
    contactEmail: "Email address",
    contactPhone: "Phone number",
    paymentMeansCode: "Usual payment means",
    iban: "IBAN",
    bic: "BIC",
    accountName: "Account holder",
    endpointId: "PEPPOL ID",
    endpointScheme: "Scheme",
  },
  legalNameHelp: "As registered with the KvK or KBO.",
  countryHelp: "The platform administrator changes the country.",
  countryNotSet: "Not set yet",
  peppolIntro:
    "Your address on the PEPPOL network. In the Netherlands that is usually 0106 with your KvK number, in Belgium 0208 with your KBO number.",
};

export const profile = { nl, en };
```

In `apps/web/lib/i18n/index.ts`: add `import { profile } from "./profile.ts";` (alphabetically after `platform`) and add `profile` to `areas` after `settings`.

In `apps/web/lib/i18n/shell.ts`:
- `nav`: add `profile: "Bedrijfsprofiel",` (nl) and `profile: "Company profile",` (en) before `settings`;
- `pages`: add `profile: { title: "Bedrijfsprofiel", breadcrumb: "Beheer / Bedrijfsprofiel" },` (nl) and `profile: { title: "Company profile", breadcrumb: "Administration / Company profile" },` (en) before `instellingen`.

In `apps/web/components/nav.ts`, before the `instellingen` item add:

```ts
  { id: "profile", label: "profile", icon: "user", href: "/profile" },
```

In `apps/web/components/AppShell.tsx`, in `activeNavId` before the `/settings` line add:

```ts
  if (pathname.includes("/profile")) return "profile";
```

- [ ] **Step 7: Add the page**

`apps/web/app/profile/page.tsx`:

```tsx
import ProfileClient from "./ProfileClient";

export default function ProfilePage() {
  return <ProfileClient />;
}
```

`apps/web/app/profile/ProfileClient.tsx`:

```tsx
"use client";

import { useCallback, useEffect, useState, useSyncExternalStore } from "react";

import { Icon } from "../../components/Icon";
import { useToast } from "../../components/Toast";
import { isApiConfigured } from "../../lib/api";
import { getServerUserSnapshot, getUserSnapshot, listCountries, subscribeToUser, type CountryOption } from "../../lib/auth";
import { text, useText } from "../../lib/i18n";
import {
  FALLBACK_PROFILE,
  ProfileSaveError,
  getProfile,
  updateProfile,
  type CompanyDetails,
  type OrganizationProfile,
  type PeppolDetails,
} from "../../lib/profile";
import { legalNameToShow, tabOf, toWire, type FieldErrors, type ProfileTab } from "../../lib/profileForm";
import { endpointSchemes, paymentMeans, registrationSchemes, type Option } from "../invoices/[invoiceId]/review/fields";

type GeneralForm = Record<keyof CompanyDetails, string>;
type PeppolForm = Record<keyof PeppolDetails, string>;

function toForm<T extends object>(values: T): Record<keyof T, string> {
  return Object.fromEntries(Object.entries(values).map(([key, value]) => [key, (value as string | null) ?? ""])) as Record<keyof T, string>;
}

function fromForm<K extends string>(form: Record<K, string>): Record<K, string | null> {
  return Object.fromEntries(Object.entries<string>(form).map(([key, value]) => [key, toWire(value)])) as Record<K, string | null>;
}

/**
 * The organisation's company profile: a General tab with the seller details
 * every format uses, and a tab per format (PEPPOL first). Only an admin may
 * change it; the API enforces that too.
 */
export default function ProfileClient() {
  const showToast = useToast();
  const t = useText();
  const p = t.profile;
  const user = useSyncExternalStore(subscribeToUser, getUserSnapshot, getServerUserSnapshot);
  const canEdit = user?.role === "Admin";

  const [profile, setProfile] = useState<OrganizationProfile | null>(null);
  const [general, setGeneral] = useState<GeneralForm | null>(null);
  const [peppol, setPeppol] = useState<PeppolForm | null>(null);
  const [tab, setTab] = useState<ProfileTab>("general");
  const [errors, setErrors] = useState<FieldErrors>({});
  const [saving, setSaving] = useState(false);
  const [countries, setCountries] = useState<CountryOption[]>([]);

  const show = useCallback((loaded: OrganizationProfile) => {
    setProfile(loaded);
    setGeneral({ ...toForm(loaded.general), legalName: legalNameToShow(loaded.general.legalName, loaded.organizationName) });
    setPeppol(toForm(loaded.peppol));
  }, []);

  useEffect(() => {
    let cancelled = false;
    listCountries().then((loaded) => {
      if (!cancelled) setCountries(loaded);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    getProfile()
      .then((loaded) => {
        if (!cancelled) show(loaded);
      })
      .catch((cause: unknown) => {
        if (cancelled) return;
        show(FALLBACK_PROFILE);
        showToast(cause instanceof Error ? cause.message : text().common.loadFailed, "danger");
      });
    return () => {
      cancelled = true;
    };
  }, [show, showToast]);

  const save = async (event: React.FormEvent) => {
    event.preventDefault();
    if (saving || general === null || peppol === null) return;

    setSaving(true);
    setErrors({});
    try {
      show(await updateProfile({ general: fromForm(general), peppol: fromForm(peppol) }));
      showToast(p.saved, "success");
    } catch (cause) {
      if (cause instanceof ProfileSaveError) {
        setErrors(cause.fieldErrors);
        const first = Object.keys(cause.fieldErrors)[0];
        if (first) setTab(tabOf(first));
      }
      showToast(cause instanceof Error ? cause.message : p.saveFailed, "danger");
    } finally {
      setSaving(false);
    }
  };

  const setField = (block: ProfileTab, key: string, value: string) =>
    block === "general"
      ? setGeneral((current) => current && { ...current, [key]: value })
      : setPeppol((current) => current && { ...current, [key]: value });

  const valueOf = (block: ProfileTab, key: string) =>
    ((block === "general" ? general : peppol) as Record<string, string> | null)?.[key] ?? "";

  const errorOf = (id: string) =>
    errors[id] ? <div className="form-help text-danger">{errors[id]}</div> : null;

  const input = (block: ProfileTab, key: string, label: string, options?: { type?: string; help?: string }) => {
    const id = `${block}.${key}`;
    return (
      <div className="form__group">
        <label htmlFor={id}>{label}</label>
        <input
          id={id}
          type={options?.type ?? "text"}
          value={valueOf(block, key)}
          readOnly={!canEdit}
          className={errors[id] ? "input--invalid" : undefined}
          onChange={(event) => setField(block, key, event.target.value)}
        />
        {errorOf(id)}
        {options?.help && <div className="form-help">{options.help}</div>}
      </div>
    );
  };

  const select = (block: ProfileTab, key: string, label: string, choices: Option[]) => {
    const id = `${block}.${key}`;
    return (
      <div className="form__group">
        <label htmlFor={id}>{label}</label>
        <select
          id={id}
          value={valueOf(block, key)}
          disabled={!canEdit}
          className={errors[id] ? "input--invalid" : undefined}
          onChange={(event) => setField(block, key, event.target.value)}
        >
          <option value="">—</option>
          {choices.map((choice) => (
            <option key={choice.value} value={choice.value}>
              {choice.label}
            </option>
          ))}
        </select>
        {errorOf(id)}
      </div>
    );
  };

  const country = profile?.countryCode
    ? (countries.find((c) => c.code === profile.countryCode)?.name ?? profile.countryCode)
    : p.countryNotSet;

  const tabButton = (id: ProfileTab, label: string) => (
    <button type="button" className={`tab ${tab === id ? "tab--active" : ""}`.trim()} onClick={() => setTab(id)}>
      {label}
    </button>
  );

  return (
    <div style={{ maxWidth: 720 }}>
      <div className="page-header">
        <div>
          <h1>
            <Icon name="user" /> {p.title}
          </h1>
          <p className="page-header__subtitle">{profile === null ? t.common.loading : p.subtitle}</p>
        </div>
      </div>

      {!isApiConfigured && (
        <div className="alert alert--info">
          <Icon name="info" />
          <div>{p.noApi}</div>
        </div>
      )}

      {profile !== null && general !== null && peppol !== null && (
        <form className="form stack" onSubmit={save}>
          <div className="tabs">
            {tabButton("general", p.tabs.general)}
            {tabButton("peppol", p.tabs.peppol)}
          </div>

          <p className="text-soft text-sm">{p.howUsed}</p>

          {tab === "general" && (
            <>
              <div className="card">
                <div className="card__header">
                  <h3>{p.cards.company}</h3>
                </div>
                <div className="card__body">
                  {input("general", "legalName", p.fields.legalName, { help: p.legalNameHelp })}
                  {input("general", "vatIdentifier", p.fields.vatIdentifier)}
                  <div className="form__row">
                    {input("general", "legalRegistrationId", p.fields.legalRegistrationId)}
                    {select("general", "legalRegistrationScheme", p.fields.legalRegistrationScheme, registrationSchemes(t))}
                  </div>
                </div>
              </div>

              <div className="card">
                <div className="card__header">
                  <h3>{p.cards.address}</h3>
                </div>
                <div className="card__body">
                  {input("general", "streetName", p.fields.streetName)}
                  {input("general", "additionalStreetName", p.fields.additionalStreetName)}
                  <div className="form__row">
                    {input("general", "postalZone", p.fields.postalZone)}
                    {input("general", "cityName", p.fields.cityName)}
                  </div>
                  <div className="form__group">
                    <label htmlFor="general.country">{p.fields.country}</label>
                    <input id="general.country" type="text" value={country} readOnly />
                    <div className="form-help">{p.countryHelp}</div>
                  </div>
                </div>
              </div>

              <div className="card">
                <div className="card__header">
                  <h3>{p.cards.contact}</h3>
                </div>
                <div className="card__body">
                  {input("general", "contactName", p.fields.contactName)}
                  <div className="form__row">
                    {input("general", "contactEmail", p.fields.contactEmail, { type: "email" })}
                    {input("general", "contactPhone", p.fields.contactPhone, { type: "tel" })}
                  </div>
                </div>
              </div>

              <div className="card">
                <div className="card__header">
                  <h3>{p.cards.payment}</h3>
                </div>
                <div className="card__body">
                  {select("general", "paymentMeansCode", p.fields.paymentMeansCode, paymentMeans(t))}
                  {input("general", "iban", p.fields.iban)}
                  <div className="form__row">
                    {input("general", "bic", p.fields.bic)}
                    {input("general", "accountName", p.fields.accountName)}
                  </div>
                </div>
              </div>
            </>
          )}

          {tab === "peppol" && (
            <div className="card">
              <div className="card__header">
                <h3>{p.cards.peppol}</h3>
              </div>
              <div className="card__body">
                <p className="text-soft text-sm">{p.peppolIntro}</p>
                <div className="form__row">
                  {input("peppol", "endpointId", p.fields.endpointId)}
                  {select("peppol", "endpointScheme", p.fields.endpointScheme, endpointSchemes(t))}
                </div>
              </div>
            </div>
          )}

          {canEdit ? (
            <div className="form__actions">
              <button type="submit" className="btn btn--primary" disabled={saving || !isApiConfigured}>
                <Icon name="save" /> {saving ? t.common.busy : t.common.save}
              </button>
            </div>
          ) : (
            <p className="text-soft text-sm">{p.adminOnly}</p>
          )}
        </form>
      )}
    </div>
  );
}
```

- [ ] **Step 8: Take the PEPPOL id off Settings**

`apps/web/lib/settings.ts`: remove `endpointId` and `endpointScheme` from `OrganizationSettings`, from `FALLBACK_SETTINGS`, and from the parameter of `updateSettings`, which becomes:

```ts
export function updateSettings(update: { defaultRulesetId: string | null }): Promise<OrganizationSettings> {
  return call("PUT", update);
}
```

`apps/web/app/settings/SettingsClient.tsx`:
- remove the `endpointId`/`endpointScheme` state, their two `set…` calls in the load effect, the two lines in `updateSettings({...})`, the import of `endpointSchemes`, and the whole `form__row` with the PEPPOL id plus the `form-help` with `endpointHelp` below it;
- add `import Link from "next/link";` and, in the place of the removed block, a pointer:

```tsx
            <p className="form-help">
              {t.settings.peppolMoved} <Link href="/profile/">{t.settings.peppolMovedLink}</Link>
            </p>
```

- update the component comment to: `The organisation's settings: the format the export menu opens on. Its own details as the seller, the PEPPOL id included, are on the company profile.`

`apps/web/lib/i18n/settings.ts`: remove `endpointId`, `endpointScheme`, `endpointHelp` from `nl` and `en`, and add

```ts
  peppolMoved: "Uw PEPPOL-id en andere bedrijfsgegevens staan onder",
  peppolMovedLink: "Bedrijfsprofiel",
```

(nl) and

```ts
  peppolMoved: "Your PEPPOL ID and other company details are under",
  peppolMovedLink: "Company profile",
```

(en).

- [ ] **Step 9: Lint, test, build**

Run: `npm run lint && npm test && npm run build`
Expected: lint clean, tests pass, build writes `out/profile/index.html`. A missing translation shows up as a type error here.

- [ ] **Step 10: Look at it**

Run `npm run dev` and open `http://localhost:3000/profile/` (without an API URL the mock fallback is used): check both tabs render, the menu item is highlighted, and `/settings/` shows the pointer instead of the PEPPOL fields. With an API and an admin login, save once with an invalid IBAN and check the field turns red on the General tab, then with a PEPPOL id without scheme and check the page switches to the PEPPOL tab.

- [ ] **Step 11: Commit**

```
git add apps/web
git commit -m "Add the company profile page with General and PEPPOL tabs"
```

---

## Task 7: Docs and final check

**Files:**
- Modify: `CLAUDE.md` (storage block and Status)

- [ ] **Step 1: Record where the profile lives**

In `CLAUDE.md`, in the storage block, change the identity line to:

```
xchange-identity   users, sessions (hashed), login attempts, invitations, sign-ups, MFA, inbox addresses,
                   per organisation SETTINGS and COMPANY_PROFILE
```

and under **Status → Review and booking** add one sentence: `The company profile (General and PEPPOL tabs) fills the seller: when a PDF is read its fixed details win over what the model read, with a notice; checking and exporting only fill empty fields.`

- [ ] **Step 2: Run everything once more**

Run from the repo root: `dotnet test`, then in `apps/web`: `npm run lint && npm test && npm run build`
Expected: all green.

- [ ] **Step 3: Commit**

```
git add CLAUDE.md
git commit -m "Note the company profile in CLAUDE.md"
```

Deployment is by CI after the branch is merged (`deploy-api.yml` on `src/**`, `deploy-ui.yml` for the app); no infrastructure change is needed — same table, same permissions.
