# xChange: organisaties per land

**Datum:** 2026-10-18
**Status:** goedgekeurd ontwerp

## Context

xChange gaat organisaties in meerdere landen bedienen. Eerst Singapore, Verenigd
Koninkrijk, Nederland en België. De eigen organisatie van de beheerder zit in
Singapore en factureert onder meer naar het VK.

Dat is in stukken opgedeeld, elk met een eigen ontwerp, plan en deploy:

1. **Organisaties per land** — dit ontwerp
2. **Volledige officiële validatie** van elk gegenereerd bestand (aparte validatiedienst)
3. **Singapore: PINT-SG-regelset**
4. **Formaat per afnemer** (relatie krijgt een standaardformaat)
5. **VK en België op PEPPOL BIS 3.0** (identificatieschema's, landcontroles; een echte
   VK-regelset zodra de specificatie verschijnt)

Later, niet nu: zelf registreren met betaalde accounts (aanvraag + goedkeuring), Engelse
schermen.

## Doel van dit stuk

- Elke organisatie heeft een **land**.
- Er kunnen **meer organisaties** bestaan, aangemaakt door de **platformbeheerder**.
- De eerste gebruiker van een organisatie richt **zelf zijn account in** via een
  uitnodigingslink.
- Landen staan op **één plek** in de code, zodat een land toevoegen één regel is.

## Niet in dit stuk

- Organisatiebeheerders die collega's uitnodigen (hetzelfde uitnodigingsmechanisme
  maakt dat later klein).
