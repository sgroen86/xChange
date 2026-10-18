# Conversion API — design

Date: 2026-10-18. Status: **draft, built on branch `feature/conversion-api`, not deployed.** The
owner asked for it overnight ("start working on the api for sending in pdf's"); the choices below
were made without them and are marked *(review)* where they deserve a second look.

## Goal

An ERP or other system sends a PDF and gets the e-invoice back, without a person in the app — and
when the invoice cannot pass the checks, a person can fix it in the app and the system still gets its
file.

## Authentication: API keys per organisation

- An admin creates a key under **Instellingen → API-sleutels**, with a name ("Exact koppeling"). The
  key is shown once: `xck_` + 32 random bytes, base64url. Only its SHA-256 hash is stored, with the
  organisation, name, a short visible prefix, creator, created and last-used time. Revoking deletes it.
- A request sends `Authorization: Bearer xck_…`. `AuthenticationMiddleware` recognises the prefix and
  resolves the key to its organisation; the organisation never comes from the request.
- A key is **not a user**: it is accepted only by the conversion endpoints. Every other route keeps
  rejecting it (401), so a leaked key cannot read the invoice list, relations or settings. *(review)*
- Keys sit in the identity table next to sessions, looked up by hash (`APIKEY#<hash>`), with a listing
  item per organisation.

## Endpoints

```
POST /api/v1/conversions?format=<rulesetId>     body: the PDF (application/pdf), header X-File-Name optional
  -> 202 { conversionId, status: "queued" }      Location: /api/v1/conversions/{id}
GET  /api/v1/conversions/{id}
  -> 200 { conversionId, status, format, invoiceNumber?, violations?, error? }
GET  /api/v1/conversions/{id}/xml
  -> 200 the file (application/xml), or 409 when not ready, 402 on a trial
```

Status values: `queued`, `processing`, `ready`, `needs_review`, `failed`.

- The PDF limit is the same 4 MB as the app (Function URL limit).
- `format` is optional: otherwise the customer's default format, then the organisation's — the same
  resolution as the app.

## Processing

A conversion is an import with source `Api`: stored, queued at once (no approval — the caller holds a
key the admin issued), and read by the worker through the same extraction as everything else, learning
included. After reading, for an `Api` import only, the worker:

1. checks the draft against the chosen format, official validation included;
2. **no errors** → books it (booked by the key, `apikey:<keyId>`), so it is in Facturen with its PDF,
   and the import leaves the review list. Status `ready`;
3. **errors** → leaves it in **Te beoordelen** for a person. Status `needs_review`, with the
   violations. Once someone books it in the app, the same conversion becomes `ready` — the invoice id
   is the import id, so no link needs storing. *(review: auto-booking without a person.)*

`GET …/xml` exports the booked invoice in the chosen format through `ExportInvoiceService`, so the
file, the trial rule (402) and the official validation are exactly the app's.

## Usage and limits

A conversion counts as one conversion, like an upload. A trial can convert and see the status but not
download the XML. No per-key rate limit yet; the queue's worker concurrency (2) already paces reading.

## Not in this piece

Sending UBL in for delivery (needs the access point), webhooks instead of polling, a public developer
page on the website, per-key scopes.
