/** Texts for the relations area. Dutch first; `en` must have exactly the same keys (typeof nl). */

const nl = {
  title: "Relaties",
  count: (n: number) => `${n} relatie(s)`,
  editTitle: "Relatie wijzigen",
  edit: "Wijzigen",
  saved: "Relatie opgeslagen.",
  saveFailed: "Opslaan mislukt.",
  fields: {
    name: "Naam",
    vatIdentifier: "Btw-nummer",
    legalRegistrationId: "KvK-nummer",
    legalRegistrationScheme: "Schema registratienummer",
    electronicAddress: "Elektronisch adres (PEPPOL-id)",
    electronicAddressScheme: "Schema elektronisch adres",
    countryCode: "Landcode",
    defaultRulesetId: "Exportformaat",
  },
  organizationDefault: "Standaard van de organisatie",
  formatHelp:
    "Facturen aan deze afnemer openen op dit formaat, bijvoorbeeld PEPPOL BIS 3.0 voor een klant in het VK.",
  empty:
    "Relaties worden vanzelf aangemaakt wanneer u een factuur boekt waarop de afnemer een btw- of KvK-nummer heeft.",
  columns: {
    name: "Naam",
    vatIdentifier: "Btw-nummer",
    legalRegistrationId: "KvK-nummer",
    electronicAddress: "Elektronisch adres",
    country: "Land",
    format: "Formaat",
  },
};

const en: typeof nl = {
  title: "Customers",
  count: (n: number) => (n === 1 ? "1 customer" : `${n} customers`),
  editTitle: "Edit customer",
  edit: "Edit",
  saved: "Customer saved.",
  saveFailed: "Saving failed.",
  fields: {
    name: "Name",
    vatIdentifier: "VAT number",
    legalRegistrationId: "Registration number",
    legalRegistrationScheme: "Registration number scheme",
    electronicAddress: "Electronic address (PEPPOL ID)",
    electronicAddressScheme: "Electronic address scheme",
    countryCode: "Country code",
    defaultRulesetId: "Export format",
  },
  organizationDefault: "Organisation default",
  formatHelp:
    "Invoices to this customer open in this format, for example PEPPOL BIS 3.0 for a customer in the UK.",
  empty:
    "Customers are created automatically when you book an invoice on which the customer has a VAT or registration number.",
  columns: {
    name: "Name",
    vatIdentifier: "VAT number",
    legalRegistrationId: "Registration number",
    electronicAddress: "Electronic address",
    country: "Country",
    format: "Format",
  },
};

export const relations = { nl, en };