- Standaardinstellingen per land (formaat, identificatieschema's, GST/btw): stukken 3–5.
- Een aanvraagformulier met goedkeuring, zelf registreren, betalen.
- E-mail versturen: xChange kan nog niet mailen; de beheerder stuurt de link zelf.

---

## Platformbeheerder

Een nieuwe rol **boven** de organisaties, naast de bestaande rollen binnen een organisatie
(Admin, User, ReadOnly).

- Vastgelegd als `isPlatformAdmin` op het gebruikersrecord.
- Bij een nieuwe installatie wordt het account uit de eerste setup platformbeheerder.
- In de bestaande installatie wordt het huidige account eenmalig platformbeheerder (zie
  "Bestaande installatie").

**De platformbeheerder beheert organisaties, maar leest hun facturen, relaties en
instellingen niet.** Al die gegevens blijven in de partitie van hun organisatie en worden
alleen gelezen met de organisatie uit de sessie (CLAUDE.md, harde regel 4). Een
platformbeheerder ziet in de gewone schermen alleen zijn eigen organisatie, net als nu.

## Landen

Eén lijst in Domain (`Countries`), met per land de ISO 3166-1-code en een naam:

| Code | Naam |
|---|---|
| SG | Singapore |
| GB | Verenigd Koninkrijk |
| NL | Nederland |
| BE | België |

Het VK heet in ISO 3166 `GB`; dat is ook wat PEPPOL verwacht. Later stukken voegen per land
de standaarden toe (formaat, schema's); dan groeit dit record, niet het aantal plekken.

## Organisatierecord

Tot nu toe bestaat een organisatie alleen als `organizationId` op gebruikers en facturen;
de naam uit de setup werd niet bewaard. Nu wel:

```
xchange-identity   pk = PLATFORM   sk = ORGANIZATION#<organizationId>
name, countryCode, createdAt, createdByUserId
```

Alle organisaties staan onder één partitiesleutel, zodat de lijst een **Query** is, geen
Scan. Dit is platformgegeven: het bevat geen facturen of relaties.

- Alleen de platformbeheerder wijzigt naam en land. Het land bepaalt het belastingregime
  en het exportformaat; een organisatie wijzigt het niet zelf.
- De organisatie-instellingen (bestaande pagina) tonen naam en land, alleen-lezen.

## Uitnodigingen

1. De platformbeheerder maakt een organisatie aan: naam, land, naam en e-mail van de eerste
   gebruiker.
2. xChange maakt een **uitnodiging** en toont eenmalig de link
   `https://greenitsolutions.net/xchange/invite/?code=<code>`.
3. De beheerder stuurt de link zelf.
4. De gebruiker opent de link, ziet organisatie en e-mailadres, kiest een wachtwoord
   (minimaal 12 tekens, zoals bij de setup) en kan zijn naam aanpassen. Daarna is hij
   **Admin** van die organisatie en meteen ingelogd.

Regels:

- De code is 32 willekeurige bytes (CSPRNG), hex. **Opgeslagen wordt alleen de SHA-256
  ervan**; een gelekte database bevat geen bruikbare links.
- **7 dagen geldig, één keer bruikbaar.** Het gebruik verwijdert de uitnodiging met een
  voorwaardelijke delete, zodat twee gelijktijdige pogingen niet allebei slagen.
- **Opnieuw uitgeven** maakt een nieuwe code en maakt de oude ongeldig.
- Bestaat er al een account met dat e-mailadres, dan mislukt het accepteren met een
  duidelijke melding; er wordt niets overschreven.
- Een onbekende, verlopen of gebruikte code geeft dezelfde melding: "Deze uitnodiging is
  ongeldig of verlopen." Zo verraadt de API niet welke codes ooit bestonden.

```
xchange-identity   pk = INVITATION#<sha256>   sk = INVITATION
organizationId, email, name, createdAt, expiresAt (TTL)
```

Het organisatierecord bewaart de hash van de openstaande uitnodiging, zodat opnieuw uitgeven
de oude kan verwijderen en de lijst kan tonen dat er nog een uitnodiging openstaat.

## Endpoints

```
GET  /api/v1/platform/organizations                          platformbeheerder
     [{ organizationId, name, countryCode, createdAt, userCount, invitationPending }]
POST /api/v1/platform/organizations                          platformbeheerder
     { name, countryCode, firstUser: { name, email } }  ->  { organization, invitationCode }
PUT  /api/v1/platform/organizations/{id}                     platformbeheerder
     { name, countryCode }
POST /api/v1/platform/organizations/{id}/invitation          platformbeheerder
     { name, email }  ->  { invitationCode }

GET  /api/v1/invitations/{code}            openbaar  ->  { organizationName, countryCode, email, name }
POST /api/v1/invitations/{code}/accept     openbaar  { name, password }  ->  sessie (zoals login)

GET  /api/v1/countries                     ingelogd  ->  [{ code, name }]
```

- Platform-endpoints weigeren iedereen zonder `isPlatformAdmin` met 403, ook een Admin van
  een organisatie.
- De openbare uitnodigings-endpoints vallen onder dezelfde pogingenbegrenzing per IP als
  inloggen.
- `GET /auth/me` en de login-respons krijgen `isPlatformAdmin`.
- Organisatie-instellingen (`GET /organization/settings`) krijgen `organizationName` en
  `countryCode`.

## Schermen

- **Beheer → Platform**, alleen zichtbaar voor de platformbeheerder: lijst van organisaties
  (naam, land, aangemaakt, gebruikers, "uitnodiging open"), een formulier om er een aan te
  maken, en per organisatie "Wijzigen" en "Nieuwe uitnodigingslink". Na aanmaken of opnieuw
  uitgeven verschijnt de link één keer, met een kopieerknop en de mededeling dat hij niet
  opnieuw getoond kan worden.
- **/invite/**, openbaar zoals de loginpagina: organisatie en e-mail, naam, wachtwoord,
  "Account aanmaken". Een ongeldige code toont de melding en een link naar inloggen.
- **Setup** (eerste installatie) vraagt ook het land.
- **Instellingen** tonen organisatienaam en land.

## Bestaande installatie

Eenmalig, met toestemming, via de AWS CLI tegen productie:

1. Het huidige account krijgt `isPlatformAdmin = true` (beide gebruikersitems).
2. Het organisatierecord voor de huidige organisatie wordt aangemaakt, land **SG**.

Dit staat in het plan als losse stap, niet als stille migratie in de code.

## Infrastructuur

De Lambda-rol krijgt `dynamodb:Query` op de identiteitstabel (organisaties opsommen,
gebruikers per organisatie tellen). Geen nieuwe tabellen.

## Testen

- Een platformbeheerder kan een organisatie aanmaken; een Admin van een organisatie krijgt
  403 op elk platform-endpoint; anoniem 401.
- Een platformbeheerder ziet via de gewone endpoints alleen de facturen van zijn eigen
  organisatie.
- Uitnodiging: accepteren maakt een Admin in de juiste organisatie en een sessie; tweede
  keer accepteren faalt; verlopen faalt; na opnieuw uitgeven werkt de oude code niet meer;
  opgeslagen wordt alleen de hash; een bestaand e-mailadres wordt niet overschreven;
  wachtwoord korter dan 12 tekens geweigerd.
- Landen: alleen codes uit `Countries` worden geaccepteerd.
- Setup bij een lege installatie maakt een platformbeheerder en een organisatierecord.
