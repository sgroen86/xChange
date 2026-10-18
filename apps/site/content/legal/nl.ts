import { company, formatAddress } from "../company.ts";
import type { LegalContent } from "./types.ts";

export const legalNl: LegalContent = {
  privacy: {
    title: "Privacyverklaring",
    description:
      "Hoe xChange omgaat met persoonsgegevens: welke gegevens we verwerken, waarom, hoe lang en welke rechten je hebt.",
    intro: [
      `Deze privacyverklaring is bedoeld voor iedereen die xChange gebruikt of onze website bezoekt: klanten, hun gebruikers en mensen die contact met ons opnemen. xChange is een dienst van ${company.name}.`,
      "We leggen hieronder uit welke persoonsgegevens we verwerken, waarvoor, op welke grondslag, met wie we ze delen en hoe lang we ze bewaren.",
    ],
    sections: [
      {
        heading: "Wie is verantwoordelijk",
        paragraphs: [
          "Verwerkingsverantwoordelijke voor de verwerkingen in deze verklaring is:",
        ],
        items: [
          `${company.name}`,
          `Adres: ${formatAddress()}`,
          `Ondernemingsnummer: ${company.registrationNumber}`,
          ...(company.vatNumber ? [`Btw-nummer: ${company.vatNumber}`] : []),
          `E-mail: ${company.contactEmail}`,
        ],
      },
      {
        heading: "Reikwijdte en onze twee rollen",
        paragraphs: [
          "We hebben bij xChange twee verschillende rollen.",
          "Voor gegevens over je account, facturatie, support en het gebruik van de website zijn wij verwerkingsverantwoordelijke. Daarover gaat deze privacyverklaring.",
          "Voor de factuurgegevens die je als klant uploadt of naar xChange mailt, ben jij de verwerkingsverantwoordelijke en zijn wij verwerker. We verwerken die gegevens alleen in jouw opdracht, volgens de verwerkersovereenkomst. Een samenvatting daarvan staat op de pagina Vertrouwen en beveiliging.",
        ],
      },
      {
        heading: "Welke gegevens we verwerken",
        items: [
          "Accountgegevens: je naam en e-mailadres, en je wachtwoord, dat we alleen als hash opslaan.",
          "Organisatiegegevens: de naam en gegevens van je organisatie, zoals adres, ondernemingsnummer en btw-nummer.",
          "Facturen en pdf’s die je uploadt of mailt, met de persoonsgegevens die daarop staan. Hiervoor zijn we verwerker.",
          "E-mails die naar je xChange-inbox worden gestuurd.",
          "Correcties die je maakt bij het nakijken, zodat xChange leert voor jouw organisatie.",
          "Betaalgegevens: die verwerkt onze betaalprovider. Wij slaan geen volledige kaartnummers op.",
          "Technische logs: IP-adres, tijdstippen en foutmeldingen.",
          "Correspondentie met ons, bijvoorbeeld supportvragen.",
        ],
        after: [
          "Onze website gebruikt geen analytische of advertentiecookies. De webapplicatie gebruikt alleen wat strikt nodig is om in te loggen.",
        ],
      },
      {
        heading: "Doelen en grondslagen",
        table: {
          head: ["Doel", "Grondslag"],
          rows: [
            ["De dienst en je account leveren", "Uitvoering van de overeenkomst"],
            ["Facturatie en boekhouding", "Wettelijke verplichting"],
            [
              "Beveiliging, voorkomen van misbruik en foutanalyse",
              "Gerechtvaardigd belang",
            ],
            ["Support", "Uitvoering van de overeenkomst of gerechtvaardigd belang"],
            [
              "Leren van correcties voor je eigen organisatie",
              "Uitvoering van de overeenkomst",
            ],
            ["Mededelingen over de dienst en je account", "Gerechtvaardigd belang"],
          ],
        },
        after: [
          "We verkopen je gegevens niet en gebruiken ze niet voor advertenties.",
        ],
      },
      {
        heading: "Gebruik van AI",
        paragraphs: [
          "Pdf’s die je uploadt of mailt, worden gelezen door een AI-model van een gespecialiseerde verwerker, dat de factuurvelden eruit haalt.",
          "Voordat er iets wordt geëxporteerd, kijkt een mens het resultaat na. Bedragen, totalen en de XML worden deterministisch berekend, zonder AI.",
          "Er is geen sprake van geautomatiseerde besluitvorming met rechtsgevolgen of vergelijkbare aanzienlijke gevolgen voor jou in de zin van artikel 22 AVG.",
        ],
      },
      {
        heading: "Verwerkers en ontvangers",
        table: {
          head: ["Verwerker", "Doel", "Locatie"],
          rows: [
            ["Hostingprovider", "Hosting en opslag", "Europese Unie"],
            [
              "Aanbieder van AI-tekstherkenning",
              "Lezen van geüploade pdf’s",
              "Europese Unie en Verenigde Staten",
            ],
            [
              "Betaalprovider",
              "Verwerking van betalingen",
              "Europese Unie en Verenigde Staten",
            ],
          ],
        },
        after: [
          `Een actuele lijst van onze verwerkers kun je opvragen via ${company.contactEmail}.`,
          "Daarnaast verstrekken we gegevens aan overheidsinstanties alleen als de wet ons daartoe verplicht.",
        ],
      },
      {
        heading: "Doorgifte buiten de EER",
        paragraphs: [
          "Bij het lezen van pdf’s kunnen gegevens buiten de EU worden verwerkt, in de Verenigde Staten. Die doorgifte gebeurt op basis van de standaardcontractbepalingen van de Europese Commissie.",
          "Andere doorgiften buiten de Europese Economische Ruimte gebeuren alleen met passende waarborgen.",
        ],
      },
      {
        heading: "Bewaartermijnen",
        items: [
          `Account- en organisatiegegevens: zolang je account bestaat en tot uiterlijk ${company.retentionAfterClosing} na het sluiten ervan.`,
          `Facturen en pdf’s: tot je ze verwijdert of je account sluit; daarna verwijderen we ze binnen ${company.retentionAfterClosing}.`,
          "Ruwe ontvangen e-mails: verwijderd na 30 dagen.",
          "Het leesresultaat van het model bij een upload: 30 dagen bewaard.",
          "Facturatiegegevens: zolang de fiscale en boekhoudkundige wetgeving dat vereist.",
          "Technische logs: een beperkte periode.",
        ],
      },
      {
        heading: "Beveiliging",
        paragraphs: [
          "We beveiligen gegevens met passende technische en organisatorische maatregelen, waaronder versleuteling tijdens transport en in opslag en strikte scheiding van de gegevens per organisatie.",
          "Meer details staan op de pagina Vertrouwen en beveiliging.",
        ],
      },
      {
        heading: "Je rechten",
        paragraphs: ["Je hebt het recht om:"],
        items: [
          "je gegevens in te zien;",
          "onjuiste gegevens te laten corrigeren;",
          "je gegevens te laten wissen;",
          "de verwerking te laten beperken;",
          "je gegevens in een gangbaar formaat te ontvangen (gegevensoverdraagbaarheid);",
          "bezwaar te maken tegen verwerking op basis van gerechtvaardigd belang;",
          "je toestemming in te trekken, als toestemming de grondslag is.",
        ],
        after: [
          `Je kunt deze rechten uitoefenen door te mailen naar ${company.contactEmail}. We antwoorden binnen een maand.`,
          "Gaat je verzoek over factuurgegevens die we als verwerker voor een klant verwerken, dan sturen we het door naar die klant en helpen we hem om het te beantwoorden.",
        ],
      },
      {
        heading: "Klachten",
        paragraphs: [
          "Heb je een klacht over hoe we met je gegevens omgaan, neem dan eerst contact met ons op. We zoeken graag samen een oplossing.",
          `Je hebt ook het recht een klacht in te dienen bij ${company.supervisoryAuthority} (${company.supervisoryAuthorityUrl}) of bij de toezichthouder in het land waar je woont of werkt.`,
        ],
      },
      {
        heading: "Wijzigingen",
        paragraphs: [
          "We kunnen deze privacyverklaring aanpassen. De actuele versie staat altijd op deze pagina.",
          "Belangrijke wijzigingen melden we vooraf aan accounthouders.",
        ],
      },
    ],
  },

  terms: {
    title: "Algemene voorwaarden",
    description:
      "De voorwaarden voor het gebruik van xChange, de dienst die pdf-facturen omzet in gevalideerde e-facturen.",
    intro: [
      `Deze voorwaarden gelden voor het gebruik van xChange, een dienst van ${company.name}.`,
      "xChange is bedoeld voor bedrijven en andere professionele gebruikers, niet voor consumenten. Door een account aan te maken of de dienst te gebruiken, ga je met deze voorwaarden akkoord namens de organisatie die je vertegenwoordigt.",
    ],
    sections: [
      {
        heading: "Definities",
        items: [
          "Dienst: xChange, de online toepassing en alles wat daarbij hoort.",
          "Klant: de organisatie die een account heeft en de dienst gebruikt.",
          "Gebruiker: een persoon die namens de klant toegang heeft tot de dienst.",
          "Conversie: één pdf die tot een factuur wordt ingelezen.",
          "Abonnement: een maandelijks abonnement met een aantal inbegrepen conversies.",
          "Credits: vooraf gekochte conversies uit een creditpakket, los van een abonnement.",
        ],
      },
      {
        heading: "De dienst",
        paragraphs: [
          "Met xChange zet je je eigen pdf-verkoopfacturen om in gevalideerde e-facturen. Je uploadt een of meer pdf’s of mailt ze naar je persoonlijke inboxadres; gemailde pdf’s wachten op je goedkeuring voordat ze worden gelezen. AI leest de factuur, en jij controleert en corrigeert de velden op een reviewscherm met live validatie.",
          "xChange exporteert naar PEPPOL BIS Billing 3.0, NLCIUS / SI-UBL 2.0 en PINT-SG (alleen als demo). Elk bestand wordt gecontroleerd tegen de volledige officiële validatieregels voordat je het kunt downloaden.",
          "Versturen via een PEPPOL-accesspoint en het ontvangen van inkomende facturen horen nog niet bij de dienst. Je downloadt het gevalideerde bestand en verstuurt het zelf, via je eigen accesspoint of boekhoudsoftware, of via de weg die je met je klant hebt afgesproken.",
          "We ontwikkelen de dienst verder. Functies kunnen daardoor veranderen, worden toegevoegd of verdwijnen.",
        ],
      },
      {
        heading: "Account en toegang",
        items: [
          "Je geeft bij het aanmaken van een account juiste en volledige gegevens en houdt die actueel.",
          "Je houdt inloggegevens vertrouwelijk en deelt ze niet.",
          "De klant is verantwoordelijk voor zijn gebruikers en voor wat zij met de dienst doen.",
          `Vermoed je misbruik van een account, laat het ons dan meteen weten via ${company.supportEmail}.`,
        ],
      },
      {
        heading: "Gratis proefperiode",
        paragraphs: [
          "Je kunt xChange gratis uitproberen met 10 conversies. Je hebt daarvoor geen betaalkaart nodig en je zit nergens aan vast.",
          "We kunnen de proefperiode voor nieuwe aanmeldingen beëindigen of de voorwaarden ervan wijzigen.",
        ],
      },
      {
        heading: "Abonnementen, prijzen en betaling",
        paragraphs: [
          "Je kiest een maandabonnement met een aantal inbegrepen conversies, of je koopt creditpakketten en betaalt per gebruik.",
          "De prijzen staan op de prijzenpagina en zijn exclusief btw. Betalingen lopen via onze betaalprovider.",
          `Prijswijzigingen kondigen we ten minste ${company.noticePeriod} vooraf aan. Ze gaan in vanaf de eerstvolgende factuurperiode.`,
        ],
      },
      {
        heading: "Opzeggen en beëindigen",
        paragraphs: [
          "Je kunt je abonnement op elk moment opzeggen. De opzegging gaat in aan het einde van de lopende factuurperiode.",
          "Bij een ernstige schending van deze voorwaarden of bij niet-betaling kunnen we je toegang opschorten of het account beëindigen, nadat we je daarover hebben geïnformeerd.",
          "Exporteer je gegevens voordat je je account sluit. Daarna verwijderen we ze zoals beschreven in de privacyverklaring.",
        ],
      },
      {
        heading: "Redelijk gebruik",
        paragraphs: ["Bij het gebruik van xChange is het niet toegestaan om:"],
        items: [
          "onrechtmatige inhoud te verwerken, of documenten die je niet mag verwerken;",
          "te proberen toegang te krijgen tot gegevens van anderen of de beveiliging te omzeilen;",
          "de dienst geautomatiseerd en massaal te gebruiken buiten wat je abonnement toelaat, of de dienst te overbelasten;",
          "de dienst door te verkopen zonder onze afspraak daarover;",
          "de software te decompileren of na te bouwen, behalve voor zover de wet dat toestaat.",
        ],
      },
      {
        heading: "Je verantwoordelijkheid voor de factuurinhoud",
        paragraphs: [
          "Je blijft verantwoordelijk voor de inhoud van je facturen en voor de gegevens die je controleert, corrigeert, boekt en exporteert.",
          "De validatie controleert of het bestand voldoet aan de regels van het formaat. Ze controleert niet of de factuur commercieel of fiscaal klopt. Kijk een factuur dus na voordat je ze verstuurt.",
          "Je blijft zelf verantwoordelijk voor je fiscale en boekhoudkundige verplichtingen.",
        ],
      },
      {
        heading: "Je gegevens",
        paragraphs: [
          "Je gegevens blijven van jou. We verwerken ze om de dienst te leveren, volgens de privacyverklaring en de verwerkersovereenkomst.",
          "Gevalideerde bestanden kun je altijd exporteren.",
        ],
      },
      {
        heading: "Lezen met AI",
        paragraphs: [
          "Velden die door AI uit een pdf worden gelezen, kunnen fouten bevatten. Daarom is het nakijken door een mens een vast onderdeel van het proces.",
        ],
      },
      {
        heading: "Beschikbaarheid en support",
        paragraphs: [
          "We doen redelijke inspanningen om xChange beschikbaar en goed werkend te houden, maar garanderen geen ononderbroken beschikbaarheid. Voor onderhoud kan de dienst tijdelijk onbereikbaar zijn.",
          `Voor hulp kun je terecht bij ${company.supportEmail}.`,
        ],
      },
      {
        heading: "Intellectuele eigendom",
        paragraphs: [
          "De dienst en de software blijven van ons. Zolang je abonnement loopt, krijg je een recht om de dienst te gebruiken.",
          "Jouw inhoud blijft van jou. Je geeft ons de rechten die nodig zijn om de dienst te leveren.",
        ],
      },
      {
        heading: "Aansprakelijkheid",
        paragraphs: [
          "Onze aansprakelijkheid is beperkt tot de bedragen die je in de 12 maanden vóór de schadeveroorzakende gebeurtenis aan ons hebt betaald.",
          "We zijn niet aansprakelijk voor indirecte schade, zoals gederfde winst, verlies van gegevens voor zover die niet uit back-ups te herstellen zijn, en boetes die het gevolg zijn van de inhoud van je facturen.",
          "Deze beperkingen gelden niet voor zover de wet ze niet toestaat, zoals bij opzet of grove nalatigheid.",
        ],
      },
      {
        heading: "Wijzigingen in deze voorwaarden",
        paragraphs: [
          `We kondigen wijzigingen in deze voorwaarden ten minste ${company.noticePeriod} vooraf aan.`,
          "Gebruik je de dienst daarna verder, dan aanvaard je de nieuwe voorwaarden. Ben je het er niet mee eens, dan kun je je abonnement opzeggen.",
        ],
      },
      {
        heading: "Toepasselijk recht en geschillen",
        paragraphs: [
          `Toepasselijk recht: ${company.governingLaw}.`,
          `Geschillen worden voorgelegd aan ${company.competentCourt}.`,
          `Heb je een probleem of vraag, neem dan eerst contact op via ${company.contactEmail}. We proberen het samen op te lossen.`,
        ],
      },
    ],
  },

  trust: {
    title: "Vertrouwen en beveiliging",
    description:
      "Waar je gegevens bij xChange staan, wie ze verwerkt, hoe lang we ze bewaren en hoe we ze beveiligen.",
    intro: [
      "Op deze pagina lees je in het kort hoe xChange met je gegevens omgaat: waar ze staan, wie ze verwerkt, hoe lang we ze bewaren en hoe we ze beveiligen.",
    ],
    sections: [
      {
        heading: "Waar je gegevens staan",
        paragraphs: [
          "xChange wordt gehost in de Europese Unie. Je documenten en factuurgegevens worden daar opgeslagen.",
        ],
      },
      {
        heading: "Hoe facturen worden gelezen",
        paragraphs: [
          "Een AI-model van een gespecialiseerde verwerker leest de pdf en haalt de factuurvelden eruit. Daarbij kunnen gegevens buiten de EU worden verwerkt, in de Verenigde Staten; die doorgifte gebeurt onder de standaardcontractbepalingen van de EU.",
          "Jij kijkt alles na voordat er iets wordt geëxporteerd.",
          "Totalen en XML worden deterministisch berekend. Er zit geen AI in de berekeningen.",
          "Elke export wordt gecontroleerd tegen de volledige officiële validatieregels. Een bestand met een fout geven we nooit vrij.",
        ],
      },
      {
        heading: "Subverwerkers",
        table: {
          head: ["Subverwerker", "Doel", "Locatie"],
          rows: [
            ["Hostingprovider", "Hosting en opslag", "Europese Unie"],
            [
              "Aanbieder van AI-tekstherkenning",
              "Lezen van geüploade pdf’s",
              "Europese Unie en Verenigde Staten",
            ],
            [
              "Betaalprovider",
              "Verwerking van betalingen",
              "Europese Unie en Verenigde Staten",
            ],
          ],
        },
        after: [
          `Een actuele lijst van onze subverwerkers kun je opvragen via ${company.contactEmail}.`,
          "Wijzigingen in de subverwerkers kondigen we vooraf aan.",
        ],
      },
      {
        heading: "Bewaartermijnen",
        table: {
          head: ["Gegevens", "Bewaartermijn"],
          rows: [
            ["Ruwe ontvangen e-mails", "30 dagen"],
            ["Leesresultaat van het model bij een upload", "30 dagen"],
            [
              "Facturen en bron-pdf’s",
              `Tot je ze verwijdert of je account sluit, daarna binnen ${company.retentionAfterClosing}`,
            ],
            [
              "Accountgegevens",
              `Zolang het account bestaat, daarna binnen ${company.retentionAfterClosing}`,
            ],
            ["Facturatiegegevens", "Zolang de wet dat vereist"],
          ],
        },
      },
      {
        heading: "Beveiligingsmaatregelen",
        items: [
          "Gegevens zijn versleuteld tijdens transport (TLS) en in opslag.",
          "De gegevens van elke organisatie worden apart opgeslagen, per organisatie, en zijn niet bereikbaar voor andere organisaties.",
          "Wachtwoorden worden alleen als bcrypt-hash opgeslagen.",
          "Na herhaalde mislukte inlogpogingen wordt inloggen tijdelijk geblokkeerd.",
          "API-sleutels en andere geheimen staan in een beheerde geheimenkluis, niet in de code.",
          "Toegang tot de productieomgeving is beperkt tot wat nodig is.",
        ],
      },
      {
        heading: "Verwerkersovereenkomst – samenvatting",
        items: [
          "Onderwerp en duur: het leveren van xChange, zolang het abonnement loopt.",
          "Aard en doel: facturen lezen, opslaan, valideren en exporteren.",
          "Soorten persoonsgegevens: namen, adressen, e-mailadressen, telefoonnummers, btw- en ondernemingsnummers en bankgegevens die op facturen staan, en accountgegevens van gebruikers.",
          "Categorieën betrokkenen: de gebruikers van de klant, en de klanten van de klant met hun contactpersonen.",
          "We verwerken de gegevens alleen op basis van gedocumenteerde instructies van de klant.",
          "Onze medewerkers zijn tot geheimhouding verplicht.",
          "We nemen passende beveiligingsmaatregelen.",
          "We schakelen subverwerkers alleen in onder gelijkwaardige verplichtingen.",
          "We helpen de klant bij verzoeken van betrokkenen en bij gegevensbeschermingseffectbeoordelingen (DPIA’s).",
          "We melden een inbreuk in verband met persoonsgegevens zonder onredelijke vertraging.",
          "Aan het einde van de dienstverlening verwijderen of retourneren we de gegevens.",
          "We stellen de informatie beschikbaar die nodig is voor audits.",
        ],
        after: [
          `De verwerkersovereenkomst maakt deel uit van de algemene voorwaarden. Je kunt een exemplaar opvragen via ${company.contactEmail}.`,
        ],
      },
      {
        heading: "Incidenten",
        paragraphs: [
          "Bij een beveiligingsincident onderzoeken we meteen wat er is gebeurd en beperken we de gevolgen.",
          "Gaat het om een inbreuk in verband met persoonsgegevens, dan informeren we de betrokken klanten zonder onredelijke vertraging, zodat zij hun eigen verplichtingen kunnen nakomen. We werken mee met de bevoegde autoriteiten.",
        ],
      },
      {
        heading: "Vragen en kwetsbaarheden melden",
        paragraphs: [
          `Heb je een vraag over beveiliging of privacy, of heb je een kwetsbaarheid gevonden, mail dan naar ${company.contactEmail}.`,
          "We stellen het op prijs als je een kwetsbaarheid eerst bij ons meldt en ons de tijd geeft om ze te verhelpen, voordat je ze openbaar maakt.",
        ],
      },
    ],
  },
};
