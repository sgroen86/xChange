# xChange: regelsets, PEPPOL-export en relaties

**Datum:** 2026-10-18
**Status:** goedgekeurd ontwerp
**Vervangt:** deel 3 van `2026-09-08-peppol-export-and-storage-design.md`

## Context

xChange leest een PDF-factuur uit met Claude, laat hem controleren en boekt hem.
Dit ontwerp voegt toe dat een geboekte factuur als **PEPPOL BIS Billing 3.0**-bestand
gedownload kan worden, gecontroleerd volgens **regelrelease 3.0.21** (verplicht sinds
17 augustus 2026), en dat xChange de gegevens van afnemers onthoudt.

De richting is **uitgaand**: de organisatie uploadt haar eigen verkoopfacturen. De
organisatie is dus de **verkoper**, de wederpartij de **afnemer**.

Er zijn vier stukken, in deze volgorde:

1. **Regelsets en het PEPPOL-bestand** — dit ontwerp, eerste plan
2. **Relaties** — dit ontwerp, tweede plan
3. Verzenden via een access point — later, eigen ontwerp
4. Inkomende facturen — later, eigen ontwerp

Stuk 3 en 4 worden niet gebouwd, maar het ontwerp houdt er ruimte voor (zie
"Voorbereid op later").

## Niet in scope

- Verzenden via een PEPPOL access point, SMP/SML-lookup, inschrijving als deelnemer.
- De volledige officiële regelset tijdens runtime. Runtime controleert verplichte
  velden en de kernregels die wij zelf implementeren; CI controleert onze bestanden
  tegen de officiële regels (zie "Testen").
- CII, NLCIUS, XRechnung en andere regelsets. Het ontwerp maakt ze mogelijk.
- Het PDF in het UBL-bestand insluiten. Komt met verzenden.
- Automatisch opzoeken van PEPPOL-id's in de PEPPOL Directory.

---

## Deel 0 — Eén model van browser tot opslag

### Het probleem

Het reviewscherm werkt met een eigen, vereenvoudigd model (`apps/web/lib/canonical.ts`:
`number`, `supplier`, `customer`, `vatRate`). Boeken stuurt dat model ongewijzigd naar de
API, die een `CanonicalInvoiceDraft` verwacht (`invoiceNumber`, `seller`, `buyer`,
`vatPercentage`). Alleen velden met toevallig dezelfde naam komen aan. Gecontroleerd:
een geboekte factuur verliest factuurnummer, valuta, verkoper, afnemer en btw-percentage.
Een geboekte factuur heropenen in een nieuwe browser leest het servermodel alsof het het
browsermodel is, en breekt.

De export werkt vanaf de geboekte factuur. Zonder dit deel is er niets te exporteren.

### De oplossing

De browser werkt voortaan met **hetzelfde canonieke model als de server**
(`CanonicalInvoiceDraft`, camelCase). Het prototypemodel verdwijnt.

- Bedragen en aantallen gaan als **strings** over de lijn, in beide richtingen. De API
  krijgt een `decimal`-converter die strings schrijft en strings of getallen leest.
  Daarmee is het bekende gat in harde regel 3 gesloten.
- Het reviewscherm bewerkt dat model direct. Wat het tot nu toe niet toonde maar PEPPOL
  nodig heeft, krijgt een veld: elektronisch adres en schema, KvK-nummer en schema,
  landcode, btw-categorie per regel, IBAN, betaalwijze, betalingskenmerk,
  inkoopordernummer en referentie afnemer.
- Regel- en documentkortingen/-toeslagen worden meegenomen in de berekening maar niet
  bewerkbaar gemaakt; ze worden getoond als ze er zijn.
- Concepten in `sessionStorage` met de oude vorm worden genegeerd in plaats van geladen.
- De lokale terugval zonder API (mockdata) levert hetzelfde model.

---

## Deel 1 — Regelsets

### Bouwstenen

Alles in **Domain**: puur, deterministisch, zonder netwerk (harde regel 2).

```
CanonicalInvoiceDraft
        │
        ▼
ValidatedCanonicalInvoice.Validate(ruleset, draft)
   ├─ InvoiceTotalsCalculator      regeltotalen, btw-specificatie, documenttotalen
   ├─ EN 16931-kernregels          BR-*, gedeeld door elke regelset
   └─ eigen regels van de set      PEPPOL-*, NL-R-*
        │
        ├── overtredingen ─► reviewscherm: "Aanvullen voor PEPPOL BIS 3.0"
        ▼
ValidatedCanonicalInvoice           gebonden aan die regelset; private constructor
        ▼
UblInvoiceWriter                    Invoice of CreditNote ─► .xml
```

