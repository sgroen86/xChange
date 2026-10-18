# Monitoring tokens and costs — masterplan

Date: 2026-10-18. Status: **plan for review**, asked for by the owner: "monitoring tokens and costs,
able to adjust etc. per user per company total everything". Nothing is built yet.

## Where we are

- Every extraction logs model, duration and input/output tokens to CloudWatch
  (`AnthropicInvoiceInterpreter`). They are not stored anywhere queryable.
- `IUsageMeter` counts **conversions** per organisation (`ALLOWANCE` with used and limit,
  `USAGE#yyyy-MM` per month). Trials have a limit of 10.
- The model is one platform-wide setting (`PlatformSettings.ExtractionModel`), chosen on the Platform
  page.
- AWS costs (Lambda, DynamoDB, S3, SQS, SES, CloudFront, the Java validator) show only in the AWS
  bill.

Measured: Haiku 5.5 is about $0.002–0.003 per invoice, Sonnet 5.5 about $0.03, Opus about $0.09.
With the proposed prices (€19 for 50 conversions and up) the model cost is small. The risk is not the
average invoice but outliers — a 40-page PDF, a retry loop, a mailbox flooded with PDFs — and
choosing a more expensive model for everyone.

## Goals

1. **See** cost per conversion, per user, per organisation, per model and for the platform, per day
   and per month — tokens and money.
2. **Adjust**: model per organisation, prices in one place, limits per organisation and per user.
3. **Be warned** before a budget is reached, not after.
4. **Reconcile** with the Anthropic and AWS invoices, so the numbers are trusted.
5. **Know the margin**: revenue per organisation against what it costs.

## Phase 1 — record every model call (foundation)

- `IInvoiceInterpreter` returns token usage with its result: input, output, cache-write and
  cache-read tokens, model, duration. Infrastructure maps the SDK's `Usage`; no SDK type leaves it.
- `ExtractInvoiceService` writes one **usage event** per call to a new `IUsageLedger`:
  `organizationId, userId (or apikey:<id> / email import), source (upload | email | api | retry),
  extractionId, model, tokens, costMicros, priceVersion, at`. Failed calls are recorded too — they cost
  money even when the conversion is refunded.
- Cost is computed **when the event is recorded**, from a price table (`ModelPrices`: per model, per
  million input/output/cache tokens, in USD micros as integers — no floating point), stored in
  platform settings with a version. A later price change never rewrites history.
- Storage (invoice table, tenant-partitioned as always):
  - `ORG#<org>  COST#<yyyy-MM-dd>#<eventId>` — the event, TTL 13 months;
  - `ORG#<org>  COSTSUM#<yyyy-MM>` — atomic `ADD` counters: conversions, tokens per kind, cost;
  - `ORG#<org>  COSTSUM#<yyyy-MM>#USER#<userId>` — the same per user;
  - `PLATFORM  COSTSUM#<yyyy-MM-dd>` and `#<yyyy-MM>` — platform totals, also per model.
  Every summary is one `UpdateItem ADD`, so recording stays cheap and needs no scan.
- Tests: the cost formula (integer micros), aggregation, and that another organisation's events are
  unreachable.

## Phase 2 — look at it

- **Platform page → "Kosten"** (platform admin only): today, this month and last month in tokens and
  money; split per model; a table of organisations (conversions, cost, average cost per conversion,
  plan, revenue, margin), sortable; drill-down into one organisation per user and its most expensive
  conversions (link to the invoice). CSV export per month.
- **Settings → Gebruik** (organisation admin): conversions per month and per user against the plan.
  Customers see conversions, not euros of model cost.

## Phase 3 — adjust

- **Model per organisation**: an override on top of the platform default (for example Haiku for
  everyone, Sonnet for a customer with difficult invoices). The extraction uses the organisation's
  model, then the platform's.
- **Prices** editable on the Platform page, versioned (phase 1).
- **Limits**:
  - per organisation: the conversions limit that exists today, plus an optional monthly **cost cap**
    as a safety net (a hard stop, separate from the plan);
  - per user: an optional monthly conversion limit, set by the organisation admin;
  - per request: refuse PDFs above N pages, or count them as several conversions *(pricing decision)*.
- **Guards against runaway cost**: a maximum number of retries per import, a per-organisation hourly
  rate limit on extractions, and the existing approval for mailed PDFs.

## Phase 4 — be warned

- A CloudWatch metric per recorded event (platform cost per hour), published by the API and worker.
- Alarms by email (SNS): platform spend per day above X; one organisation above Y per day; failure
  rate above Z. Thresholds as parameters in `service.yml`.
- An organisation at 80% and at 100% of its plan gets a banner in the app and, later, an email.
- An Anthropic workspace spend limit as the outer safety net (set in the Anthropic console).

## Phase 5 — total everything and reconcile

- **AWS**: cost-allocation tags on every stack resource (`app=xchange`, `component=api|worker|...`),
  and a monthly Cost Explorer pull into `PLATFORM COSTSUM#<yyyy-MM>#AWS` by component.
- **Anthropic**: the Usage and Cost API, pulled daily into `PLATFORM COSTSUM#<yyyy-MM-dd>#ANTHROPIC`
  and compared with our own sum. A difference above a few percent is flagged on the Platform page.
- **Margin per organisation**: revenue from Stripe (once phase D of the site plan exists) against
  model cost plus a share of fixed AWS cost per conversion.

## Order and size

| Phase | What it gives | Size |
|---|---|---|
| 1 Record | the truth, per call | 1–2 days |
| 2 Look | platform dashboard, customer usage view | 2 days |
| 3 Adjust | model per org, limits, guards | 1–2 days |
| 4 Warn | alarms, 80%/100% banners | 1 day |
| 5 Reconcile | AWS and Anthropic totals, margin | 2 days, after Stripe |

Phase 1 first, and soon: every day without it is data that cannot be recovered later.

## Decisions for the owner

1. Customers see conversions only, never euros of model cost — agreed?
2. Large PDFs: a page limit, or count per N pages?
3. Default alarm thresholds (suggestion: platform > $10/day, one organisation > $3/day).
4. Is a per-user limit needed now, or only per organisation?
