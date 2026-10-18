# xChange: Singapore, PINT-SG-regelset

**Datum:** 2026-10-18
**Status:** goedgekeurd ontwerp
**Stuk 3 van 5** (zie `2026-10-18-organisaties-per-land-design.md`)

## Doel

Een tweede regelset naast PEPPOL BIS Billing 3.0: **PINT-SG Billing 1.4.1**, het formaat van
InvoiceNow en de GST InvoiceNow Requirement van IRAS. Organisaties in Singapore krijgen het
als standaardformaat.

| | |
|---|---|
| Regelset-id | `pint-sg-billing-1` |
| CustomizationID | `urn:peppol:pint:billing-1@sg-1` (factuur en creditnota) |
| ProfileID | `urn:peppol:bis:billing` |
| Release | 1.4.1 (sinds mei 2026 op docs.peppol.eu; zelf nog "Draft" genoemd in de release notes; maakt de IRAS-verplichte velden fatal) |
| Officiële regels | `phive-rules-peppol-pint` 4.6.3: `pint-sg/1.4.1` gedeelde PINT-regels + Singapore-regels, in het validatiemanifest |

PINT-SG is geen BIS 3.0 en omgekeerd; een bestand is voor één van de twee geldig.

## Regelsets verschillen nu ook in belasting en syntaxdetails

Tot nu toe ging alles uit van de Europese btw. Dit stuk maakt dat een eigenschap van de
regelset:

- **Belastingcategorieën per regelset.** BIS 3.0: S, Z, E, AE, G, O. PINT-SG: SR, SRCA-S,
  SRCA-C, ZR, SRRC, SROVR-RS, SROVR-LVG, SRLVG, ES33, ESN33, DS, OS, NG, NA. Het
  reviewscherm biedt de categorieën van het gekozen formaat aan.
- **Belastingnaam**: "btw" of "GST", ook in de labels.
- **UBL-dialect** van de regelset: `TaxScheme/ID` (`VAT` of `GST`), welke categorie geen
  percentage krijgt (O bij BIS, NG bij PINT-SG), en of UUID en belastingvaluta geschreven
  worden.
- **Regels opgesplitst**: gemeenschappelijke structuurregels (verplichte velden, regels,
  kortingen, decimalen, betaling) gedeeld; de Europese btw-categorieregels alleen bij BIS 3.0;
  de GST-regels alleen bij PINT-SG.

## Wat PINT-SG extra vraagt

- **UUID** (BT-SG-003): afgeleid van de factuur-id van xChange, zodat hij gelijk blijft bij
  opnieuw exporteren of boeken.
- **GST-nummer** van de verkoper (BT-31), **UEN** als registratienummer (BT-30), straat en
  postcode van verkoper en afnemer, bij elke "geregistreerde" categorie (SR, SRCA-S, SRCA-C,
  ZR, SRRC, SROVR-RS, SROVR-LVG, SRLVG, NA).
- **Elektronisch adres** schema **0195** met waarde `SGUEN` + UEN.
- **Creditnota**: een opmerking met de reden.
- **Vervaldatum** wanneer er iets te betalen is.
- **Factuur niet in SGD**: belastingvaluta SGD, de GST in SGD (tweede TaxTotal) en de totalen
  exclusief en inclusief GST in SGD (twee `AdditionalDocumentReference`-regels). De
  **wisselkoers vult de gebruiker in** (IRAS-goedgekeurd; er is geen officiële bron om hem op
  te halen). Bedragen in SGD: exclusief en GST elk afgerond op centen, inclusief is hun som.
- **NG** (niet GST-geregistreerd): alleen NG op de factuur, geen GST-nummers, geen percentage.
- **Betaalwijzen** GIRO (Z01) en PayNow (Z02) erbij.

## Standaardformaat per land

`Country` krijgt een standaardregelset: SG → PINT-SG; GB, NL, BE → PEPPOL BIS 3.0. De
volgorde wordt: keuze bij de download → standaard van de organisatie → standaard van het land
→ systeemstandaard. De instellingen-API geeft de uiteindelijke standaard terug.

## Niet in dit stuk

- Het formaat per afnemer (VK-klanten van een Singaporese organisatie): stuk 4.
- De kopie naar IRAS: hoort bij verzenden.
- Uitlezen van GST-categorieën door Claude: het extractieschema kent alleen de Europese
  categorieën; de gebruiker kiest de GST-categorie op het reviewscherm.

## Testen

- Referentiebestanden PINT-SG (SGD-factuur, USD-factuur met koers, creditnota) door de
  officiële PINT-SG-regels in CI.
- Per runtime-GST-regel een test dat overtreden de export blokkeert; BIS 3.0-tests blijven
  ongewijzigd groen.
- Rekenen in SGD met dezelfde voorbeelden in C# en TypeScript.
- Volgorde van de standaardregelset, inclusief het land.
