/** Texts for the review area. Dutch first; `en` must have exactly the same keys (typeof nl). */

const nl = {
  title: "Factuur beoordelen",
  untitled: "Concept zonder nummer",
  fileNameFallback: "factuur",
  payable: "Te betalen",
  book: "Boeken",
  bookAgain: "Opnieuw boeken",
  vat: "Btw",
  canonicalXml: "Canonieke XML",
  sessionExpired: "Uw sessie is verlopen. Log opnieuw in.",

  notFound: {
    subtitle: "Concept niet gevonden",
    before: "Er is geen concept voor",
    after: "in deze sessie. Upload de PDF opnieuw om een nieuw concept te maken.",
    toUpload: "Naar uploaden",
  },

  status: {
    notBooked: "Niet geboekt",
    changed: "Gewijzigd sinds boeken",
    booked: "Geboekt",
  },

  pdf: {
    title: "Bron-PDF",
    missing: "De PDF is niet gevonden in de opslag van deze browser.",
    missingHint: "Upload het bestand opnieuw om het hier te bekijken.",
    reupload: "Opnieuw uploaden",
  },

  formatNote: {
    customer: (name: string) => `Standaardformaat van afnemer ${name}`,
    unknownCustomer: "(onbekend)",
    receiver: "Gekozen omdat de afnemer dit via PEPPOL ontvangt.",
  },

  receiverWarning: {
    otherFormats: (formats: string) => `Deze afnemer ontvangt dit formaat niet via PEPPOL. Wel: ${formats}.`,
    noFormats: "Deze afnemer staat op PEPPOL, maar ontvangt geen van de formaten die xChange maakt.",
  },

  tabs: {
    invoice: "Factuur",
    notices: "Meldingen",
  },

  xml: {
    badge: "Deterministisch gegenereerd",
  },

  notices: {
    title: "Meldingen bij het uitlezen",
    none: "Geen meldingen.",
    error: "Fout",
    info: "Ingevuld",
    warning: "Let op",
  },

  toast: {
    savedInBrowser: "Concept opgeslagen in deze browser.",
    alreadyBooked: "Deze factuur is al geboekt. Klik op Opnieuw boeken om je wijzigingen te bewaren.",
    savedInBrowserOnly: "Concept opgeslagen in deze browser. De PDF is niet beschikbaar, dus niet op de server.",
    saved: "Concept opgeslagen. Je vindt het terug onder Facturen.",
    fixNumbersFirst: "Corrigeer eerst de getallen die niet gelezen kunnen worden.",
    pdfMissing: "De PDF is niet beschikbaar; upload hem opnieuw voordat je boekt.",
    booked: "Factuur geboekt en opgeslagen.",
    bookFailed: "Boeken is mislukt.",
    canonicalDownloaded: "Canonieke XML gedownload.",
    notCompliant: "Het bestand voldoet niet aan de officiële regels. Zie de meldingen.",
    downloaded: (fileName: string) => `${fileName} gedownload.`,
    downloadedWithWarnings: (fileName: string, count: number) =>
      `${fileName} gedownload, met ${count} waarschuwing(en).`,
    exportFailed: "Exporteren is mislukt.",
  },

  export: {
    title: "Exporteren",
    format: "Formaat",
    bookFirst: "Eerst boeken",
    download: "Downloaden",
    blockedTrial: "In de proefperiode kun je alles controleren. Het bestand downloaden kan met een abonnement of gekocht tegoed.",
    choosePlan: "Abonnement of tegoed kiezen",
    blockedNoApi: "Zonder API is er geen export mogelijk.",
    blockedNotBooked: "Boek de factuur eerst: het bestand wordt gemaakt uit de geboekte versie.",
    blockedIncomplete: "Vul eerst aan wat hieronder staat.",
    numberCode: "Getal",
    completeFor: (format: string) => `Aanvullen voor ${format}`,
    missing: "Ontbreekt",
    checkFailed: "De controle kon niet worden uitgevoerd. Probeer het opnieuw.",
    checking: "Controleren…",
    complete: (format: string) => `Compleet voor ${format}.`,
    lastWarnings: "Waarschuwingen bij de laatste download",
    warning: "Let op",
    footnote: (release: string) =>
      `Tijdens het typen: verplichte velden en kernregels. Bij downloaden: de volledige officiële regels (release ${release}); een bestand met een fout wordt niet vrijgegeven.`,
  },

  details: {
    title: "Factuurgegevens",
    invoiceNumber: "Factuurnummer",
    type: "Soort",
    currency: "Valuta",
    issueDate: "Factuurdatum",
    dueDate: "Vervaldatum",
    rate: (from: string, to: string) => `Wisselkoers ${from} → ${to}`,
    rateHint: (from: string, to: string) =>
      `Aantal ${to} voor 1 ${from}, op de factuurdatum. De belasting wordt ook in ${to} vermeld.`,
    buyerReference: "Referentie afnemer",
    buyerReferenceHint: "Of een inkoopordernummer: één van beide is verplicht.",
    purchaseOrder: "Inkoopordernummer",
    precedingInvoice: "Oorspronkelijk factuurnummer",
    note: "Opmerking",
  },

  party: {
    seller: "Verkoper (uw organisatie)",
    buyer: "Afnemer",
    name: "Naam",
    vatIdentifier: "Btw-nummer",
    registrationId: "KvK-nummer",
    registrationScheme: "Schema registratienummer",
    electronicAddress: "Elektronisch adres (PEPPOL-id)",
    electronicAddressScheme: "Schema elektronisch adres",
    street: "Straat en huisnummer",
    postalCode: "Postcode",
    city: "Plaats",
    countryCode: "Landcode",
  },

  receiver: {
    checking: "PEPPOL controleren…",
    onPeppol: "Op PEPPOL",
    as: "als",
    filled: " (elektronisch adres hiermee ingevuld)",
    receives: (formats: string) => `Ontvangt: ${formats}.`,
    receivesNone: "Ontvangt geen van de formaten die xChange maakt.",
    adopt: "Dit adres overnemen",
    notFound: (tried: string) =>
      `Niet gevonden op PEPPOL (gezocht op ${tried}). De afnemer kan dan geen e-factuur via PEPPOL ontvangen; controleer het btw- of KvK-nummer.`,
    unavailable: "PEPPOL is nu niet bereikbaar; de controle wordt bij een wijziging opnieuw gedaan.",
    nothingToLookUp: "Vul een btw-, KvK- of PEPPOL-nummer in om te zien of de afnemer e-facturen kan ontvangen.",
  },

  lines: {
    title: "Factuurregels",
    add: "Regel toevoegen",
    remove: "Regel verwijderen",
    description: "Omschrijving",
    quantity: "Aantal",
    unit: "Eenheid",
    price: "Prijs",
    net: "Netto",
    taxCategory: (taxName: string) => `${taxName}-categorie`,
    taxPercentage: (taxName: string) => `${taxName}-percentage`,
    adjustments: (count: number) => `Inclusief ${count} korting(en)/toeslag(en) van de factuur.`,
  },

  payment: {
    title: "Betaling",
    means: "Betaalwijze",
    reference: "Betalingskenmerk",
    iban: "IBAN",
    bic: "BIC",
    accountName: "Ten name van",
  },

  totals: {
    title: "Totalen",
    calculated: "Berekend",
    lines: "Totaal regels",
    charge: "Toeslag",
    allowance: "Korting",
    exclusive: (taxName: string) => `Totaal exclusief ${taxName}`,
    over: "over",
    reasonPlaceholder: "Vrijstellingsreden, bijv. 'Btw verlegd'",
    reasonLabel: (category: string) => `Vrijstellingsreden btw-categorie ${category}`,
    prepaid: "Vooruitbetaald",
    payable: "Te betalen",
    exclusiveIn: (taxName: string, currency: string) => `Totaal exclusief ${taxName} in ${currency}`,
    taxIn: (taxName: string, currency: string) => `${taxName} in ${currency}`,
    inclusiveIn: (taxName: string, currency: string) => `Totaal inclusief ${taxName} in ${currency}`,
  },

  /** Number checks made in the browser before the server can read the draft (lib/canonical.ts). */
  issues: {
    quantity: (line: number) => `Regel ${line}: aantal is geen getal. Gebruik een punt als decimaalteken.`,
    unitPrice: (line: number) => `Regel ${line}: prijs is geen getal. Gebruik een punt als decimaalteken.`,
    vatPercentage: (line: number) => `Regel ${line}: btw-percentage is geen getal.`,
    baseQuantity: (line: number) => `Regel ${line}: basishoeveelheid is geen getal.`,
    taxCurrencyRate: "De wisselkoers is geen getal. Gebruik een punt als decimaalteken.",
    prepaidAmount: "Vooruitbetaald bedrag is geen getal.",
  },

  /** Names in the code lists of review/fields.tsx; the code itself is added there. */
  codes: {
    invoice: "Factuur",
    creditNote: "Creditnota",
    endpoint: {
      "0106": "KvK-nummer",
      "0190": "OIN (overheid)",
      "9944": "Nederlands btw-nummer",
      "0208": "Belgisch ondernemingsnummer",
      "9925": "Belgisch btw-nummer",
      "9932": "Brits btw-nummer",
      "0088": "GLN",
      "0195": "Singapore UEN",
    },
    registration: {
      "0106": "KvK",
      "0190": "OIN",
      "0208": "Belgisch ondernemingsnummer",
      "0195": "Singapore UEN",
    },
    units: {
      C62: "stuk",
      HUR: "uur",
      DAY: "dag",
      MON: "maand",
      KGM: "kilogram",
      MTR: "meter",
      LTR: "liter",
    },
    paymentMeans: {
      "30": "Overschrijving",
      "58": "SEPA-overschrijving",
      "48": "Betaalkaart",
      "57": "Doorlopende opdracht",
    },
  },
};

