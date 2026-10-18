# xChange: verzenden via een PEPPOL access point (plan)

**Datum:** 2026-10-18
**Status:** plan; wacht op de keuze van een access point (zie "Beslissingen")

## Doel

Een geboekte, gevalideerde factuur met één klik bij de afnemer afleveren via PEPPOL (en
in Singapore via InvoiceNow), met een status die laat zien of hij is aangekomen. Daarmee
wordt xChange van een converter een werkend systeem: PDF erin, factuur afgeleverd.

## Zelf access point worden of een partner?

Een partner. Zelf access point worden betekent per regio een overeenkomst met de PEPPOL-
autoriteit, OpenPeppol-lidmaatschap, AS4-infrastructuur met certificaten, tests,
beveiligingseisen en in Singapore een aparte IMDA-accreditatie. Dat is voor later, als het
volume het rechtvaardigt. Met een partner verstuurt xChange het eigen, officieel
gevalideerde UBL-bestand via de API van de partner.

## Welke partner

Eis: geaccrediteerd voor **Singapore** (IMDA-lijst "InvoiceNow Access Point Provider",
bijgewerkt 21 september 2026) **en** werkzaam in Europa (NL, BE, VK), met een API voor
softwareleveranciers die **ons eigen UBL** ongewijzigd verstuurt, statusmeldingen
(webhooks), een sandbox, en later ook ontvangen.

| Partner | IMDA (SG) | Europa | API voor software | Prijs (openbaar, niet geverifieerd) |
|---|---|---|---|---|
| **Storecove** (Datajust B.V., NL) | ja | ja | ja, gericht op SaaS | abonnement + per document (vanaf ca. €99/mnd volgens derden) |
| **Ademico** (BE) | ja | ja | ja | per document, vanaf €0,12 bij 6.000/jaar; test gratis |
| Tickstar, ecosio, Pagero, Avalara | ja | ja | ja, meer enterprise | op aanvraag |
| Recommand, e-invoice.be | **nee** | ja | ja, goedkoop (€0,07–0,30) | valt af voor Singapore |

**Voorstel:** sandbox-accounts aanvragen bij **Storecove** en **Ademico**, beide API's
proberen met onze referentiebestanden (BIS 3.0, PINT-SG, NLCIUS), en kiezen op:
1. ondersteuning van PINT-SG en NLCIUS als ruw document;
2. de kwaliteit van de API en webhooks;
3. de prijs bij ons volume;
4. een contract dat past bij een platform met meerdere organisaties.

## Hoe het werkt

### Organisatie aanmelden (eenmalig)

- In Instellingen kiest een beheerder **"Verzenden via PEPPOL activeren"**. xChange
  meldt de organisatie bij de partner aan als afzender, met naam, land, btw/GST-nummer,
  registratienummer (KvK/UEN) en PEPPOL-id. De partner doet zijn eigen controle (KYC).
- **Let op:** een PEPPOL-id kan maar bij één access point geregistreerd zijn voor
  ontvangen. Staat een organisatie al bij een ander access point (bijvoorbeeld via haar
  boekhoudpakket), dan:
  - kan versturen vaak wel;
  - vraagt ontvangen via xChange een verhuizing van die registratie.

  xChange controleert dit vooraf met de SMP-opzoeking die er al is.

### Versturen

1. Op een geboekte factuur verschijnt **"Verzenden"** als:
   - de afnemer op PEPPOL is gevonden (opzoeking bestaat al);
   - het gekozen formaat is wat de afnemer aanneemt;
   - de officiële validatie geen fouten geeft (bestaat al).
2. xChange maakt het bestand zoals bij downloaden, legt een **verzending** vast
   (`ORG#<org>` / `TRANSMISSION#<id>`: factuur, formaat, ontvanger, documenttype, status) en
   zet hem op een nieuwe SQS-wachtrij `xchange-sending`.
3. De worker verstuurt via de partner-API. Een tijdelijke fout gaat terug naar de wachtrij
   en na drie pogingen naar een dead-letter-wachtrij met melding.
4. De partner meldt per webhook **afgeleverd** of **mislukt**. Er komt een eigen endpoint
   met een geheim of handtekening, en de partner wordt ook bevraagd als vangnet.
5. Statussen op de factuur en in de lijst: *in de wachtrij → verzonden → afgeleverd*, of
   *mislukt* (met reden, opnieuw proberen).

### Na verzending is de factuur definitief

Een verzonden factuur kan niet meer gewijzigd of opnieuw geboekt worden; een correctie
gaat met een creditnota. Dit beantwoordt de open beslissing uit CLAUDE.md
("Whether booking stays reversible") voor verzonden facturen. Niet-verzonden facturen
blijven aanpasbaar.

### Grenzen en regels

- Alleen het bestand dat de officiële validatie doorstond, gaat de deur uit. Dat is
  dezelfde code als downloaden; er is geen AI in dit pad (harde regel 1).
- Alles per organisatie (harde regel 4): verzendingen, statussen en de aanmelding bij de
  partner.
- De API-sleutel van de partner staat in Secrets Manager, zoals die van Anthropic.

## Onderdelen (code)

- **Application**
  - `IAccessPoint`: afzender aanmelden, document versturen, status opvragen,
    webhook-melding lezen.
  - `SendInvoiceService`.
  - `ITransmissionRepository`.
- **Infrastructure:** één adapter per partner (`AccessPoints/<Partner>`), DynamoDB-opslag
  voor verzendingen, en de SQS-wachtrij voor versturen.
- **Worker:** een tweede SQS-handler voor versturen.
- **Api**
  - `POST /invoices/{id}/send`, `GET /invoices/{id}/transmissions`;
  - `POST /webhooks/access-point` (geen sessie, wel handtekening);
  - activeren in de instellingen.
- **Web:** de knop en het statusoverzicht op de factuur, een statuskolom in de
  factuurlijst, en activeren in Instellingen.
- **Infra:** wachtrij en dead-letter-wachtrij, de webhook-route, het geheim, en rechten
  voor de deploy-rol.

## Fasen

0. **Partner kiezen.**
   - De gebruiker vraagt sandbox-accounts aan bij Storecove en Ademico (contract en KYC
     zijn aan hem).
   - xChange probeert beide API's met de referentiebestanden. Daarna: keuze.
1. **Versturen in de sandbox:** adapter, verzendingen, wachtrij en worker, met statussen
   via polling.
2. **Afmaken:** webhooks, definitief na verzending, de schermen en aanmelden in
   Instellingen.
3. **Live met de eigen organisatie:** Green IT Solutions verstuurt een echte factuur naar
   een echte afnemer (VK via BIS 3.0, en een Singaporese via PINT-SG).
4. **Later:** ontvangen (inkomende facturen naar PDF) via dezelfde partner.

## Kosten (indicatie)

Per verzonden factuur:
- €0,07–0,30 aan de partner (afhankelijk van partner en volume);
- ongeveer $0,10 voor het lezen van de PDF, als die via xChange is ingelezen;
- een fractie van een cent aan AWS.

## Beslissingen voor de gebruiker

1. **Welke partner:** sandbox-accounts aanvragen bij Storecove en Ademico (voorstel).
2. **Definitief na verzending:** akkoord dat een verzonden factuur alleen met een
   creditnota wordt gecorrigeerd (voorstel: ja).
3. **Aanmelden per organisatie:** elke organisatie wordt als eigen afzender aangemeld bij
   de partner, met haar eigen gegevens (voorstel: ja). xChange verstuurt dus nooit
   namens zichzelf.
