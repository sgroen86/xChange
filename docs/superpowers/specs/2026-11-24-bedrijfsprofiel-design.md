# xChange: bedrijfsprofiel

**Datum:** 2026-11-24
**Status:** ontwerp, akkoord van de eigenaar; nog niet gebouwd

## Doel

Een organisatie vult één keer haar eigen gegevens in: de gegevens die als verkoper op elke
factuur staan en die PEPPOL nodig heeft, zoals het PEPPOL-id. xChange zet die gegevens daarna op
elke ingelezen PDF. Het model hoeft de eigen gegevens dan niet meer foutloos te lezen. Het
profiel is ook de basis voor het aanmelden bij een access point
(`2026-10-18-verzenden-via-access-point-design.md`), dat precies deze gegevens vraagt.

Nu kent Instellingen alleen het PEPPOL-id en het schema (`OrganizationSettings.EndpointId` en
`EndpointScheme`), en `InvoiceEnrichment` vult alleen dat ene veld in.

## Besluiten

- **Het profiel hoort bij de organisatie, niet bij een gebruiker.** De organisatie is de verkoper.
  Iedere gebruiker van de organisatie ziet het profiel, alleen een beheerder wijzigt het.
- **Het profiel wint van de PDF** bij de vaste gegevens, met een melding (zie "Op ingelezen
  facturen").
- **Een apart profielrecord** in plaats van de instellingen uit te breiden.
- **Tabbladen per formaat.** Het profiel is nu voor PEPPOL. Later komen er formaten bij die
  hun eigen extra gegevens vragen, zoals XRechnung, en die krijgen dan een eigen tabblad. De
  gedeelde gegevens staan maar één keer in het profiel.

## Opbouw

E-factuurformaten (PEPPOL BIS 3.0, NLCIUS, XRechnung, PINT) zijn allemaal specialisaties van
EN 16931. De gegevens van de verkoper zijn daarom grotendeels gelijk, en elk formaat vraagt maar
een paar velden extra. Het profiel heeft twee lagen.

### Algemeen (alle formaten)

| Groep | Veld | BT | Opmerking |
|---|---|---|---|
| Bedrijf | Juridische naam | BT-27 | Begint als de naam waarmee de organisatie is aangemaakt (`Organization.Name`). Het profiel overschrijft hem zonder die naam te wijzigen. |
| | Handelsnaam | BT-28 | |
| | Btw-nummer | BT-31 | |
| | KvK-/KBO-nummer + schema | BT-30, BT-30-1 | `0106` KvK, `0208` KBO |
| Adres | Straat, extra adresregel, postcode, plaats | BT-35, 36, 38, 37 | |
| | Land | BT-40 | Niet te wijzigen: komt van `Organization.CountryCode`. |
| Contact | Naam, e-mail, telefoon | BT-41, 43, 42 | |
| Betaling | Standaard betaalwijze | BT-81 | UNTDID 4461, bijvoorbeeld `30` of `58` |
| | IBAN, BIC, rekeninghouder | BT-84, 86, 85 | |

### PEPPOL (tabblad)

| Veld | BT | Opmerking |
|---|---|---|
| PEPPOL-id + schema | BT-34, BT-34-1 | Voorkeur: `0106` + KvK (NL), `0208` + KBO (BE) |

Dit zijn de verkopersvelden die de regels in de code eisen: `PeppolRules` (BT-34),
`EuVatRules` (BT-31), en `NlciusRules` voor BR-NL-1 (KvK), BR-NL-3 (adres) en BR-NL-11
(betaalwijze).

## Opslag

- Een nieuw record in `xchange-invoices`: `pk = ORG#<organizationId>`, `sk = PROFILE` (harde
  regel 4: de organisatie is de partitiesleutel).
- Het record heeft een blok `General` en per netwerk of formaat een eigen blok, nu alleen `Peppol`.
  Een nieuw formaat is een nieuw blok, geen nieuwe tabel.
- `OrganizationSettings` houdt alleen `DefaultRulesetId`. `EndpointId` en `EndpointScheme`
  verhuizen naar `Peppol` in het profiel.
- **Overgang zonder migratiescript.** Zolang een profiel geen PEPPOL-blok heeft, leest xChange
  het PEPPOL-id nog uit de instellingen. Bij de eerste keer opslaan van het profiel gaat het id naar
  het profiel en wordt het uit de instellingen gewist.

## Op ingelezen facturen

`InvoiceEnrichment` vult de verkoper uit het profiel. Dit draait na het lezen door het model, en
na het toepassen van geleerde correcties. Het geldt voor een losse upload, een batch en een PDF
per mail. Facturen die al bestaan, veranderen niet.

| Soort | Velden | Gedrag |
|---|---|---|
| **Identiteit**: het profiel wint | juridische naam, btw-nummer, KvK/KBO + schema, adres, land, PEPPOL-id + schema, IBAN, BIC, rekeninghouder | De waarde uit het profiel vervangt wat het model las. Stond er in de PDF iets anders, dan komt er een melding met ernst **Warning**: "uit het bedrijfsprofiel; de PDF toonde X". Was het veld leeg, dan een melding met ernst **Info**. |
| **Standaard**: alleen als leeg | handelsnaam, contactnaam, e-mail, telefoon, betaalwijze | Alleen ingevuld als het model niets vond, met een melding met ernst **Info**. |

- Waarden worden vergeleken na normalisatie: btw-nummer en KvK zoals in `RelationKeys`, en het
  IBAN zonder spaties en in hoofdletters. Een verschil in schrijfwijze geeft dus geen waarschuwing.
- Is een veld in het profiel leeg, dan blijft de waarde uit de PDF staan.
- De meldingen krijgen code `ENRICH-PROFILE` en het pad van het veld (bijvoorbeeld
  `seller.vatIdentifier`). Het beoordelingsscherm toont ze al. De teksten komen uit
  `EnrichmentText`, in Nederlands en Engels.
- Een beoordelaar kan per factuur nog alles aanpassen.
- Het leren van correcties blijft ongewijzigd. Omdat het profiel daarna draait, heeft het profiel
  het laatste woord over de eigen gegevens.
- Geen model in dit pad (harde regel 2): het is gewone code.

## Controle bij opslaan

Deterministisch, in Domain, waar mogelijk met wat er al is:

- het btw-nummer heeft de vorm van het land van de organisatie (`VatNumberShapes`);
- een KvK-nummer heeft 8 cijfers, een KBO-nummer 10;
- het schema van het PEPPOL-id staat in de bekende lijst (`PeppolIdentifiers`);
- een IBAN klopt volgens mod 97 (ISO 13616);
- een betaalwijze is een geldige UNCL 4461-code (`CodeLists`).

Alle velden zijn optioneel. Wie niets invult, houdt het gedrag van nu. Bij een fout wordt niets
opgeslagen en komt er een melding per veld, uit een berichtencatalogus in Nederlands en Engels.

## API

- `GET /organization/profile`: voor elke ingelogde gebruiker. Geeft het profiel, met de
  juridische naam en het PEPPOL-id ingevuld volgens de overgang hierboven.
- `PUT /organization/profile`: alleen `UserRoleName.Admin`. Geeft 400 met de meldingen per veld
  als de controle faalt.
- Geen organisatie-id in de route: die komt uit de sessie.
- `GET`/`PUT /organization/settings` verliezen `EndpointId` en `EndpointScheme`.
- Contract-DTO's in `InvoicePlatform.Contracts`, volgens het patroon van de bestaande
  instellingen.

## Scherm

- Een nieuwe pagina **Bedrijfsprofiel** op `/profile/`, in het menu naast Instellingen.
- Tabbladen in de stijl `.tabs` > `.tab` (zie `docs/bookkeeping-ui-style.md`): **Algemeen** en
  **PEPPOL**.
- **Algemeen** heeft vier kaarten: Bedrijf, Adres, Contact en Betaling. Het land wordt getoond,
  maar kan niet worden gewijzigd.
- **PEPPOL** heeft het PEPPOL-id en het schema, met een korte uitleg welke keuze in Nederland en
  België gebruikelijk is.
- Een beheerder ziet invulvelden en een knop Opslaan. Andere gebruikers zien dezelfde gegevens
  zonder ze te kunnen wijzigen.
- Fouten van de server verschijnen bij het veld waar ze over gaan.
- Het PEPPOL-id verdwijnt van de pagina Instellingen.
- Alle teksten komen uit een nieuw woordenboek `apps/web/lib/i18n/profile.ts`, in Nederlands en
  Engels.
- Zonder API-adres valt de pagina terug op mockgegevens, zoals de rest van de app.

## Tests

- **Domain:** de controles bij opslaan, per regel een goed en een fout voorbeeld: btw-vorm voor
  NL en BE, KvK, KBO, IBAN, PEPPOL-schema en betaalwijze.
- **Application:**
  - het profiel overschrijft een identiteitsveld en geeft een waarschuwing;
  - het vult een leeg identiteitsveld met een info-melding;
  - een standaardveld wordt alleen ingevuld als het leeg is;
  - een verschil in schrijfwijze geeft geen waarschuwing;
  - een leeg profiel laat de waarde uit de PDF staan;
  - een PEPPOL-id in de instellingen wordt gebruikt zolang het profiel er geen heeft, en
    verhuist bij opslaan;
  - **tenant:** het profiel van organisatie A komt nooit op een factuur van organisatie B.
- **Integration:**
  - `GET` geeft 200 voor een gewone gebruiker;
  - `PUT` geeft 403 voor een gewone gebruiker, 200 voor een beheerder en 401 zonder sessie;
  - een ongeldig btw-nummer geeft 400 met een melding.
- **Web:** `npm test` en `npm run build` slagen; ontbrekende vertalingen geven een fout bij het
  bouwen.

## Niet in dit stuk

- Een tabblad voor XRechnung of een ander formaat. De opbouw maakt het later mogelijk.
- Het aanmelden van de organisatie bij een access point.
- Een persoonlijke profielpagina (naam, e-mail, wachtwoord).
- Het PEPPOL-id van de organisatie zelf opzoeken in het PEPPOL-netwerk.
- Een melding in de app die vraagt het profiel in te vullen.