**`IInvoiceRuleset`** — één klasse per regelset:

| Eigenschap | PEPPOL BIS 3.0 |
|---|---|
| `Id` | `peppol-bis-billing-3.0` |
| `DisplayName` | `PEPPOL BIS Billing 3.0` |
| `RulesRelease` | `3.0.21` |
| `Syntax` | UBL 2.1 |
| `CustomizationId` | `urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0` |
| `ProfileId` | `urn:fdc:peppol.eu:2017:poacc:billing:01:1.0` |

Het puntrelease staat niet in het bestand: 3.0.21 weigert een `CustomizationID` met
versie-informatie.

**`RuleViolation`** — regel-id (`BR-07`, `PEPPOL-EN16931-R003`, `NL-R-003`), business
term (`BT-49`), veldpad (`buyer.electronicAddress`, `lines[0].vatCategoryCode`) en een
Nederlandse melding. Het veldpad is gelijk aan het `id` van het invoerveld op het
reviewscherm, zodat een melding naar haar veld kan springen.

**`ValidatedCanonicalInvoice`** — alleen te maken via `Validate`, en draagt de regelset
waarmee hij gecontroleerd is. Een voor PEPPOL gecontroleerde factuur kan daardoor niet
per ongeluk als iets anders geschreven worden.

**`InvoiceRulesetRegistry`** — vindt een regelset op id. PEPPOL BIS 3.0 is de
systeemstandaard.

### Totalen worden berekend, niet overgenomen

Bij het valideren rekent `InvoiceTotalsCalculator` de regelnetto's, de
btw-specificatie per (categorie, percentage) en de documenttotalen opnieuw uit uit de
regels, kortingen en toeslagen, volgens EN 16931: btw per categorie over de opgetelde
grondslag, afronden op centen, half van nul af. Vooruitbetaald bedrag en
afrondingsbedrag komen uit het concept. Een vrijstellingsreden per btw-categorie blijft
behouden.

De gevalideerde factuur bevat de berekende waarden; het bestand dus ook. Daardoor
kloppen de rekenregels van de officiële set (BR-CO-10 t/m 17) altijd. Wijken de
afgedrukte bedragen af van de berekende, dan meldt de bestaande extractiecontrole dat al
als waarschuwing (`CALC-*`). Het reviewscherm toont dezelfde berekende bedragen.

### Welke regels runtime gecontroleerd worden

- **Aanwezigheid** van alles wat EN 16931 en PEPPOL verplicht stellen en ons model kent:
  factuurnummer, datum, type, valuta, namen, adres en landcode van beide partijen,
  elektronisch adres met schema van beide partijen, referentie afnemer of
  inkoopordernummer, minimaal één regel, per regel aantal, eenheid, prijs, artikelnaam,
  btw-categorie.
- **Codelijsten**: factuurtype (PEPPOL-subset), valuta (ISO 4217-vorm), landcode
  (ISO 3166-1 alpha-2-vorm), btw-categorie (UNCL 5305-subset), elektronisch-adresschema
  (EAS), betaalwijze (UNCL 4461), eenheid (UN/ECE Rec 20-vorm).
- **Btw-categorieregels**: percentage hoort bij de categorie (0 bij Z, E, AE, K, G, O;
  meer dan 0 bij S), vrijstellingsreden bij E, AE, K, G en O, btw-nummers waar de
  categorie dat eist.
- **Nederlandse regels** (NL-R-*), wanneer de verkoper in Nederland zit.
- **Decimalen**: bedragen maximaal twee decimalen.

De precieze lijst staat in het plan, per regel met een test.

### BT-153, de artikelnaam

De omschrijving van de regel is de artikelnaam (`cac:Item/cbc:Name`). Is er geen
omschrijving, dan vraagt het aanvulblok erom. BT-154 (omschrijving) wordt niet apart
geschreven, zodat dezelfde tekst niet twee keer in het bestand staat.

### UBL-schrijver

