# xChange: opslaan en PEPPOL-export

**Datum:** 2026-09-08
**Status:** ontwerp, ter beoordeling

## Context

xChange leest nu een PDF-factuur uit met Claude, controleert de rekenkundige
samenhang en toont het resultaat ter beoordeling. Daarna houdt het op: er wordt
niets bewaard, en het enige exportformaat is een eigen canonieke XML.

Dit ontwerp voegt drie dingen toe, in deze volgorde:

1. onjuiste teksten in de UI weghalen
2. een factuur kunnen **boeken**, zodat hij bewaard blijft
3. een **PEPPOL BIS Billing 3.0**-bestand kunnen downloaden

xChange staat op zichzelf. Het deelt een domein met de Bookkeeping-app en heeft
daar de vormgeving van geleend, maar geen code, geen database en geen gebruikers.
Dat blijft zo.

## Niet in scope

Bewust weggelaten, zodat het later een eigen beslissing blijft:

- **Verzenden via een PEPPOL access point**, en SMP/SML-lookup. Dit ontwerp
  levert een bestand, geen verzending.
- **De volledige EN 16931 Schematron-regelset** (~200 regels). Wij controleren
  de verplichte velden en de structuur. Een bestand dat xChange goedkeurt kan
  dus alsnog struikelen over een officiële validator. Wil je die zekerheid, dan
  is dat een aparte stap.
- **CII** en andere syntaxen. Het ontwerp maakt ze mogelijk; het bouwt ze niet.
- **Doorboeken naar een boekhoudpakket.**

---

## Deel 1 — Onjuiste teksten weghalen

Op het uploadscherm staat een blok "Wat er nog niet gebeurt" met de tekst dat er
geen OCR, taalmodel of AWS-dienst wordt aangeroepen, en de ondertitel zegt dat de
verwerking gesimuleerd is. Beide zijn sinds de echte extractie onwaar.

Het blok verdwijnt. De ondertitel wordt feitelijk: dat de PDF door een taalmodel
gelezen wordt en dat de gegevens ter controle worden voorgelegd.

Klein, maar het gaat vooruit aan de rest: een scherm dat over zichzelf liegt
maakt elke andere melding erop minder geloofwaardig.

---

## Deel 2 — Boeken en opslaan

### Opslag

| Wat | Waar | Waarom |
|---|---|---|
| PDF | S3, `xchange-documents-<account>` | Blobs horen niet in een database (CLAUDE.md) |
| Factuurgegevens | DynamoDB, `xchange-invoices` | Free tier, past bij Lambda |

De database bewaart de S3-sleutel, nooit de PDF zelf.

### Datamodel

Elke record draagt `OrganizationId`, en **elke query filtert erop**. Dit is
CLAUDE.md regel 4: een gemiste filter is geen bug maar een datalek tussen
klanten. De partitiesleutel dwingt dat af:

```
pk = ORG#<organizationId>
sk = INVOICE#<invoiceId>
```

Een factuur van een andere organisatie is daarmee niet alleen ongeautoriseerd,
maar onbereikbaar: hij ligt in een andere partitie.

Velden: `invoiceId`, `organizationId`, `status`, `canonicalInvoice` (JSON),
`documentKey` (S3), `sourceFileName`, `sourceByteSize`, `createdAt`,
`createdByUserId`, `bookedAt`, `bookedByUserId`, `providerModel`.

### Status

`concept` → `geboekt`. Meer statussen komen er pas als er een reden voor is.

Boeken is niet onomkeerbaar in deze fase: een geboekte factuur mag opnieuw
bewerkt en opnieuw geboekt worden. Vergrendelen na boeken is een aparte keuze
die met een echt boekhoudproces meekomt, niet ervoor.

### Endpoints

```
POST   /api/v1/invoices              boek een factuur (canoniek model + PDF)
GET    /api/v1/invoices              lijst van deze organisatie
GET    /api/v1/invoices/{id}         één factuur
GET    /api/v1/invoices/{id}/document   de originele PDF
```

Alle vier achter een login, `readonly`-accounts mogen alleen lezen.

### UI

- Reviewscherm krijgt een knop **Boeken** naast Opslaan.
- Nieuw menu-item **Facturen** met een lijst: nummer, leverancier, datum, bedrag,
  status. Klikken opent het bestaande reviewscherm.
- De PDF komt nu van de server in plaats van uit IndexedDB, dus hij overleeft
  ook een andere browser. De IndexedDB-opslag blijft als snelle weergave vóór
  het boeken.

---

## Deel 3 — PEPPOL-export

### Formaten als adapters

```csharp
public interface IInvoiceOutputFormat
{
    string Id { get; }            // "peppol-bis-3", "xchange-canonical"
    string DisplayName { get; }
    string FileExtension { get; }
    string MediaType { get; }
    string Render(ValidatedCanonicalInvoice invoice);
}
```

Een registry lost het formaat op via `Id`. Twee implementaties nu; een derde
toevoegen is één klasse plus één registratie.

