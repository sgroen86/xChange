# xChange: leren van correcties

**Datum:** 2026-10-18
**Status:** gevraagd ("ai learning for the next invoice"); details zonder verder overleg
gekozen

## Doel

De tweede factuur hoeft niet opnieuw op dezelfde punten gecorrigeerd te worden. Een
organisatie uploadt haar eigen verkoopfacturen, en die komen bijna altijd uit hetzelfde
facturatiepakket, met dezelfde opmaak en dezelfde eigen gegevens. Wat de beoordelaar één keer
verbetert, weet xChange de volgende keer.

## Hoe

1. **Bij het uploaden** bewaart xChange wat het model las (de ruwe extractie), 30 dagen,
   in de partitie van de organisatie: `ORG#<org>` / `EXTRACTION#<extractionId>`, met TTL.
   Het `invoiceId` dat de extractie al teruggaf, is het `extractionId`.
2. **Bij het boeken** stuurt het scherm het `extractionId` mee. xChange vergelijkt de ruwe
   extractie met de geboekte factuur en onthoudt de verschillen
   (`ORG#<org>` / `LEARNING`, maximaal 50, de recentste blijven). Daarna wordt de extractie
   verwijderd, zodat dezelfde boeking niet twee keer leert.
3. **Bij de volgende upload** gebeurt twee dingen:
   - **Toepassen** (deterministisch, met een melding "zoals eerder gecorrigeerd"). Alleen als
     het model precies hetzelfde las als toen (de "van"-waarde), wordt het de verbeterde
     waarde:
     - *eigen gegevens* (per organisatie): naam, btw- en registratienummer van de verkoper,
       adres, betaalwijze, IBAN, BIC en rekeninghouder. Die staan op elke factuur gelijk;
     - *belasting per afnemer* (per relatie, herkend op btw- of registratienummer): de
       categorie en het tarief per regel (bijvoorbeeld S 9% → AE 0% voor een Britse klant),
       en de vrijstellingsreden bij die categorie.
   - **Hints** voor het model: verbeteringen aan de koptekst (referentie afnemer,
     inkoopordernummer, vervaldatum, soort, valuta) en aan de eenheid per regel (met de
     omschrijving erbij). Ze gaan als gegevens mee in het verzoek. De systeemprompt zegt hoe
     ze te gebruiken: om het document op dezelfde manier te lezen, nooit om een waarde over
     te nemen die dit document niet toont. De regel "niets verzinnen" blijft gelden.
4. **Zelfherstellend.** Draait de beoordelaar een toegepaste correctie terug, dan vergeet
   xChange haar.
5. **Inzichtelijk.** Onder Instellingen staat "Geleerd van correcties": wat, van → naar,
   hoe vaak, voor wie, met een knop "Vergeten".

## Grenzen

- Er wordt per organisatie geleerd, nooit over organisaties heen (harde regel 4).
- Geen AI in de rekenkunde of het XML (harde regels 1 en 2): het model krijgt alleen hints
  om beter te lezen, en het toepassen is gewone code.
- Wat de afnemer betreft, vult de bestaande relatie-verrijking al naam, adres en nummers
  aan. Daar wordt niets dubbel geleerd.

## Niet in dit stuk

- Leren per pagina-indeling of per leverancier van inkomende facturen (die bestaan nog niet).
- Correcties delen tussen organisaties.
