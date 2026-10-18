# Conversion API

Send a supplier PDF, get the e-invoice (UBL XML) back - without anyone opening the app. When an
invoice cannot pass the checks on its own, it waits for a person in the app, and the same conversion
becomes ready once they book it.

Base URL: the API's address (in production the `XCHANGE_API_URL` the web app is built with), followed
by `/api/v1`.

## Authentication

An admin creates a key under **Instellingen → API-sleutels**. The key looks like `xck_…` and is shown
**once**; xChange keeps only its SHA-256 hash. Lost it? Revoke it there and create a new one.

Send it as a bearer token:

```
Authorization: Bearer xck_…
```

A key belongs to one organisation, and everything it sends or reads is that organisation's. It opens
the three conversion endpoints below and **nothing else**: every other route (invoices, relations,
settings, imports, key management) answers `401` to a key. A signed-in app session in turn is not
accepted by the conversion endpoints. Revoking a key takes effect immediately.

## Endpoints

### `POST /conversions`

The body is the PDF itself, not a form.

| | |
|---|---|
| `Content-Type` | `application/pdf` |
| `X-File-Name` | optional; the file name shown in the app (default `document.pdf`) |
| `?format=` | optional ruleset id, e.g. `peppol-bis-billing-3.0`, `nlcius-si-ubl-2.0`, `pint-sg-billing-1` |

Without `format`, the format is chosen as in the app: the customer's default, else the
organisation's, else its country's.

`202 Accepted`, with `Location: /api/v1/conversions/{id}`:

```json
{ "conversionId": "4f0c…", "status": "queued", "format": "peppol-bis-billing-3.0",
  "invoiceNumber": null, "violations": null, "error": null }
```

`400` when the body is empty or not a PDF, or the format does not exist; `413` over 4 MB.

### `GET /conversions/{id}`

`200` with the same shape. `404` for an id that is not one of this organisation's conversions.

### `GET /conversions/{id}/xml`

`200` with the file (`application/xml`, `Content-Disposition` carries the file name). Official
warnings that do not block the file come base64-encoded as JSON in `X-Validation-Warnings`.

| Status | Meaning |
|---|---|
| `409` | not ready yet (see the status), or its format no longer exists |
| `402` | the organisation is on a trial (see below) |
| `422` | the booked invoice no longer passes the rules; the body lists the violations |
| `503` | the official validator is unavailable; try again shortly |

## Statuses

| Status | Meaning | Next |
|---|---|---|
| `queued` | stored, waiting to be read | poll |
| `processing` | being read | poll |
| `ready` | booked; the file can be fetched | `GET …/xml` |
| `needs_review` | read, but something needs a person: rule violations (listed in `violations`), a total on the PDF that does not add up, or the official validator was unavailable. It is in the organisation's **Te beoordelen** list. | poll; becomes `ready` once someone books it |
| `failed` | the PDF could not be read (`error` says why), or it was removed in the app without booking | send it again if appropriate |

A conversion that passes every check - the format's runtime rules, the complete official rules, and
no arithmetic or relation warnings from reading - is booked automatically and appears under
**Facturen** with its PDF.

Polling every 5-10 seconds is plenty; reading takes from a few seconds to about a minute.

## Example

```bash
KEY=xck_...
API=https://<api-host>/api/v1

curl -s -X POST "$API/conversions?format=peppol-bis-billing-3.0" \
  -H "Authorization: Bearer $KEY" \
  -H "Content-Type: application/pdf" \
  -H "X-File-Name: invoice-2026-041.pdf" \
  --data-binary @invoice-2026-041.pdf
# {"conversionId":"4f0c…","status":"queued",…}

curl -s "$API/conversions/4f0c…" -H "Authorization: Bearer $KEY"
# {"conversionId":"4f0c…","status":"ready","invoiceNumber":"2026-041",…}

curl -s -OJ "$API/conversions/4f0c…/xml" -H "Authorization: Bearer $KEY"
# saves 2026-041-peppol-bis-billing-3.0.xml
```

## Limits

- **4 MB per PDF.** The API runs behind a Lambda Function URL, which refuses requests over 6 MB, and a
  binary body is base64-encoded on the way in.
- Each conversion counts as one conversion of the organisation's allowance, exactly like an upload in
  the app. When the allowance is used up, the conversion fails with that message.
- There is no per-key rate limit yet; reading is paced by the import queue (two at a time).

## Trial

An organisation on a trial can send PDFs and follow their status, including `needs_review` and
`ready`, but `GET …/xml` answers `402 Payment Required` - the same rule as downloading in the app.
