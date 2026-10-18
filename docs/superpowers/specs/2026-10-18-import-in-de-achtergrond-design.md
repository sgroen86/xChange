# xChange: importeren in de achtergrond (batch, later e-mail)

**Datum:** 2026-10-18
**Status:** gevraagd ("3. this we can build as well"). Twee open beslissingen zijn
genomen door de gebruiker:
- **wachtrij:** AWS SQS;
- **e-mail:** een subdomein `inbox.greenitsolutions.net`, ontvangen door AWS SES.

## Doel

Meer facturen tegelijk aanbieden zonder op elke PDF te wachten. Het lezen gebeurt in de
achtergrond, en een lijst "Te beoordelen" laat zien waar elke factuur is. Hetzelfde
mechanisme ontvangt later de PDF's die per e-mail binnenkomen.

## Stap 1 (dit stuk): batch-upload

- **API.**
  - `POST /api/v1/imports` (multipart, dezelfde controles als één upload): de PDF gaat naar
    S3 onder het id dat de factuur bij het boeken krijgt, de import naar DynamoDB
    (`ORG#<org>` / `IMPORT#<id>`) en het id naar SQS. De API antwoordt meteen met 202.
  - Verder `GET /imports`, `GET /imports/{id}` (met concept en meldingen),
    `GET /imports/{id}/document`, `POST /imports/{id}/retry` en `DELETE /imports/{id}`.
  - Alles op de organisatie uit de sessie; de import van een ander bestaat niet.
- **Worker.** `InvoicePlatform.Worker` is nu een Lambda (`xchange-worker`) die de wachtrij
  leest:
  - één bericht per keer, hoogstens twee tegelijk (de grens van Anthropic is de
    bottleneck);
  - het lezen is dezelfde `ExtractInvoiceService` als bij één upload, inclusief leren
    van correcties;
  - kan het model de PDF niet lezen, dan staat dat op de import ("Mislukt", met reden)
    en kan de gebruiker "Opnieuw" kiezen;
  - een andere fout (opslag, netwerk) gaat terug naar SQS, en na drie pogingen naar
    `xchange-imports-failed`.
- **Scherm.**
  - Upload accepteert meerdere PDF's. Eén PDF werkt zoals altijd: meteen lezen en
    beoordelen.
  - Meerdere gaan naar "Te beoordelen". Die lijst ververst zichzelf zolang er iets
    gelezen wordt.
  - "Beoordelen" opent de import in het gewone beoordelingsscherm.
  - Boeken haalt de import van de lijst. De PDF blijft, want de geboekte factuur
    gebruikt hetzelfde document.

## Stap 2 (volgt): e-mail

- Elke organisatie krijgt een moeilijk te raden adres `<code>@inbox.greenitsolutions.net`.
- SES ontvangt de mail in Frankfurt (`inbound-smtp.eu-central-1.amazonaws.com`) en legt
  hem in S3. De worker haalt de PDF-bijlagen eruit en zet ze als imports met afzender en
  onderwerp in dezelfde lijst. Een bijlage is niet aan de 4 MB van de API gebonden.
- De gebruiker zet één MX-record en de DKIM-records voor het subdomein bij zijn
  hostingpartij.

## Niet in dit stuk

- Een afzenderlijst per organisatie (eerst kijken hoeveel ongewenste mail er komt).
- Inkomende facturen van leveranciers: dit is nog steeds de eigen verkoopfactuur.
