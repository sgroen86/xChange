/** Texts for the company profile. Dutch first; `en` must have exactly the same keys (typeof nl). */

const nl = {
  title: "Bedrijfsprofiel",
  subtitle: "Uw gegevens als verkoper. Ze komen op elke factuur die u inleest.",
  noApi: "Er is geen API geconfigureerd. Het profiel kan niet worden opgeslagen.",
  saved: "Bedrijfsprofiel opgeslagen.",
  saveFailed: "Opslaan mislukt.",
  adminOnly: "Alleen een beheerder kan dit wijzigen.",
  howUsed:
    "Naam, nummers, adres, PEPPOL-id en bankgegevens uit dit profiel gaan voor wat er in een PDF staat; u ziet een melding als ze verschillen. Contact en betaalwijze vullen alleen aan wat ontbreekt. Al geboekte facturen veranderen niet.",
  tabs: { general: "Algemeen", peppol: "PEPPOL" },
  cards: { company: "Bedrijf", address: "Adres", contact: "Contact", payment: "Betaling", peppol: "PEPPOL" },
  fields: {
    legalName: "Juridische naam",
    vatIdentifier: "Btw-nummer",
    legalRegistrationId: "Inschrijvingsnummer (KvK of KBO)",
    legalRegistrationScheme: "Schema",
    streetName: "Straat en huisnummer",
    additionalStreetName: "Extra adresregel",
    postalZone: "Postcode",
    cityName: "Plaats",
    country: "Land",
    contactName: "Contactpersoon",
    contactEmail: "E-mailadres",
    contactPhone: "Telefoonnummer",
    paymentMeansCode: "Standaard betaalwijze",
    iban: "IBAN",
    bic: "BIC",
    accountName: "Rekeninghouder",
    endpointId: "PEPPOL-id",
    endpointScheme: "Schema",
  },
  legalNameHelp: "Zoals ingeschreven bij de KvK of KBO.",
  countryHelp: "Het land wijzigt de platformbeheerder.",
  countryNotSet: "Nog niet vastgelegd",
  peppolIntro:
    "Uw adres op het PEPPOL-netwerk. In Nederland is dat meestal 0106 met uw KvK-nummer, in België 0208 met uw KBO-nummer.",
};

const en: typeof nl = {
  title: "Company profile",
  subtitle: "Your details as the seller. They go on every invoice you read in.",
  noApi: "No API is configured. The profile cannot be saved.",
  saved: "Company profile saved.",
  saveFailed: "Saving failed.",
  adminOnly: "Only an administrator can change this.",
  howUsed:
    "Name, numbers, address, PEPPOL ID and bank details from this profile take precedence over what a PDF says; you see a notice when they differ. Contact and payment means only fill in what is missing. Invoices already booked do not change.",
  tabs: { general: "General", peppol: "PEPPOL" },
  cards: { company: "Company", address: "Address", contact: "Contact", payment: "Payment", peppol: "PEPPOL" },
  fields: {
    legalName: "Legal name",
    vatIdentifier: "VAT number",
    legalRegistrationId: "Registration number (KvK or KBO)",
    legalRegistrationScheme: "Scheme",
    streetName: "Street and number",
    additionalStreetName: "Additional address line",
    postalZone: "Postcode",
    cityName: "City",
    country: "Country",
    contactName: "Contact person",
    contactEmail: "Email address",
    contactPhone: "Phone number",
    paymentMeansCode: "Usual payment means",
    iban: "IBAN",
    bic: "BIC",
    accountName: "Account holder",
    endpointId: "PEPPOL ID",
    endpointScheme: "Scheme",
  },
  legalNameHelp: "As registered with the KvK or KBO.",
  countryHelp: "The platform administrator changes the country.",
  countryNotSet: "Not set yet",
  peppolIntro:
    "Your address on the PEPPOL network. In the Netherlands that is usually 0106 with your KvK number, in Belgium 0208 with your KBO number.",
};

export const profile = { nl, en };
