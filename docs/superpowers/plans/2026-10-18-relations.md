# Relations — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** xChange remembers each organisation's customers (VAT, KvK, electronic address, country) when an invoice is booked, and fills the empty buyer fields of a new extraction from them.

**Architecture:** Spec `docs/superpowers/specs/2026-10-18-rulesets-peppol-relaties-design.md`, part 4. Builds on `2026-10-18-rulesets-and-peppol-export.md` (which must be done first): `InvoiceEnrichment` gains relations, `BookInvoiceService` upserts the buyer. Relations live in `xchange-invoices` under `RELATION#<id>` in the organisation's partition.

**Tech Stack:** .NET 8, xUnit, DynamoDB, Next.js static export.

**Execution note:** executed inline by the same session that wrote the spec.

---

## Task 1: Relation record and matching (Domain)

**Files:** `src/InvoicePlatform.Domain/Relations/Relation.cs`, `RelationKeys.cs`; test `tests/InvoicePlatform.Domain.Tests/Relations/RelationKeysTests.cs`

```csharp
public sealed record Relation
{
    public required string RelationId { get; init; }
    public required string OrganizationId { get; init; }
    public string? Name { get; init; }
    public string? VatIdentifier { get; init; }
    public string? LegalRegistrationId { get; init; }
    public string? LegalRegistrationScheme { get; init; }
    public string? ElectronicAddress { get; init; }
    public string? ElectronicAddressScheme { get; init; }
    public string? CountryCode { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

public static class RelationKeys
{
    /// <summary>Upper case, no spaces, dots or dashes: "nl 8149.12.345 b01" -> "NL814912345B01".</summary>
    public static string? NormalizeVat(string? vat);
    /// <summary>Digits only: "1234 5678" -> "12345678".</summary>
    public static string? NormalizeRegistration(string? id);
    /// <summary>VAT first, then registration number. Never the name.</summary>
    public static Relation? FindMatch(IEnumerable<Relation> relations, CanonicalParty party);
}
```

Tests: normalisation cases; VAT match wins over a different KvK match; KvK used when no VAT; same name but no identifiers → no match; empty party → no match.

Commit `Add relations and how they are recognised`.

## Task 2: Repository interface, service, booking hook (Application)

**Files:** `src/InvoicePlatform.Application/Relations/IRelationRepository.cs`, `RelationService.cs`; modify `BookInvoiceService.cs` (constructor gains `RelationService`; after saving the invoice, `RememberBuyerAsync`), `InvoiceEnrichment.cs` (buyer from relations); tests `RelationServiceTests.cs`, `InvoiceEnrichmentTests.cs` additions, `BookInvoiceServiceTests.cs` additions, `Fakes.cs` (`FakeRelationStore`).

```csharp
public interface IRelationRepository
{
    Task<IReadOnlyList<Relation>> ListAsync(string organizationId, CancellationToken ct = default);
    Task<Relation?> GetAsync(string organizationId, string relationId, CancellationToken ct = default);
    Task SaveAsync(Relation relation, CancellationToken ct = default);
}
```

`RelationService(IRelationRepository, IClock)`:
- `RememberBuyerAsync(org, CanonicalParty? buyer)` — skip when no VAT and no KvK; match → update with non-empty values only; else create (`Guid` id).
- `FindForAsync(org, CanonicalParty? party)` → match or null.
- `ListAsync(org)`, `UpdateAsync(org, relationId, RelationUpdate)` → null when not in this organisation; values trimmed, empty → null; VAT/KvK normalised.

Enrichment (buyer): when a relation matches, fill each empty field among electronic address + scheme, legal registration + scheme, VAT, country code; notice `ENRICH-RELATION` (Info) per filled field "… ingevuld uit relatiegegevens."; when the document's value differs from the relation's (compared normalised for VAT/KvK, exact otherwise) → `RELATION-CONFLICT` (Warning) "… wijkt af van de relatiegegevens (<value>). De waarde uit de PDF is behouden.".

Tests: tenant isolation (B never sees A's relation, update across organisations → null); booking creates then updates one relation (no duplicate); booking a buyer without identifiers stores nothing; enrichment fills only empty fields; conflict keeps the PDF value and warns.

Commit `Remember customers when booking and fill new invoices from them`.

## Task 3: DynamoDB repository and registration

**Files:** `src/InvoicePlatform.Infrastructure/Relations/DynamoDbRelationRepository.cs`; modify `DependencyInjection.cs` (register in `AddInvoiceStorage`).

`pk=ORG#<org>`, `sk=RELATION#<id>`; listing is a Query with `begins_with(sk, "RELATION#")`, paginated until `LastEvaluatedKey` is empty. Same table name option as invoices. No new IAM permissions (GetItem/PutItem/Query exist).

Commit `Store relations in DynamoDB`.

## Task 4: Endpoints

**Files:** `src/InvoicePlatform.Api/Relations/RelationEndpoints.cs`, `src/InvoicePlatform.Contracts/Relations/RelationContracts.cs`; modify `Program.cs`; tests `tests/InvoicePlatform.Integration.Tests/RelationEndpointTests.cs`; `SignedInFactory` gains the in-memory relation store.

```
GET /relations                RequireAuthenticatedUser
PUT /relations/{relationId}   RequireAuthenticatedUser + RequireRole(Admin, User); 404 outside the organisation
```

Tests: 401 anonymous; `readonly-a` PUT → 403; `admin-b` PUT org-a relation → 404; `user-a` PUT round-trips.

Commit `Expose relations over HTTP`.

## Task 5: Relations page

**Files:** `apps/web/lib/relations.ts`, `apps/web/app/relations/page.tsx`, `RelationsClient.tsx`; modify `components/nav.ts` (xChange section: "Relaties", icon `users`, `href: "/relations"`), `AppShell.tsx`.

Table: naam, btw-nummer, KvK, elektronisch adres, land. "Wijzigen" opens an edit card (same field set; scheme selects as on the review screen); ReadOnly sees no edit button. Empty state: "Relaties worden vanzelf aangemaakt wanneer u een factuur boekt."

`npm run lint && npm run build`. Commit `Add the relations page`.

## Task 6: Verify

- [ ] `dotnet test`, `npm test`, `npm run lint`, `npm run build` green.
- [ ] Mock-mode click-through still works (relations page shows its empty state without an API).
