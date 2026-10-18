# xChange: VK en België op PEPPOL BIS 3.0

**Datum:** 2026-10-18
**Status:** ontwerp, gemaakt zonder overleg; de keuzes onder "Besluiten" staan open voor
herziening
**Stuk 5 van 5** (zie `2026-10-18-organisaties-per-land-design.md`)

## Context

Het VK en België gebruiken PEPPOL BIS Billing 3.0. Release 3.0.21 heeft geen VK- of
Belgische landregels zoals de Nederlandse (NL-R-*). Wel controleert hij het Belgische
ondernemingsnummer (`PEPPOL-COMMON-R043`, 10 cijfers met mod-97), en die controle zit al
in onze runtime-regels. Een echte VK-regelset volgt pas als HMRC de specificatie
publiceert.

Wat wel ontbreekt, is wat een gebruiker in deze landen tegenkomt.

- **Schema's.** Het Britse btw-nummer als elektronisch adres (EAS `9932`) staat niet in de
  keuzelijsten.
- **Btw-nummers.** BIS controleert alleen dat een btw-nummer met een landcode begint
  (BR-CO-09). Een tikfout in een Belgisch nummer of een Brits nummer met te weinig cijfers
  gaat erdoor, en wordt pas door de ontvanger geweigerd.
- **Singapore naar het VK** (stuk 4). Een Singaporese verkoper heeft een GST-nummer, geen
  btw-nummer. In PINT-SG hoort het in de btw-identificatie (BT-31). In EN 16931 is het
  een *tax registration identifier* (BT-32, schema `TAX`). Zonder omzetting faalt een
  BIS-factuur van een Singaporese organisatie altijd op BR-CO-09.

## Besluiten

1. **GST-nummer naar BT-32.** `Country` krijgt `TaxNumberIsVatIdentifier`: waar voor GB,
   NL en BE, onwaar voor SG. BIS 3.0 zet in `Prepare` het btw-nummer van een verkoper
   uit zo'n land om naar `TaxRegistrationId`, als dat nog leeg is. De factuur zelf blijft
   ongewijzigd, zodat dezelfde factuur als PINT-SG het nummer op de oude plek houdt.
2. **De belastingcategorie kiest de gebruiker.** xChange zet SR/ZR niet zelf om naar een
   BIS-categorie. Voor diensten aan een Britse onderneming is dat meestal `AE`
   (verlegd: de afnemer draagt de Britse btw af). Dat vereist het btw-nummer van de
   afnemer. Het voorbeeld hieronder doet het zo, maar het blijft een fiscale keuze.
3. **Vorm van btw-nummers**, als runtime-regel onder BIS 3.0, voor verkoper en afnemer.
   De regel geldt alleen als het nummer met het voorvoegsel van dat land begint:

   | Regel | Vorm |
   |---|---|
   | `XC-VAT-NL` | `NL` + 9 cijfers + `B` + 2 cijfers |
   | `XC-VAT-BE` | `BE` + 10 cijfers, beginnend met 0 of 1, mod-97 als het ondernemingsnummer |
   | `XC-VAT-GB` | `GB` + 9 of 12 cijfers, of `GBGD` + 3 cijfers (overheid), of `GBHA` + 3 cijfers (zorg) |

   Het voorvoegsel `XC-` maakt duidelijk dat het een xChange-regel is, geen officiële.
   Ze blokkeren de export net als de andere runtime-regels.
4. **Schema's.** `9932 · Brits btw-nummer` in de keuzelijst voor het elektronisch adres.
   België had `0208` en `9925` al.
5. **Referentiebestanden**, door CI met de officiële BIS-regels gecontroleerd:
   - `invoice-gb-standard.xml`: Brits naar Brits, 20%, GBP;
   - `invoice-be-standard.xml`: Belgisch naar Belgisch, 21%, adressen op `0208`;
   - `invoice-sg-to-gb.xml`: Singapore naar het VK, verlegd (`AE`), GBP, GST-nummer als
     BT-32.

## Niet in dit stuk

- Een VK-regelset (UK CIUS), zodra die er is.
- Standaardschema's per land (bijvoorbeeld `0208` voorinvullen voor een Belgische
  organisatie).
- Companies House als registratieschema: er is geen gangbare ICD voor.
