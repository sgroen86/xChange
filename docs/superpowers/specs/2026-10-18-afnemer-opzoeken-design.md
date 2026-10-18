# xChange: de afnemer opzoeken op PEPPOL

**Datum:** 2026-10-18
**Status:** gevraagd ("defo work on 1"); details zonder verder overleg gekozen

## Doel

Tijdens het beoordelen weten of de afnemer e-facturen kan ontvangen, op welk adres, en in
welke van onze formaten. Een leeg elektronisch adres, de meest voorkomende reden dat een
PEPPOL-bestand wordt geweigerd, vult xChange zelf in.

## Hoe PEPPOL het vertelt (geverifieerd op 2026-10-18)

1. **SML (DNS).** Een deelnemer staat onder
   `<base32(sha256(lowercase("0106:12345678")))>.iso6523-actorid-upis.<zone>` met een
   U-NAPTR-record (`Meta:SMP`) dat naar zijn SMP wijst. De zone is die van OpenPeppol,
   `participant.sml.prod.tech.peppol.org`. Die vervangt per 1 september 2026 die van de
   Europese Commissie, en die van de Commissie antwoordt al niet meer voor deze deelnemer.
   CNAME/MD5 bestaat sinds februari 2026 niet meer.
2. **SMP (HTTP).** `GET <smp>/iso6523-actorid-upis%3A%3A0106%3A12345678` geeft de
   ServiceGroup met een verwijzing per documenttype. Bij 404 staat de deelnemer niet
   geregistreerd.

Beide zijn openbaar; er is geen certificaat voor nodig. De PEPPOL Directory (zoeken op naam)
is niet gezaghebbend en wordt niet gebruikt.

## Besluiten

- **Op nummer, nooit op naam.** De kandidaten worden in deze volgorde geprobeerd:
  1. het elektronisch adres van de factuur;
  2. het btw-nummer (NL `9944`, BE `9925`, VK `9932`; een Belgisch btw-nummer ook als
     ondernemingsnummer `0208`);
  3. het registratienummer (KvK `0106`, `0208`, Singapore-UEN `0195` als `SGUEN…`; zonder
     schema alleen waar het land het eenduidig maakt).

  Alle kandidaten worden tegelijk gevraagd. De betrouwbaarste die geregistreerd is, wint.
- **Formaten.** Een formaat wordt geaccepteerd als de ontvanger facturen aanneemt met onze
  CustomizationID, of met een wildcard (`peppol-doctype-wildcard`, `…billing-1*`) waar die
  onder valt.
- **Netwerken.** `IReceiverNetwork` in Application, met PEPPOL als eerste implementatie. Een
  ander netwerk komt erbij als nieuwe implementatie.
- **Nooit blokkeren.** 5 seconden per deelnemer. Een storing heet "niet bereikbaar", niet
  "niet gevonden".
- **Veiligheid.** Het SMP-adres komt uit DNS en wordt gecontroleerd: alleen http(s) en
  alleen naar publieke adressen, ook na een redirect. Antwoorden zijn maximaal 2 MB. Alleen
  ingelogde gebruikers kunnen opzoeken, zodat de API geen open doorgeefluik is.
- **Niets opgeslagen.** Het opzoeken gebeurt bij het beoordelen. Het gevonden adres komt via
  het boeken in de relatie terecht, zoals elk ander veld.

## Op het scherm

- **Afnemerkaart:** "Op PEPPOL als 9944:NL… · ontvangt: BIS 3.0, NLCIUS", of "Niet
  gevonden op PEPPOL (gezocht op …)". Een leeg elektronisch adres wordt ingevuld, met een
  melding. Een afwijkend adres wordt nooit overschreven; daarvoor is er de knop "Dit adres
  overnemen".
- **Formaat:** de keuze voor deze download, dan het formaat van de afnemer (stuk 4), dan
  een formaat dat de ontvanger aanneemt (het standaardformaat van de organisatie als hij dat
  aanneemt), dan het standaardformaat van de organisatie. Een Singaporese organisatie met
  een Britse afnemer die alleen BIS 3.0 ontvangt, opent zo vanzelf op BIS 3.0.
- **Exportpaneel:** een waarschuwing als het gekozen formaat niet is wat de ontvanger
  aanneemt.

## Niet in dit stuk

- Opslaan van het resultaat bij de relatie, en de relatiepagina laten opzoeken.
- Zoeken op naam via de PEPPOL Directory.
- Andere netwerken.
