/**
 * Legal pages (privacy policy, terms of service, trust & security).
 *
 * Every language has the same documents with the same number of sections in
 * the same order, so a translation can be checked section by section.
 */
export type LegalTable = { head: string[]; rows: string[][] };

export type LegalSection = {
  heading: string;
  paragraphs?: string[];
  items?: string[];
  table?: LegalTable;
  /** Paragraphs rendered after the list/table. */
  after?: string[];
};

export type LegalDoc = {
  title: string;
  /** One sentence, used as the meta description. */
  description: string;
  intro: string[];
  sections: LegalSection[];
};

export type LegalContent = {
  privacy: LegalDoc;
  terms: LegalDoc;
  trust: LegalDoc;
};
