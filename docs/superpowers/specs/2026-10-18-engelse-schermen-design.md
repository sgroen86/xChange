# English app screens — design

Date: 2026-10-18. Status: accepted by the owner in conversation the same day.

## Goal

The app (`apps/web`) and the messages the API sends to it are available in English as well as
Dutch. French follows later and must only need new dictionary files, not new plumbing.

## Choosing the language

- A NL/EN switch in the top bar of the app and on the public pages (login, sign-up, invite).
- First visit: Dutch when the browser prefers Dutch (`navigator.languages` starts with `nl`), English
  otherwise. The choice is kept in `localStorage` (`xchange.lang`), wrapped in try/catch; without
  storage the browser preference applies on every visit.
- Not stored on the user account yet. That becomes useful once xChange sends notification email; the
  sign-up confirmation mail follows the language of the sign-up request (see "API messages").
- `<html lang>` follows the choice.

## App screens

- `apps/web/lib/i18n/`: one file per area (`common.ts`, `nav.ts`, `review.ts`, `upload.ts`, …), each
  exporting `{ nl, en }` where `en` is typed `typeof nl` — a missing or extra key is a compile error.
  Parametrised texts are functions: `saved: (n: number) => \`${n} opgeslagen\``.
- `lib/i18n/index.ts` assembles the dictionaries, exposes `useText()` for components (a
  `useSyncExternalStore` over the language store, server snapshot `nl` for the static export) and
  `text()` for non-React code (`lib/*.ts` error messages).
- Money and dates follow the language: `formatMoney` keeps its integer arithmetic and only switches
  separators (nl: `€ 1.234,50`; en: `€1,234.50`). No floating point is introduced.
- Demo/mock data in `lib/canonical.ts` stays Dutch: it is an invoice, not interface text.

## API messages

- The web app sends `Accept-Language: nl` or `en` on every request.
- The API runs ASP.NET Core request localisation with `nl` (default) and `en`, which sets
  `CultureInfo.CurrentUICulture` for the request. Worker and mail functions run in `nl`.
- Messages live in catalogues per area, one C# class per language implementing a shared interface or
  record, so a missing translation is a compile error. A small `Texts` accessor in Domain picks the
  catalogue from `CurrentUICulture`. Domain gains no framework reference (it only reads
  `CultureInfo`), so `DependencyRuleTests` stays green.
- Covered: `Results.Problem` titles/details in Api, error strings and the two sign-up emails in
  Application, rule messages in Domain (BR-*, XC-*, BR-NL-*, SG GST, PEPPOL in-house checks, learning
  notices), country names and tax-category labels.
- Not covered: messages from the official validators (CEN, PEPPOL, SI-UBL, PINT-SG XSLT), which are
  English already. Rule ids, outcomes, amounts and the XML do not depend on the language — only text.

## Delivery

- Part A: switch, i18n plumbing, all screens. Deploys on its own.
- Part B: `Accept-Language`, server catalogues. Deploys after A.

## Testing

- Web: a test that walks both dictionaries and fails on an empty string; type-checking guards key
  parity. Existing web tests keep passing; `formatMoney` gains tests for `en`.
- .NET: tests that the same rule produces the same `RuleId` and a different message under `en`;
  an integration test that `Accept-Language: en` turns "Niet ingelogd" into its English text.
