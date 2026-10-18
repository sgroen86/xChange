# xChange: van app naar een site die geld verdient (plan)

**Datum:** 2026-10-18
**Status:** plan. Doel van de gebruiker: "eerst een werkende site die geld verdient, daarna
uitbreiden met access points enzovoort".

## Besluiten tot nu toe

- **Markt:** België en Nederland; Singapore alleen als demo.
- **Adressen:**
  - www.xchangeinvoice.com: de website, verborgen tot de lancering;
  - app.xchangeinvoice.com: de app;
  - inbox.xchangeinvoice.com: de e-mailimport.
- **Hosting:** AWS, met S3 en CloudFront in de eigen account en regio. Strato houdt de
  domeinregistratie en de eigen e-mail.
- **Website:** Nederlands, Engels en Frans.
- **Rekenen:** beide, dus abonnementen met een proefperiode én betalen per conversie.
- **Betalen:** Stripe (voorstel). Werkt in NL en BE (iDEAL, Bancontact, SEPA, kaarten) en
  ook met de Singaporese vennootschap. Mollie vereist een EU-, VK- of Zwitserse
  vennootschap.
- **Verkopende vennootschap:** nog niet besloten. Daarom is het een instelling, en de
  juridische teksten krijgen plaatshouders.

## Fasen

### A. Het nieuwe domein (eerst, alles wat volgt bouwt erop)

- **DNS van xchangeinvoice.com naar AWS Route 53** (ca. €0,50 per maand).
  - Dat is nodig, niet optioneel: CloudFront en het e-mailadres van de inbox vragen elk
    meerdere verificatie-records, en Strato staat maar één CNAME toe.
  - De gebruiker zet bij Strato de nameservers om; de registratie blijft bij Strato.
  - Bestaande of gewenste e-mail op het domein (bijv. info@xchangeinvoice.com bij Strato)
    blijft werken met MX-records in Route 53.
- **De app op app.xchangeinvoice.com** via S3 en CloudFront, met een certificaat van ACM
  (in us-east-1, zoals CloudFront eist).
  - De deploy gaat via CI zoals nu. De opzet van branch `prepare/xchangeinvoice-domain`
    voor API, CORS en inbox wordt hergebruikt.
  - greenitsolutions.net/xchange gaat daarna met een vaste doorverwijzing naar het nieuwe
    adres.
- **De inbox op inbox.xchangeinvoice.com**, geverifieerd met DKIM, nu automatisch in
  Route 53. Bestaande adressen houden hun code.

### B. De website (verborgen tot de lancering)

- **Opzet:** een eigen statische site (`apps/site`, Next.js-export) op S3/CloudFront, in
  NL, EN en FR.
- **Pagina's:**
  - home en product;
  - hoe het werkt;
  - prijzen;
  - resources: o.a. de Belgische verplichting sinds 2026, PEPPOL BIS 3.0, NLCIUS en het
    PEPPOL-adres van uw klant;
  - FAQ en contact;
  - inloggen en aanmelden;
  - privacybeleid, voorwaarden, en een vertrouwenspagina met de verwerkersovereenkomst
    en subverwerkers (AWS, Anthropic, Stripe).
- **Verborgen:**
  - een wachtwoord via een CloudFront-functie;
  - `noindex, nofollow` als meta en header;
  - `robots.txt` die alles weigert, geen sitemap;
  - geen links ernaartoe.

### C. Zelf aanmelden met een proefperiode

- **Aanmelden op de site:** e-mail, naam, bedrijfsnaam en land (NL/BE/VK).
  - Na bevestiging per e-mail ontstaat een eigen organisatie, met de aanmelder als
    beheerder. Dat is hetzelfde mechanisme als de uitnodigingen nu.
- **E-mail versturen** (bevestiging, wachtwoord vergeten): via SES vanaf
  `noreply@xchangeinvoice.com`. Daarvoor moet het SES-account uit de sandbox; dat is een
  aanvraag bij AWS.
- **Proefperiode:** een aantal gratis conversies (voorstel: 10) zonder betaalgegevens.
- **Beschermen:**
  - een limiet op aanmeldingen per IP;
  - geen wegwerp-e-mailadressen;
  - de bestaande goedkeuring van gemailde PDF's.

### D. Betalen en gebruik

- **Abonnementen** via Stripe Checkout, en per conversie via tegoed (credits). De klant
  beheert zelf zijn abonnement en facturen in het Stripe-klantportaal.
- **Gebruik tellen per organisatie per maand:**
  - elke gelezen PDF telt één (het moment dat de tokens worden verbruikt);
  - downloaden en exporteren tellen niet.
- **Bij de limiet:** uploaden stopt met een duidelijke melding en een knop om op te
  waarderen of tegoed te kopen.
- **Stripe-webhooks** houden het abonnement van de organisatie bij. De sleutel staat in
  Secrets Manager.
- **Btw:** Stripe Tax, afhankelijk van de verkopende vennootschap.
- **Prijzen:** de gebruiker bepaalt de bedragen. De kosten zijn ca. €0,09 per PDF (Claude),
  plus later €0,07–0,30 per verzending via een access point.

### E. Lancering

- De juridische teksten zijn definitief, met de gekozen vennootschap.
- Het wachtwoord gaat eraf; `noindex` gaat weg op de website (niet op de app); een
  sitemap en Search Console komen erbij.
- Eventueel privacyvriendelijke statistieken (zonder cookiebanner).

### Daarna

Versturen via een access point (België en Nederland eerst, zie
`2026-10-18-verzenden-via-access-point-design.md`), ontvangen, Engelse en Franse
schermen in de app.

## Wat de gebruiker moet doen of beslissen

1. **Stripe** bevestigen, en later een Stripe-account openen op naam van de verkopende
   vennootschap.
2. **Nameservers van xchangeinvoice.com** bij Strato omzetten naar Route 53 (fase A; de
   juiste waarden volgen bij de uitvoering).
3. **Prijzen:** bedragen voor de abonnementen en per conversie, plus de omvang van de
   proefperiode.
4. **Verkopende vennootschap**, vóór de lancering.
5. **SES uit de sandbox**: een aanvraag bij AWS; tekst volgt bij de uitvoering.