Beide adapters staan in Domain: ze zijn puur en deterministisch, zonder netwerk,
en volledig testbaar (CLAUDE.md, harde regel 2).

### `ValidatedCanonicalInvoice` wordt afgedwongen

CLAUDE.md zegt dat output-adapters alleen een gevalideerde factuur accepteren.
Dat is tot nu toe een belofte op papier geweest. Nu wordt het typewerk:

```csharp
public sealed record ValidatedCanonicalInvoice
{
    private ValidatedCanonicalInvoice(CanonicalInvoiceDraft draft) { ... }

    public static ValidatedCanonicalInvoice? TryCreate(
        CanonicalInvoiceDraft draft,
        out IReadOnlyList<MissingRequirement> missing);
}
```

De constructor is privaat. Een onvolledige factuur kán een adapter dus niet
bereiken — dat is een compileerfout in plaats van een controle die iemand
vergeet.

### Wat er ontbreekt, en hoe je het aanvult

`PeppolCompletenessValidator` levert per ontbrekend verplicht veld het
BT-nummer, het veldpad en een Nederlandse omschrijving:

```csharp
public sealed record MissingRequirement(
    string BusinessTerm,   // "BT-49"
    string Path,           // "buyer.electronicAddress"
    string Label);         // "Elektronisch adres van de afnemer"
```

De verplichte velden die zelden op een PDF staan, en dus meestal aangevuld
moeten worden:

| BT | Veld |
|---|---|
| BT-34 / BT-49 | Elektronisch adres van verkoper en afnemer, met scheme-id |
| BT-40 / BT-55 | Landcode van beide adressen |
| BT-151 | Btw-categorie per factuurregel |
| BT-118 / BT-119 | Btw-categorie en -percentage per btw-specificatieregel |
| BT-153 | Naam van het artikel per regel |

Het reviewscherm toont een blok **"Aanvullen voor PEPPOL"** met precies deze
velden en niets meer. Is het compleet, dan gaat de PEPPOL-download open. De
canonieke XML blijft altijd beschikbaar, ook bij een onvolledige factuur.

### Openstaand punt: BT-153

BT-153 (artikelnaam per regel) is verplicht in PEPPOL, maar `itemName` is vandaag
uit het extractieschema gehaald om onder de grammatica-limiet van de API te
komen. Drie mogelijkheden:

1. **`description` gebruiken als artikelnaam.** Geen extra veld nodig; de
   omschrijving is op de meeste facturen ook feitelijk de artikelnaam.
2. **`itemName` terugzetten** en er iets anders voor inleveren, bijvoorbeeld
   `note` of `purchaseOrderReference`.
3. **Handmatig invullen** in het aanvulblok.

Voorstel: **optie 1**, met optie 3 als terugval wanneer de omschrijving leeg is.
Dat kost geen ruimte in het schema en klopt in de praktijk.

### PEPPOL-specifieke vaste waarden

`CustomizationID` en `ProfileID` zijn vaste tekenreeksen die het document als
PEPPOL BIS Billing 3.0 identificeren. Die horen in de adapter, niet in het
canonieke model: het canonieke model beschrijft de factuur, niet het formaat
waarin hij toevallig verstuurd wordt.

### UI

Op het reviewscherm vervangt een keuzemenu de huidige knop "XML downloaden":

- **PEPPOL BIS 3.0** — uitgeschakeld zolang er velden ontbreken, met de lijst erbij
- **Canonieke XML** — altijd beschikbaar

---

## Testen

Het uitgangspunt: alles wat deterministisch is, wordt getest zonder netwerk.

- **PEPPOL-adapter** — vaste factuur in, verwacht UBL uit. Verplichte elementen
  aanwezig, bedragen met twee decimalen, `currencyID` op elk bedrag, geen
  hardgecodeerde voorbeeldwaarden, speciale tekens ontsnapt.
- **Compleetheidsvalidator** — per verplicht business term één test die aantoont
  dat het weglaten ervan de export blokkeert.
- **`ValidatedCanonicalInvoice`** — een onvolledige factuur kan er niet doorheen.
- **Opslag** — een record uit organisatie A is onbereikbaar vanuit organisatie B.
  Dit is de test die er het meest toe doet.
- **Endpoints** — anoniem geweigerd, `readonly` mag niet boeken.

## Risico's

- **Een geldig ogend bestand kan alsnog geweigerd worden.** Wij dekken de
  verplichte velden, niet de volledige regelset. Dat staat hierboven bij niet in
  scope en hoort ook in de UI vermeld te worden.
- **De uploadlimiet blijft 4 MB** zolang de API op Lambda draait. Opslaan
  verandert daar niets aan.
- **Kosten.** S3 en DynamoDB blijven binnen de free tier bij dit volume, maar
  opslag groeit wel mee met het gebruik, anders dan bij de huidige situatie waar
  niets bewaard wordt.
