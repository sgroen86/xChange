# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Status

Scaffolded only. The solution, projects, reference graph, a `/health` endpoint and the Next.js app
exist and build; **no invoice processing, AWS SDKs, authentication or database migrations are
implemented yet** — that was deliberate, not an oversight. Domain, Application, Infrastructure and
Contracts are empty by design.

## What this is

A multi-tenant SaaS that turns supplier invoice PDFs into validated, structured invoice data and
emits it to downstream systems. It is a **modular monolith, not microservices**: one solution, one
deployable API, one deployable worker, module boundaries enforced by project references rather than
by network calls.

## Processing pipeline

```
PDF
  -> document extraction        (raw text/layout from the PDF)
  -> invoice interpretation     (fields identified; LLM/OCR allowed here)
  -> CanonicalInvoiceDraft      (unvalidated, may contain low-confidence fields)
  -> review                     (human confirmation/correction in the web app)
  -> ValidatedCanonicalInvoice  (the only type downstream code may consume)
  -> output adapters            (XML and other target formats)
```

`CanonicalInvoiceDraft` and `ValidatedCanonicalInvoice` are distinct types, not one type with an
`IsValidated` flag. Output adapters accept only `ValidatedCanonicalInvoice`; if a code path lets a
draft reach an adapter, that is a bug in the design, not a case to special-case.

## Solution layout and dependency rule

```
InvoicePlatform.sln
src/
  InvoicePlatform.Domain           entities, value objects, invariants, domain services
  InvoicePlatform.Application      use cases, orchestration, provider *interfaces*
  InvoicePlatform.Infrastructure   provider implementations (AWS, storage, OCR, LLM, EF Core/Postgres, queue)
  InvoicePlatform.Contracts        wire-level DTOs shared with the frontend
  InvoicePlatform.Api              ASP.NET Core Web API — HTTP concerns only
  InvoicePlatform.Worker           .NET Worker — long-running invoice processing
tests/
  InvoicePlatform.Domain.Tests         xUnit; references Domain only
  InvoicePlatform.Integration.Tests    xUnit; references Api only
apps/
  web                              Next.js + TypeScript frontend
```

Target framework is `net9.0` throughout.

Dependencies point inward only:

```
Api ─┐
     ├─> Application ─> Domain
Worker ─┘        ^
Infrastructure ──┘   (implements Application's interfaces; referenced only by Api/Worker composition roots)

Api also ─> Contracts
```

Current reference edges, which must not be widened casually:

| Project | References |
|---|---|
| Domain | *(none)* |
| Application | Domain |
| Infrastructure | Application, Domain |
| Contracts | *(none — add Domain only if a contract genuinely needs a domain type)* |
| Api | Application, Infrastructure, Contracts |
| Worker | Application, Infrastructure |
| Domain.Tests | Domain |
| Integration.Tests | Api |

`InvoicePlatform.Domain.Tests` contains a test that fails if Domain ever gains a reference to
ASP.NET Core, EF Core, or an AWS assembly. Keep it passing rather than relaxing it.

Concretely:

- **Domain** must not reference AWS SDKs, OCR/LLM clients, EF Core, ASP.NET Core, or any web
  framework. If Domain needs something from the outside, Application declares an interface for it.
- **Application** owns the use cases and the provider interfaces (`IDocumentExtractor`,
  `IInvoiceInterpreter`, `IDocumentStore`, `IJobQueue`, and so on). It knows nothing about which
  vendor implements them.
- **Infrastructure** implements those interfaces. **Provider SDK types must never escape
  Infrastructure** — no `Amazon.*`, OCR, or LLM SDK type may appear in an Application or Domain
  signature. Map to domain/application types at the adapter boundary.
- **Api** does HTTP and nothing else: routing, model binding, auth, serialization, status codes. No
  business rules, no direct provider calls. Handlers call Application use cases.
- **Worker** runs the long-running processing stages off the queue. Anything that can take seconds
  (extraction, interpretation) belongs here, not in an API request.

## Hard rules

These are not style preferences. Violating them is a defect.

1. **LLMs never generate final XML.** An LLM may interpret a document into structured fields; the
   XML is produced by deterministic serialization code from a `ValidatedCanonicalInvoice`. Never
   prompt a model to emit the output document.
2. **Financial calculations and XML generation are deterministic.** Same input, same output, no
   model in the path, fully unit-testable without network access.
3. **Use `decimal` for financial amounts.** Never `double` or `float`, in C# or in persistence
   (Postgres `numeric`). On the TypeScript side, do not round-trip amounts through JS `number` where
   precision matters — carry them as strings from the API.