Eén schrijver voor UBL 2.1. Type 381 wordt een `CreditNote`, al het andere een
`Invoice`. `CustomizationID` en `ProfileID` komen van de regelset. Elk bedrag krijgt
`currencyID` en precies twee decimalen. Lege velden worden weggelaten, nooit
aangevuld met een voorbeeldwaarde. Escaping gaat via `XmlWriter`.

---

## Deel 2 — Organisatie-instellingen en de keuze van de regelset

### Opslag

Eén record per organisatie in `xchange-identity`:

```
pk = ORG#<organizationId>   sk = SETTINGS
defaultRulesetId, endpointId, endpointScheme
```

Het ligt in dezelfde partitie als de gebruikers van die organisatie. De setupcontrole
kijkt alleen naar `USER#`-items en merkt dit record dus niet op.

- **Standaard-regelset.** Leeg betekent PEPPOL BIS 3.0. Een opgeslagen id die niet
  (meer) bestaat valt stil terug op PEPPOL BIS 3.0.
- **Eigen elektronisch adres** (BT-34) met schema. Wordt ingevuld in elke factuur
  waarvan het verkopersadres leeg is: bij extractie (zichtbaar op het reviewscherm) en
  bij controle en export (voor eerder geboekte facturen).
- Alleen een **Admin** wijzigt. Iedereen mag lezen.

### Keuze per download

Het formaatmenu op het reviewscherm opent op de standaard van de organisatie. Een
andere keuze geldt voor die ene download en wordt niet bewaard. Het aanvulblok
controleert tegen de regelset die in het menu staat.

---

## Deel 3 — Controleren en exporteren

### Endpoints

```
GET  /api/v1/organization/settings
     200 { defaultRulesetId, endpointId, endpointScheme, rulesets: [{ id, displayName, rulesRelease }] }
     alle rollen

PUT  /api/v1/organization/settings
     body { defaultRulesetId, endpointId, endpointScheme }
     200, of 400 bij een onbekende regelset; alleen Admin (anders 403)

POST /api/v1/rulesets/{rulesetId}/check
     body: het concept  ─►  200 { violations: [...] }
     niets opgeslagen; 404 bij een onbekende regelset; alle rollen

GET  /api/v1/invoices/{invoiceId}/export?ruleset=peppol-bis-billing-3.0
     zonder ruleset: de standaard van de organisatie
     200  het bestand, bijv. F-2026-041-peppol-bis-billing-3.0.xml
     422  { violations: [...] }    nog niet compleet
     404  geen factuur met dat id in deze organisatie
     400  onbekende regelset
     alle rollen; exporteren is lezen
```

De organisatie komt altijd uit de sessie, nooit uit de route of de body.

### Waarom export vanaf de geboekte factuur

Het bestand wordt gemaakt uit de factuur zoals hij is opgeslagen, zodat wat je
downloadt altijd overeenkomt met het record. Heeft het reviewscherm niet-geboekte
wijzigingen, of is de factuur nog nooit geboekt, dan toont het PEPPOL-menu-item
"Eerst boeken".

### Het aanvulblok

Het reviewscherm stuurt het concept zoals het nu bewerkt is naar het check-endpoint,
kort nadat er gestopt is met typen. De overtredingen verschijnen in een blok
"Aanvullen voor PEPPOL BIS 3.0". Een klik op een melding zet de cursor in het
bijbehorende veld. Onder het blok staat: "Gecontroleerd op verplichte velden en
kernregels (release 3.0.21)."

De canonieke XML blijft altijd downloadbaar, zonder regelset en zonder controle.

---

## Deel 4 — Relaties

Een relatie is een afnemer van de organisatie, met de gegevens die PEPPOL nodig heeft
en een PDF meestal niet bevat.

### Opslag

```
xchange-invoices   pk = ORG#<organizationId>   sk = RELATION#<relationId>
name, vatIdentifier, legalRegistrationId, legalRegistrationScheme,
electronicAddress, electronicAddressScheme, countryCode, updatedAt
```

Per organisatie. Een afnemer die bij twee klanten van xChange voorkomt, staat er twee
keer in: niets wordt tussen tenants gedeeld. Zoeken is een Query op de eigen partitie,
nooit een Scan.

### Herkennen

Op **btw-nummer** (BT-48), anders op **KvK-nummer** (BT-47). Beide genormaliseerd:
hoofdletters, zonder spaties en punten. **Nooit op naam**: een verkeerde gok zet
andermans PEPPOL-id in het bestand.

### Vullen

