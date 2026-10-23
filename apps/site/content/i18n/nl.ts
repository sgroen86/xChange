import type { Dictionary } from "./types.ts";

export const nl: Dictionary = {
  meta: {
    siteName: "xChange",
    description:
      "Zet je eigen PDF-verkoopfacturen om in gevalideerde e-facturen: PEPPOL BIS 3.0 en NLCIUS, gecontroleerd tegen de officiële validatieregels.",
  },
  nav: {
    product: "Product",
    pricing: "Prijzen",
    resources: "Kennisbank",
    faq: "Veelgestelde vragen",
    login: "Inloggen",
    signup: "Aanmelden",
    openMenu: "Menu openen",
    closeMenu: "Menu sluiten",
    mainNav: "Hoofdnavigatie",
    languageLabel: "Taal",
    skipToContent: "Naar de inhoud",
    homeLabel: "xChange — naar de startpagina",
  },
  footer: {
    tagline: "Van PDF-factuur naar gevalideerde e-factuur. Gemaakt voor ondernemers in België en Nederland.",
    productHeading: "Product",
    companyHeading: "Contact",
    legalHeading: "Juridisch",
    languagesHeading: "Taal",
    contact: "Contact",
    privacy: "Privacybeleid",
    terms: "Algemene voorwaarden",
    trust: "Vertrouwen & beveiliging",
    hostedInEu: "Gehost in de EU",
    rights: "Alle rechten voorbehouden.",
  },
  common: {
    comingSoon: "Binnenkort",
    demo: "Demo",
    learnMore: "Meer lezen",
    externalLinkNote: "(opent de app)",
  },
  home: {
    meta: {
      title: "PDF-facturen omzetten naar gevalideerde e-facturen",
      description:
        "Upload je PDF-verkoopfactuur, controleer de gegevens en download een e-factuur in PEPPOL BIS 3.0 of NLCIUS die de officiële validatieregels doorstaat.",
    },
    hero: {
      eyebrow: "E-facturatie voor België en Nederland",
      title: "Van PDF-factuur naar gevalideerde e-factuur",
      lead: "Je maakt je facturen al. xChange leest je PDF, laat je alles controleren en levert een PEPPOL BIS 3.0- of NLCIUS-bestand dat de officiële validatieregels doorstaat — zonder nieuw boekhoudpakket.",
      primaryCta: "Gratis proberen",
      secondaryCta: "Zo werkt het",
      note: "{n} gratis conversie, geen creditcard nodig. Downloaden met een abonnement of tegoed.",
    },
    mock: {
      label: "Voorbeeld: een PDF-factuur die xChange omzet naar een gevalideerde e-factuur",
      fileName: "factuur-2026-0142.pdf",
      reading: "Gelezen en gecontroleerd",
      customer: "Klant",
      customerValue: "Janssens Bouw BV",
      vatNumber: "Btw-nummer",
      peppolId: "PEPPOL-ID",
      total: "Totaal incl. btw",
      totalValue: "€ 1.210,00",
      format: "PEPPOL BIS Billing 3.0",
      valid: "Geldig — 0 fouten",
      receives: "Ontvangt e-facturen via PEPPOL",
      download: "XML downloaden",
    },
    problem: {
      eyebrow: "Het probleem",
      title: "Een PDF is geen e-factuur meer",
      lead: "Steeds meer klanten willen je factuur als gestructureerd bestand via PEPPOL ontvangen. Je huidige werkwijze opnieuw inrichten kost tijd die je liever aan je klanten besteedt.",
      points: [
        {
          title: "In België verplicht",
          text: "Sinds 1 januari 2026 moeten Belgische ondernemingen elkaar e-facturen sturen. Een PDF per e-mail volstaat daar niet meer.",
        },
        {
          title: "Overheid vraagt erom",
          text: "Nederlandse overheden verwachten e-facturen in NLCIUS of PEPPOL BIS 3.0. Eén ontbrekend veld en de factuur wordt geweigerd.",
        },
        {
          title: "Overstappen is veel werk",
          text: "Een nieuw facturatiepakket betekent nieuwe sjablonen, nieuwe gewoontes en je historiek verhuizen. Vaak wil je gewoon je huidige PDF blijven maken.",
        },
      ],
    },
    steps: {
      eyebrow: "Zo werkt het",
      title: "In vier stappen een geldige e-factuur",
      lead: "Je blijft facturen maken zoals je gewend bent. xChange doet de omzetting en de controle.",
      items: [
        {
          title: "Upload of mail je PDF",
          text: "Sleep één of meerdere PDF's in de app, of mail ze naar je persoonlijke xChange-adres. Gemailde PDF's wachten op jouw goedkeuring voordat ze gelezen worden.",
        },
        {
          title: "AI leest de factuur",
          text: "AI haalt leverancier, klant, regels, btw en totalen uit je PDF.",
        },
        {
          title: "Jij controleert",
          text: "Op het controlescherm zie je elk veld naast de bron, met live validatie. Je corrigeert waar nodig; de bedragen worden exact herberekend.",
        },
        {
          title: "Download de e-factuur",
          text: "Kies het formaat dat je klant aanvaardt. Het bestand wordt gecontroleerd tegen de volledige officiële validatieregels voordat je het kunt downloaden.",
        },
      ],
    },
    features: {
      eyebrow: "Product",
      title: "Alles wat je nodig hebt om e-facturen te leveren",
      lead: "Gebouwd rond één belofte: een bestand met een fout krijg je nooit mee.",
      items: [
        {
          icon: "shieldCheck",
          title: "Officieel gevalideerd",
          text: "Elk bestand doorloopt de volledige officiële validatieregels — dezelfde die het netwerk gebruikt. Zit er een fout in, dan kun je het niet downloaden.",
        },
        {
          icon: "search",
          title: "PEPPOL-opzoeking",
          text: "Zoek je klant op via btw- of ondernemingsnummer, nooit op naam. Je ziet meteen of hij e-facturen ontvangt, en zijn PEPPOL-adres wordt ingevuld.",
        },
        {
          icon: "fileCheck",
          title: "Het juiste formaat",
          text: "xChange kiest een formaat dat je klant op PEPPOL aanvaardt: PEPPOL BIS 3.0 of NLCIUS.",
        },
        {
          icon: "calculator",
          title: "Exacte totalen",
          text: "Bedragen, btw en de XML worden deterministisch berekend. Geen AI in de rekensom, dus geen afrondingsverrassingen.",
        },
        {
          icon: "users",
          title: "Onthoudt je klanten",
          text: "Klantgegevens worden bewaard als relatie en bij de volgende factuur automatisch ingevuld.",
        },
        {
          icon: "refresh",
          title: "Leert van je correcties",
          text: "Corrigeer je je eigen bedrijfsgegevens of de btw-behandeling van een klant, dan onthoudt xChange dat. In de instellingen zie je wat geleerd is en kun je het vergeten.",
        },
        {
          icon: "inbox",
          title: "Eigen inbox",
          text: "Mail PDF's naar je persoonlijke adres. Niets wordt gelezen voordat jij het goedkeurt.",
        },
        {
          icon: "lock",
          title: "Data in de EU",
          text: "Je gegevens staan veilig opgeslagen in de EU. Ontvangen e-mails worden na 30 dagen verwijderd.",
        },
      ],
    },
    formats: {
      eyebrow: "Formaten en landen",
      title: "Klaar voor België en Nederland",
      lead: "De formaten die je klanten en overheden vragen, elk gecontroleerd tegen de eigen officiële regels.",
      items: [
        {
          country: "België",
          format: "PEPPOL BIS Billing 3.0",
          text: "Het standaardformaat voor de verplichte B2B-e-facturatie. Klanten zijn meestal bereikbaar via hun ondernemingsnummer.",
          demo: false,
        },
        {
          country: "Nederland",
          format: "PEPPOL BIS 3.0 · NLCIUS / SI-UBL 2.0",
          text: "BIS 3.0 voor zakelijke klanten op PEPPOL, NLCIUS voor de Nederlandse overheid.",
          demo: false,
        },
        {
          country: "Verenigd Koninkrijk",
          format: "PEPPOL BIS Billing 3.0",
          text: "Voor klanten in het VK die via PEPPOL ontvangen.",
          demo: false,
        },
        {
          country: "Singapore",
          format: "PINT-SG",
          text: "Beschikbaar als demonstratie van het internationale PINT-formaat.",
          demo: true,
        },
      ],
      validationTitle: "Geen fout bestand de deur uit",
      validationText:
        "Elk formaat wordt gecontroleerd met de volledige set officiële validatieregels. Een bestand met een fout wordt nooit aangeboden om te downloaden — je ziet welk veld je moet aanpassen.",
    },
    roadmap: {
      title: "Binnenkort",
      items: [
        {
          icon: "send",
          title: "Rechtstreeks versturen via PEPPOL",
          text: "Vandaag download je het gevalideerde bestand en verstuur je het via je eigen kanaal. Versturen vanuit xChange via een PEPPOL-accesspoint komt eraan.",
        },
        {
          icon: "mail",
          title: "Inkomende facturen ontvangen",
          text: "Ook het ontvangen van e-facturen van je leveranciers staat op de planning.",
        },
      ],
    },
    cta: {
      title: "Probeer het met je eigen facturen",
      text: "{n} gratis conversie, geen creditcard nodig. Je ziet meteen of je klant e-facturen kan ontvangen. De e-factuur downloaden doe je met een abonnement of tegoed.",
      primary: "Gratis proberen",
      secondary: "Bekijk de prijzen",
    },
  },
  pricing: {
    meta: {
      title: "Prijzen",
      description:
        "Abonnementen met inbegrepen conversies, of betaal per conversie met tegoed. Probeer {n} conversie gratis, zonder creditcard.",
    },
    eyebrow: "Prijzen",
    title: "Eenvoudige prijzen, per factuur",
    lead: "Kies een maandabonnement of koop tegoed. Je betaalt per PDF die wordt omgezet. Maandelijks opzegbaar.",
    perMonth: "per maand",
    conversionsPerMonth: "{n} facturen per maand",
    mostPopular: "Meest gekozen",
    choosePlan: "Kies {plan}",
    plans: {
      starter: { blurb: "Voor zelfstandigen en kleine ondernemingen met een handvol facturen per week." },
      pro: { blurb: "Voor groeiende bedrijven die elke dag factureren." },
      business: { blurb: "Voor bedrijven en kantoren met een groot factuurvolume." },
    },
    trial: {
      title: "Gratis proberen",
      text: "Begin met {n} gratis conversie en controleer het resultaat. Geen creditcard, geen verplichtingen. De e-factuur downloaden kan met een abonnement of tegoed.",
      cta: "Gratis proberen",
    },
    payg: {
      title: "Betalen per gebruik",
      text: "Factureer je onregelmatig? Koop tegoed en betaal alleen wat je gebruikt.",
      packLabel: "{n} conversies",
      perConversion: "{price} per conversie",
      noSubscription: "Geen abonnement",
      cta: "Tegoed kopen",
    },
    included: {
      title: "Inbegrepen in elk abonnement",
      lead: "Geen functies achter een duurder abonnement. Je kiest alleen het volume.",
      items: [
        "AI leest je PDF-facturen",
        "Controlescherm met live validatie",
        "Meerdere PDF's tegelijk uploaden",
        "Persoonlijk e-mailadres voor je PDF's, met goedkeuring vooraf",
        "Export naar PEPPOL BIS 3.0 en NLCIUS / SI-UBL 2.0",
        "Controle tegen de volledige officiële validatieregels",
        "PEPPOL-opzoeking op btw- of ondernemingsnummer",
        "Klanten onthouden en automatisch invullen",
        "Leren van je correcties",
        "Data gehost in de EU",
      ],
      comingSoon: ["Rechtstreeks versturen via een PEPPOL-accesspoint", "Inkomende facturen ontvangen"],
    },
    notes: [
      "Alle prijzen zijn exclusief btw.",
      "Een conversie is één PDF die wordt ingelezen tot een factuur.",
      "Abonnementen zijn maandelijks opzegbaar. Je betaalt veilig online.",
    ],
    questions: {
      title: "Vragen over de prijzen",
      items: [
        {
          q: "Wat telt als een conversie?",
          a: "Eén PDF die door xChange wordt gelezen en omgezet in een factuur. Corrigeren en opnieuw exporteren in een ander formaat telt niet opnieuw.",
        },
        {
          q: "Heb ik een creditcard nodig om te proberen?",
          a: "Nee. Je krijgt {n} gratis conversie zonder betaalgegevens en kunt het resultaat volledig controleren. Om de e-factuur te downloaden kies je een abonnement of koop je tegoed.",
        },
        {
          q: "Kan ik van abonnement wisselen of opzeggen?",
          a: "Ja. Abonnementen lopen per maand en je kunt op elk moment opzeggen. De opzegging gaat in aan het einde van de lopende maand.",
        },
        {
          q: "Zijn de prijzen inclusief btw?",
          a: "Nee, alle prijzen zijn exclusief btw. De btw wordt op je factuur vermeld.",
        },
      ],
    },
  },
  resources: {
    meta: {
      title: "Kennisbank",
      description:
        "Praktische uitleg over e-facturatie in België en Nederland: de Belgische verplichting, PEPPOL, BIS 3.0 en NLCIUS.",
    },
    eyebrow: "Kennisbank",
    title: "E-facturatie, helder uitgelegd",
    lead: "Wat de Belgische verplichting betekent, hoe PEPPOL werkt en welk formaat je klant nodig heeft.",
    readArticle: "Lees het artikel",
    readingTime: "{n} min. lezen",
    backToResources: "Terug naar de kennisbank",
    breadcrumb: "Kruimelpad",
    cta: {
      title: "Zelf aan de slag?",
      text: "Zet je eerste PDF-factuur gratis om en zie meteen of je klant e-facturen kan ontvangen.",
      button: "Gratis proberen",
    },
  },
  faq: {
    meta: {
      title: "Veelgestelde vragen",
      description:
        "Antwoorden over formaten, nauwkeurigheid, privacy, de e-mailinbox, PEPPOL-opzoeking, prijzen en opzeggen.",
    },
    eyebrow: "Veelgestelde vragen",
    title: "Vragen en antwoorden",
    lead: "Staat je vraag er niet bij? Neem gerust contact met ons op.",
    items: [
      {
        q: "Welke formaten kan xChange maken?",
        a: "PEPPOL BIS Billing 3.0 (voor België, Nederland en het Verenigd Koninkrijk) en NLCIUS / SI-UBL 2.0 voor de Nederlandse overheid. PINT-SG voor Singapore is beschikbaar als demo.",
      },
      {
        q: "Hoe nauwkeurig leest de AI mijn facturen?",
        a: "De AI leest de meeste facturen goed, maar niet foutloos. Daarom controleer je elke factuur op het controlescherm voordat je exporteert: je ziet elk veld, met live validatie, en kunt alles aanpassen. Bedragen en totalen worden niet door de AI berekend maar exact door xChange zelf.",
      },
      {
        q: "Hoe weet ik dat het bestand klopt?",
        a: "Elk bestand wordt gecontroleerd tegen de volledige officiële validatieregels van het gekozen formaat — dezelfde regels die het netwerk gebruikt. Een bestand met een fout kun je niet downloaden; je ziet welk veld aangepast moet worden. Of de inhoud van je factuur zakelijk en fiscaal klopt, blijft jouw verantwoordelijkheid.",
      },
      {
        q: "Waar worden mijn gegevens bewaard?",
        a: "In de EU. Voor het lezen van PDF's werken we met zorgvuldig gekozen verwerkers, gebonden aan verwerkersovereenkomsten. Worden gegevens buiten de EU verwerkt, dan gebeurt dat onder de standaardcontractbepalingen van de EU. Meer lees je in onze privacyverklaring.",
      },
      {
        q: "Hoe lang bewaren jullie mijn gegevens?",
        a: "Ontvangen e-mails worden na 30 dagen verwijderd. Wat het AI-model uit een upload heeft gelezen, bewaren we 30 dagen om van je correcties te leren. Je facturen bewaren we zolang je account bestaat of tot je ze verwijdert.",
      },
      {
        q: "Hoe werkt de e-mailinbox?",
        a: "Je krijgt een persoonlijk e-mailadres. PDF's die je daarheen stuurt (of laat doorsturen) verschijnen in de app, maar worden pas gelezen nadat jij ze hebt goedgekeurd. Zo bepaal je zelf wat verwerkt wordt.",
      },
      {
        q: "Hoe zoekt xChange mijn klant op PEPPOL op?",
        a: "Op basis van het btw- of ondernemingsnummer van je klant, nooit op naam. Je ziet of de klant e-facturen kan ontvangen, zijn PEPPOL-adres wordt ingevuld en xChange kiest een formaat dat hij aanvaardt.",
      },
      {
        q: "Kan ik facturen rechtstreeks via PEPPOL versturen?",
        a: "Nog niet. Vandaag download je het gevalideerde bestand en verstuur je het via je eigen boekhoudpakket of PEPPOL-dienstverlener, of zoals afgesproken met je klant. Rechtstreeks versturen vanuit xChange komt binnenkort, net als het ontvangen van inkomende facturen.",
      },
      {
        q: "Wordt xChange slimmer naarmate ik het gebruik?",
        a: "Ja. Klanten worden onthouden en bij de volgende factuur ingevuld, en correcties aan je eigen bedrijfsgegevens of de btw-behandeling per klant worden toegepast op volgende facturen. In de instellingen zie je wat geleerd is en kun je het laten vergeten.",
      },
      {
        q: "Kan ik gratis proberen?",
        a: "Ja. Je krijgt {n} gratis conversie, zonder creditcard, en kunt alles controleren. De e-factuur downloaden (en straks versturen) kan alleen met een lopend abonnement of gekocht tegoed.",
      },
      {
        q: "Kan ik op elk moment opzeggen?",
        a: "Ja. Abonnementen lopen per maand en zijn op elk moment opzegbaar. De opzegging gaat in aan het einde van de lopende maand.",
      },
      {
        q: "In welke talen is xChange beschikbaar?",
        a: "Deze website is beschikbaar in het Nederlands, Engels en Frans. De app zelf is op dit moment in het Nederlands.",
      },
    ],
    more: {
      title: "Nog een vraag?",
      text: "We helpen je graag verder.",
      cta: "Neem contact op",
    },
  },
  contact: {
    meta: {
      title: "Contact",
      description: "Neem contact op met xChange voor vragen over het product, prijzen of ondersteuning.",
    },
    eyebrow: "Contact",
    title: "Neem contact op",
    lead: "Vragen over xChange, de prijzen of e-facturatie? Stuur ons een e-mail.",
    generalTitle: "Algemene vragen",
    generalText: "Over het product, de prijzen, privacy of samenwerking.",
    supportTitle: "Ondersteuning",
    supportText: "Hulp bij het gebruik van de app.",
    companyTitle: "Bedrijfsgegevens",
    registrationLabel: "Ondernemingsnummer",
    vatLabel: "Btw-nummer",
  },
  signup: {
    meta: {
      title: "Aanmelden",
      description: "Maak je xChange-account aan en begin gratis, of kies meteen een abonnement.",
    },
    eyebrow: "Aanmelden",
    title: "Maak je account aan",
    lead: "Je account maak je aan in de xChange-app. Je bevestigt je e-mailadres, stelt tweestapsverificatie in en kunt meteen aan de slag.",
    trialNote: "Je begint met {n} gratis conversie, zonder creditcard. De e-factuur downloaden kan met een abonnement of tegoed.",
    signupCta: "Account aanmaken",
    pricingText: "Liever meteen een abonnement? Kies het op de prijspagina; na het bevestigen ga je direct door naar de betaling.",
    pricingCta: "Bekijk de prijzen",
    loginText: "Heb je al een account?",
    loginCta: "Inloggen",
    appLanguageNote: "De app is beschikbaar in het Nederlands en het Engels.",
  },
  login: {
    meta: {
      title: "Inloggen",
      description: "Log in op de xChange-app.",
    },
    title: "Inloggen bij xChange",
    lead: "Je logt in op de app, waar je facturen uploadt, controleert en exporteert.",
    cta: "Naar de app",
    noAccount: "Nog geen account?",
    signupCta: "Aanmelden",
  },
  legal: {
    draftNote: "Concept — wordt juridisch nagekeken",
    lastUpdated: "Laatst bijgewerkt:",
    contents: "Inhoud",
  },
};
