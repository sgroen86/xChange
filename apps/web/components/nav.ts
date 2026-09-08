/**
 * Navigation data, in the same declarative shape Bookkeeping uses
 * (js/components/sidebar.js): a flat array mixing `{ section }` separators with
 * items carrying `id`, `label`, `icon`, optional `badge` and optional `hidden`.
 *
 * The Bookkeeping sections are kept here so the structure stays recognisable,
 * but flagged `hidden` — Bookkeeping's own mechanism for parking an item —
 * because those routes do not exist in xChange and dead links help nobody.
 * Give them an `href` and drop `hidden` once the apps are linked up.
 */

export interface NavSection {
  section: string;
}

export interface NavItem {
  id: string;
  label: string;
  icon: string;
  href?: string;
  badge?: number | "auto";
  hidden?: boolean;
  /** Rendered only when a review is in progress. */
  requiresInvoice?: boolean;
}

export type NavEntry = NavSection | NavItem;

export function isSection(entry: NavEntry): entry is NavSection {
  return "section" in entry;
}

export const navStructure: NavEntry[] = [
  { section: "xChange" },
  { id: "upload", label: "Factuur uploaden", icon: "fileUp", href: "/upload" },
  { id: "review", label: "Beoordelen", icon: "fileText", requiresInvoice: true },

  { section: "Boekhouden" },
  { id: "intake", label: "Inkomende Facturen", icon: "fileUp", hidden: true },
  { id: "facturen-overzicht", label: "Facturenoverzicht", icon: "fileText", hidden: true },
  { id: "invoeren", label: "Facturen Invoeren", icon: "filePlus", hidden: true },

  { section: "Aangiftes" },
  { id: "btw", label: "BTW Aangifte", icon: "euro", hidden: true },

  { section: "Beheer" },
  { id: "gebruikers", label: "Gebruikers", icon: "users", hidden: true },
  { id: "instellingen", label: "Instellingen", icon: "settings", hidden: true },
];

/** Page titles shown in the header, keyed by nav id. */
export const pageTitles: Record<string, { title: string; breadcrumb: string }> = {
  upload: { title: "Factuur uploaden", breadcrumb: "xChange / Uploaden" },
  review: { title: "Factuur beoordelen", breadcrumb: "xChange / Beoordelen" },
};
