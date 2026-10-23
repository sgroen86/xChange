# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Status

Last updated 2026-10-23. Running in production on `app.xchangeinvoice.com`:

- **Identity** — xChange's own accounts (email + bcrypt), sessions stored only as a SHA-256 hash,
  lockout per client address and per account, two-step verification (TOTP + recovery codes;
  mandatory for admins and platform admins, secrets encrypted with KMS), invitations, and self
  sign-up with a trial of 10 conversions (closed until launch; the platform admin opens it).
- **Extraction** — a PDF is read by Claude (model chosen on the Platform page) into a
  `CanonicalInvoiceDraft` with per-field evidence and arithmetic warnings. A single upload is read
  inside the request; batch uploads and PDFs mailed to an organisation's inbox address go through an
  SQS queue to the Worker (mailed PDFs wait for a user's approval first). Corrections a reviewer
  made are learned and applied to the next invoice of that organisation.
- **Review and booking** — the app recalculates deterministically and lets the draft be corrected;
  an invoice can be saved as a concept, booked or deleted. Booking remembers the buyer as a relation
  (customer) and looks the buyer up on PEPPOL.
- **Export** — a booked invoice becomes a `ValidatedCanonicalInvoice` and is written as UBL for
  PEPPOL BIS Billing 3.0 (NL, BE, UK), NLCIUS / SI-UBL 2.0 or PINT-SG, in the format chosen per
  customer, and checked by the official validation artefacts (a Java/Saxon Lambda in `validator/`).
- **Payments** — Stripe (sandbox): Starter, Pro and Business plans and a one-off credit pack, through
  Checkout and the Customer Portal; webhooks drive the plan; a usage meter counts conversions.
- **Monitoring** — every model call is recorded with tokens and cost, and every reviewed booking with
  how accurately the model read it; platform admins see both on the Kosten page.
- **Languages** — the app and the API's messages in Dutch and English.
- **Website** — `apps/site` on `www.xchangeinvoice.com`, behind a password and `noindex` until launch.

Not built yet, deliberately:

- **Sending and receiving over PEPPOL** through an access point — designed
  (`specs/2026-10-18-verzenden-via-access-point-design.md`), no code.
- **The conversion API** for ERP systems — built on branch `feature/conversion-api`, **parked** by the
  owner; it needs bringing up to date before it can be merged.
- **VAT on payments (Stripe Tax) and live Stripe** — wait for the choice of selling company.
- **Postgres, EF Core, migrations.** There is no relational database; DynamoDB and S3 carry
  everything.

## What this is

A multi-tenant SaaS that turns invoice PDFs into validated, structured invoice data and e-invoices.
It is a **modular monolith, not microservices**: one solution, one deployable API, one deployable
set of worker functions, module boundaries enforced by project references rather than by network
calls.

## Processing pipeline

```
PDF
  -> document extraction        (raw text/layout from the PDF)
  -> invoice interpretation     (fields identified; LLM/OCR allowed here)
  -> CanonicalInvoiceDraft      (unvalidated, may contain low-confidence fields)
  -> review                     (human confirmation/correction in the web app)
  -> booking                    (draft + source PDF kept, scoped to the organisation)
  -> ValidatedCanonicalInvoice  (the only type downstream code may consume)
  -> output adapters            (UBL per ruleset, canonical XML)
```

Extraction and interpretation are one step: `AnthropicInvoiceInterpreter` hands the PDF to Claude
and gets structured fields back. Export runs a ruleset's checks on the booked invoice, produces a
`ValidatedCanonicalInvoice` only when they pass, and `UblInvoiceWriter` writes the file from that
type alone; the official validator then checks the file before it is handed out.

`CanonicalInvoiceDraft` and `ValidatedCanonicalInvoice` are distinct types, not one type with an
`IsValidated` flag. Output adapters accept only `ValidatedCanonicalInvoice`; if a code path lets a
draft reach an adapter, that is a bug in the design, not a case to special-case.

## Solution layout and dependency rule

```
InvoicePlatform.sln
src/
  InvoicePlatform.Domain           entities, value objects, invariants, rulesets, domain services
  InvoicePlatform.Application      use cases, orchestration, provider *interfaces*
  InvoicePlatform.Infrastructure   provider implementations (Anthropic, DynamoDB, S3, SQS, SES, KMS, Stripe, Secrets Manager)
  InvoicePlatform.Contracts        wire-level DTOs shared with the frontend
  InvoicePlatform.Api              ASP.NET Core Web API — HTTP concerns only
  InvoicePlatform.Worker           Lambda functions: ImportFunction (SQS) and MailFunction (SES)
tests/
  InvoicePlatform.Domain.Tests         xUnit; references Domain only
  InvoicePlatform.Application.Tests    xUnit; references Application only
  InvoicePlatform.Integration.Tests    xUnit; references Api (WebApplicationFactory)
validator/                         Java: the official validation artefacts (Saxon) as a Lambda
apps/
  web                              Next.js + TypeScript app, static export
  site                             Next.js public website, static export (NL/EN/FR)
tools/                             model-comparison, stripe-setup
infra/aws/                         CloudFormation (see Deployment)
.github/workflows/                 deploy-api, deploy-ui, deploy-site (+ one-off retire-strato-xchange)
docs/                              deployment, UI conventions, designs and plans
```

Target framework is `net10.0` throughout — the LTS runtime AWS Lambda offers as a
managed runtime (`dotnet10`, supported until November 2028). Only move to another
version when Lambda offers it as a managed runtime; a non-LTS version (net11.0)
would mean moving the API off the managed runtime first.

Dependencies point inward only:

```
Api ─┐
     ├─> Application ─> Domain
Worker ─┘        ^
Infrastructure ──┘   (implements Application's interfaces; referenced only by Api/Worker composition roots)

Api also ─> Contracts ─> Domain
```

Current reference edges, which must not be widened casually:

| Project | References |
|---|---|
| Domain | *(none)* |
| Application | Domain |
| Infrastructure | Application, Domain |
| Contracts | Domain (the wire DTOs carry `CanonicalInvoiceDraft` itself) |
| Api | Application, Infrastructure, Contracts |
| Worker | Application, Infrastructure |
| Domain.Tests | Domain |
| Application.Tests | Application |
| Integration.Tests | Api |

`InvoicePlatform.Domain.Tests/DependencyRuleTests.cs` fails if Domain ever gains a reference to
ASP.NET Core, EF Core, or an AWS assembly. Keep it passing rather than relaxing it.

Concretely:

- **Domain** must not reference AWS SDKs, OCR/LLM clients, EF Core, ASP.NET Core, or any web
  framework. If Domain needs something from the outside, Application declares an interface for it.
- **Application** owns the use cases and the provider interfaces (`IInvoiceInterpreter`,
  `IInvoiceRepository`, `IDocumentStore`, `IUserStore`, `ISessionStore`, `IUsageMeter`,
  `IBillingProvider`, `IClock`, and so on). It knows nothing about which vendor implements them.
- **Infrastructure** implements those interfaces. **Provider SDK types must never escape
  Infrastructure** — no `Amazon.*`, Anthropic or Stripe SDK type may appear in an Application or
  Domain signature. Map to domain/application types at the adapter boundary.
- **Api** does HTTP and nothing else: routing, model binding, auth, serialization, status codes. No
  business rules, no direct provider calls. Handlers call Application use cases.
- **Worker** holds the background processing: reading imported PDFs from the queue, and turning
  received mail into imports. Like Api, it only composes Application and Infrastructure.

User-facing messages come from catalogues per area (an interface with a Dutch and an English class,
picked per request from `Accept-Language` through `Texts`); never hardcode a message in one language.

## Hard rules

These are not style preferences. Violating them is a defect.

1. **LLMs never generate final XML.** An LLM may interpret a document into structured fields; the
   XML is produced by deterministic serialization code from a `ValidatedCanonicalInvoice`. Never
   prompt a model to emit the output document.
2. **Financial calculations and XML generation are deterministic.** Same input, same output, no
   model in the path, fully unit-testable without network access. `InvoiceCalculationValidator`,
   `CanonicalInvoiceXmlSerializer` and `UblInvoiceWriter` are pure; so is `apps/web/lib/money.ts`.
3. **Use `decimal` for financial amounts.** Never `double` or `float`, in C# or in persistence. On
   the TypeScript side, do not round-trip amounts through JS `number` — amounts cross the wire as
   strings (`DecimalStringJsonConverter` in Contracts) and stay strings in the app.
4. **Every tenant-owned record contains `OrganizationId`.** Every query that reads tenant data filters
   on it. Add it to the schema, the entity, and the query — a missing filter is a cross-tenant data
   leak, so treat it as a security bug.
5. **Do not add functionality that was not requested.** No speculative abstractions, no extra
   endpoints, no "while I'm here" features.
6. **Keep the first implementation minimal and locally runnable.** Prefer the smallest thing that
   runs end to end on a laptop over the most general design.

## Multi-tenancy

`OrganizationId` is the tenant key. It is resolved from the authenticated session by
`AuthenticationMiddleware` at the API boundary and passed explicitly into Application use cases —
never read from ambient/static state inside Domain or Application, and **never taken from the
request**. If a tenant route grows an `organizationId` parameter, that is the bug. The only routes
that take one are under `/platform/`, each guarded by `RequirePlatformAdmin`.

In storage the tenant is the partition key, not a filter:

```
xchange-invoices   pk = ORG#<organizationId>   sk = INVOICE#…, IMPORT#…, RELATION#…, EXTRACTION#…,
                                                    LEARNING, ALLOWANCE, USAGE#…, COST#…, QUALITY#…
                   pk = PLATFORM                    platform-wide sums for the Kosten page
                   pk = STRIPE#…                    Stripe customer -> organisation, processed events
xchange-identity   users, sessions (hashed), login attempts, invitations, sign-ups, MFA, inbox addresses
```

so another organisation's invoice is unreachable rather than merely unauthorised. S3 keys are
`<organizationId>/<invoiceId>.pdf`, and a document is only ever fetched via its invoice record.

`BookInvoiceServiceTests.An_invoice_is_invisible_to_another_organisation` is the test that matters
most here. If it fails, the keys are wrong, not the test.

## Storage and queueing

- Documents (source PDFs, derived artifacts) go to S3 behind `IDocumentStore`. The database stores
  keys and metadata, never blobs. Received mail lands in its own bucket and expires after 30 days.
- Records go to DynamoDB. Listing is a Query on the partition, never a Scan.
- **Imports are queued:** batch uploads and mailed PDFs are stored, put on SQS (`xchange-imports`, with
  a dead-letter queue) and read by the Worker. A **single upload is still read inside the request**
  (Lambda timeout 300 s); moving it onto the queue is possible but not done.
- Production runs on AWS. Local development runs without AWS credentials for anything that does not
  touch storage: `dotnet test` passes offline, and the frontend falls back to mock data when no API
  URL is configured. Login and booking do need real AWS, because DynamoDB and S3 have no local
  stand-in yet.

## Deployment

- **API and Worker** — zips on the managed `dotnet10` Lambda runtime (the API behind a Function URL),
  region `eu-central-1`, stack `xchange-service` (`infra/aws/service.yml`). Pushed by
  `.github/workflows/deploy-api.yml` on any change under `src/**` or `infra/aws/**`; a red
  `dotnet test` blocks the deploy. The Anthropic and Stripe keys live in Secrets Manager and are read
  at runtime by the function's own role, never as environment variables.
- **A Function URL rejects requests over 6 MB**, and a binary body is base64-encoded first, so
  uploads are capped at 4 MB in both the API and the UI. Raising it means presigned S3 uploads, not
  a bigger constant.
- **App** — built and shipped by `.github/workflows/deploy-ui.yml` to S3 behind CloudFront
  (stack `xchange-app`, `infra/aws/app.yml`), served at `https://app.xchangeinvoice.com/`. The API
  allows only that origin (CORS). The former copy at `greenitsolutions.net/xchange/` was taken
  offline on 2026-10-23; nothing deploys to Strato any more.
- **Website** — `.github/workflows/deploy-site.yml` to S3 + CloudFront (stack `xchange-site`),
  `https://www.xchangeinvoice.com/`; the preview password is the secret `xchange/site-preview`.
- **Applied by hand, once or rarely:** `bootstrap.yml` (the GitHub deploy role — apply it *before*
  pushing a change that needs new deploy permissions), `domain.yml` (Route 53 zone for
  xchangeinvoice.com; Strato keeps the registration and the info@/support@ mailboxes) and
  `certificate.yml` (ACM, in us-east-1).
- **Deploy through CI, not by hand.** A manual `update-function-code` during earlier debugging left
  production ahead of the repository.

`docs/aws-deployment.md` has the account bootstrap, the OIDC trust and the troubleshooting notes.
Its closing sections still describe App Runner and an unauthenticated API and predate login and
storage; the workflow files are the accurate source.

## Commands

Run from the repo root unless noted. Building needs the **.NET 10 SDK**.

```
dotnet build                     # whole solution
dotnet test                      # all three test projects
dotnet test tests/InvoicePlatform.Application.Tests          # one project
dotnet test --filter "FullyQualifiedName~HealthEndpointTests.Health_endpoint_reports_healthy"

dotnet run --project src/InvoicePlatform.Api            # http://localhost:5xxx/health
# needs a key: dotnet user-secrets set "Anthropic:ApiKey" "<key>" --project src/InvoicePlatform.Api
#              (or the ANTHROPIC_API_KEY environment variable)
```

```
cd apps/web            # or apps/site
npm run dev
npm run build      # static export -> out/
npm run lint
npm test
```

**Node >= 20.9.0 is required** by Next.js 16. The build fails immediately on older runtimes with
`You are using Node.js <version>. For Next.js, Node.js version ">=20.9.0" is required.` — that
message means the toolchain, not the code.

There is no `docker compose`. Add it when something gains a local stand-in worth running.

## Frontend

`apps/web` is a **static export** (`output: "export"`, no base path, `trailingSlash`), served
from S3 through CloudFront with no Node runtime. `npm run build` writes `apps/web/out`; CI uploads
its contents. Do not introduce server components that need a runtime, route handlers, middleware or
`next/image` optimisation: none of them exist in an export.

Invoice ids are created at runtime, so `/invoices/[invoiceId]/review` cannot be fully pre-rendered.
Only a placeholder is emitted and the CloudFront request function in `infra/aws/app.yml` serves
any id from it; the client reads the real id from the URL. Client-side navigation never touches it.

All interface text comes from the dictionaries in `apps/web/lib/i18n/` (one file per area, `en` typed
as `nl`, so a missing translation is a compile error); the language switch is in the header.

The API URL is baked in at build time (the workflow reads it from the `xchange-service` stack).
**With it unset the app falls back to the mock in `apps/web/lib/canonical.ts`** — that path still
exists so a build that has never been pointed at an API works rather than failing in the browser. It
is a fallback, not the normal flow. Money is integer-scaled in `apps/web/lib/money.ts`, never
floating point, and the XML serialiser is deterministic.

The UI is a port of the Bookkeeping design system — plain CSS in `apps/web/styles/`, tokens copied
verbatim from `variables.css`, icons copied from `js/components/icons.js`. **No CSS framework and no
component library.** See `docs/bookkeeping-ui-style.md` before changing anything visual.

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

xChange keeps its own users, its own database, its own domain and its own deploy street. It shares a
problem domain with Bookkeeping and borrows its look; it shares no code, no data, no accounts and no
web origin. Keep it that way. (Only inbox addresses on the old `inbox.greenitsolutions.net` domain
still receive, for organisations that use them.)

## Designs and plans

`docs/superpowers/specs/` holds accepted designs, `docs/superpowers/plans/` the step-by-step plans
built from them. Both are dated and are the record of *why*, not just what. Most are built; the
ones that are not:

- `specs/2026-10-18-verzenden-via-access-point-design.md` — sending over PEPPOL; not started.
- `specs/2026-10-18-conversie-api-design.md` — built on a branch, parked.
- `specs/2026-10-18-kosten-en-tokens-masterplan.md` — phases 1 and 2 (recording, the Kosten page)
  are built; 3–5 (adjusting, alerts, reconciling) are not.
- `specs/2026-10-18-verdienende-site-plan.md` — the road to launch; phase E (launch) is open.
- `specs/2026-10-18-xchange-en-bookkeeping-verkenning.md` — an exploration, not a decision.

## Open decisions

Do not silently pick one of these and build on it; raise it first.

- **The selling company** (new, the owner's Dutch company, or the Singapore one) — it decides the
  legal texts, VAT handling (Stripe Tax) and which Stripe account goes live.
- **A local stand-in for DynamoDB and S3**, so login and booking run without AWS credentials.
- **Whether booking stays reversible.** Today a booked invoice can be edited and re-booked; locking
  belongs with a real accounting process, not before it.
- **Which access point** to send through (the design proposes one per sender country).

Settled, for the record: interpretation is Claude via the Anthropic SDK (model in configuration,
never hardcoded at the call site); identity is xChange's own login rather than an external provider;
persistence is DynamoDB and S3, so there is no migration tooling to choose yet; the queue is SQS;
BT-153 (the article name per line) is written from the line's description; payments are Stripe.
