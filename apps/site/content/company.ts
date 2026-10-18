/**
 * The company that sells xChange.
 *
 * Green IT Solutions Pte. Ltd., Singapore (as on greenitsolutions.net). Values
 * still in square brackets must be replaced by the owner before the site goes
 * public; the legal pages (privacy policy, terms, trust & security) and the
 * contact page read these values directly.
 */
export const company = {
  /** Legal name of the selling company, including legal form. */
  name: "Green IT Solutions Pte. Ltd.",
  address: {
    street: "160 Robinson Road, #14-04, Singapore Business Federation Center",
    postalCode: "068914",
    city: "Singapore",
    country: "Singapore",
  },
  /** Company registration number (e.g. KBO/BCE in Belgium, KvK in the Netherlands). */
  registrationNumber: "202535720K (UEN)",
  /** Null while the company has no VAT/GST number; the line is then left out. */
  vatNumber: null as string | null,
  /** General, sales and privacy questions. */
  contactEmail: "info@xchangeinvoice.com",
  /** Help with the application. */
  supportEmail: "support@xchangeinvoice.com",
  /** Law that governs the terms of service, e.g. "Belgian law". */
  governingLaw: "[Toepasselijk recht]",
  /** Courts competent for disputes, e.g. "the courts of Ghent". */
  competentCourt: "[Bevoegde rechtbank]",
  /** Data protection authority customers can complain to, matching the company's seat. */
  supervisoryAuthority: "[Toezichthoudende autoriteit]",
  supervisoryAuthorityUrl: "[Website toezichthouder]",
  /** How long account data and invoices are kept after an account is closed. */
  retentionAfterClosing: "[Termijn na opzegging]",
  /** Notice given before a price change or a change to the terms takes effect. */
  noticePeriod: "[Opzegtermijn bij wijzigingen]",
  /** Date the legal drafts were last revised (ISO). */
  legalUpdated: "2026-10-18",
} as const;

export type Company = typeof company;

export function formatAddress(c: Company = company): string {
  const { street, postalCode, city, country } = c.address;
  // A city-state is written "Singapore 068914", without repeating the country.
  return city === country
    ? `${street}, ${city} ${postalCode}`
    : `${street}, ${postalCode} ${city}, ${country}`;
}
