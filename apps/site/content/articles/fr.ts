import type { Articles } from "./types.ts";

export const articlesFr: Articles = {
  "belgium-e-invoicing-2026": {
    title: "Belgique : la facturation électronique obligatoire depuis 2026, ce que cela change pour vous",
    description:
      "Depuis le 1er janvier 2026, les factures entre entreprises belges assujetties à la TVA doivent être des factures électroniques structurées. Qui est concerné, ce qui compte comme facture électronique et comment vous y préparer.",
    readingMinutes: 3,
    blocks: [
      {
        type: "p",
        text: "Depuis le 1er janvier 2026, la Belgique impose la facturation électronique structurée pour les transactions entre entreprises. Pour beaucoup de fournisseurs, c'est un vrai changement : la facture PDF envoyée par e-mail, qui fonctionnait très bien jusque-là, ne suffit plus pour une grande partie de leurs clients. Voici ce que recouvre l'obligation et comment vous organiser concrètement.",
      },
      { type: "h2", text: "Qui est concerné ?" },
      {
        type: "p",
        text: "L'obligation vise les factures échangées entre entreprises assujetties à la TVA et établies en Belgique, autrement dit le B2B domestique. Si vous facturez un client belge lui-même assujetti à la TVA, votre facture doit être électronique et structurée. Les factures adressées à des particuliers ne sont pas visées par cette obligation.",
      },
      {
        type: "p",
        text: "Certaines catégories d'entreprises sont exclues. Plutôt que de vous fier à une liste générale, vérifiez votre situation, et au besoin celle de vos clients, avec votre comptable.",
      },
      { type: "h2", text: "Qu'est-ce qu'une facture électronique ?" },
      {
        type: "p",
        text: "Une facture électronique au sens de la réglementation n'est pas simplement une facture envoyée par voie électronique. C'est un fichier structuré, lisible par une machine, dans lequel chaque donnée (numéro de facture, date, fournisseur, client, lignes, taux de TVA, montants) occupe un champ précis. Le logiciel du destinataire peut ainsi l'importer sans ressaisie.",
      },
      {
        type: "p",
        text: "Un PDF, même envoyé par e-mail et même généré par un logiciel comptable, ne compte donc plus comme facture électronique pour ces transactions. Le format de référence est PEPPOL BIS Billing 3.0, une spécification en XML (UBL) conforme à la norme européenne EN 16931. Les factures circulent via le réseau PEPPOL, sur lequel la plupart des entreprises belges sont joignables grâce à leur numéro d'entreprise (BCE), avec le code de schéma 0208 : par exemple 0208:0123456789.",
      },
      { type: "h2", text: "Les étapes pour un fournisseur qui facture encore en PDF" },
      {
        type: "p",
        text: "Si vos factures sortent aujourd'hui en PDF de Word, d'Excel ou d'un outil de facturation simple, voici une manière raisonnable d'avancer.",
      },
      {
        type: "ol",
        items: [
          "Vérifiez l'enregistrement PEPPOL de vos clients. Pour chaque client belge assujetti, recherchez son numéro d'entreprise dans le PEPPOL Directory ou demandez-lui son adresse PEPPOL. Vous saurez qui peut déjà recevoir.",
          "Choisissez votre voie d'envoi. Une facture PEPPOL passe par un prestataire certifié, appelé point d'accès. De nombreux logiciels comptables en intègrent un ; vous pouvez aussi faire appel à un prestataire distinct.",
          "Produisez des fichiers BIS 3.0 valides. Soit votre outil de facturation les génère directement, soit vous convertissez vos factures existantes. xChange, par exemple, lit vos factures PDF, vous laisse vérifier chaque champ et produit un fichier BIS Billing 3.0 contrôlé selon les règles de validation officielles. Vous téléchargez ce fichier et l'envoyez par votre propre point d'accès ou logiciel comptable ; l'envoi direct depuis xChange est en préparation.",
          "Continuez à relire vos données. Un fichier structuré ne pardonne pas : un numéro de TVA erroné ou un taux mal appliqué fera refuser la facture ou obligera à la corriger. Prévoyez un contrôle avant chaque envoi.",
        ],
      },
      { type: "h2", text: "N'oubliez pas la réception" },
      {
        type: "p",
        text: "L'obligation ne concerne pas seulement l'envoi. Vos propres fournisseurs belges doivent eux aussi vous transmettre des factures électroniques, ce qui suppose que vous soyez joignable sur PEPPOL. Concrètement, il vous faut un point d'accès qui reçoit les factures pour vous, enregistré sous votre numéro d'entreprise. Renseignez-vous auprès de votre fournisseur de logiciel comptable ou de votre prestataire, et assurez-vous que votre enregistrement est actif avant que les premières factures n'arrivent.",
      },
      {
        type: "note",
        text: "Cet article donne un aperçu général. Certaines catégories d'entreprises sont exclues et d'autres étapes, comme le e-reporting, ont été annoncées pour les années à venir. Vérifiez votre situation précise avec votre comptable.",
      },
    ],
  },

  "what-is-peppol": {
    title: "Qu'est-ce que PEPPOL (et un identifiant PEPPOL) ?",
    description:
      "PEPPOL est le réseau par lequel circulent les factures électroniques en Belgique et ailleurs. Comment il fonctionne, à quoi ressemble un identifiant PEPPOL et comment savoir si un client peut recevoir.",
    readingMinutes: 3,
    blocks: [
      {
        type: "p",
        text: "PEPPOL revient dans chaque discussion sur la facturation électronique, surtout en Belgique depuis 2026. Le terme désigne à la fois un réseau, un ensemble de règles et une manière d'identifier les entreprises. Voici l'essentiel, sans jargon inutile.",
      },
      { type: "h2", text: "Un réseau pour échanger des documents commerciaux" },
      {
        type: "p",
        text: "PEPPOL est un réseau international d'échange de documents commerciaux électroniques, dont les factures et les notes de crédit. Il est géré par OpenPeppol, une association internationale qui fixe les règles techniques et les accords auxquels les participants adhèrent. PEPPOL n'est ni un logiciel ni une plateforme centrale où l'on dépose ses factures : c'est un cadre commun qui permet à des systèmes différents de se comprendre.",
      },
      { type: "h2", text: "Les points d'accès et le modèle à quatre coins" },
      {
        type: "p",
        text: "On ne se connecte pas directement au réseau. Expéditeurs et destinataires passent chacun par un prestataire de services certifié, appelé point d'accès (access point). C'est ce qu'on appelle le modèle à quatre coins :",
      },
      {
        type: "ol",
        items: [
          "l'expéditeur prépare sa facture dans son logiciel ;",
          "son point d'accès l'envoie sur le réseau ;",
          "le point d'accès du destinataire la reçoit ;",
          "le destinataire la récupère dans son propre logiciel.",
        ],
      },
      {
        type: "p",
        text: "L'intérêt est simple : un seul prestataire vous suffit pour atteindre tous les participants du réseau, quel que soit le prestataire qu'ils ont choisi. C'est comparable au téléphone, où vous n'avez pas besoin d'être chez le même opérateur que votre interlocuteur.",
      },
      { type: "h2", text: "À quoi ressemble un identifiant PEPPOL ?" },
      {
        type: "p",
        text: "Chaque destinataire est enregistré sous un identifiant PEPPOL composé de deux parties séparées par deux-points : un code de schéma, qui indique de quel type de numéro il s'agit, puis le numéro lui-même. Quelques schémas courants :",
      },
      {
        type: "ul",
        items: [
          "0208 : le numéro d'entreprise belge (BCE), par exemple 0208:0123456789 ;",
          "0106 : le numéro de la Chambre de commerce néerlandaise (KvK), utilisé par de nombreuses entreprises aux Pays-Bas ;",
          "0190 : l'OIN, l'identifiant des organismes publics néerlandais.",
        ],
      },
      {
        type: "p",
        text: "Une même entreprise peut être enregistrée sous plusieurs identifiants. Ce qui compte, c'est d'utiliser celui sous lequel elle peut effectivement recevoir.",
      },
      { type: "h2", text: "SML, SMP et PEPPOL Directory : savoir qui peut recevoir" },
      {
        type: "p",
        text: "Pour savoir où livrer une facture, le point d'accès de l'expéditeur effectue une recherche en deux temps. Le SML (Service Metadata Locator) indique quel service gère les métadonnées d'un identifiant donné ; le SMP (Service Metadata Publisher) précise ensuite ce que ce destinataire accepte. Cette recherche vous apprend donc deux choses : si le destinataire peut recevoir via PEPPOL, et quels types de documents et formats il accepte.",
      },
      {
        type: "p",
        text: "Il existe aussi un annuaire public, le PEPPOL Directory, consultable dans un navigateur, où l'on peut chercher une entreprise par son identifiant. Pour l'envoi, c'est toutefois la recherche SML/SMP qui fait foi. xChange effectue cette recherche à partir du numéro de TVA ou d'entreprise de votre client et vous montre s'il peut recevoir des factures électroniques.",
      },
      { type: "h2", text: "BIS Billing 3.0 en bref" },
      {
        type: "p",
        text: "BIS Billing 3.0 est la spécification de facture de PEPPOL. Elle décrit précisément comment une facture doit être structurée en XML, selon la syntaxe UBL, et elle est conforme à la norme européenne EN 16931. Elle s'accompagne de règles de validation officielles : un fichier qui ne les respecte pas peut être refusé. C'est le format de référence en Belgique pour les factures entre entreprises, et il est largement accepté ailleurs sur le réseau.",
      },
    ],
  },

  "bis-3-or-nlcius": {
    title: "BIS 3.0 ou NLCIUS : quel format pour votre client néerlandais ?",
    description:
      "Aux Pays-Bas, deux formats de facture électronique coexistent : PEPPOL BIS Billing 3.0 et NLCIUS. D'où ils viennent, ce qui les distingue et comment choisir le bon.",
    readingMinutes: 3,
    blocks: [
      {
        type: "p",
        text: "Si vous facturez des clients aux Pays-Bas, vous croiserez deux noms : PEPPOL BIS Billing 3.0 et NLCIUS. Ce sont deux formats de facture électronique, ils reposent sur la même norme européenne, et pourtant votre client peut en attendre un en particulier. Voici comment vous y retrouver.",
      },
      { type: "h2", text: "Une base commune : EN 16931" },
      {
        type: "p",
        text: "EN 16931 est la norme européenne de facturation électronique. Elle définit le contenu d'une facture : quelles informations doivent y figurer, comment elles s'appellent et comment elles se rapportent les unes aux autres. Elle laisse toutefois une certaine marge. Les pays et les réseaux peuvent donc publier une CIUS (core invoice usage specification), une spécification d'usage qui précise et restreint la norme pour leur contexte, sans en sortir.",
      },
      { type: "h2", text: "PEPPOL BIS Billing 3.0 et NLCIUS" },
      {
        type: "p",
        text: "PEPPOL BIS Billing 3.0 est la spécification de facture du réseau PEPPOL. Elle est conforme à EN 16931 et utilisée dans de nombreux pays, dont la Belgique, où elle est le format de référence pour les factures entre entreprises.",
      },
      {
        type: "p",
        text: "NLCIUS est la spécification nationale néerlandaise. Elle ajoute aux règles européennes des règles néerlandaises supplémentaires, plus strictes sur certains points. Dans sa forme technique, on la rencontre sous le nom SI-UBL 2.0. Elle est notamment utilisée par les acheteurs publics néerlandais.",
      },
      {
        type: "p",
        text: "Point commun important : les deux formats sont exprimés en UBL, la même syntaxe XML. Ce qui les distingue, ce sont les règles appliquées au fichier. Une facture est établie pour l'une ou l'autre spécification, et c'est au regard des règles de validation de celle-ci qu'elle est contrôlée.",
      },
      { type: "h2", text: "Comment décider ?" },
      {
        type: "p",
        text: "La règle la plus simple : utilisez le format que votre client accepte. Pour le savoir, vous avez deux moyens.",
      },
      {
        type: "ul",
        items: [
          "Demandez-le à votre client. Son service comptable ou son portail fournisseurs sait en général quel format et quelle adresse PEPPOL utiliser.",
          "Consultez son enregistrement PEPPOL. L'enregistrement d'un destinataire liste les types de documents qu'il peut recevoir. Si NLCIUS y figure et pas BIS 3.0, ou l'inverse, la question est tranchée.",
        ],
      },
      {
        type: "p",
        text: "xChange exporte les deux formats et contrôle chaque fichier par rapport à ses règles de validation officielles avant de le mettre à disposition. Lorsqu'il recherche votre client sur le réseau PEPPOL, il lit les types de documents acceptés et choisit un format que le destinataire accepte.",
      },
      { type: "h2", text: "Acheteurs publics et clients privés" },
      {
        type: "p",
        text: "Les acheteurs publics néerlandais, c'est-à-dire l'administration centrale et de nombreux autres organismes publics, attendent des factures électroniques, en général via PEPPOL. Ils acceptent NLCIUS et, en règle générale, aussi PEPPOL BIS Billing 3.0. Ils sont souvent enregistrés sous leur OIN, avec le schéma 0190. Si votre client est un organisme public, suivez les instructions qu'il donne à ses fournisseurs.",
      },
      {
        type: "p",
        text: "Pour les clients privés, il n'existe pas (encore) d'obligation générale de facturation électronique entre entreprises aux Pays-Bas. Beaucoup d'entreprises néerlandaises reçoivent néanmoins des factures via PEPPOL, souvent sous leur numéro KvK (schéma 0106). Sur PEPPOL, BIS Billing 3.0 est largement accepté et constitue un choix sûr, sauf si votre client demande expressément NLCIUS.",
      },
      {
        type: "note",
        text: "En cas de doute, ne devinez pas. Vérifiez l'enregistrement PEPPOL de votre client ou posez-lui la question avant le premier envoi : un format refusé, c'est une facture à refaire et à renvoyer.",
      },
    ],
  },

  "find-peppol-address": {
    title: "Comment trouver l'adresse PEPPOL de votre client",
    description:
      "Pour envoyer une facture via PEPPOL, il vous faut l'identifiant sous lequel votre client reçoit. Comment le construire, le vérifier et le conserver.",
    readingMinutes: 3,
    blocks: [
      {
        type: "p",
        text: "Une facture PEPPOL n'est pas envoyée à une adresse e-mail mais à un identifiant PEPPOL : l'adresse sous laquelle votre client est enregistré sur le réseau. Sans cet identifiant, votre point d'accès ne sait pas où livrer la facture. Heureusement, il se retrouve en général facilement à partir de données que vous possédez déjà.",
      },
      { type: "h2", text: "Pourquoi cette adresse est indispensable" },
      {
        type: "p",
        text: "Sur le réseau PEPPOL, chaque destinataire est enregistré sous un identifiant composé d'un code de schéma et d'un numéro, par exemple 0208:0123456789. Lors de l'envoi, le point d'accès recherche cet identifiant pour savoir quel prestataire reçoit pour votre client et quels formats celui-ci accepte. Avec une adresse erronée ou inexistante, la facture ne peut tout simplement pas être livrée.",
      },
      { type: "h2", text: "Ce qu'il faut demander, et comment l'identifiant est construit" },
      {
        type: "p",
        text: "Le plus simple est de demander directement à votre client son adresse PEPPOL. À défaut, demandez son numéro de TVA ou son numéro d'entreprise : dans la plupart des cas, l'identifiant s'en déduit.",
      },
      {
        type: "ul",
        items: [
          "Belgique : le schéma 0208 suivi du numéro d'entreprise (BCE). Le numéro d'entreprise correspond aux dix chiffres du numéro de TVA, sans le préfixe pays. Le numéro de TVA BE0123456789 donne ainsi le numéro d'entreprise 0123456789 et l'identifiant PEPPOL 0208:0123456789.",
          "Pays-Bas, entreprises : souvent le schéma 0106 suivi du numéro KvK (Chambre de commerce).",
          "Pays-Bas, organismes publics : en général le schéma 0190 suivi de l'OIN de l'organisme.",
        ],
      },
      {
        type: "p",
        text: "Une entreprise peut cependant être enregistrée sous un autre identifiant que celui que vous attendez. Construire l'identifiant ne suffit donc pas : il faut vérifier qu'il existe.",
      },
      { type: "h2", text: "Vérifier l'enregistrement" },
      {
        type: "p",
        text: "Pour vérifier qu'un client est bien joignable, vous disposez de deux moyens :",
      },
      {
        type: "ul",
        items: [
          "le PEPPOL Directory, l'annuaire public du réseau, où vous pouvez rechercher un identifiant ou un numéro ;",
          "votre point d'accès ou votre prestataire de services, qui peut effectuer la recherche sur le réseau et vous dire si l'identifiant est actif et quels types de documents il accepte.",
        ],
      },
      {
        type: "p",
        text: "Cherchez toujours par numéro plutôt que par nom. Les noms d'entreprises sont ambigus : orthographes différentes, formes juridiques abrégées, noms commerciaux, sociétés aux noms presque identiques. Un nom n'est pas unique, un numéro l'est. C'est pourquoi xChange ne recherche un client que par son numéro de TVA ou d'entreprise, jamais par son nom, puis reprend sur la facture l'adresse PEPPOL trouvée.",
      },
      { type: "h2", text: "Si votre client n'est pas enregistré" },
      {
        type: "p",
        text: "Il arrive qu'aucun enregistrement ne soit trouvé. Ne concluez pas trop vite : votre client utilise peut-être un autre identifiant, ou son enregistrement est en cours. Posez-lui la question. Un client belge assujetti à la TVA doit en principe pouvoir recevoir des factures électroniques depuis 2026, sauf s'il relève d'une catégorie exclue. S'il n'est réellement pas joignable via PEPPOL, convenez avec lui d'une autre voie de transmission.",
      },
      {
        type: "p",
        text: "Une fois l'adresse confirmée, enregistrez-la dans votre fichier clients, à côté du numéro de TVA, pour ne pas devoir la rechercher à chaque facture. xChange retient vos clients et reprend leurs données, dont l'adresse PEPPOL, sur la facture suivante.",
      },
      {
        type: "note",
        text: "Revérifiez de temps en temps l'enregistrement de vos clients réguliers, par exemple lorsqu'ils changent de logiciel comptable ou de prestataire : les formats qu'ils acceptent peuvent évoluer.",
      },
    ],
  },
};