const en: typeof nl = {
  title: "Review invoice",
  untitled: "Draft without a number",
  fileNameFallback: "invoice",
  payable: "Payable amount",
  book: "Book",
  bookAgain: "Book again",
  vat: "VAT",
  canonicalXml: "Canonical XML",
  sessionExpired: "Your session has expired. Please log in again.",

  notFound: {
    subtitle: "Draft not found",
    before: "There is no draft for",
    after: "in this session. Upload the PDF again to create a new draft.",
    toUpload: "Go to upload",
  },

  status: {
    notBooked: "Not booked",
    changed: "Changed since booking",
    booked: "Booked",
  },

  pdf: {
    title: "Source PDF",
    missing: "The PDF was not found in this browser's storage.",
    missingHint: "Upload the file again to view it here.",
    reupload: "Upload again",
  },

  formatNote: {
    customer: (name: string) => `Default format of customer ${name}`,
    unknownCustomer: "(unknown)",
    receiver: "Chosen because the customer receives this format via PEPPOL.",
  },

  receiverWarning: {
    otherFormats: (formats: string) => `This customer does not receive this format via PEPPOL. It does receive: ${formats}.`,
    noFormats: "This customer is on PEPPOL, but receives none of the formats xChange produces.",
  },

  tabs: {
    invoice: "Invoice",
    notices: "Notices",
  },

  xml: {
    badge: "Generated deterministically",
  },

  notices: {
    title: "Notices from reading the PDF",
    none: "No notices.",
    error: "Error",
    info: "Filled in",
    warning: "Warning",
  },

  toast: {
    savedInBrowser: "Draft saved in this browser.",
    alreadyBooked: "This invoice is already booked. Click Book again to keep your changes.",
    savedInBrowserOnly: "Draft saved in this browser. The PDF is not available, so it was not saved on the server.",
    saved: "Draft saved. You'll find it under Invoices.",
    fixNumbersFirst: "First correct the numbers that cannot be read.",
    pdfMissing: "The PDF is not available; upload it again before you book.",
    booked: "Invoice booked and saved.",
    bookFailed: "Booking failed.",
    canonicalDownloaded: "Canonical XML downloaded.",
    notCompliant: "The file does not meet the official rules. See the notices.",
    downloaded: (fileName: string) => `${fileName} downloaded.`,
    downloadedWithWarnings: (fileName: string, count: number) =>
      `${fileName} downloaded, with ${count} ${count === 1 ? "warning" : "warnings"}.`,
    exportFailed: "Export failed.",
  },

  export: {
    title: "Export",
    format: "Format",
    bookFirst: "Book first",
    download: "Download",
    blockedTrial: "During the trial you can check everything. Downloading the file requires a plan or purchased credits.",
    choosePlan: "Choose a plan or credits",
    blockedNoApi: "Exporting is not possible without an API.",
    blockedNotBooked: "Book the invoice first: the file is made from the booked version.",
    blockedIncomplete: "First complete what is listed below.",
    numberCode: "Number",
    completeFor: (format: string) => `To complete for ${format}`,
    missing: "Missing",
    checkFailed: "The check could not be run. Please try again.",
    checking: "Checking…",
    complete: (format: string) => `Complete for ${format}.`,
    lastWarnings: "Warnings from the last download",
    warning: "Warning",
    footnote: (release: string) =>
      `While you type: required fields and core rules. On download: the full official rules (release ${release}); a file with an error is not released.`,
  },

  details: {
    title: "Invoice details",
    invoiceNumber: "Invoice number",
    type: "Type",
    currency: "Currency",
    issueDate: "Issue date",
    dueDate: "Due date",
    rate: (from: string, to: string) => `Exchange rate ${from} → ${to}`,
    rateHint: (from: string, to: string) =>
      `Number of ${to} per 1 ${from}, on the issue date. The tax is also stated in ${to}.`,
    buyerReference: "Buyer reference",
    buyerReferenceHint: "Or a purchase order number: one of the two is required.",
    purchaseOrder: "Purchase order number",
    precedingInvoice: "Original invoice number",
    note: "Note",
  },

  party: {
    seller: "Seller (your organisation)",
    buyer: "Customer",
    name: "Name",
    vatIdentifier: "VAT number",
    registrationId: "Registration number",
    registrationScheme: "Registration number scheme",
    electronicAddress: "Electronic address (PEPPOL ID)",
    electronicAddressScheme: "Electronic address scheme",
    street: "Street and number",
    postalCode: "Postcode",
    city: "City",
    countryCode: "Country code",
  },

  receiver: {
    checking: "Checking PEPPOL…",
    onPeppol: "On PEPPOL",
    as: "as",
    filled: " (electronic address filled in from this)",
    receives: (formats: string) => `Receives: ${formats}.`,
    receivesNone: "Receives none of the formats xChange produces.",
    adopt: "Use this address",
    notFound: (tried: string) =>
      `Not found on PEPPOL (searched for ${tried}). The customer cannot receive an e-invoice via PEPPOL then; check the VAT or registration number.`,
    unavailable: "PEPPOL cannot be reached right now; the check runs again when something changes.",
    nothingToLookUp: "Enter a VAT, registration or PEPPOL number to see whether the customer can receive e-invoices.",
  },

  lines: {
    title: "Invoice lines",
    add: "Add line",
    remove: "Remove line",
    description: "Description",
    quantity: "Quantity",
    unit: "Unit",
    price: "Price",
    net: "Net amount",
    taxCategory: (taxName: string) => `${taxName} category`,
    taxPercentage: (taxName: string) => `${taxName} rate`,
    adjustments: (count: number) =>
      `Includes ${count} ${count === 1 ? "allowance/charge" : "allowances/charges"} from the invoice.`,
  },

  payment: {
    title: "Payment",
    means: "Payment means",
    reference: "Payment reference",
    iban: "IBAN",
    bic: "BIC",
    accountName: "Account name",
  },

  totals: {
    title: "Totals",
    calculated: "Calculated",
    lines: "Sum of lines",
    charge: "Charge",
    allowance: "Allowance",
    exclusive: (taxName: string) => `Total excluding ${taxName}`,
    over: "on",
    reasonPlaceholder: "Exemption reason, e.g. 'Reverse charge'",
    reasonLabel: (category: string) => `Exemption reason for VAT category ${category}`,
    prepaid: "Prepaid amount",
    payable: "Payable amount",
    exclusiveIn: (taxName: string, currency: string) => `Total excluding ${taxName} in ${currency}`,
    taxIn: (taxName: string, currency: string) => `${taxName} in ${currency}`,
    inclusiveIn: (taxName: string, currency: string) => `Total including ${taxName} in ${currency}`,
  },

  issues: {
    quantity: (line: number) => `Line ${line}: quantity is not a number. Use a full stop as the decimal separator.`,
    unitPrice: (line: number) => `Line ${line}: price is not a number. Use a full stop as the decimal separator.`,
    vatPercentage: (line: number) => `Line ${line}: VAT rate is not a number.`,
    baseQuantity: (line: number) => `Line ${line}: base quantity is not a number.`,
    taxCurrencyRate: "The exchange rate is not a number. Use a full stop as the decimal separator.",
    prepaidAmount: "The prepaid amount is not a number.",
  },

  codes: {
    invoice: "Invoice",
    creditNote: "Credit note",
    endpoint: {
      "0106": "Dutch Chamber of Commerce number",
      "0190": "OIN (Dutch government)",
      "9944": "Dutch VAT number",
      "0208": "Belgian enterprise number",
      "9925": "Belgian VAT number",
      "9932": "UK VAT number",
      "0088": "GLN",
      "0195": "Singapore UEN",
    },
    registration: {
      "0106": "KvK (Dutch Chamber of Commerce)",
      "0190": "OIN",
      "0208": "Belgian enterprise number",
      "0195": "Singapore UEN",
    },
    units: {
      C62: "piece",
      HUR: "hour",
      DAY: "day",
      MON: "month",
      KGM: "kilogram",
      MTR: "metre",
      LTR: "litre",
    },
    paymentMeans: {
      "30": "Credit transfer",
      "58": "SEPA credit transfer",
      "48": "Bank card",
      "57": "Standing agreement",
    },
  },
};

export const review = { nl, en };
