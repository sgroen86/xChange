/**
 * Navigation data, in the same declarative shape Bookkeeping uses
 * (js/components/sidebar.js): a flat array mixing `{ section }` separators with
 * items carrying `id`, `label`, `icon`, optional `badge` and optional `hidden`.
 * Labels are keys into the shell dictionary (lib/i18n/shell.ts), so the
 * sidebar and the header follow the language switch.
 *
 * The Bookkeeping sections are kept here so the structure stays recognisable,
 * but flagged `hidden` — Bookkeeping's own mechanism for parking an item —
 * because those routes do not exist in xChange and dead links help nobody.
 * Give them an `href` and drop `hidden` once the apps are linked up.
 */

import type { NavLabelKey, NavSectionKey } from "../lib/i18n/shell";

export interface NavSection {
  section: NavSectionKey;
}

export interface NavItem {
  id: string;
  label: NavLabelKey;
  icon: string;
  href?: string;
  badge?: number | "auto";
  hidden?: boolean;
  /** Rendered only when a review is in progress. */
  requiresInvoice?: boolean;
  /** Rendered only for the platform administrator. */
  requiresPlatformAdmin?: boolean;
}

export type NavEntry = NavSection | NavItem;

export function isSection(entry: NavEntry): entry is NavSection {
  return "section" in entry;
}

export const navStructure: NavEntry[] = [
  { section: "xchange" },
  { id: "upload", label: "upload", icon: "fileUp", href: "/upload" },
  { id: "imports", label: "imports", icon: "clock", href: "/imports" },
  { id: "invoices", label: "invoices", icon: "fileText", href: "/invoices" },
  { id: "relations", label: "relations", icon: "users", href: "/relations" },
  { id: "review", label: "review", icon: "fileText", requiresInvoice: true },

  { section: "bookkeeping" },
  { id: "intake", label: "intake", icon: "fileUp", hidden: true },
  { id: "facturen-overzicht", label: "invoiceOverview", icon: "fileText", hidden: true },
  { id: "invoeren", label: "enterInvoices", icon: "filePlus", hidden: true },

  { section: "returns" },
  { id: "btw", label: "vatReturn", icon: "euro", hidden: true },

  { section: "admin" },
  { id: "gebruikers", label: "users", icon: "users", hidden: true },
  { id: "instellingen", label: "settings", icon: "settings", href: "/settings" },
  { id: "platform", label: "platform", icon: "users", href: "/platform", requiresPlatformAdmin: true },
];

/* Page titles shown in the header live in the shell dictionary, under `pages`, keyed by nav id. */
