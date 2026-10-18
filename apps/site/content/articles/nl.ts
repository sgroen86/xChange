import type { Articles } from "./types.ts";

export const articlesNl: Articles = {
  "belgium-e-invoicing-2026": {
    title: "België: verplicht e-factureren sinds 2026, wat betekent dat voor jou?",
    description:
      "Sinds 1 januari 2026 moeten Belgische bedrijven elkaar gestructureerde e-facturen sturen. Wat telt als e-factuur, wie valt eronder en welke stappen zet je als je nu nog pdf-facturen maakt?",
    readingMinutes: 3,
    blocks: [
      {
        type: "p",
        text: "Sinds 1 januari 2026 is factureren tussen Belgische bedrijven ingrijpend veranderd. Facturen tussen btw-plichtige ondernemingen die in België gevestigd zijn, moeten voortaan gestructureerde elektronische facturen zijn. Een pdf die je per e-mail verstuurt, voldoet daar niet meer aan. In dit artikel lees je wie onder de verplichting valt, wat precies als e-factuur telt en welke stappen je zet als je vandaag nog pdf-facturen maakt.",
      },
      { type: "h2", text: "Voor wie geldt de verplichting?" },
      {
        type: "p",
        text: "De verplichting geldt voor binnenlandse B2B-facturen: facturen tussen btw-plichtige ondernemingen die allebei in België gevestigd zijn. Factureer je als Belgisch bedrijf aan een andere Belgische onderneming, dan valt die factuur er in principe onder. Facturen aan particulieren vallen er niet onder, net zomin als facturen aan buitenlandse klanten.",
      },
      {
        type: "p",
        text: "Sommige categorieën van ondernemingen zijn uitgezonderd. Twijfel je of dat voor jou geldt, bekijk je situatie dan met je boekhouder. De verplichting werkt in twee richtingen: je moet e-facturen kunnen versturen, maar ook kunnen ontvangen.",
      },
      { type: "h2", text: "Wat telt als e-factuur?" },
      {
        type: "p",
        text: "Een e-factuur is geen digitaal plaatje van een papieren factuur, maar een gestructureerd bestand dat een computer zonder tussenkomst van een mens kan lezen. Elk gegeven, van factuurnummer en btw-bedrag tot de gegevens van koper en verkoper, staat op een vaste plek in een vast formaat. Een pdf is voor mensen leesbaar, maar voor software niet betrouwbaar te verwerken. Daarom telt een pdf niet als e-factuur, ook niet als je hem per e-mail verstuurt.",
      },
      {
        type: "p",
        text: "De standaard in België is PEPPOL BIS Billing 3.0: een XML-formaat dat voldoet aan de Europese norm voor e-facturen, EN 16931. Facturen worden uitgewisseld via het PEPPOL-netwerk, waarop de meeste Belgische ondernemingen bereikbaar zijn via hun ondernemingsnummer (KBO-nummer). In PEPPOL hoort daar de code 0208 bij, bijvoorbeeld 0208:0123456789.",
      },
      { type: "h2", text: "Je maakt nog pdf-facturen: dit zijn je stappen" },
      {
        type: "p",
        text: "Veel kleinere bedrijven maken hun facturen in een tekstverwerker, een spreadsheet of een eenvoudig programma dat alleen een pdf oplevert. Dan zijn dit de stappen om te zetten:",
      },
      {
        type: "ol",
        items: [
          "Controleer of je klanten op PEPPOL geregistreerd zijn. Zoek op hun ondernemingsnummer, niet op hun naam.",
          "Kies hoe je gaat versturen: via een PEPPOL-dienstverlener (een zogenoemd access point) of via boekhoudsoftware die op PEPPOL is aangesloten.",
          "Zorg dat je facturen als PEPPOL BIS Billing 3.0 beschikbaar zijn. Dat kan door ze rechtstreeks in dat formaat te maken, of door je bestaande pdf-facturen om te zetten.",
          "Blijf je gegevens nakijken. Een e-factuur wordt automatisch verwerkt, dus een fout btw-nummer of een verkeerd totaal valt minder snel op dan op papier.",
        ],
      },
      {
        type: "p",
        text: "Voor die derde stap is xChange bedoeld. Je uploadt je eigen pdf-factuur, AI leest de gegevens uit en jij controleert ze op een reviewscherm. De totalen worden exact berekend, zonder AI, en het bestand wordt tegen de volledige officiële validatieregels gecontroleerd voordat je het kunt downloaden. Rechtstreeks versturen via een access point kan xChange nog niet; daar wordt aan gewerkt. Voorlopig verstuur je het bestand via je eigen access point of boekhoudsoftware.",
      },
      { type: "h2", text: "Vergeet de ontvangstkant niet" },
      {
        type: "p",
        text: "Ook als je zelf weinig factureert, krijg je facturen van leveranciers. Die zullen je steeds vaker via PEPPOL willen bereiken. Zorg dus dat je ook kunt ontvangen: via een access point of via je boekhoudpakket, gekoppeld aan je ondernemingsnummer. Vraag je boekhouder hoe ontvangen facturen in jouw boekhouding terechtkomen, zodat je ze niet alsnog handmatig moet overtypen.",
      },
      {
        type: "note",
        text: "Voor latere jaren zijn verdere stappen aangekondigd. Houd die in de gaten via je boekhouder of de officiële informatie van de overheid, maar begin nu met de basis: e-facturen kunnen versturen en ontvangen.",
      },
    ],
  },

  "what-is-peppol": {
    title: "Wat is PEPPOL (en wat is een PEPPOL-ID)?",
    description:
      "PEPPOL is het netwerk waarlangs steeds meer e-facturen lopen. Hoe werkt het, wat is een access point, hoe ziet een PEPPOL-ID eruit en hoe zie je of iemand e-facturen kan ontvangen?",
    readingMinutes: 3,
    blocks: [
      {
        type: "p",
        text: "Wie met e-facturen begint, komt al snel de naam PEPPOL tegen. Overheden in Nederland en België gebruiken het, en sinds 2026 loopt ook het Belgische zakelijke verkeer er grotendeels via. Toch is lang niet altijd duidelijk wat PEPPOL eigenlijk is. In dit artikel leggen we uit hoe het netwerk werkt, wat een PEPPOL-ID is en hoe je ziet of je klant e-facturen kan ontvangen.",
      },
      { type: "h2", text: "Een netwerk, geen programma" },
      {
        type: "p",
        text: "PEPPOL is een internationaal netwerk voor het uitwisselen van zakelijke documenten, zoals facturen en creditnota’s. Het is geen softwarepakket en geen website waar je inlogt, maar een verzameling afspraken: hoe documenten eruitzien, hoe ze verstuurd worden en hoe partijen elkaar vinden. Het netwerk wordt beheerd door OpenPeppol, een internationale vereniging.",
      },
      {
        type: "p",
        text: "Je sluit niet zelf rechtstreeks op het netwerk aan. Dat gaat via een gecertificeerde dienstverlener, een zogenoemd access point. Vaak zit die aansluiting al ingebouwd in je boekhoudsoftware.",
      },
      { type: "h2", text: "Het vierhoekmodel" },
      {
        type: "p",
        text: "PEPPOL werkt volgens het zogenoemde vierhoekmodel. De vier hoeken zijn:",
      },
      {
        type: "ol",
        items: [
          "de verzender, die een factuur klaarzet;",
          "het access point van de verzender, dat de factuur het netwerk op stuurt;",
          "het access point van de ontvanger, dat de factuur in ontvangst neemt;",
          "de ontvanger, die de factuur in zijn eigen software binnenkrijgt.",
        ],
      },
      {
        type: "p",
        text: "Het voordeel: verzender en ontvanger hoeven niet bij dezelfde dienstverlener te zitten. Net als bij e-mail kan iedereen met een aansluiting iedereen bereiken, ongeacht welke aanbieder de ander gebruikt.",
      },
      { type: "h2", text: "Hoe ziet een PEPPOL-ID eruit?" },
      {
        type: "p",
        text: "Wie facturen wil ontvangen, wordt op PEPPOL geregistreerd met een PEPPOL-ID. Dat bestaat uit twee delen, gescheiden door een dubbele punt: een schemacode die aangeeft wat voor nummer het is, en het nummer zelf. Veelgebruikte voorbeelden:",
      },
      {
        type: "ul",
        items: [
          "0208: het Belgische ondernemingsnummer (KBO-nummer), bijvoorbeeld 0208:0123456789.",
          "0106: het nummer bij de Nederlandse Kamer van Koophandel (KvK-nummer).",
          "0190: het Organisatie-identificatienummer (OIN) dat Nederlandse overheidsorganisaties gebruiken.",
        ],
      },
      {
        type: "p",
        text: "Een PEPPOL-ID is dus geen willekeurige code, maar een bestaand bedrijfsnummer met een voorvoegsel. Wie het ondernemingsnummer of KvK-nummer van een klant kent, kan meestal ook diens PEPPOL-ID afleiden.",
      },
      { type: "h2", text: "Opzoeken: kan iemand ontvangen, en wat?" },
      {
        type: "p",
        text: "Achter het netwerk zit een opzoeksysteem, bekend als SML en SMP. Met een PEPPOL-ID vraag je daar op of iemand geregistreerd is, bij welk access point, en welke documenttypen en formaten hij kan ontvangen. Dat laatste is belangrijk: niet elke ontvanger accepteert elk formaat. Daarnaast is er de openbare PEPPOL Directory, een zoekbare lijst van deelnemers waar je zelf in kunt kijken.",
      },
      {
        type: "p",
        text: "xChange gebruikt diezelfde opzoeking. Het zoekt je klant op btw- of ondernemingsnummer op, laat zien of hij e-facturen kan ontvangen en kiest een formaat dat hij accepteert.",
      },
      { type: "h2", text: "BIS Billing 3.0 in het kort" },
      {
        type: "p",
        text: "PEPPOL BIS Billing 3.0 is de specificatie voor facturen en creditnota’s op het PEPPOL-netwerk. Het beschrijft welke gegevens een factuur bevat en hoe die in UBL, een XML-formaat, worden vastgelegd. BIS Billing 3.0 voldoet aan EN 16931, de Europese norm voor elektronische facturen. Bij de specificatie horen officiële validatieregels, zodat een ontvanger vooraf kan nagaan of een factuur klopt. In België is het de standaard voor e-facturen tussen bedrijven, en in veel andere landen wordt het breed geaccepteerd.",
      },
    ],
  },

  "bis-3-or-nlcius": {
    title: "BIS 3.0 of NLCIUS: welk formaat heeft je Nederlandse klant nodig?",
    description:
      "Factureer je aan een Nederlandse klant, dan kom je twee formaten tegen: PEPPOL BIS Billing 3.0 en NLCIUS. Wat is het verschil en hoe weet je welk formaat je klant verwacht?",
    readingMinutes: 3,
    blocks: [
      {
        type: "p",
        text: "Wie e-facturen naar Nederland stuurt, ziet al snel twee namen opduiken: PEPPOL BIS Billing 3.0 en NLCIUS, ook wel SI-UBL 2.0 genoemd. Ze lijken op elkaar en worden allebei geaccepteerd, maar niet door iedereen en niet altijd allebei. In dit artikel lees je waar ze vandaan komen, waarin ze verschillen en hoe je bepaalt welk formaat je klant nodig heeft.",
      },
      { type: "h2", text: "Eén Europese basis: EN 16931" },
      {
        type: "p",
        text: "Beide formaten zijn gebouwd op EN 16931, de Europese norm voor elektronische facturen. Die norm legt vast welke gegevens een factuur minimaal moet bevatten en wat ze betekenen: wie de leverancier is, wie de koper is, welke regels er op de factuur staan, hoe de btw is berekend. Landen en netwerken mogen daar eigen afspraken bovenop zetten, zolang ze binnen de norm blijven. Zo’n uitwerking heet een CIUS.",
      },
      { type: "h2", text: "Twee uitwerkingen van dezelfde norm" },
      {
        type: "p",
        text: "PEPPOL BIS Billing 3.0 is de uitwerking van het PEPPOL-netwerk. Ze wordt in heel Europa en daarbuiten gebruikt en is in België de standaard voor e-facturen tussen bedrijven.",
      },
      {
        type: "p",
        text: "NLCIUS is de Nederlandse nationale uitwerking. Ze voegt aanvullende, en op sommige punten strengere, Nederlandse regels toe aan de Europese norm. In de praktijk wordt NLCIUS vastgelegd als SI-UBL 2.0. Het formaat wordt vooral gebruikt door Nederlandse overheidsorganisaties.",
      },
      {
        type: "p",
        text: "Beide formaten zijn UBL, een XML-taal voor zakelijke documenten. Een factuur in het ene formaat lijkt daarom sterk op dezelfde factuur in het andere. Toch zijn het geen uitwisselbare bestanden: elk formaat heeft zijn eigen validatieregels, en een ontvanger controleert tegen de regels van het formaat dat hij verwacht.",
      },
      { type: "h2", text: "Hoe bepaal je welk formaat je klant nodig heeft?" },
      {
        type: "p",
        text: "Gok niet, maar ga na wat je klant accepteert. Dat kan op twee manieren:",
      },
      {
        type: "ul",
        items: [
          "Vraag het je klant. Grotere organisaties en overheden hebben vaak een pagina of instructie voor leveranciers waarin staat hoe ze facturen willen ontvangen.",
          "Kijk naar de PEPPOL-registratie van je klant. Daarin staat welke documenttypen en formaten hij kan ontvangen. Je access point of boekhoudsoftware kan dat voor je opzoeken.",
        ],
      },
      {
        type: "p",
        text: "Factureer je aan een Nederlandse overheidsorganisatie, zoals de rijksoverheid of een andere publieke instelling, dan wordt een e-factuur verwacht, meestal via PEPPOL. Overheden accepteren NLCIUS en in de regel ook PEPPOL BIS Billing 3.0. Overheidsorganisaties zijn op PEPPOL vaak geregistreerd met hun OIN, schemacode 0190.",
      },
      {
        type: "p",
        text: "Voor zakelijke klanten in Nederland geldt (nog) geen algemene verplichting om e-facturen te ontvangen. Veel bedrijven doen het wel, vaak geregistreerd met hun KvK-nummer onder schemacode 0106. Op PEPPOL wordt BIS Billing 3.0 breed geaccepteerd, dus dat is voor private klanten meestal een veilige keuze. Kijk wel even of je klant iets anders vraagt.",
      },
      { type: "h2", text: "Hoe xChange hiermee omgaat" },
      {
        type: "p",
        text: "xChange kan beide formaten maken: PEPPOL BIS Billing 3.0 en NLCIUS / SI-UBL 2.0. Elk bestand wordt gecontroleerd tegen de volledige officiële validatieregels van dat formaat, dezelfde regels die de netwerken gebruiken. Een bestand met een fout wordt niet uitgegeven. Als je de klant op btw- of KvK-nummer opzoekt, leest xChange uit de PEPPOL-registratie welke formaten de klant accepteert en kiest het een formaat dat past.",
      },
      {
        type: "note",
        text: "Krijg je een factuur teruggestuurd met een melding over validatie, kijk dan eerst of je het formaat hebt gebruikt dat de klant verwacht. Een factuur die correct is als BIS Billing 3.0, kan toch worden afgewezen door een ontvanger die NLCIUS eist, en andersom.",
      },
    ],
  },

  "find-peppol-address": {
    title: "Zo vind je het PEPPOL-adres van je klant",
    description:
      "Om een e-factuur via PEPPOL te sturen, heb je het PEPPOL-adres van je klant nodig. Hoe is zo’n adres opgebouwd, waar controleer je het en wat doe je als je klant niet geregistreerd is?",
    readingMinutes: 3,
    blocks: [
      {
        type: "p",
        text: "Een e-factuur via PEPPOL heeft een adres nodig, net als een brief of een e-mail. Dat adres is het PEPPOL-ID van je klant. Zonder dat ID weet het netwerk niet waar de factuur naartoe moet. Gelukkig is het meestal niet moeilijk te vinden, als je weet waar je moet kijken en waarnaar je moet vragen.",
      },
      { type: "h2", text: "Waarom je het adres nodig hebt" },
      {
        type: "p",
        text: "Op PEPPOL verstuur je een factuur niet naar een e-mailadres, maar naar een geregistreerde deelnemer. Je access point zoekt aan de hand van het PEPPOL-ID op bij welk access point je klant is aangesloten en welke formaten hij kan ontvangen. Klopt het ID niet, dan komt de factuur niet aan, of bij de verkeerde partij.",
      },
      { type: "h2", text: "Hoe een PEPPOL-adres is opgebouwd" },
      {
        type: "p",
        text: "Een PEPPOL-ID bestaat uit een schemacode en een nummer, gescheiden door een dubbele punt. De schemacode zegt wat voor nummer het is. De meest voorkomende voor Belgische en Nederlandse klanten:",
      },
      {
        type: "ul",
        items: [
          "Belgische ondernemingen: schemacode 0208 met het ondernemingsnummer (KBO-nummer). Heeft je klant btw-nummer BE0123456789, dan is het ondernemingsnummer de tien cijfers 0123456789 en het PEPPOL-ID 0208:0123456789.",
          "Nederlandse bedrijven: vaak schemacode 0106 met het KvK-nummer.",
          "Nederlandse overheidsorganisaties: meestal schemacode 0190 met hun OIN.",
        ],
      },
      {
        type: "p",
        text: "Het makkelijkst is het om je klant gewoon te vragen onder welk PEPPOL-ID hij facturen ontvangt. Sommige bedrijven vermelden dat al op hun eigen facturen, in hun e-mailhandtekening of op een pagina voor leveranciers. Vraag in elk geval naar het btw-nummer en het ondernemingsnummer of KvK-nummer, als je die nog niet hebt.",
      },
      { type: "h2", text: "Controleren voordat je verstuurt" },
      {
        type: "p",
        text: "Ook als je een ID hebt gekregen, loont het om te controleren of het werkt. Dat kan op een paar manieren:",
      },
      {
        type: "ul",
        items: [
          "Zoek het nummer op in de openbare PEPPOL Directory, een zoekbare lijst van deelnemers op het netwerk.",
          "Laat je access point of boekhoudsoftware het opzoeken. Die kan ook zien welke documenttypen en formaten je klant ontvangt.",
        ],
      },
      {
        type: "p",
        text: "Zoek daarbij op nummer, niet op naam. Bedrijfsnamen zijn niet uniek, worden anders gespeld of afgekort, en een handelsnaam is vaak niet de naam waaronder een bedrijf geregistreerd is. Een btw-nummer of ondernemingsnummer hoort bij precies één onderneming. Om die reden zoekt xChange klanten alleen op btw- of bedrijfsnummer op en nooit op naam. Het laat zien of de klant e-facturen kan ontvangen en vult het PEPPOL-adres voor je in.",
      },
      { type: "h2", text: "Als je klant niet geregistreerd is" },
      {
        type: "p",
        text: "Vind je je klant niet op PEPPOL, vraag dan eerst na of hij onder een ander nummer of een andere schemacode geregistreerd is. Is hij echt niet geregistreerd, spreek dan samen af hoe je de factuur aflevert. Voor Belgische klanten die onder de verplichting vallen, is dat ook een signaal om het onderwerp aan te kaarten: zij moeten e-facturen kunnen ontvangen.",
      },
      {
        type: "note",
        text: "Heb je het PEPPOL-adres eenmaal gevonden en gecontroleerd, leg het dan vast in je klantgegevens. Dan hoef je het de volgende keer niet opnieuw uit te zoeken. xChange onthoudt klanten en vult hun gegevens bij de volgende factuur zelf in.",
      },
    ],
  },
};
