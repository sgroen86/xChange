import type { Dictionary } from "./types.ts";

export const fr: Dictionary = {
  meta: {
    siteName: "xChange",
    description:
      "Transformez vos propres factures de vente PDF en factures électroniques validées : PEPPOL BIS 3.0 et NLCIUS, contrôlées selon les règles de validation officielles.",
  },
  nav: {
    product: "Produit",
    pricing: "Tarifs",
    resources: "Ressources",
    faq: "FAQ",
    login: "Se connecter",
    signup: "S’inscrire",
    openMenu: "Ouvrir le menu",
    closeMenu: "Fermer le menu",
    mainNav: "Navigation principale",
    languageLabel: "Langue",
    skipToContent: "Aller au contenu",
    homeLabel: "xChange — accueil",
  },
  footer: {
    tagline:
      "De la facture PDF à la facture électronique validée. Conçu pour les entreprises en Belgique et aux Pays-Bas.",
    productHeading: "Produit",
    companyHeading: "Contact",
    legalHeading: "Mentions légales",
    languagesHeading: "Langue",
    contact: "Contact",
    privacy: "Politique de confidentialité",
    terms: "Conditions générales",
    trust: "Confiance et sécurité",
    hostedInEu: "Hébergé dans l’UE (Francfort)",
    rights: "Tous droits réservés.",
  },
  common: {
    comingSoon: "Bientôt",
    demo: "Démo",
    learnMore: "En savoir plus",
    externalLinkNote: "(ouvre l’application)",
  },
  home: {
    meta: {
      title: "Transformez vos factures PDF en factures électroniques validées",
      description:
        "Importez votre facture de vente PDF, vérifiez les données et téléchargez une facture électronique PEPPOL BIS 3.0 ou NLCIUS conforme aux règles de validation officielles.",
    },
    hero: {
      eyebrow: "La facturation électronique pour la Belgique et les Pays-Bas",
      title: "De la facture PDF à la facture électronique validée",
      lead: "Vous créez déjà vos factures. xChange lit votre PDF, vous laisse tout vérifier et produit un fichier PEPPOL BIS 3.0 ou NLCIUS conforme aux règles de validation officielles — sans changer de logiciel comptable.",
      primaryCta: "Essayer gratuitement",
      secondaryCta: "Comment ça marche",
      note: "{n} conversions gratuites, sans carte bancaire.",
    },
    mock: {
      label: "Exemple : une facture PDF que xChange transforme en facture électronique validée",
      fileName: "facture-2026-0142.pdf",
      reading: "Lue et contrôlée",
      customer: "Client",
      customerValue: "Janssens Bouw SRL",
      vatNumber: "Numéro de TVA",
      peppolId: "Identifiant PEPPOL",
      total: "Total TVAC",
      totalValue: "1 210,00 €",
      format: "PEPPOL BIS Billing 3.0",
      valid: "Valide — 0 erreur",
      receives: "Reçoit des factures électroniques via PEPPOL",
      download: "Télécharger le XML",
    },
    problem: {
      eyebrow: "Le problème",
      title: "Un PDF n’est plus une facture électronique",
      lead: "De plus en plus de clients veulent recevoir votre facture sous forme de fichier structuré via PEPPOL. Revoir toute votre façon de facturer prend du temps que vous préférez consacrer à vos clients.",
      points: [
        {
          title: "Obligatoire en Belgique",
          text: "Depuis le 1er janvier 2026, les entreprises belges doivent s’échanger des factures électroniques. Un PDF envoyé par e-mail ne suffit plus.",
        },
        {
          title: "Exigé par les pouvoirs publics",
          text: "Les administrations néerlandaises attendent des factures électroniques en NLCIUS ou en PEPPOL BIS 3.0. Il suffit d’un champ manquant pour que la facture soit refusée.",
        },
        {
          title: "Changer d’outil, c’est lourd",
          text: "Un nouveau logiciel de facturation, ce sont de nouveaux modèles, de nouvelles habitudes et un historique à migrer. Souvent, vous voulez simplement garder le PDF que vous faites aujourd’hui.",
        },
      ],
    },
    steps: {
      eyebrow: "Comment ça marche",
      title: "Une facture électronique valide en quatre étapes",
      lead: "Continuez à facturer comme d’habitude. xChange se charge de la conversion et des contrôles.",
      items: [
        {
          title: "Importez ou envoyez votre PDF",
          text: "Déposez un ou plusieurs PDF dans l’application, ou envoyez-les à votre adresse e-mail xChange personnelle. Les PDF reçus par e-mail attendent votre accord avant d’être lus.",
        },
        {
          title: "L’IA lit la facture",
          text: "Claude, le modèle d’IA d’Anthropic, extrait de votre PDF le fournisseur, le client, les lignes, la TVA et les totaux.",
        },
        {
          title: "Vous vérifiez",
          text: "L’écran de vérification affiche chaque champ avec une validation en direct. Corrigez ce qui doit l’être ; les montants sont recalculés au centime près.",
        },
        {
          title: "Téléchargez la facture électronique",
          text: "Choisissez le format que votre client accepte. Le fichier est contrôlé selon l’ensemble des règles de validation officielles avant que vous puissiez le télécharger.",
        },
      ],
    },
    features: {
      eyebrow: "Produit",
      title: "Tout ce qu’il faut pour livrer des factures électroniques",
      lead: "Construit autour d’une promesse : vous ne recevez jamais un fichier qui contient une erreur.",
      items: [
        {
          icon: "shieldCheck",
          title: "Validation officielle",
          text: "Chaque fichier passe par l’ensemble des règles de validation officielles — celles qu’utilise le réseau. S’il contient une erreur, il ne peut pas être téléchargé.",
        },
        {
          icon: "search",
          title: "Recherche PEPPOL",
          text: "Retrouvez votre client par son numéro de TVA ou d’entreprise, jamais par son nom. Vous voyez tout de suite s’il reçoit des factures électroniques, et son adresse PEPPOL est complétée.",
        },
        {
          icon: "fileCheck",
          title: "Le bon format",
          text: "xChange choisit un format que votre client accepte sur PEPPOL : PEPPOL BIS 3.0 ou NLCIUS.",
        },
        {
          icon: "calculator",
          title: "Des totaux exacts",
          text: "Les montants, la TVA et le XML sont calculés de façon déterministe. Pas d’IA dans les calculs, donc pas de surprise d’arrondi.",
        },
        {
          icon: "users",
          title: "Mémorise vos clients",
          text: "Les données de vos clients sont enregistrées comme relations et complétées automatiquement sur la facture suivante.",
        },
        {
          icon: "refresh",
          title: "Apprend de vos corrections",
          text: "Corrigez les données de votre entreprise ou le traitement TVA d’un client, et xChange s’en souvient. Les paramètres montrent ce qui a été appris, et vous pouvez le faire oublier.",
        },
        {
          icon: "inbox",
          title: "Votre propre boîte de réception",
          text: "Envoyez vos PDF à votre adresse personnelle. Rien n’est lu avant que vous ne l’ayez approuvé.",
        },
        {
          icon: "lock",
          title: "Données dans l’UE",
          text: "Vos données sont stockées chez Amazon Web Services à Francfort. Les e-mails reçus sont supprimés après 30 jours.",
        },
      ],
    },
    formats: {
      eyebrow: "Formats et pays",
      title: "Prêt pour la Belgique et les Pays-Bas",
      lead: "Les formats que demandent vos clients et les pouvoirs publics, chacun contrôlé selon ses propres règles officielles.",
      items: [
        {
          country: "Belgique",
          format: "PEPPOL BIS Billing 3.0",
          text: "Le format standard de la facturation électronique B2B obligatoire. Les clients sont généralement joignables via leur numéro d’entreprise.",
          demo: false,
        },
        {
          country: "Pays-Bas",
          format: "PEPPOL BIS 3.0 · NLCIUS / SI-UBL 2.0",
          text: "BIS 3.0 pour les clients professionnels sur PEPPOL, NLCIUS pour l’administration néerlandaise.",
          demo: false,
        },
        {
          country: "Royaume-Uni",
          format: "PEPPOL BIS Billing 3.0",
          text: "Pour les clients britanniques qui reçoivent via PEPPOL.",
          demo: false,
        },
        {
          country: "Singapour",
          format: "PINT-SG",
          text: "Disponible à titre de démonstration du format international PINT.",
          demo: true,
        },
      ],
      validationTitle: "Aucun fichier erroné ne sort",
      validationText:
        "Chaque format est contrôlé avec l’ensemble de ses règles de validation officielles. Un fichier qui contient une erreur n’est jamais proposé au téléchargement — vous voyez quel champ corriger.",
    },
    roadmap: {
      title: "Bientôt",
      items: [
        {
          icon: "send",
          title: "Envoi direct via PEPPOL",
          text: "Aujourd’hui, vous téléchargez le fichier validé et l’envoyez par votre propre canal. L’envoi depuis xChange via un point d’accès PEPPOL est en préparation.",
        },
        {
          icon: "mail",
          title: "Réception des factures entrantes",
          text: "La réception des factures électroniques de vos fournisseurs est également prévue.",
        },
      ],
    },
    cta: {
      title: "Essayez avec vos propres factures",
      text: "{n} conversions gratuites, sans carte bancaire. Vous voyez immédiatement si votre client peut recevoir des factures électroniques.",
      primary: "Essayer gratuitement",
      secondary: "Voir les tarifs",
    },
  },
  pricing: {
    meta: {
      title: "Tarifs",
      description:
        "Des abonnements avec conversions incluses, ou un paiement à la conversion grâce aux crédits. Commencez avec {n} conversions gratuites, sans carte bancaire.",
    },
    eyebrow: "Tarifs",
    title: "Des tarifs simples, à la facture",
    lead: "Choisissez un abonnement mensuel ou achetez des crédits. Vous payez par PDF converti. Résiliable chaque mois.",
    perMonth: "par mois",
    conversionsPerMonth: "{n} factures par mois",
    mostPopular: "Le plus choisi",
    choosePlan: "Choisir {plan}",
    plans: {
      starter: { blurb: "Pour les indépendants et les petites entreprises qui envoient quelques factures par semaine." },
      pro: { blurb: "Pour les entreprises en croissance qui facturent tous les jours." },
      business: { blurb: "Pour les entreprises et les fiduciaires qui traitent un grand volume de factures." },
    },
    trial: {
      title: "Essai gratuit",
      text: "Commencez avec {n} conversions gratuites. Sans carte bancaire, sans engagement.",
      cta: "Essayer gratuitement",
    },
    payg: {
      title: "Paiement à l’usage",
      text: "Vous facturez de manière irrégulière ? Achetez des crédits et ne payez que ce que vous utilisez.",
      packLabel: "{n} conversions",
      perConversion: "{price} par conversion",
      noSubscription: "Sans abonnement",
      cta: "Acheter des crédits",
    },
    included: {
      title: "Inclus dans chaque abonnement",
      lead: "Aucune fonctionnalité réservée à un abonnement plus cher. Vous choisissez seulement le volume.",
      items: [
        "Lecture de vos factures PDF par l’IA",
        "Écran de vérification avec validation en direct",
        "Import de plusieurs PDF à la fois",
        "Adresse e-mail personnelle pour vos PDF, avec approbation préalable",
        "Export en PEPPOL BIS 3.0 et NLCIUS / SI-UBL 2.0",
        "Contrôle selon l’ensemble des règles de validation officielles",
        "Recherche PEPPOL par numéro de TVA ou d’entreprise",
        "Clients mémorisés et complétés automatiquement",
        "Apprentissage à partir de vos corrections",
        "Données hébergées dans l’UE (Francfort)",
      ],
      comingSoon: ["Envoi direct via un point d’accès PEPPOL", "Réception des factures entrantes"],
    },
    notes: [
      "Tous les prix s’entendent hors TVA.",
      "Une conversion correspond à un PDF lu et transformé en facture.",
      "Les abonnements sont mensuels et résiliables à tout moment. Le paiement est traité en toute sécurité par Stripe.",
    ],
    questions: {
      title: "Questions sur les tarifs",
      items: [
        {
          q: "Qu’est-ce qui compte comme une conversion ?",
          a: "Un PDF que xChange lit et transforme en facture. Le corriger, ou l’exporter à nouveau dans un autre format, ne compte pas une seconde fois.",
        },
        {
          q: "Faut-il une carte bancaire pour l’essai ?",
          a: "Non. Vous recevez {n} conversions gratuites sans données de paiement. Vous ne payez, via Stripe, qu’une fois un abonnement ou des crédits choisis.",
        },
        {
          q: "Puis-je changer d’abonnement ou résilier ?",
          a: "Oui. Les abonnements sont mensuels et résiliables à tout moment. La résiliation prend effet à la fin du mois en cours.",
        },
        {
          q: "Les prix incluent-ils la TVA ?",
          a: "Non, tous les prix s’entendent hors TVA. La TVA figure sur votre facture.",
        },
      ],
    },
  },
  resources: {
    meta: {
      title: "Ressources",
      description:
        "Des guides pratiques sur la facturation électronique en Belgique et aux Pays-Bas : l’obligation belge, PEPPOL, BIS 3.0 et NLCIUS.",
    },
    eyebrow: "Ressources",
    title: "La facturation électronique, expliquée clairement",
    lead: "Ce que l’obligation belge implique, comment fonctionne PEPPOL et quel format votre client attend.",
    readArticle: "Lire l’article",
    readingTime: "{n} min de lecture",
    backToResources: "Retour aux ressources",
    breadcrumb: "Fil d’Ariane",
    cta: {
      title: "Prêt à vous lancer ?",
      text: "Convertissez gratuitement vos premières factures PDF et voyez immédiatement si votre client peut recevoir des factures électroniques.",
      button: "Essayer gratuitement",
    },
  },
  faq: {
    meta: {
      title: "Questions fréquentes",
      description:
        "Réponses sur les formats, la fiabilité, la confidentialité, la boîte de réception, la recherche PEPPOL, les tarifs et la résiliation.",
    },
    eyebrow: "FAQ",
    title: "Questions et réponses",
    lead: "Vous ne trouvez pas votre question ? N’hésitez pas à nous contacter.",
    items: [
      {
        q: "Quels formats xChange peut-il produire ?",
        a: "PEPPOL BIS Billing 3.0 (pour la Belgique, les Pays-Bas et le Royaume-Uni) et NLCIUS / SI-UBL 2.0 pour l’administration néerlandaise. PINT-SG pour Singapour est disponible en démonstration.",
      },
      {
        q: "L’IA lit-elle mes factures correctement ?",
        a: "L’IA lit bien la plupart des factures, mais pas sans erreur. C’est pourquoi vous vérifiez chaque facture sur l’écran de vérification avant l’export : vous voyez chaque champ, avec une validation en direct, et pouvez tout modifier. Les montants et les totaux ne sont pas calculés par l’IA, mais au centime près par xChange lui-même.",
      },
      {
        q: "Comment savoir si le fichier est correct ?",
        a: "Chaque fichier est contrôlé selon l’ensemble des règles de validation officielles du format choisi — celles qu’utilise le réseau. Un fichier qui contient une erreur ne peut pas être téléchargé ; vous voyez quel champ corriger. L’exactitude commerciale et fiscale de votre facture reste de votre responsabilité.",
      },
      {
        q: "Où mes données sont-elles stockées ?",
        a: "Dans l’UE, chez Amazon Web Services à Francfort. Le PDF est lu par Anthropic aux États-Unis, dans le cadre des clauses contractuelles types de l’UE. La page Confiance et sécurité liste tous les sous-traitants et les durées de conservation.",
      },
      {
        q: "Combien de temps conservez-vous mes données ?",
        a: "Les e-mails reçus sont supprimés après 30 jours. Ce que le modèle d’IA a lu dans un import est conservé 30 jours afin d’apprendre de vos corrections. Vos factures sont conservées tant que votre compte existe ou jusqu’à ce que vous les supprimiez.",
      },
      {
        q: "Comment fonctionne la boîte de réception e-mail ?",
        a: "Vous recevez une adresse e-mail personnelle. Les PDF que vous y envoyez (ou y transférez) apparaissent dans l’application, mais ne sont lus qu’après votre approbation. Vous décidez de ce qui est traité.",
      },
      {
        q: "Comment xChange recherche-t-il mon client sur PEPPOL ?",
        a: "À partir du numéro de TVA ou d’entreprise de votre client, jamais de son nom. Vous voyez si le client peut recevoir des factures électroniques, son adresse PEPPOL est complétée et xChange choisit un format qu’il accepte.",
      },
      {
        q: "Puis-je envoyer mes factures directement via PEPPOL ?",
        a: "Pas encore. Aujourd’hui, vous téléchargez le fichier validé et l’envoyez via votre logiciel comptable ou votre prestataire PEPPOL, ou selon ce que vous avez convenu avec votre client. L’envoi direct depuis xChange arrive bientôt, tout comme la réception des factures entrantes.",
      },
      {
        q: "xChange s’améliore-t-il à l’usage ?",
        a: "Oui. Vos clients sont mémorisés et complétés sur la facture suivante, et les corrections apportées aux données de votre entreprise ou au traitement TVA de chaque client sont appliquées aux factures suivantes. Les paramètres montrent ce qui a été appris, et vous pouvez le faire oublier.",
      },
      {
        q: "Puis-je essayer gratuitement ?",
        a: "Oui. Vous recevez {n} conversions gratuites, sans carte bancaire. Ensuite, vous choisissez un abonnement ou achetez des crédits.",
      },
      {
        q: "Puis-je résilier à tout moment ?",
        a: "Oui. Les abonnements sont mensuels et résiliables à tout moment. La résiliation prend effet à la fin du mois en cours.",
      },
      {
        q: "En quelles langues xChange est-il disponible ?",
        a: "Ce site est disponible en néerlandais, en anglais et en français. L’application elle-même est pour l’instant en néerlandais.",
      },
    ],
    more: {
      title: "Une autre question ?",
      text: "Nous vous aidons volontiers.",
      cta: "Nous contacter",
    },
  },
  contact: {
    meta: {
      title: "Contact",
      description: "Contactez xChange pour toute question sur le produit, les tarifs ou l’assistance.",
    },
    eyebrow: "Contact",
    title: "Nous contacter",
    lead: "Des questions sur xChange, les tarifs ou la facturation électronique ? Envoyez-nous un e-mail.",
    generalTitle: "Questions générales",
    generalText: "Sur le produit, les tarifs, la confidentialité ou un partenariat.",
    supportTitle: "Assistance",
    supportText: "De l’aide pour utiliser l’application.",
    companyTitle: "Coordonnées de l’entreprise",
    registrationLabel: "Numéro d’entreprise",
    vatLabel: "Numéro de TVA",
  },
  signup: {
    meta: {
      title: "S’inscrire",
      description: "Les inscriptions à xChange ouvrent bientôt.",
    },
    eyebrow: "Inscription",
    title: "Les inscriptions ouvrent bientôt",
    lead: "Nous mettons la dernière main à la création de compte en ligne. Vous ne voulez pas attendre ? Contactez-nous et nous préparerons votre compte.",
    trialNote: "Vous commencerez avec {n} conversions gratuites, sans carte bancaire.",
    loginText: "Vous avez déjà un compte ?",
    loginCta: "Se connecter",
    contactText: "Envie de commencer maintenant, ou une question ?",
    contactCta: "Nous contacter",
  },
  login: {
    meta: {
      title: "Se connecter",
      description: "Connectez-vous à l’application xChange.",
    },
    title: "Se connecter à xChange",
    lead: "Vous vous connectez à l’application, où vous importez, vérifiez et exportez vos factures.",
    cta: "Ouvrir l’application",
    noAccount: "Pas encore de compte ?",
    signupCta: "S’inscrire",
  },
  legal: {
    draftNote: "Projet — en cours de relecture juridique",
    lastUpdated: "Dernière mise à jour :",
    contents: "Sommaire",
  },
};
