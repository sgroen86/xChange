# xChange: formaat per afnemer

**Datum:** 2026-10-18
**Status:** ontwerp, gemaakt zonder overleg ("werk aan 4 en 5 terwijl ik boodschappen doe");
de keuzes onder "Besluiten" staan open voor herziening
**Stuk 4 van 5** (zie `2026-10-18-organisaties-per-land-design.md`)

## Context

Een organisatie heeft een standaardformaat: haar eigen keuze, anders dat van haar land
(Singapore: PINT-SG; VK, NL, BE: BIS 3.0). Maar Green IT Solutions in Singapore factureert
ook aan klanten in het VK, en die ontvangen PEPPOL BIS 3.0, geen PINT-SG. Nu moet bij elke
download van zo'n factuur het formaat met de hand worden gewisseld.

## Doel

Een relatie (afnemer) kan een **eigen standaardformaat** hebben. Een factuur aan die
afnemer opent daarop.

## Besluiten

1. **Volgorde.** Keuze bij de download → formaat van de afnemer → formaat van de
   organisatie → formaat van het land → systeemstandaard. Een opgeslagen formaat dat niet
   meer bestaat, wordt stil overgeslagen (zoals nu bij de organisatie).
2. **Met de hand ingesteld**, op de relatiepagina. xChange leert het formaat niet uit
   downloads en leidt het niet af uit het land van de afnemer. Eén keer instellen per
   VK-klant is weinig werk, en een afgeleide keuze die net niet klopt is lastiger te
   doorzien dan een lege.
3. **De afnemer wordt herkend zoals nu**: op btw-nummer, anders op registratienummer,
   nooit op naam (`RelationKeys`).
4. **Het beoordelingsscherm** herkent de afnemer zelf, met dezelfde sleutels, uit de
   relatielijst die het al kan ophalen. Het formaatmenu opent op het formaat van de
   afnemer en zegt dat erbij ("standaard voor <naam>"). Een wijziging van het btw-nummer
   van de afnemer werkt dat direct bij. Geen nieuw endpoint.
5. **De server** past dezelfde volgorde toe bij een export zonder gekozen formaat. De
   relatie komt uit de verrijking, die haar al zoekt.

## Wijzigingen

- `Relation.DefaultRulesetId` (DynamoDB-attribuut `defaultRulesetId`), in
  `RelationResponse` en `UpdateRelationRequest`. Een onbekend formaat geeft 400.
- `RememberBuyerAsync` laat het formaat ongemoeid.
- `InvoiceRulesetRegistry.Resolve(requested, params defaults)`: de eerste default die
  bestaat, anders de systeemstandaard.
- `EnrichmentResult.Relation`: de herkende relatie.
- `ExportInvoiceService` leest de factuur vóór het kiezen van het formaat.
- Web: kolom en keuzelijst "Formaat" op de relatiepagina; `matchRelation` in
  `lib/relations.ts` (met tests); het formaatmenu toont de bron van zijn standaard.

## Niet in dit stuk

- Een formaat afleiden uit het land van de afnemer.
- Een formaat per afnemer voor inkomende facturen (er zijn nog geen inkomende facturen).
