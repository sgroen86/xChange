# xChange and Bookkeeping — exploring a combination

Date: 2026-10-18. Status: **exploration, not a decision.** Asked for by the owner ("think about
combining xchange and bookkeeping"). CLAUDE.md currently says xChange shares no code, data or
accounts with Bookkeeping; any option below except A changes that rule, which is the owner's call.

## The two products today

| | Bookkeeping (`E:\greenitsolutions\Bookkeeping`) | xChange |
|---|---|---|
| What | Dutch bookkeeping for small businesses: sales and purchase invoices, purchase intake from PDF, bank matching, ledger and journal, VAT return, P&L and balance sheet, time tracking, relations, IMAP inbox | PDF invoices to validated e-invoices (PEPPOL BIS 3.0, NLCIUS, PINT-SG), relations with PEPPOL ids, learning from corrections, mail inbox, batch import |
| Who | in-house tool, several administrations per installation; no sign-up, trial or billing | multi-tenant SaaS with self sign-up, trial, usage metering, plans |
| Stack | PHP 8.1, CodeIgniter 4, MySQL, plain JS; Strato shared hosting | .NET 8 on Lambda, DynamoDB, S3, SQS; static Next.js app |
| Size and tests | ~13k PHP + ~10k JS (plus a duplicated `public/js`), no tests | ~500 tests |
| Invoice reading | Claude via curl, model hardcoded, amounts as PHP `float` | Claude via SDK, model configurable, decimals, evidence and arithmetic checks |
| E-invoicing | none (no UBL or PEPPOL) | the core of the product; access point sending is planned |
| Public API | none; session-cookie SPA backend | conversion API in progress (`2026-10-18-conversie-api-design.md`) |

Bookkeeping also contains `app/Xhange/`, an unfinished, unwired PHP start of the canonical invoice
model — the predecessor of xChange's Domain.

### Fix first, whatever is chosen

In Bookkeeping, `app/Config/Routes.php` lines 81–84 (relations) and the projects, activities and
time-entries routes have **no `apiauth` filter**, there is no global filter in `app/Config/Filters.php`,
and the controllers fall back to company 1 without a session (`RelationsApi.php:12`,
`TimeEntriesApi.php:13`). Read from the code, anyone who can reach the site can read and change
company 1's relations — names, addresses, IBANs, VAT numbers — without logging in. Not verified
against the live site. This is worth fixing now, independent of this exploration.

## Options

**A. Separate products; Bookkeeping is xChange's first API customer.**
Bookkeeping sends its sales invoices to xChange through the conversion API (and, once the access
point exists, has them delivered over PEPPOL); later it pulls received invoices back.
Pro: xChange stays a focused product; Bookkeeping proves the API every ERP will use. Con: two logins
and two customer lists; Bookkeeping's invoices must be mapped to the canonical model (rate per line
→ VAT category). Effort: small to medium, mostly PHP.

**B. Two-way connector.**
A, plus a proper authenticated import endpoint in Bookkeeping: purchase invoices read and checked by
xChange (and later received over PEPPOL) arrive in Bookkeeping as booked purchase invoices with their
journal entry. Bookkeeping's own Claude OCR is retired — one reading engine, the better one.
Pro: Bookkeeping becomes "the ledger behind xChange". Con: relations must be matched (VAT/KvK number as
the shared key), sync errors need handling. Effort: medium.

**C. One product, bookkeeping as an add-on module.**
One sign-up at xChange; bookkeeping offered to xChange organisations through single sign-on, still
served by the PHP app at first. Pro: one brand, an upsell path. Con: SSO and tenant mapping across
MySQL and DynamoDB; Bookkeeping's gaps (no tests, floats, the auth hole, shared hosting, Dutch-only UI)
become customer-facing. Effort: large.

**D. Full merge: rebuild bookkeeping inside xChange.**
Ledger, VAT return, bank and reports rebuilt in .NET; the PHP app retired. Pro: one codebase, one
standard. Con: months of work, a relational database, and xChange turns from a converter that works
*with* Exact, Moneybird and e-Boekhouden into a competitor of them. Effort: very large.

## Recommendation

**A now, B later; not C or D for the foreseeable future.**

- The goal is a working, paying xChange first. A costs xChange nothing: it is the conversion API
  being built anyway, and Bookkeeping becomes its first real integration test.
- B is the natural next step once sending and receiving over PEPPOL work. It gives Bookkeeping
  e-invoicing without building any of it.
- C and D only make sense if bookkeeping itself becomes something to sell. That is a separate market
  with established players, and it needs Bookkeeping brought up to xChange's standard first (tests,
  decimals, auth). Revisit if customers start asking xChange for bookkeeping.

## Questions for the owner

1. Is Bookkeeping meant to stay an internal tool, or could it become a product?
2. Should the CLAUDE.md separation rule be relaxed for an API integration (A/B)? A keeps the rule
   intact: Bookkeeping is then simply a customer with an API key.
3. Fix the Bookkeeping auth hole now? (A small change in `Routes.php` and the two controllers.)
