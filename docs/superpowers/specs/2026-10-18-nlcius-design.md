# xChange: NLCIUS (SI-UBL 2.0) en landen zichtbaar in het formaatmenu

**Datum:** 2026-10-18
**Status:** gevraagd ("both 1 and 2"); details hieronder zonder verder overleg gekozen

## Context

Het VK, België en Nederland gebruiken alle drie PEPPOL BIS Billing 3.0. In het menu stond
daarom alleen "PEPPOL BIS Billing 3.0", en de gebruiker zag niet dat die landen ondersteund
worden. Nederland heeft daarnaast een eigen formaat: **NLCIUS, uitgewerkt als SI-UBL 2.0**,
dat onder meer Nederlandse overheden en Nederlandse boekhoudpakketten gebruiken.

## 1. Landen in de naam

- BIS 3.0 heet "PEPPOL BIS Billing 3.0 (VK, België, Nederland)".
- PINT-SG heet "PINT-SG (Singapore, InvoiceNow)".
- NLCIUS heet "NLCIUS / SI-UBL 2.0 (Nederland)".

De namen staan op één plek (`DisplayName` van de regelset), dus het formaatmenu,
de instellingen en de relatiepagina volgen vanzelf.

## 2. NLCIUS / SI-UBL 2.0

- **Versie:** de validatieset SI-UBL 2.0.3.13 van de Nederlandse Peppol Autoriteit
  (release `2026-05-21`, verplicht vanaf 17 augustus 2026; MIT-licentie). Eén XSLT met de
  EN 16931-kernregels (CEN 1.3.16) en de Nederlandse regels BR-NL-*.
- **Identificatie:** CustomizationID
  `urn:cen.eu:en16931:2017#compliant#urn:fdc:nen.nl:nlcius:v1.0`, ProfileID het PEPPOL-
  factuurproces `urn:fdc:peppol.eu:2017:poacc:billing:01:1.0`.
- **Belasting:** gewoon btw, dezelfde categorieën als BIS 3.0.
- **Runtime-regels:** de gedeelde kernregels, de EU-btw-regels, de vorm van btw-nummers
  en de Nederlandse BR-NL-regels, die gelden als de verkoper in Nederland zit:

  | Regel | Wat |
  |---|---|
  | BR-NL-1 | Registratienummer van de verkoper, KvK (0106) of OIN (0190) |
  | BR-NL-2 | Referentie afnemer of inkoopordernummer |
  | BR-NL-3 | Straat, plaats en postcode van de verkoper |
  | BR-NL-4 | Idem voor een Nederlandse afnemer |
  | BR-NL-7 | Factuursoort 380, 381, 384 of 389 |
  | BR-NL-9 | Een corrigerende factuur (384) verwijst naar de oorspronkelijke |
  | BR-NL-10 | Een Nederlandse afnemer heeft een KvK- of OIN-nummer |
  | BR-NL-11 | Betaalwijze als er iets te betalen is |
  | BR-NL-12 | Betaalwijze 30, 48, 49, 57, 58 of 59 |

  BR-NL-10 is strenger dan BIS 3.0 (NL-R-005 vraagt het schema alleen als er een nummer
  is). De PEPPOL-regels (PEPPOL-EN16931-*) gelden niet: NLCIUS is geen BIS 3.0.
- **Afgeraden velden:** NLCIUS raadt de naam van de rekeninghouder (BR-NL-30) en een tekst
  bij de betaalwijze (BR-NL-29) af. Die laat xChange in een NLCIUS-bestand weg. De
  geboekte factuur houdt ze.
- **Officiële validatie:** de XSLT wordt een vierde artefact in `validation/rulesets.json`.
  Omdat hij als los bestand wordt gepubliceerd (niet in een zip of jar), mag een artefact
  zonder `entry` het gedownloade bestand zelf zijn. CI valideert `nlcius-*.xml`.
- **Standaard:** Nederland blijft standaard op BIS 3.0, het gangbare PEPPOL-formaat. NLCIUS
  kies je per organisatie of per afnemer (stuk 4), of per download.

## Niet in dit stuk

- De G-rekening-extensie van SI-UBL 2.0 en NLCIUS in CII-syntaxis.
