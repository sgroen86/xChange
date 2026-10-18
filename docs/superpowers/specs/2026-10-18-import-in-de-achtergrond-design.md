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

## Stap 2: e-mail

- Elke organisatie krijgt een moeilijk te raden adres `<code>@inbox.greenitsolutions.net`.
- SES ontvangt de mail in Frankfurt (`inbound-smtp.eu-central-1.amazonaws.com`) en legt
  hem in S3. De worker haalt de PDF-bijlagen eruit en zet ze als imports met afzender en
  onderwerp in dezelfde lijst. Een bijlage is niet aan de 4 MB van de API gebonden.
- De gebruiker zet één MX-record voor het subdomein bij zijn hostingpartij (Strato) en
  verifieert het domein voor SES.
  - Strato staat maar één CNAME toe, dus de drie DKIM-records uit de outputs van de
    service-stack (`InboxDkim1` tot en met `InboxDkim3`) worden niet gebruikt.
  - In plaats daarvan staat er één TXT-record: `_amazonses.inbox.greenitsolutions.net`.
    Het token kwam op 2026-10-18 met de hand uit `aws ses verify-domain-identity`; de
    CloudFormation-resource kent alleen DKIM.
  - Ontvangen heeft alleen die verificatie nodig. Wordt de identiteit ooit opnieuw
    aangemaakt, dan opnieuw `verify-domain-identity` draaien en het TXT-record bijwerken.
- **Hoe het gebouwd is.**
  - SES (regelset `xchange-inbox`, door de deploy-workflow geactiveerd) zet de mail in
    `xchange-mail-<account>/inbound/` (30 dagen) en roept `xchange-mail` aan. Die zit in
    hetzelfde pakket als de worker.
  - Mail met een virus- of spamoordeel FAIL wordt genegeerd.
  - Het adres is 12 tekens uit een alfabet zonder verwarrende tekens. Het staat in de
    identity-tabel in twee richtingen (`INBOX#<code>` en `ORG#<org>/INBOX`).
  - Alleen een beheerder maakt een (nieuw) adres; het oude werkt dan niet meer.
  - Per mail tellen hoogstens 20 PDF's van elk hoogstens 25 MB.

## Niet in dit stuk

- Een afzenderlijst per organisatie (eerst kijken hoeveel ongewenste mail er komt).
- Inkomende facturen van leveranciers: dit is nog steeds de eigen verkoopfactuur.
