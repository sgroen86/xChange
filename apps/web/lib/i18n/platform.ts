/** Texts for the platform area. Dutch first; `en` must have exactly the same keys (typeof nl). */

const nl = {
  title: "Platform",
  count: (n: number) => `${n} organisatie(s)`,
  adminOnly: "Deze pagina is alleen voor de platformbeheerder.",
  create: "Organisatie aanmaken",
  saved: "Organisatie opgeslagen.",
  saveFailed: "Opslaan mislukt.",
  linkCopied: "Link gekopieerd.",
  copyFailed: "Kopiëren lukte niet; selecteer de link en kopieer hem zelf.",
  linkTitle: (organization: string) => `Uitnodigingslink voor ${organization}`,
  linkHelp:
    "Stuur deze link zelf naar de gebruiker. Hij is 7 dagen geldig en werkt één keer. Hij wordt niet opnieuw getoond; maak zo nodig een nieuwe.",
  linkLabel: "Uitnodigingslink",
  copy: "Kopiëren",
  editTitle: (organization: string) => `${organization} wijzigen`,
  inviteTitle: (organization: string) => `Nieuwe uitnodiging voor ${organization}`,
  organizationName: "Naam organisatie",
  country: "Land",
  chooseCountry: "Kies een land",
  firstUserName: "Naam eerste gebruiker",
  email: "E-mailadres",
  inviteHelp: "Een eventuele eerdere link voor deze organisatie werkt daarna niet meer.",
  createAndShowLink: "Aanmaken en link tonen",
  columns: {
    organization: "Organisatie",
    country: "Land",
    created: "Aangemaakt",
    users: "Gebruikers",
    invitation: "Uitnodiging",
  },
  pending: (email: string | null) => `Open · ${email}`,
  edit: "Wijzigen",
  newLink: "Nieuwe link",

  remove: {
    title: (organization: string) => `${organization} verwijderen`,
    irreversible: "Dit is definitief en kan niet ongedaan worden gemaakt. Alles van deze organisatie wordt verwijderd:",
    what: [
      "alle gebruikers: ze worden direct uitgelogd en kunnen niet meer inloggen; ook hun tweestapsverificatie en een open uitnodiging",
      "alle facturen, imports en relaties, met hun PDF's",
      "de instellingen, het inboxadres, wat xChange van correcties heeft geleerd, het verbruik en de kosten- en kwaliteitsgegevens",
    ],
    stripe:
      "Een lopend abonnement moet eerst zijn opgezegd. De klant in Stripe blijft bestaan, met zijn betaalgeschiedenis voor de boekhouding.",
    confirmLabel: (organization: string) => `Typ ter bevestiging de naam van de organisatie: ${organization}`,
    confirm: "Definitief verwijderen",
    deleted: (organization: string) => `${organization} is verwijderd.`,
    failed: "Verwijderen mislukt.",
  },

  model: {
    title: "Leesmodel en aanmelden",
    label: "Model dat de PDF's leest",
    configDefault: "Standaard uit de configuratie",
    saved: "Instellingen opgeslagen.",
    help: "Geldt voor alle organisaties, voor elke volgende upload en import. Het aantal tokens per factuur staat in de logboeken, zodat het effect op de kosten te meten is.",
    signupOpen: "Zelf aanmelden open (nieuwe organisaties krijgen een proefperiode)",
    signupHelp: "Pas openzetten bij de lancering: tot dan kan niemand zelf een account aanmaken.",
  },
};

const en: typeof nl = {
  title: "Platform",
  count: (n: number) => (n === 1 ? "1 organisation" : `${n} organisations`),
  adminOnly: "This page is only for the platform administrator.",
  create: "Create organisation",
  saved: "Organisation saved.",
  saveFailed: "Saving failed.",
  linkCopied: "Link copied.",
  copyFailed: "Copying did not work; select the link and copy it yourself.",
  linkTitle: (organization: string) => `Invitation link for ${organization}`,
  linkHelp:
    "Send this link to the user yourself. It is valid for 7 days and works once. It will not be shown again; create a new one if needed.",
  linkLabel: "Invitation link",
  copy: "Copy",
  editTitle: (organization: string) => `Edit ${organization}`,
  inviteTitle: (organization: string) => `New invitation for ${organization}`,
  organizationName: "Organisation name",
  country: "Country",
  chooseCountry: "Choose a country",
  firstUserName: "Name of first user",
  email: "Email address",
  inviteHelp: "Any earlier link for this organisation will stop working.",
  createAndShowLink: "Create and show link",
  columns: {
    organization: "Organisation",
    country: "Country",
    created: "Created",
    users: "Users",
    invitation: "Invitation",
  },
  pending: (email: string | null) => `Open · ${email}`,
  edit: "Edit",
  newLink: "New link",

  remove: {
    title: (organization: string) => `Delete ${organization}`,
    irreversible: "This is permanent and cannot be undone. Everything of this organisation is deleted:",
    what: [
      "all users: they are signed out at once and cannot sign in again; also their two-step verification and any open invitation",
      "all invoices, imports and relations, with their PDFs",
      "the settings, the inbox address, what xChange learned from corrections, the usage and the cost and quality records",
    ],
    stripe:
      "A running subscription must be cancelled first. The customer in Stripe remains, with its payment history for the accounts.",
    confirmLabel: (organization: string) => `To confirm, type the organisation's name: ${organization}`,
    confirm: "Delete permanently",
    deleted: (organization: string) => `${organization} has been deleted.`,
    failed: "Deleting failed.",
  },

  model: {
    title: "Reading model and sign-up",
    label: "Model that reads the PDFs",
    configDefault: "Default from configuration",
    saved: "Settings saved.",
    help: "Applies to all organisations, for every upload and import from now on. The token count per invoice is in the logs, so the effect on cost can be measured.",
    signupOpen: "Self sign-up open (new organisations get a trial)",
    signupHelp: "Only open this at launch: until then nobody can create an account themselves.",
  },
};

export const platform = { nl, en };