- **Bij boeken** worden de afnemersgegevens van de factuur opgeslagen als relatie, of
  werken ze de bestaande bij (alleen niet-lege waarden). Zonder btw- of KvK-nummer
  wordt niets opgeslagen: zo'n relatie is later niet terug te vinden.
- **Bij extractie** vult een bekende relatie alleen **lege** velden van de afnemer.
  Het reviewscherm meldt per ingevuld veld "Ingevuld uit relatiegegevens".
- **Bij een conflict** (de PDF zegt iets anders dan de relatie) blijft de waarde uit
  de PDF staan en verschijnt een waarschuwing.

### Scherm

Een pagina **Relaties** met een lijst en een formulier om een relatie te corrigeren.
Admin en User mogen corrigeren, ReadOnly alleen kijken.

```
GET /api/v1/relations                 alle rollen
PUT /api/v1/relations/{relationId}    Admin en User; 404 buiten de eigen organisatie
```

---

## Voorbereid op later

- **`direction`** op elke factuur (`Outgoing`; ontbreekt het in opslag, dan
  `Outgoing`). Inkomende facturen passen dan in dezelfde opslag en lijst.
- **Syntaxlaag** — de schrijver staat los van de regelset. Een lezer voor inkomende
  UBL komt ernaast; de regelsets controleren het canonieke model, ongeacht waar het
  vandaan komt.
- **Status** blijft een uitbreidbare enum; verzonden/afgeleverd/mislukt zijn later
  toe te voegen zonder migratie.
- **"Relaties"** is bewust neutraal: dezelfde lijst kan later ook leveranciers bevatten.
- Het PDF insluiten (`AdditionalDocumentReference`) komt met verzenden. Een download
  zou er nu de responslimiet van 6 MB mee raken.

## Testen

Zonder netwerk, in `dotnet test`:

- **UBL-schrijver** — vaste facturen in, vergeleken met referentiebestanden
  (`Invoice` en `CreditNote`); twee decimalen en `currencyID` op elk bedrag;
  speciale tekens ontsnapt; lege velden weggelaten.
- **Totalencalculator** — regelnetto, btw per categorie, kortingen en toeslagen,
  afronding.
- **Regels** — per geïmplementeerde regel een test dat overtreden de export blokkeert.
- **`ValidatedCanonicalInvoice`** — een onvolledige factuur komt er niet door.
- **Keuze van de regelset** — gebruikerskeuze boven organisatiestandaard boven
  systeemstandaard; een verdwenen id valt terug.
- **Relaties** — onbereikbaar vanuit een andere organisatie; herkennen op btw dan
  KvK; alleen lege velden; conflict geeft een waarschuwing.
- **Endpoints** — alleen Admin wijzigt instellingen; export van andermans factuur
  geeft 404; onvolledig geeft 422 met overtredingen; alles achter de login.
- **Bedragen als strings** — een bedrag overleeft de rondreis browser–API exact.

**Officiële regels in CI.** De deploy-workflow valideert de referentiebestanden met de
officiële XSLT-artefacten van PEPPOL BIS 3.0.21 (CEN- en PEPPOL-regels) via Saxon-HE.
Een `fatal` blokkeert de deploy, net als een rode `dotnet test`. Omdat de
schrijver-tests eisen dat de uitvoer gelijk is aan die bestanden, bewijst dit dat de
schrijver geldige bestanden maakt voor die facturen.

Wat dat niet dekt: een echte factuur kan in runtime nog struikelen over een officiële
regel die wij niet zelf hebben geïmplementeerd. Het scherm zegt dat ("gecontroleerd op
verplichte velden en kernregels"), en bij verzenden (stuk 3) valideert het access point
elk bestand volledig.

## Risico's

- **Een geldig ogend bestand kan alsnog geweigerd worden** — zie hierboven.
- **Afgedrukt ≠ berekend.** Rondt een eigen factuur btw per regel af in plaats van per
  categorie, dan wijkt het bestand een cent af van de PDF. De extractiewaarschuwing
  toont dat; de gebruiker beslist.
- **Twee keer rekenen.** Het reviewscherm (TypeScript) en de export (C#) rekenen elk.
  Dezelfde methode, dezelfde afronding; de testen van beide gebruiken dezelfde
  voorbeelden.
- **Regelreleases.** PEPPOL publiceert in mei en november. De CI-artefacten zijn
  vastgepind op 3.0.21 en worden bewust bijgewerkt.
