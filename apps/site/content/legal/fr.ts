import { company, formatAddress } from "../company.ts";
import type { LegalContent } from "./types.ts";

export const legalFr: LegalContent = {
  privacy: {
    title: "Politique de confidentialité",
    description: `Comment ${company.name} traite les données à caractère personnel dans le cadre de xChange et du site web.`,
    intro: [
      `Cette politique s’adresse aux utilisateurs de xChange, aux personnes qui nous contactent et aux visiteurs de notre site web. Elle explique quelles données à caractère personnel nous traitons, pourquoi, pendant combien de temps et quels sont vos droits.`,
      `xChange est un service de ${company.name}.`,
    ],
    sections: [
      {
        heading: "Responsable du traitement",
        paragraphs: [
          `Le responsable du traitement est ${company.name}, ${formatAddress()}.`,
          `Numéro d’entreprise : ${company.registrationNumber}.${company.vatNumber ? ` Numéro de TVA : ${company.vatNumber}.` : ""}`,
          `Pour toute question relative à la vie privée, écrivez-nous à ${company.contactEmail}.`,
        ],
      },
      {
        heading: "Champ d’application et nos deux rôles",
        paragraphs: [
          `Pour les données de compte, de facturation, de support et du site web, nous agissons en tant que responsable du traitement : nous déterminons nous-mêmes pourquoi et comment ces données sont traitées.`,
          `Pour les données de facturation que nos clients chargent dans xChange (les factures et les PDF qu’ils contiennent), nous agissons en tant que sous-traitant. Le client est alors le responsable du traitement et nous ne traitons ces données que pour son compte. Ce traitement est régi par l’accord de traitement des données, dont un résumé figure sur la page Confiance et sécurité.`,
        ],
      },
      {
        heading: "Données que nous traitons",
        items: [
          `Données de compte : nom, adresse e-mail et mot de passe. Le mot de passe est uniquement conservé sous forme de hachage, jamais en clair.`,
          `Données de l’organisation : nom, adresse, numéro d’entreprise, numéro de TVA et autres coordonnées que vous saisissez.`,
          `Factures et PDF que vous chargez ou envoyez par e-mail. Pour ces données, nous agissons en tant que sous-traitant.`,
          `E-mails envoyés à votre adresse de réception xChange.`,
          `Corrections que vous apportez lors de la vérification, que nous utilisons pour apprendre au profit de votre propre organisation.`,
          `Données de paiement : elles sont traitées par notre prestataire de paiement. Nous ne conservons pas les numéros complets de carte bancaire.`,
          `Journaux techniques : adresse IP, horodatages et messages d’erreur.`,
          `La correspondance que vous échangez avec nous.`,
        ],
        after: [
          `Notre site web n’utilise pas de cookies d’analyse ni de cookies publicitaires.`,
        ],
      },
      {
        heading: "Finalités et bases juridiques",
        table: {
          head: ["Finalité", "Base juridique"],
          rows: [
            ["Fournir le service et gérer votre compte", "Exécution du contrat"],
            ["Facturation et comptabilité", "Obligation légale"],
            [
              "Sécurité, prévention des abus et analyse des erreurs",
              "Intérêt légitime",
            ],
            ["Support", "Exécution du contrat ou intérêt légitime"],
            [
              "Apprendre de vos corrections au profit de votre propre organisation",
              "Exécution du contrat",
            ],
            [
              "Annonces de service concernant votre compte",
              "Intérêt légitime",
            ],
          ],
        },
        after: [
          `Nous ne vendons pas vos données et ne les utilisons pas à des fins publicitaires.`,
        ],
      },
      {
        heading: "Utilisation de l’IA",
        paragraphs: [
          `Les PDF que vous chargez sont lus par un modèle d’IA d’un sous-traitant spécialisé afin d’en extraire les champs de la facture.`,
          `Une personne vérifie le résultat avant que quoi que ce soit ne soit exporté. Les montants et le fichier XML sont calculés de manière déterministe, sans IA.`,
          `Nous ne prenons aucune décision fondée exclusivement sur un traitement automatisé produisant des effets juridiques ou des effets similaires significatifs au sens de l’article 22 du RGPD.`,
        ],
      },
      {
        heading: "Sous-traitants et destinataires",
        table: {
          head: ["Sous-traitant", "Finalité", "Localisation"],
          rows: [
            ["Hébergeur", "Hébergement et stockage", "Union européenne"],
            [
              "Prestataire de reconnaissance de texte par IA",
              "Lecture des PDF chargés",
              "Union européenne et États-Unis",
            ],
            [
              "Prestataire de paiement",
              "Traitement des paiements",
              "Union européenne et États-Unis",
            ],
          ],
        },
        after: [
          `Une liste à jour de nos sous-traitants est disponible sur demande à ${company.contactEmail}.`,
          `Nous ne communiquons des données aux autorités que lorsque la loi nous y oblige.`,
        ],
      },
      {
        heading: "Transferts hors de l’EEE",
        paragraphs: [
          `La lecture des PDF peut impliquer un traitement hors de l’UE, aux États-Unis. Ce transfert est encadré par les clauses contractuelles types de l’Union européenne.`,
          `Tout autre transfert en dehors de l’Espace économique européen n’a lieu qu’avec des garanties appropriées.`,
        ],
      },
      {
        heading: "Durée de conservation",
        items: [
          `Données de compte et de l’organisation : pendant toute la durée du compte et jusqu’à ${company.retentionAfterClosing} après sa clôture.`,
          `Factures et PDF : jusqu’à ce que vous les supprimiez ou que le compte soit clôturé, puis supprimés dans un délai de ${company.retentionAfterClosing}.`,
          `E-mails reçus bruts : supprimés après 30 jours.`,
          `Lecture d’un document chargé par le modèle : conservée 30 jours.`,
          `Données de facturation : aussi longtemps que l’exige la législation fiscale et comptable.`,
          `Journaux techniques : pendant une durée limitée.`,
        ],
      },
      {
        heading: "Sécurité",
        paragraphs: [
          `Nous protégeons vos données par des mesures techniques et organisationnelles appropriées, notamment le chiffrement en transit et au repos, la séparation des données par organisation et un accès limité à l’environnement de production.`,
          `La page Confiance et sécurité décrit ces mesures plus en détail.`,
        ],
      },
      {
        heading: "Vos droits",
        paragraphs: [`Vous disposez des droits suivants :`],
        items: [
          `droit d’accès à vos données ;`,
          `droit de rectification ;`,
          `droit à l’effacement ;`,
          `droit à la limitation du traitement ;`,
          `droit à la portabilité des données ;`,
          `droit d’opposition ;`,
          `droit de retirer votre consentement, lorsque le traitement repose sur celui-ci.`,
        ],
        after: [
          `Pour exercer ces droits, écrivez à ${company.contactEmail}. Nous répondons dans un délai d’un mois.`,
          `Si votre demande porte sur des données de facturation que nous traitons en tant que sous-traitant, nous la transmettons au client concerné et l’aidons à y répondre.`,
        ],
      },
      {
        heading: "Réclamations",
        paragraphs: [
          `Si vous n’êtes pas satisfait de la manière dont nous traitons vos données, contactez-nous d’abord : nous chercherons une solution avec vous.`,
          `Vous avez également le droit d’introduire une réclamation auprès de l’autorité de contrôle compétente, ${company.supervisoryAuthority} (${company.supervisoryAuthorityUrl}), ou auprès de l’autorité de protection des données du pays où vous résidez ou travaillez.`,
        ],
      },
      {
        heading: "Modifications de cette politique",
        paragraphs: [
          `Nous publions toute modification de cette politique sur cette page. Les titulaires d’un compte sont informés à l’avance des modifications importantes.`,
        ],
      },
    ],
  },

  terms: {
    title: "Conditions générales d’utilisation",
    description: `Les conditions applicables à l’utilisation de xChange, service fourni par ${company.name}.`,
    intro: [
      `Les présentes conditions s’appliquent à l’utilisation de xChange, service fourni par ${company.name}, ${formatAddress()}.`,
      `xChange est destiné exclusivement aux entreprises. Le service n’est pas proposé aux consommateurs. En créant un compte, vous déclarez agir dans le cadre de votre activité professionnelle.`,
    ],
    sections: [
      {
        heading: "Définitions",
        items: [
          `Service : l’application xChange et les fonctionnalités qui y sont associées.`,
          `Client : l’entreprise qui crée un compte et utilise le Service.`,
          `Utilisateur : toute personne qui accède au Service au nom du Client.`,
          `Conversion : la lecture d’un PDF pour en faire une facture.`,
          `Formule : l’abonnement mensuel choisi par le Client, comprenant un nombre de Conversions.`,
          `Crédits : des Conversions achetées à l’avance sous forme de pack, sans abonnement.`,
        ],
      },
      {
        heading: "Le service",
        paragraphs: [
          `xChange transforme vos propres factures de vente au format PDF en factures électroniques validées. Vous chargez un ou plusieurs PDF, ou vous les envoyez par e-mail à votre adresse de réception personnelle ; les PDF reçus par e-mail attendent votre approbation avant d’être lus. Une IA lit la facture, puis vous vérifiez et corrigez les champs sur un écran de vérification avec validation en direct.`,
          `Le Service exporte aux formats PEPPOL BIS Billing 3.0, NLCIUS / SI-UBL 2.0 et PINT-SG (ce dernier à titre de démonstration uniquement). Chaque fichier est contrôlé au regard de l’ensemble des règles de validation officielles avant de pouvoir être téléchargé ; un fichier comportant une erreur n’est jamais délivré.`,
          `Ne sont pas encore inclus : l’envoi de factures via un point d’accès PEPPOL et la réception de factures entrantes. Aujourd’hui, vous téléchargez le fichier validé et vous l’envoyez via votre propre point d’accès ou logiciel comptable, ou vous le remettez à votre client par la voie convenue avec lui.`,
          `Le Service évolue : des fonctionnalités peuvent être ajoutées, modifiées ou retirées au fil du temps.`,
        ],
      },
      {
        heading: "Compte et accès",
        paragraphs: [
          `Vous fournissez des informations exactes et complètes lors de la création du compte et les tenez à jour.`,
          `Vous gardez vos identifiants de connexion confidentiels. Le Client est responsable de ses Utilisateurs et des actions effectuées avec leurs comptes.`,
          `Si vous constatez ou soupçonnez une utilisation abusive de votre compte, prévenez-nous sans délai.`,
        ],
      },
      {
        heading: "Essai gratuit",
        paragraphs: [
          `Un essai gratuit vous donne droit à 1 Conversion, sans carte de paiement et sans engagement.`,
          `Le téléchargement du fichier de facture électronique (et, dès qu’il sera disponible, son envoi) nécessite un abonnement en cours ou des Crédits achetés. Lorsque votre abonnement a pris fin et que vos Crédits sont épuisés, le téléchargement n’est plus possible.`,
          `Nous pouvons mettre fin aux conditions de l’essai gratuit ou les modifier pour les nouvelles inscriptions.`,
        ],
      },
      {
        heading: "Formules, prix et paiement",
        paragraphs: [
          `Nous proposons des abonnements mensuels comprenant un nombre de Conversions, ainsi que des packs de Crédits sans abonnement.`,
          `Les prix figurent sur la page tarifs et s’entendent hors TVA. Le paiement s’effectue via notre prestataire de paiement.`,
          `Toute modification de prix est annoncée au moins ${company.noticePeriod} à l’avance et prend effet à partir de la période de facturation suivante.`,
        ],
      },
      {
        heading: "Résiliation",
        paragraphs: [
          `Vous pouvez résilier votre abonnement à tout moment. La résiliation prend effet à la fin de la période de facturation en cours.`,
          `Nous pouvons suspendre ou résilier votre accès en cas de manquement grave aux présentes conditions ou de défaut de paiement, après vous en avoir averti.`,
          `Exportez vos données avant de clôturer votre compte. Après la clôture, les données sont supprimées conformément à la politique de confidentialité.`,
        ],
      },
      {
        heading: "Usage raisonnable",
        paragraphs: [`Lorsque vous utilisez le Service, vous vous engagez à ne pas :`],
        items: [
          `charger de contenu illicite, ni de documents que vous n’êtes pas autorisé à traiter ;`,
          `tenter d’accéder aux données d’autres clients ou de contourner la sécurité du Service ;`,
          `utiliser le Service de manière automatisée et massive au-delà de votre Formule, ni le surcharger ;`,
          `revendre le Service sans notre accord ;`,
          `procéder à de l’ingénierie inverse du logiciel, sauf dans la mesure où la loi l’autorise.`,
        ],
      },
      {
        heading: "Votre responsabilité quant au contenu des factures",
        paragraphs: [
          `Vous restez responsable du contenu de vos factures ainsi que des données que vous vérifiez, corrigez, enregistrez et exportez.`,
          `La validation contrôle que le fichier respecte les règles du format. Elle ne vérifie pas si la facture est correcte sur le plan commercial ou fiscal. Vérifiez donc chaque facture avant de l’envoyer.`,
          `Vous restez responsable de vos obligations fiscales et comptables.`,
        ],
      },
      {
        heading: "Vos données",
        paragraphs: [
          `Vous restez propriétaire de vos données. Nous les traitons pour fournir le Service, conformément à la politique de confidentialité et à l’accord de traitement des données.`,
          `Vous pouvez exporter les fichiers validés.`,
        ],
      },
      {
        heading: "Lecture assistée par l’IA",
        paragraphs: [
          `Les champs lus par l’IA peuvent contenir des erreurs. C’est pourquoi la vérification par vos soins fait partie intégrante du processus.`,
        ],
      },
      {
        heading: "Disponibilité et support",
        paragraphs: [
          `Nous faisons des efforts raisonnables pour que le Service soit disponible, sans garantir de disponibilité. Le Service peut être temporairement indisponible, notamment pour maintenance.`,
          `Le support est assuré via ${company.supportEmail}.`,
        ],
      },
      {
        heading: "Propriété intellectuelle",
        paragraphs: [
          `Le Service et le logiciel restent notre propriété. Pendant la durée de votre abonnement, vous bénéficiez d’une licence d’utilisation du Service.`,
          `Votre contenu reste le vôtre. Vous nous accordez les droits nécessaires pour fournir le Service.`,
        ],
      },
      {
        heading: "Responsabilité",
        paragraphs: [
          `Notre responsabilité est limitée au montant que vous nous avez payé au cours des 12 mois précédant l’événement à l’origine du dommage.`,
          `Nous ne sommes pas responsables des dommages indirects, tels que le manque à gagner, la perte de données au-delà de la restauration à partir des sauvegardes, ou les amendes résultant du contenu de vos factures.`,
          `Ces limitations ne s’appliquent pas lorsque la loi ne le permet pas, notamment en cas de faute intentionnelle ou de faute grave.`,
        ],
      },
      {
        heading: "Modifications des conditions",
        paragraphs: [
          `Toute modification des présentes conditions est annoncée au moins ${company.noticePeriod} à l’avance.`,
          `Si vous continuez à utiliser le Service après l’entrée en vigueur des modifications, vous les acceptez. Si vous n’êtes pas d’accord, vous pouvez résilier votre abonnement.`,
        ],
      },
      {
        heading: "Droit applicable et litiges",
        paragraphs: [
          `Droit applicable : ${company.governingLaw}.`,
          `Juridiction compétente en cas de litige : ${company.competentCourt}.`,
          `Pour toute question concernant ces conditions, contactez-nous à ${company.contactEmail}.`,
        ],
      },
    ],
  },

  trust: {
    title: "Confiance et sécurité",
    description: `Comment xChange traite et protège vos données : hébergement, sous-traitants, conservation et sécurité.`,
    intro: [
      `Cette page résume comment xChange traite vos données : où elles sont stockées, qui y a accès, combien de temps nous les conservons et comment nous les protégeons.`,
    ],
    sections: [
      {
        heading: "Où sont vos données",
        paragraphs: [
          `xChange est hébergé dans l’Union européenne. Vos documents et vos factures y sont stockés.`,
        ],
      },
      {
        heading: "Comment les factures sont lues",
        paragraphs: [
          `Un modèle d’IA d’un sous-traitant spécialisé lit le PDF et en extrait les champs de la facture. Ce traitement peut avoir lieu hors de l’UE, aux États-Unis ; ce transfert est encadré par les clauses contractuelles types de l’Union européenne.`,
          `Vous vérifiez tout le résultat avant l’export. Les totaux et le fichier XML sont calculés de manière déterministe : l’IA n’intervient pas dans les calculs.`,
          `Chaque export est contrôlé au regard de l’ensemble des règles de validation officielles. Un fichier comportant une erreur n’est jamais délivré.`,
        ],
      },
      {
        heading: "Sous-traitants",
        table: {
          head: ["Sous-traitant", "Finalité", "Localisation"],
          rows: [
            ["Hébergeur", "Hébergement et stockage", "Union européenne"],
            [
              "Prestataire de reconnaissance de texte par IA",
              "Lecture des PDF chargés",
              "Union européenne et États-Unis",
            ],
            [
              "Prestataire de paiement",
              "Traitement des paiements",
              "Union européenne et États-Unis",
            ],
          ],
        },
        after: [
          `Une liste à jour de nos sous-traitants ultérieurs est disponible sur demande à ${company.contactEmail}.`,
          `Toute modification de la liste des sous-traitants est annoncée à l’avance.`,
        ],
      },
      {
        heading: "Durée de conservation",
        table: {
          head: ["Données", "Conservation"],
          rows: [
            ["E-mails reçus bruts", "30 jours"],
            ["Lecture d’un document chargé par le modèle", "30 jours"],
            [
              "Factures et PDF sources",
              `Jusqu’à ce que vous les supprimiez ou clôturiez le compte, puis supprimés dans un délai de ${company.retentionAfterClosing}`,
            ],
            [
              "Données de compte",
              `Pendant toute la durée du compte, puis supprimées dans un délai de ${company.retentionAfterClosing}`,
            ],
            ["Données de facturation", "Aussi longtemps que la loi l’exige"],
          ],
        },
      },
      {
        heading: "Mesures de sécurité",
        items: [
          `Les données sont chiffrées en transit (TLS) et au repos.`,
          `Les données de chaque organisation sont stockées séparément, sous la clé de cette organisation, et ne sont pas accessibles aux autres organisations.`,
          `Les mots de passe sont uniquement conservés sous forme de hachages bcrypt.`,
          `Après plusieurs tentatives de connexion échouées, la connexion est temporairement bloquée.`,
          `Les clés d’API et autres secrets sont conservés dans un coffre-fort de secrets géré, et non dans le code.`,
          `L’accès à l’environnement de production est limité au strict nécessaire.`,
        ],
      },
      {
        heading: "Accord de traitement des données : résumé",
        items: [
          `Objet et durée : la fourniture de xChange pendant la durée de l’abonnement.`,
          `Nature et finalité : la lecture, le stockage, la validation et l’export de factures.`,
          `Types de données à caractère personnel : noms, adresses, adresses e-mail, numéros de téléphone, numéros de TVA et d’entreprise et coordonnées bancaires figurant sur les factures, ainsi que les données des comptes utilisateurs.`,
          `Catégories de personnes concernées : les utilisateurs du client, les clients du client et leurs personnes de contact.`,
          `Nos obligations : traiter les données uniquement sur instruction documentée du client ; garantir la confidentialité de notre personnel ; appliquer des mesures de sécurité appropriées ; ne faire appel à des sous-traitants que s’ils sont soumis à des obligations équivalentes ; aider le client à répondre aux demandes des personnes concernées et à réaliser des analyses d’impact (AIPD) ; notifier toute violation de données à caractère personnel dans les meilleurs délais ; supprimer ou restituer les données à la fin du contrat ; mettre à disposition les informations nécessaires aux audits.`,
        ],
        after: [
          `L’accord de traitement des données fait partie intégrante des conditions générales d’utilisation. Vous pouvez en demander une copie à ${company.contactEmail}.`,
        ],
      },
      {
        heading: "Incidents",
        paragraphs: [
          `En cas de violation de données, nous examinons ce qui s’est passé et prenons des mesures pour en limiter les conséquences.`,
          `Nous informons les clients concernés dans les meilleurs délais et coopérons avec les autorités compétentes.`,
        ],
      },
      {
        heading: "Questions et signalement d’une vulnérabilité",
        paragraphs: [
          `Pour toute question sur la sécurité ou pour signaler une vulnérabilité, écrivez à ${company.contactEmail}.`,
          `Nous apprécions une divulgation responsable : donnez-nous l’occasion de corriger le problème avant de le rendre public.`,
        ],
      },
    ],
  },
};