4. **Every tenant-owned record contains `OrganizationId`.** Every query that reads tenant data filters
   on it. Add it to the schema, the entity, and the query — a missing filter is a cross-tenant data
   leak, so treat it as a security bug.
5. **Do not add functionality that was not requested.** No speculative abstractions, no extra
   endpoints, no "while I'm here" features.
6. **Keep the first implementation minimal and locally runnable.** Prefer the smallest thing that
   runs end to end on a laptop over the most general design.

## Multi-tenancy

`OrganizationId` is the tenant key. It is resolved from the authenticated principal at the API
boundary and passed explicitly into Application use cases — never read from ambient/static state
inside Domain or Application. Worker jobs carry the `OrganizationId` in the job payload; a job
without one must fail rather than process against a default tenant.

## Storage and queueing

- Documents (source PDFs, derived artifacts) go to S3-compatible object storage behind an
  Application-level interface. The database stores keys and metadata, never blobs.
- Processing is asynchronous: the API accepts an upload, persists it, enqueues a job, and returns.
  The Worker consumes the queue. The API never blocks on extraction or interpretation.
- Production runs on AWS. Local development must run without AWS credentials — the same interfaces,
  bound to local adapters. Do not write code that only works against real AWS.

## Commands

Run from the repo root unless noted.

```
dotnet build                     # whole solution
dotnet test                      # both test projects
dotnet test tests/InvoicePlatform.Domain.Tests          # one project
dotnet test --filter "FullyQualifiedName~HealthEndpointTests.Health_endpoint_reports_healthy"

dotnet run --project src/InvoicePlatform.Api            # http://localhost:5xxx/health
dotnet run --project src/InvoicePlatform.Worker
```

```
cd apps/web
npm run dev
npm run build      # static export -> apps/web/out
npm run lint
```

**Node >= 20.9.0 is required** by Next.js 16. The build fails immediately on older runtimes with
`You are using Node.js <version>. For Next.js, Node.js version ">=20.9.0" is required.` — that
message means the toolchain, not the code.

There is no `docker compose` yet; add it when the first real dependency (Postgres, object storage,
queue) lands.

## Frontend deployment

`apps/web` is a **static export** (`output: "export"`, `basePath: "/xchange"`, `trailingSlash`),
because production is `https://www.greenitsolutions.net/xchange/` — Apache + PHP shared hosting with
no Node runtime. `npm run build` writes `apps/web/out`; deploy its contents to `/xchange/` on that
host. Do not introduce server components that need a runtime, route handlers, middleware or
`next/image` optimisation: none of them exist in an export.

Invoice ids are created at runtime, so `/invoices/[invoiceId]/review` cannot be fully pre-rendered.
Only a placeholder is emitted and `apps/web/public/.htaccess` rewrites any id onto it; the client
reads the real id from the URL. Client-side navigation never touches Apache.

The UI is a port of the Bookkeeping design system — plain CSS in `apps/web/styles/`, tokens copied
verbatim from `variables.css`, icons copied from `js/components/icons.js`. **No CSS framework and no
component library.** See `docs/bookkeeping-ui-style.md` before changing anything visual.

The prototype's processing is mocked end to end (`apps/web/lib/canonical.ts`): no OCR, no LLM, no
AWS, no PEPPOL. Money is integer-scaled in `apps/web/lib/money.ts`, never floating point, and the
XML serialiser is deterministic.

## Repository context

Origin is https://github.com/sgroen86/xChange. The project lives at `E:\xChange`, outside the
`greenitsolutions` working tree.

`docs/bookkeeping-ui-style.md` records the UI conventions, design tokens and reusable components of
the sibling Bookkeeping app (`E:\greenitsolutions\Bookkeeping`), which xChange's frontend should
match. **Read it before writing any UI**, and note its section 11: Bookkeeping already contains a
PHP canonical-invoice implementation covering the same problem space as `InvoicePlatform.Domain`.

The `greenitsolutions` repo alongside it holds unrelated projects (`BillingDocViewer`,
`Bookkeeping`, `TradingApp`, `greenitsolutions.net`). Do not import code from them. The exception is
Bookkeeping, whose UI conventions xChange deliberately follows — see the doc above.

## Open decisions

Do not silently pick one of these and build on it; raise it first.

- Concrete queue technology in production (SQS vs. alternative) and its local stand-in.
- Document extraction provider and interpretation model.
- Auth/identity provider and how `OrganizationId` is carried in the token.
- Migration tooling for Postgres.
