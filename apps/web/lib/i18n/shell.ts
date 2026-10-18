/** Texts for the shell area. Dutch first; `en` must have exactly the same keys (typeof nl). */

const nl = {
  brandSub: "Factuurconversie",
  prototype: "Prototype",
  prototypeNote: "Prototype — mockverwerking",
  poweredBy: "Aangedreven door",
  toggleSidebar: "Zijbalk in- of uitklappen",
  logout: "Uitloggen",
  openMenu: "Menu openen",
  toUpload: "Naar uploaden",
  roles: {
    admin: "Beheerder",
    readOnly: "Alleen lezen",
    user: "Gebruiker",
  },
  /** Sidebar section headings, keyed as in components/nav.ts. */
  sections: {
    xchange: "xChange",
    bookkeeping: "Boekhouden",
    returns: "Aangiftes",
    admin: "Beheer",
  },
  /** Sidebar item labels, keyed as in components/nav.ts. */
  nav: {
    upload: "Factuur uploaden",
    imports: "Te beoordelen",
    invoices: "Facturen",
    relations: "Relaties",
    review: "Beoordelen",
    intake: "Inkomende Facturen",
    invoiceOverview: "Facturenoverzicht",
    enterInvoices: "Facturen Invoeren",
    vatReturn: "BTW Aangifte",
    users: "Gebruikers",
    settings: "Instellingen",
    platform: "Platform",
  },
  /** Header title and breadcrumb, keyed by the active nav id. */
  pages: {
    upload: { title: "Factuur uploaden", breadcrumb: "xChange / Uploaden" },
    invoices: { title: "Facturen", breadcrumb: "xChange / Facturen" },
    review: { title: "Factuur beoordelen", breadcrumb: "xChange / Beoordelen" },
    relations: { title: "Relaties", breadcrumb: "xChange / Relaties" },
    instellingen: { title: "Instellingen", breadcrumb: "Beheer / Instellingen" },
    platform: { title: "Platform", breadcrumb: "Beheer / Platform" },
    invite: { title: "Uitnodiging", breadcrumb: "xChange / Account aanmaken" },
  },
  /** Errors created by lib/api.ts (API problem details are shown as they come). */
  api: {
    unreachableCors: "De API is niet bereikbaar. Controleer de verbinding of de CORS-instelling.",
    sessionExpired: "Uw sessie is verlopen. Log opnieuw in.",
    tooLarge: "Het bestand is te groot voor de API.",
    providerFailed: "De extractie is mislukt bij de provider.",
  },
  /** PDF checks in lib/pdf.ts. */
  pdf: {
    noExtension: "Het bestand heeft geen .pdf-extensie.",
    unexpectedType: (type: string) => `Onverwacht bestandstype: ${type}. Alleen PDF wordt ondersteund.`,
    empty: "Het bestand is leeg (0 bytes).",
    tooLarge: (size: string, limit: string) => `Het bestand is ${size}. De limiet is ${limit}.`,
    noHeader: "Het bestand begint niet met een geldige PDF-header (%PDF-).",
  },
};

const en: typeof nl = {
  brandSub: "Invoice conversion",
  prototype: "Prototype",
  prototypeNote: "Prototype — mock processing",
  poweredBy: "Powered by",
  toggleSidebar: "Collapse or expand sidebar",
  logout: "Log out",
  openMenu: "Open menu",
  toUpload: "Go to upload",
  roles: {
    admin: "Administrator",
    readOnly: "Read only",
    user: "User",
  },
  sections: {
    xchange: "xChange",
    bookkeeping: "Bookkeeping",
    returns: "Tax returns",
    admin: "Administration",
  },
  nav: {
    upload: "Upload invoice",
    imports: "To review",
    invoices: "Invoices",
    relations: "Customers",
    review: "Review",
    intake: "Incoming invoices",
    invoiceOverview: "Invoice overview",
    enterInvoices: "Enter invoices",
    vatReturn: "VAT return",
    users: "Users",
    settings: "Settings",
    platform: "Platform",
  },
  pages: {
    upload: { title: "Upload invoice", breadcrumb: "xChange / Upload" },
    invoices: { title: "Invoices", breadcrumb: "xChange / Invoices" },
    review: { title: "Review invoice", breadcrumb: "xChange / Review" },
    relations: { title: "Customers", breadcrumb: "xChange / Customers" },
    instellingen: { title: "Settings", breadcrumb: "Administration / Settings" },
    platform: { title: "Platform", breadcrumb: "Administration / Platform" },
    invite: { title: "Invitation", breadcrumb: "xChange / Create account" },
  },
  api: {
    unreachableCors: "The API cannot be reached. Check the connection or the CORS setting.",
    sessionExpired: "Your session has expired. Please log in again.",
    tooLarge: "The file is too large for the API.",
    providerFailed: "Extraction failed at the provider.",
  },
  pdf: {
    noExtension: "The file does not have a .pdf extension.",
    unexpectedType: (type: string) => `Unexpected file type: ${type}. Only PDF is supported.`,
    empty: "The file is empty (0 bytes).",
    tooLarge: (size: string, limit: string) => `The file is ${size}. The limit is ${limit}.`,
    noHeader: "The file does not start with a valid PDF header (%PDF-).",
  },
};

export const shell = { nl, en };

export type NavLabelKey = keyof typeof nl.nav;
export type NavSectionKey = keyof typeof nl.sections;
export type PageKey = keyof typeof nl.pages;
