# xChange: volledige officiële validatie van elk bestand

**Datum:** 2026-10-18
**Status:** goedgekeurd ontwerp
**Stuk 2 van 5** (zie `2026-10-18-organisaties-per-land-design.md`)

## Context

Vandaag controleert xChange een factuur vóór het genereren met ongeveer 80 officiële regels
die in C# zijn nagebouwd. De volledige officiële regelset (Schematron, als XSLT 2.0) draait
alleen in CI, op drie referentiebestanden. Een echte factuur kan daardoor nog struikelen
over een officiële regel die niet is nagebouwd, over een code die er goed uitziet maar niet
bestaat ("XYZ"), of over iets in de XML zelf.

Dit stuk laat elk gegenereerd bestand door de volledige officiële regels gaan voordat het
gedownload kan worden, en maakt dat uitbreidbaar naar nieuwe regelsets (PINT-SG, later de
VK-specificatie) en nieuwe releases.

## Wat er verandert voor de gebruiker

- Elke PEPPOL-download wordt eerst tegen de volledige officiële regels gecontroleerd. Bij
  een `fatal` geen bestand; de fouten verschijnen in het blok "Aanvullen voor …" met het
  officiële regel-id, de officiële tekst en de plek in het document.
- Waarschuwingen blokkeren niet; ze verschijnen na de download in hetzelfde blok.
- De controle tijdens het typen blijft de snelle C#-controle. Die krijgt de volledige
  ISO-lijsten voor valuta en landen, zodat "XYZ" al tijdens het typen opvalt.
- De eerste download na een stille periode duurt een paar seconden langer.

## Bouwstenen

### Manifest: één plek per regelset

`validation/rulesets.json` beschrijft per regelset welke officiële artefacten erbij horen:

```json
{
  "peppol-bis-billing-3.0": {
    "release": "3.0.21",
    "artefacts": [
      { "name": "CEN EN 16931 UBL 1.3.16", "url": "…/en16931-ubl-1.3.16.zip", "sha256": "…",
        "entry": "xslt/EN16931-UBL-validation.xslt" },
      { "name": "PEPPOL BIS 3.0.21", "url": "…/phive-rules-peppol-4.6.3.jar", "sha256": "…",
        "entry": "external/schematron/openpeppol/2026.5/xslt/PEPPOL-EN16931-UBL.xslt" }
    ]
  }
}
```

- De CI-controle (`tools/peppol-validation/validate.sh`) en de validatiedienst lezen
  **hetzelfde manifest**. Ze kunnen niet uit elkaar lopen.
- Een regelset of release toevoegen = een manifest-entry (plus, voor een nieuwe regelset,
  de C#-regelsetklasse zoals nu). Dienst, API en schermen veranderen niet.
- `validate.sh` krijgt de regelset als argument: `validate.sh <ruleset-id> <bestand>…`.

### Validatiedienst

Een tweede Lambda, `xchange-validator`, in **Java 21** met **Saxon-HE 12**.

- Invoer: `{ rulesetId, xml }`. Uitvoer: `{ rulesetId, release, failures: [{ flag, ruleId,
  message, location }] }`.
- De artefacten zitten in het pakket, opgehaald en op SHA-256 gecontroleerd tijdens de build
  aan de hand van het manifest. Geen downloads tijdens het draaien.
- Alle stylesheets worden bij het opstarten één keer gecompileerd en daarna hergebruikt.
- **Lambda SnapStart** (gratis voor Java) maakt een momentopname na het compileren, zodat
  een koude start de compilatie overslaat. Daarvoor publiceert elke deploy een versie en
  wijst een alias `live` ernaar; de API roept de alias aan.
- Een onbekende regelset geeft een fout terug, geen lege lijst.

### API

- `IOfficialValidator` (Application) met een implementatie in Infrastructure die de Lambda
  aanroept (AWS SDK, synchroon). De functienaam komt uit configuratie.
- `ExportInvoiceService` roept hem aan **na** het schrijven en **vóór** het teruggeven:
  - `fatal` → geen bestand, 422 met de officiële meldingen;
  - alleen waarschuwingen → het bestand, met de waarschuwingen in de header
    `X-Validation-Warnings` (base64-JSON);
  - dienst onbereikbaar of fout → **geen bestand**, 503 met "De officiële controle is nu
    niet beschikbaar. Probeer het zo opnieuw." Nooit een ongecontroleerd bestand.
- Zonder geconfigureerde dienst (lokale ontwikkeling) weigert export met dezelfde 503. Tests
  gebruiken een vervanger.
- CORS stelt `Content-Disposition` en `X-Validation-Warnings` bloot aan de browser. (De
  bestandsnaam van de download viel cross-origin tot nu toe terug op `<id>-<regelset>.xml`,
  omdat die header niet zichtbaar was.)

### Infrastructuur en CI

- `infra/aws/service.yml`: de validatiefunctie (Java 21, 1024 MB, 60 s, SnapStart), een
  versie per deploy, alias `live`, en voor de API-rol `lambda:InvokeFunction` op die alias.
- `deploy-api.yml`: bouwt de validator met Maven, haalt de artefacten op via het manifest,
  uploadt het pakket naast de API en geeft de sleutel aan de stack.
- De bestaande CI-controle op de referentiebestanden blijft, nu via het manifest.

## C#-controle tijdens het typen

`CodeLists.IsCurrencyCode` en `IsCountryCode` controleren tegen de volledige lijsten uit de
officiële CEN-regels (BR-CL-04 en BR-CL-14), in plaats van alleen de vorm.

## Testen

- Validator (Java, JUnit): de drie referentiebestanden geven geen `fatal`; een bestand zonder
  verkopers-endpoint geeft `PEPPOL-EN16931-R020`; een fout totaal geeft `BR-CO-16`; een
  onbekende regelset geeft een fout.
- API: export met een vervanger die `fatal` meldt → 422 met die melding; alleen
  waarschuwingen → 200 met header; dienst onbereikbaar → 503 en geen bestand.
- C#: `XYZ` is geen valuta, `XX` geen land; `EUR`, `SGD`, `GB` wel.
- CI: `validate.sh` via het manifest op de referentiebestanden.

## Niet in dit stuk

- PINT-SG-artefacten (stuk 3 voegt ze toe aan het manifest).
- De officiële controle tijdens het typen (te traag en onnodig: de download beslist).
